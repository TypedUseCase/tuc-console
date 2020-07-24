namespace MF.TucConsole

module Console =
    open MF.ConsoleApplication

    let commandHelp lines = lines |> String.concat "\n\n" |> Some

    /// Concat two lines into one line for command help, so they won't be separated by other empty line
    let inline (<+>) line1 line2 = sprintf "%s\n%s" line1 line2

    (* [<RequireQualifiedAccess>]
    module Argument =
        let repositories = Argument.requiredArray "repositories" "Path to dir containing repositories." *)

    (* [<RequireQualifiedAccess>]
    module Option =
        let outputFile = Option.optional "output" (Some "o") "File where the output will be written." None *)

    (* [<RequireQualifiedAccess>]
    module Input =
        let getRepositories = Input.getArgumentValueAsList "repositories" *)
