namespace Tuc.Console.Command

open Feather.ConsoleApplication
open Feather.ErrorHandling
open Tuc.Console
open Tuc.Console.Console

[<RequireQualifiedAccess>]
module Domain =
    open Tuc.Domain

    let check = ExecuteAsync <| fun (input, output) ->
        let domain = (input, output) |> Input.getDomain

        let execute domain: Async<unit> =
            match input with
            | Input.Option.Has "only-resolved" _ ->
                async {
                    let! domains =
                        domain
                        |> parseDomain (input, output)

                    match domains with
                    | Ok domains -> domains |> List.iter (Dump.parsedDomain output)
                    | Error errors -> errors |> List.iter (ParseError.format >> output.Error)
                }

            | _ ->
                async {
                    let! domainResult = domain |> checkDomain (input, output)

                    match input, domainResult with
                    | _, Error error ->
                        error |> CheckDomainError.show output

                    | Input.Option.Has "count" _, Ok domainTypes ->
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
                }

        async {
            match input with
            | Input.Option.Has "watch" _ ->
                let path, watchSubdirs =
                    match domain with
                    | File file -> file, WatchSubdirs.No
                    | Dir (dir, _) -> dir, WatchSubdirs.Yes

                (path, "*Domain.fsx")
                |> Watch.watch output watchSubdirs (execute None)
                |> Async.Start

                do! Watch.executeAndWaitForWatch output (execute (Some domain))
            | _ ->
                do! execute (Some domain)

            return ExitCode.Success
        }
