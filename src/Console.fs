namespace MF.TucConsole

module Console =
    open MF.ConsoleApplication

    let commandHelp lines = lines |> String.concat "\n\n" |> Some

    /// Concat two lines into one line for command help, so they won't be separated by other empty line
    let inline (<+>) line1 line2 = sprintf "%s\n%s" line1 line2

    [<RequireQualifiedAccess>]
    module Argument =
        let domain = Argument.required "domain" "Path to a file or dir containing a domain specification (in F# type notation)."

    type DomainArgument =
        | SingleFile of string
        | Dir of string * string list

    [<RequireQualifiedAccess>]
    module Input =
        open System.IO

        let getDomain ((input, output): IO) =
            let domain =
                match input |> Input.getArgumentValueAsString "domain" with
                | Some fsx when fsx |> File.Exists && fsx.EndsWith ".fsx" ->
                    SingleFile fsx

                | Some dir when dir |> Directory.Exists ->
                    Dir (
                        dir,
                        [ dir ] |> FileSystem.getAllFiles |> List.filter (fun f -> f.EndsWith ".fsx")
                    )

                | invalidPath -> failwithf "Domain path %A is invalid." invalidPath

            match domain with
            | SingleFile file -> [[ file ]]
            | Dir (_, files) -> files |> List.map List.singleton
            |> output.Options "Domain files:"

            domain
