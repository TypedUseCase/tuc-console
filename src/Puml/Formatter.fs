namespace MF.Puml

open MF.TucConsole

[<RequireQualifiedAccess>]
module Format =
    type private Formatter = Formatter of (string -> string)

    [<RequireQualifiedAccess>]
    module private Formatter =
        open System.Text.RegularExpressions

        let italics = Formatter (function
            | withoutAsterisk when withoutAsterisk.Contains "*" |> not -> withoutAsterisk
            | original ->
                let withoutBold = Regex.Replace(original, @"\*{2}([^\*]+)\*{2}", "")
                let italicsMatches = Regex.Matches(withoutBold, @"(\*[^\*]+\*)")

                if italicsMatches.Count > 0
                    then
                        let creoleItalics (value: string) =
                            value.Trim '*'
                            |> sprintf "//%s//"

                        italicsMatches
                        |> Seq.map (fun m -> m.Value)
                        |> Seq.sortByDescending String.length
                        |> Seq.fold (fun (formatted: string) italicMatch ->
                            formatted.Replace(italicMatch, creoleItalics italicMatch)
                        ) original
                    else original
        )

    let private useFormatters formatters input =
        formatters
        |> List.fold (fun input (Formatter format) -> format input) input

    let format =
        useFormatters [
            Formatter.italics
        ]
