namespace Tuc.Console.Command

open System
open Feather.ConsoleApplication
open Tuc.Console
open Tuc.Console.Console

[<RequireQualifiedAccess>]
module Tuc =
    open Tuc
    open Tuc.Parser
    open Feather.ErrorHandling

    let check = ExecuteAsync <| fun (input, output) ->
        let domain = (input, output) |> Input.getDomain
        let tucFileOrDir = (input, output) |> Input.getTucFileOrDir

        let withDiagnostics =
            match input with
            | Input.Option.Has "diagnostics" _ -> true
            | _ -> false

        let baseIndentation =
            if output.IsVerbose() then "[yyyy-mm-dd HH:MM:SS]    ".Length else 0

        let execute domain: Async<ExitCode> = async {
            if output.IsVerbose() then output.Section "Parsing Domain ..."

            match! domain |> checkDomain (input, output) with
            | Error error ->
                error
                |> CheckDomainError.show output
                |> output.NewLine

                return ExitCode.Error

            | Ok domainTypes ->
                if output.IsVerbose() then output.Section "Parsing Tuc ..."

                match tucFileOrDir with
                | File tucFile ->
                    match tucFile |> Parser.parse output withDiagnostics domainTypes with
                    | Ok tucs ->
                        output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                        let dump =
                            match input with
                            | Input.Option.Has "detail" _ -> Dump.detailedParsedTuc
                            | _ -> Dump.parsedTuc

                        tucs
                        |> List.iter (dump output)

                        return ExitCode.Success
                    | Error errors ->
                        output.Message <| sprintf "\n<c:gray>%s</c>\n" ("-" |> String.replicate 100)

                        errors
                        |> List.iter (
                            ParseError.format baseIndentation
                            >> output.Message
                            >> output.NewLine
                        )

                        return ExitCode.Error

                | Dir (_, tucFiles) ->
                    let mutable exitCode = ExitCode.Success

                    tucFiles
                    |> List.map (fun tucFile ->
                        match tucFile |> Parser.parse output withDiagnostics domainTypes with
                        | Ok tucs -> [ tucFile; (tucs |> List.length |> string); "OK"; "" ]
                        | Error errors ->
                            exitCode <- ExitCode.Error
                            [ tucFile; "0"; "Error"; errors |> List.map (ParseError.errorName) |> List.distinct |> String.concat ", " ]
                    )
                    |> output.Table [ "Tuc file"; "Tucs in file"; "Status"; "Detail" ]

                    return exitCode
        }

        async {
            match input with
            | Input.Option.Has "watch" _ ->
                let domainPath, watchDomainSubdirs = domain |> FileOrDir.watch
                let tucPath, watchTucSubdirs = tucFileOrDir |> FileOrDir.watch

                [
                    (tucPath, "*.tuc")
                    |> Watch.watch output watchTucSubdirs (execute None |> Async.Ignore)

                    (domainPath, "*Domain.fsx")
                    |> Watch.watch output watchDomainSubdirs (execute None |> Async.Ignore)
                ]
                |> List.iter Async.Start

                do! Watch.executeAndWaitForWatch output (execute (Some domain) |> Async.Ignore)

                return ExitCode.Success
            | _ ->
                return! execute (Some domain)
        }

    open Tuc.Puml

    [<RequireQualifiedAccess>]
    type private GeneratePuml =
        | InWatch
        | Immediately of FileOrDir

    let generate = ExecuteAsync <| fun (input, output) ->
        let domain = (input, output) |> Input.getDomain
        let tucFileOrDir = (input, output) |> Input.getTucFileOrDir
        let style = (input, output) |> Input.getStyle

        let withDiagnostics =
            match input with
            | Input.Option.Has "diagnostics" _ -> true
            | _ -> false

        let generateAll =
            match input with
            | Input.Option.Has "all" _ -> true
            | _ -> false

        let baseIndentation =
            if output.IsVerbose() then "[yyyy-mm-dd HH:MM:SS]    ".Length else 0

        let specificTuc =
            match tucFileOrDir, input with
            | Dir _, Input.Option.Value "tuc" _ -> failwithf "Specific tuc can be generated only for a single file, not a directory."
            | File _, Input.Option.Value "tuc" tuc ->
                if generateAll
                    then failwithf "You can not mix --all and --tuc option. Use either of them."
                    else Some tuc
            | _ -> None

        let outputFile =
            match tucFileOrDir, input with
            | Dir _, Input.Option.Value "output" _ -> failwithf "Output can be explicitly defined only for a single file, not a directory."
            | File _, Input.Option.Value "output" output ->
                if output.EndsWith ".puml"
                    then Some output
                    else failwithf "Output file must be a .puml file."
            | _ ->
                None

        let outputImage =
            match tucFileOrDir, input with
            | Dir _, Input.Option.Value "output" _ -> failwithf "Image can be explicitly defined only for a single file, not a directory."
            | File _, Input.Option.Value "image" image ->
                let imageFormat =
                    image
                    |> IO.Path.GetExtension
                    |> Generate.ImageFormat.parseExtension

                Some (image, imageFormat)
            | _ -> None

        match tucFileOrDir with
        | File _ ->
            output.Table [ "Tuc"; "Output"; "Image" ] [
                [
                    specificTuc |> Option.defaultValue "*"
                    outputFile |> Option.defaultValue "stdout"
                    outputImage |> Option.map fst |> Option.defaultValue "-"
                ]
            ]
        | _ -> ()

        let generatePumlForTuc generate domainTypes specificTuc outputFile outputImage tucFile = result {
            let tucFileName = tucFile |> Path.fileNameWithoutExtension

            let! tucs =
                tucFile
                |> Parser.parse output withDiagnostics domainTypes
                |> Result.mapError (List.map (ParseError.format baseIndentation))

            let generateTucs prefix specificTuc outputFile outputImage tucs = result {
                let! tucs =
                    match specificTuc with
                    | Some specific ->
                        tucs
                        |> List.tryFind (Tuc.name >> (=) (TucName specific))
                        |> Result.ofOption [ sprintf "<c:red>Specific tuc %A is not parsed.</c>" specificTuc ]
                        |> Result.map List.singleton
                    | _ -> Ok tucs

                if output.IsVerbose() then output.Section <| sprintf "%sGenerating Puml ..." prefix

                let! puml =
                    tucs
                    |> Generate.puml output style tucFileName
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
                |> Option.iter (fun (imagePath, imageFormat) ->
                    let run =
                        match generate with
                        | GeneratePuml.InWatch -> Async.Start
                        | GeneratePuml.Immediately _ -> Async.RunSynchronously

                    async {
                        let imageName = imagePath |> Path.fileNameWithoutExtension

                        output.Message <| sprintf "%s<c:gray>[Image]</c> <c:yellow>Generating</c> <c:cyan>%s.%s</c> ..." prefix imageName (imageFormat |> Generate.ImageFormat.extension)
                        let! image = puml |> Generate.image imageFormat

                        return
                            match image with
                            | Ok (PumlImage image) ->
                                IO.File.WriteAllBytes(imagePath, image);
                                output.Message <| sprintf "%s<c:gray>[Image]</c> <c:green>Created ✅ </c>" prefix
                            | Error error ->
                                sprintf "%s[Image] Not created due error: %s" prefix error
                                |> output.Error
                            |> output.NewLine
                    }
                    |> run
                )
            }

            do!
                tucs
                |> List.map ParsedTuc.tuc
                |> generateTucs "" specificTuc outputFile outputImage

            if generateAll && tucs |> List.length > 1 then
                let inline (/) a b = IO.Path.Combine(a, b)

                let tucFileDir = tucFile |> IO.Path.GetDirectoryName
                let subTucDirPath = tucFileDir / tucFileName

                output.Message <| sprintf "<c:gray>[All]</c> <c:yellow>Generate all sub-tucs</c> [<c:magenta>%d</c>] <c:yellow>for</c> <c:cyan>%s</c>.\n" (tucs |> List.length) tucFileName

                let fileName extension (TucName name) =
                    sprintf "%s.%s" (name.Replace(" ", "-")) extension

                let outputFile name =
                    subTucDirPath / (name |> fileName "puml")
                    |> Some

                let imageFormat =
                    match outputImage with
                    | Some (_, imageFormat) -> imageFormat
                    | _ -> Generate.ImageFormat.Svg

                let imagePath name =
                    subTucDirPath / (name |> fileName (imageFormat |> Generate.ImageFormat.extension))

                do!
                    tucs
                    |> List.map (ParsedTuc.tuc >> fun tuc ->
                        [ tuc ]
                        |> generateTucs "<c:gray>[Sub]</c>" None (outputFile tuc.Name) (Some (imagePath tuc.Name, imageFormat))
                    )
                    |> Validation.ofResults
                    |> Validation.toResult
                    |> Result.map ignore
                    |> Result.mapError List.concat

            return "Done"
        }

        let execute generate: Async<ExitCode> = async {
            let domain =
                match generate with
                | GeneratePuml.InWatch -> None
                | GeneratePuml.Immediately domain -> Some domain

            if output.IsVerbose() then output.Section "Parsing Domain ..."

            match! domain |> checkDomain (input, output) with
            | Error error ->
                error |> CheckDomainError.show output
                |> output.NewLine

                return ExitCode.Error

            | Ok domainTypes ->
                if output.IsVerbose() then output.Section "Parsing Tuc ..."

                match tucFileOrDir with
                | File tucFile ->
                    return
                        tucFile
                        |> generatePumlForTuc generate domainTypes specificTuc outputFile outputImage
                        |> function
                            | Ok message ->
                                output.Success message
                                ExitCode.Success
                            | Error messages ->
                                messages
                                |> List.iter (output.Message >> output.NewLine)
                                ExitCode.Error

                | Dir (_, tucFiles) ->
                    let mutable exitCode = ExitCode.Success

                    tucFiles
                    |> List.map (fun tucFile ->
                        let tucFilePath = tucFile |> IO.Path.GetDirectoryName
                        let tucFileName = tucFile |> Path.fileNameWithoutExtension
                        let outputTucName = IO.Path.Combine (tucFilePath, tucFileName)

                        tucFile
                        |> generatePumlForTuc generate domainTypes None
                            (Some (outputTucName + ".puml"))
                            (Some (outputTucName + ".svg", Generate.ImageFormat.Svg))
                        |> function
                            | Ok message -> [ tucFile; message |> sprintf "<c:green>%s</c>"; "" ]
                            | Error errors ->
                                exitCode <- ExitCode.Error
                                [ tucFile; "<c:red>Error</c>"; errors |> List.distinct |> String.concat ", " ]
                    )
                    |> output.Table [ "Tuc file"; "Status"; "Detail" ]

                    return exitCode
        }

        async {
            match input with
            | Input.Option.Has "watch" _ ->
                let domainPath, watchDomainSubdirs = domain |> FileOrDir.watch
                let tucPath, watchTucSubdirs = tucFileOrDir |> FileOrDir.watch

                [
                    (tucPath, "*.tuc")
                    |> Watch.watch output watchTucSubdirs (execute GeneratePuml.InWatch |> Async.Ignore)

                    (domainPath, "*Domain.fsx")
                    |> Watch.watch output watchDomainSubdirs (execute GeneratePuml.InWatch |> Async.Ignore)
                ]
                |> List.iter Async.Start

                do! Watch.executeAndWaitForWatch output (execute (GeneratePuml.Immediately domain) |> Async.Ignore)

                return ExitCode.Success
            | _ ->
                return! execute (GeneratePuml.Immediately domain)
        }
