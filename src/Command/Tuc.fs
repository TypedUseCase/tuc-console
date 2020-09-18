namespace MF.TucConsole.Command

open System
open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console

[<RequireQualifiedAccess>]
module Tuc =
    open MF.Tuc
    open MF.Tuc.Parser
    open ErrorHandling

    let check: ExecuteCommand = fun (input, output) ->
        let domain = (input, output) |> Input.getDomain
        let tucFileOrDir = (input, output) |> Input.getTucFileOrDir

        let baseIndentation =
            if output.IsVerbose() then "[yyyy-mm-dd HH:MM:SS]    ".Length else 0

        let execute domain =
            if output.IsVerbose() then output.Section "Parsing Domain ..."
            let domainTypes =
                domain
                |> checkDomain (input, output)
                |> Result.orFail

            if output.IsVerbose() then output.Section "Parsing Tuc ..."

            match tucFileOrDir with
            | File tucFile ->
                match tucFile |> Parser.parse output domainTypes with
                | Ok tucs ->
                    output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                    tucs
                    |> List.iter (Dump.parsedTuc output)

                    ExitCode.Success
                | Error errors ->
                    output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                    errors
                    |> List.iter (
                        ParseError.format baseIndentation
                        >> output.Message
                        >> output.NewLine
                    )

                    ExitCode.Error

            | Dir (_, tucFiles) ->
                let mutable exitCode = ExitCode.Success

                tucFiles
                |> List.map (fun tucFile ->
                    match tucFile |> Parser.parse output domainTypes with
                    | Ok tucs -> [ tucFile; (tucs |> List.length |> string); "OK"; "" ]
                    | Error errors ->
                        exitCode <- ExitCode.Error
                        [ tucFile; "0"; "Error"; errors |> List.map (ParseError.errorName) |> List.distinct |> String.concat ", " ]
                )
                |> output.Table [ "Tuc file"; "Tucs in file"; "Status"; "Detail" ]

                exitCode

        match input with
        | Input.HasOption "watch" _ ->
            let domainPath, watchDomainSubdirs = domain |> FileOrDir.watch
            let tucPath, watchTucSubdirs = tucFileOrDir |> FileOrDir.watch

            [
                (tucPath, "*.tuc")
                |> watch output watchTucSubdirs (fun _ -> execute None |> ignore)

                (domainPath, "*.fsx")
                |> watch output watchDomainSubdirs (fun _ -> execute None |> ignore)
            ]
            |> List.iter Async.Start

            executeAndWaitForWatch output (fun _ -> execute (Some domain) |> ignore)
            |> Async.RunSynchronously

            ExitCode.Success
        | _ ->
            execute (Some domain)

    open MF.Puml

    [<RequireQualifiedAccess>]
    type private GeneratePuml =
        | InWatch
        | Immediately of FileOrDir

    let generate: ExecuteCommand = fun (input, output) ->
        let domain = (input, output) |> Input.getDomain
        let tucFile = (input, output) |> Input.getTuc
        let style = (input, output) |> Input.getStyle

        let baseIndentation =
            if output.IsVerbose() then "[yyyy-mm-dd HH:MM:SS]    ".Length else 0

        let specificTuc =
            match input with
            | Input.OptionValue "tuc" tuc -> Some tuc
            | _ -> None

        let outputFile =
            match input with
            | Input.OptionValue "output" output ->
                if output.EndsWith ".puml"
                    then Some output
                    else failwithf "Output file must be a .puml file."
            | _ ->
                None

        let outputImage =
            match input with
            | Input.OptionValue "image" image ->
                let extension = image |> IO.Path.GetExtension
                let imageFormat = extension |> Generate.ImageFormat.parseExtension

                Some (image, imageFormat)
            | _ -> None

        output.Table [ "Tuc"; "Output"; "Image" ] [
            [
                specificTuc |> Option.defaultValue "*"
                outputFile |> Option.defaultValue "stdout"
                outputImage |> Option.map fst |> Option.defaultValue "-"
            ]
        ]

        let execute generate =
            result {
                let domain =
                    match generate with
                    | GeneratePuml.InWatch -> None
                    | GeneratePuml.Immediately domain -> Some domain

                if output.IsVerbose() then output.Section "Parsing Domain ..."
                let domainTypes =
                    domain
                    |> checkDomain (input, output)
                    |> Result.orFail

                if output.IsVerbose() then output.Section "Parsing Tuc ..."
                let! tucs =
                    tucFile
                    |> Parser.parse output domainTypes
                    |> Result.mapError (List.map (ParseError.format baseIndentation))

                let! tucs =
                    match specificTuc with
                    | Some specific ->
                        tucs
                        |> List.tryFind (Tuc.name >> (=) (TucName specific))
                        |> Result.ofOption [ sprintf "<c:red>Specific tuc %A is not parsed.</c>" specificTuc ]
                        |> Result.map List.singleton
                    | _ -> Ok tucs

                if output.IsVerbose() then output.Section "Generating Puml ..."
                let pumlName = tucFile |> IO.Path.GetFileNameWithoutExtension

                let! puml =
                    tucs
                    |> Generate.puml output style pumlName
                    |> Result.mapError PumlError.format
                    |> Validation.ofResult

                match outputFile with
                | Some outputFile ->
                    IO.File.WriteAllText (outputFile, puml |> Puml.value)
                | _ ->
                    output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)
                    puml|> Puml.value |> output.Message
                    output.Message <| sprintf "<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                outputImage
                |> Option.iter (fun (imagePath, outputFormat) ->
                    let run =
                        match generate with
                        | GeneratePuml.InWatch -> Async.Start
                        | GeneratePuml.Immediately _ -> Async.RunSynchronously

                    async {
                        output.Message "<c:gray>[Image]</c> Generating ..."
                        let! image = puml |> Generate.image outputFormat

                        return
                            match image with
                            | Ok (PumlImage image) ->
                                IO.File.WriteAllBytes(imagePath, image);
                                output.Message "<c:gray>[Image]</c> Created."
                            | Error error ->
                                sprintf "[Image] Not created due error: %s" error
                                |> output.Error
                            |> output.NewLine
                    }
                    |> run
                )

                return "Done"
            }
            |> function
                | Ok message ->
                    output.Success message
                    ExitCode.Success
                | Error messages ->
                    messages
                    |> List.iter (output.Message >> output.NewLine)
                    ExitCode.Error

        match input with
        | Input.HasOption "watch" _ ->
            let domainPath, watchDomainSubdirs = domain |> FileOrDir.watch

            [
                (tucFile, "*.tuc")
                |> watch output WatchSubdirs.No (fun _ -> execute GeneratePuml.InWatch |> ignore)

                (domainPath, "*.fsx")
                |> watch output watchDomainSubdirs (fun _ -> execute GeneratePuml.InWatch |> ignore)
            ]
            |> List.iter Async.Start

            executeAndWaitForWatch output (fun _ -> execute (GeneratePuml.Immediately domain) |> ignore)
            |> Async.RunSynchronously

            ExitCode.Success
        | _ ->
            execute (GeneratePuml.Immediately domain)
