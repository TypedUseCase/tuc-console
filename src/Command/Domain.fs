namespace Tuc.Console.Command

open MF.ConsoleApplication
open Tuc.Console
open Tuc.Console.Console

[<RequireQualifiedAccess>]
module Domain =
    open Tuc.Domain
    open ErrorHandling

    let check: ExecuteCommand = fun (input, output) ->
        let domain = (input, output) |> Input.getDomain

        let execute domain =
            match input with
            | Input.HasOption "only-resolved" _ ->
                domain
                |> parseDomain (input, output)
                |> runOrShowErrors
                |> List.iter (Dump.parsedDomain output)

            | _ ->
                let domainResult =
                    domain
                    |> checkDomain (input, output)
                    |> Async.RunSynchronously

                match input, domainResult with
                | _, Error errors -> errors |> List.iter (showCheckError output)

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
                | File file -> file, WatchSubdirs.No
                | Dir (dir, _) -> dir, WatchSubdirs.Yes

            (path, "*Domain.fsx")
            |> Watch.watch output watchSubdirs (fun _ -> execute None)
            |> Async.Start

            Watch.executeAndWaitForWatch output (fun _ -> execute (Some domain))
            |> Async.RunSynchronously
        | _ ->
            execute (Some domain)

        ExitCode.Success
