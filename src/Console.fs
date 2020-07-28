namespace MF.TucConsole

module Console =
    open MF.ConsoleApplication

    let commandHelp lines = lines |> String.concat "\n\n" |> Some

    /// Concat two lines into one line for command help, so they won't be separated by other empty line
    let inline (<+>) line1 line2 = sprintf "%s\n%s" line1 line2

    [<RequireQualifiedAccess>]
    module Argument =
        let domains = Argument.requiredArray "domains" "Path to a file or dir containing a domain specification (in F# type notation)."

    (* [<RequireQualifiedAccess>]
    module Option =
        let outputFile = Option.optional "output" (Some "o") "File where the output will be written." None *)

    [<RequireQualifiedAccess>]
    module Input =
        open System.IO

        let getDomains ((input, output): IO) =
            let domains =
                input
                |> Input.getArgumentValueAsList "domains"
                |> List.collect (function
                    | fsx when fsx |> File.Exists && fsx.EndsWith ".fsx" -> [ fsx ]
                    | dir when dir |> Directory.Exists -> [ dir ] |> FileSystem.getAllFiles |> List.filter (fun f -> f.EndsWith ".fsx")
                    | _ -> []
                )

            domains
            |> List.map List.singleton
            |> output.Options "Domain files:"

            domains
