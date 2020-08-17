namespace MF.TucConsole.Command

open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console

[<RequireQualifiedAccess>]
module Domain =
    open MF.Domain

    let check: ExecuteCommand = fun (input, output) ->
        let domain = (input, output) |> Input.getDomain

        let execute domain =
            match input with
            | Input.HasOption "only-resolved" _ ->
                domain
                |> parseDomain (input, output)
                |> List.iter (Dump.parsedDomain output)

            | _ ->
                let domainResult = domain |> checkDomain (input, output)

                match input, domainResult with
                | _, Error error -> error |> showParseDomainError output

                | Input.HasOption "count" _, Ok domainTypes ->
                    domainTypes
                    |> List.length
                    |> sprintf "Count of resolved types: <c:magenta>%d</c>"
                    |> output.Message

                    output.NewLine()

                | _, Ok domainTypes ->
                    domainTypes
                    |> tee (fun _ ->
                        output.NewLine()
                        output.Section <| sprintf "Domain types [%d]" (domainTypes |> List.length)
                    )
                    |> List.iter (fun (DomainType domainType) -> domainType |> Dump.parsedType output)

        match input with
        | Input.HasOption "watch" _ ->
            let path, watchSubdirs =
                match domain with
                | SingleFile file -> file, WatchSubdirs.No
                | Dir (dir, _) -> dir, WatchSubdirs.Yes

            (path, "*.fsx")
            |> watch output watchSubdirs (fun _ -> execute None)
            |> Async.Start

            executeAndWaitForWatch output (fun _ -> execute (Some domain))
            |> Async.RunSynchronously
        | _ ->
            execute (Some domain)

        ExitCode.Success
