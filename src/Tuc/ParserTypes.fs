namespace MF.Tuc.Parser

open System
open MF.TucConsole
open MF.Tuc
open MF.Domain

type private DomainTypes = DomainTypes of Map<TypeName, ResolvedType>

type private Depth = Depth of int
type private Indentation = Indentation of int
type private IndentationLevel = IndentationLevel of Indentation

[<RequireQualifiedAccess>]
module private Indentation =
    let size (Indentation size) = size

    let goDeeperBy (Depth depth) (IndentationLevel (Indentation level)) (Indentation indentation) =
        if depth < 0 then failwithf "[Logic] Indentation cannot go deeper by %d." depth
        Indentation (indentation * depth + level)

    let goDeeper =
        goDeeperBy (Depth 1)

[<RequireQualifiedAccess>]
module private IndentationLevel =
    let indentation (IndentationLevel indentation) = indentation
    let size = indentation >> Indentation.size

[<RequireQualifiedAccess>]
module private Depth =
    let value (Depth depth) = depth
    let ofIndentation level indentation =
        (indentation |> Indentation.size) / (level |> IndentationLevel.size)
        |> Depth

type private RawLine = {
    Number: int
    Original: string
    Indentation: Indentation
    Content: string
    Comment: string option
}

[<RequireQualifiedAccess>]
module private RawLine =
    let parse index line =
        let content, comment =
            match line with
            | Regex @"^(.*?)?(#.*){1}$" [ content; comment ] -> content, Some comment
            | content -> content, None

        let content = content.Trim ' '

        let indentation =
            match line with
            | Regex "^([ ]*)" [ indentation ] -> Indentation indentation.Length
            | _ -> Indentation 0

        {
            Number = index + 1
            Original = line
            Indentation = indentation
            Content = content
            Comment = comment
        }

    let isEmpty ({ Content = content }: RawLine) = content |> String.IsNullOrWhiteSpace

    let valuei ({ Number = number; Original = line }: RawLine) =
        sprintf "% 3i| %s" number line

type private Line = {
    Number: int
    Original: string
    Depth: Depth
    Indentation: Indentation
    Content: string
    Tokens: string list
    Comment: string option
}

[<RequireQualifiedAccess>]
module private Line =
    let ofRawLine indentationLevel (rawLine: RawLine) =
        {
            Number = rawLine.Number
            Original = rawLine.Original
            Depth = Depth ((rawLine.Indentation |> Indentation.size) / (indentationLevel |> IndentationLevel.size))
            Indentation = rawLine.Indentation
            Content = rawLine.Content
            Tokens = rawLine.Content |> String.split " " |> List.map (String.trim ' ')
            Comment = rawLine.Comment
        }

    let format ({ Number = number; Original = line }: Line) =
        sprintf "<c:gray>% 3i|</c> %s" number line

    let debug ({ Number = number; Original = line; Depth = depth; Indentation = indentation }: Line) =
        sprintf "<c:gray>% 3i|</c> %s  <c:gray>// %A | %A</c>" number line depth indentation

    let valuei ({ Number = number; Original = line }: Line) =
        sprintf "% 3i| %s" number line

    let content ({ Content = content }: Line) = content
    let indentation ({ Indentation = indentation }: Line) = indentation

    let isIndented indentation ({ Indentation = lineIndentation }: Line) = lineIndentation = indentation
    let isIndentedOrMore indentation ({ Indentation = lineIndentation }: Line) = lineIndentation >= indentation

    let error (Indentation position) ({ Number = number; Original = line }: Line) = number, position, line

[<RequireQualifiedAccess>]
module private Errors =
    let calledUndefinedMethod indentation service definedMethods line =
        let n, p, c = line |> Line.error indentation
        CalledUndefinedMethod (n, p, c, service, definedMethods)

    let calledUndefinedHandler indentation service definedHandlers line =
        let n, p, c = line |> Line.error indentation
        CalledUndefinedHandler (n, p, c, service, definedHandlers)

    let wrongEventName indentation line =
        let n, p, c = line |> Line.error indentation

        function
        | EventError.Empty -> WrongEventName (n, p, c, "it has empty name")
        | EventError.WrongFormat -> WrongEventName (n, p, c, "it has a wrong format (it must not start/end with . and not contains any spaces)")

    let wrongEvent indentation line cases =
        let n, p, c = line |> Line.error indentation
        WrongEvent (n, p, c, cases)

    let undefinedParticipantInDomain indentation line domain =
        let n, p, c = line |> Line.error indentation
        UndefinedParticipantInDomain (n, p, c, domain |> DomainName.value)

    let wrongComponentParticipantDomain indentation line componentDomain =
        let n, p, c = line |> Line.error indentation
        WrongComponentParticipantDomain (n, p, c, componentDomain |> DomainName.value)

module private ParserPatterns =
    let (|HasDomainType|_|) name (DomainTypes domainTypes) =
        domainTypes
        |> Map.tryFind (TypeName name)
        |> Option.map DomainType

    let (|LineDepth|_|) (Depth depth): Line -> _ = function
        | { Depth = (Depth lineDepth) } as line when lineDepth = depth -> Some line
        | _ -> None

    let (|DeeperLine|_|) (Depth depth): Line -> _ = function
        | { Depth = (Depth lineDepth) } as line when lineDepth > depth -> Some line
        | _ -> None

    let (|IndentedLine|_|) (Indentation indentation): Line -> _ = function
        | { Indentation = (Indentation lineIndentation) } as line when lineIndentation = indentation -> Some line
        | _ -> None

    let (|LineContent|_|) content: Line -> _ = function
        | { Content = lineContent } when lineContent = content -> Some ()
        | _ -> None
