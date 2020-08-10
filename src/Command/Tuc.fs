namespace MF.TucConsole.Command

open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console

[<RequireQualifiedAccess>]
module Tuc =
    open MF.Tuc
    open ErrorHandling

    let check: ExecuteCommand = fun (input, output) ->
        let domain = (input, output) |> Input.getDomain
        let tucFile = (input, output) |> Input.getTuc

        let execute domain =
            if output.IsVerbose() then output.Section "Parsing Domain ..."
            let domainTypes =
                domain
                |> checkDomain (input, output)
                |> Result.orFail

            if output.IsVerbose() then output.Section "Parsing Tuc ..."

            match tucFile |> Parser.parse output domainTypes with
            | Ok tucs ->
                output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                tucs
                |> List.iter (Dump.parsedTuc output)

                ExitCode.Success
            | Error error ->
                output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                error
                |> ParseError.format
                |> output.Message
                |> output.NewLine

                ExitCode.Error

        match input with
        | Input.HasOption "watch" _ ->
            let domainPath, watchDomainSubdirs =
                match domain with
                | SingleFile file -> file, WatchSubdirs.No
                | Dir (dir, _) -> dir, WatchSubdirs.Yes

            [
                (tucFile, "*.tuc")
                |> watch output WatchSubdirs.No (fun _ -> execute None |> ignore)

                (domainPath, "*.fsx")
                |> watch output watchDomainSubdirs (fun _ -> execute None |> ignore)
            ]
            |> List.iter Async.Start

            executeAndWaitForWatch output (fun _ -> execute (Some domain) |> ignore)
            |> Async.RunSynchronously

            ExitCode.Success
        | _ ->
            execute (Some domain)

    let generate: ExecuteCommand = fun (input, output) ->
        (* let domain = (input, output) |> Input.getDomain
        let domainResult = Some domain |> checkDomain (input, output)

        match input, domainResult with
        | _, Error error -> error |> showParseDomainError output
        | _, Ok domainTypes -> *)
            // todo ...

        failwith "Not implemented yet ..."

        ExitCode.Success
