namespace MF.Tuc

open MF.TucConsole
open MF.Domain

type ParseError =
    // Tuc file
    | MissingParticipants
    | MissingIndentation
    | WrongIndentation of lineNumber: int * position: int * line: string
    | WrongIndentationLevel of indentationLevel: int * lines: string list

    // Participants
    | WrongParticipantIndentation of lineNumber: int * position: int * line: string
    | ComponentWithoutParticipants of lineNumber: int * position: int * line: string
    | UndefinedComponentParticipant of lineNumber: int * position: int * line: string * componentName: string * definedFields: string list
    | InvalidParticipantIndentation of lineNumber: int * position: int * line: string
    | InvalidParticipant of lineNumber: int * position: int * line: string
    | UndefinedParticipant of lineNumber: int * position: int * line: string

[<RequireQualifiedAccess>]
module ParseError =
    let private formatLine lineNumber line =
        sprintf "<c:gray>% 3i|</c> %s" lineNumber line

    let private errorAtPostion position error =
        sprintf "%s<c:red>^---</c> %s"
            (" " |> String.replicate (position + "999| ".Length))
            error

    let private formatLineWithError lineNumber position line error =
        sprintf "%s\n%s"
            (line |> formatLine lineNumber)
            (error |> errorAtPostion position)

    let private red = sprintf "<c:red>%s</c>"

    let format = function
        | MissingParticipants ->
            red "There are no participants defined in the tuc file. (Or the \"participants\" keyword is wrongly written)"

        | MissingIndentation ->
            red "There are no indented line in the tuc file."

        | WrongIndentation (lineNumber, position, line) ->
            "There is a wrong indentation."
            |> red
            |> formatLineWithError lineNumber position line

        | WrongIndentationLevel (indentationLevel, lines) ->
            sprintf "<c:red>There is a wrong indentation level on these lines. (It should be multiples of %d leading spaces, which is based on the first indented line in the tuc file):</c>%s"
                indentationLevel
                (lines |> List.formatLines "" id)

        | WrongParticipantIndentation (lineNumber, position, line) ->
            "Participant is wrongly indented. (It is probably indented too much)"
            |> red
            |> formatLineWithError lineNumber position line

        | ComponentWithoutParticipants (lineNumber, position, line) ->
            "Component must have its participants defined, there are none here. (Or they are not indented maybe?)"
            |> red
            |> formatLineWithError lineNumber position line

        | UndefinedComponentParticipant (lineNumber, position, line, componentName, definedFields) ->
            let error =
                sprintf "This participant is not defined as one of the field of the component %s." componentName
                |> red
                |> formatLineWithError lineNumber position line

            sprintf "%s\n\n<c:red>Component %s has defined fields:%s</c>"
                error
                componentName
                (definedFields |> List.formatLines "  - " id)

        | InvalidParticipantIndentation (lineNumber, position, line) ->
            "There is an invalid participant indentation."
            |> red
            |> formatLineWithError lineNumber position line

        | InvalidParticipant (lineNumber, position, line) ->
            "<c:red>There is an invalid participant. Participant format is:</c> <c:cyan>ServiceName Domain</c> <c:yellow>(as \"alias\")</c> <c:red>(Alias part is optional)</c>"
            |> formatLineWithError lineNumber position line

        | UndefinedParticipant (lineNumber, position, line) ->
            "There is an undefined participant. (It is not defined in the given Domain types)"
            |> red
            |> formatLineWithError lineNumber position line

[<RequireQualifiedAccess>]
module Parser =
    open System
    open System.IO
    open ErrorHandling

    type private DomainTypes = Map<TypeName, ResolvedType>

    type private Depth = Depth of int
    type private Indentation = Indentation of int
    type private IndentationLevel = IndentationLevel of Indentation

    [<RequireQualifiedAccess>]
    module private Indentation =
        let size (Indentation size) = size
        let goDeeper (IndentationLevel (Indentation level)) (Indentation indentation) = Indentation (indentation + level)

    [<RequireQualifiedAccess>]
    module private IndentationLevel =
        let indentation (IndentationLevel indentation) = indentation
        let size = indentation >> Indentation.size

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
                | Regex @"^(.*?)?(\/\/.*){1}$" [ content; comment ] -> content, Some comment
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

        let valuei ({ Number = number; Original = line }: Line) =
            sprintf "% 3i| %s" number line

        let content ({ Content = content }: Line) = content
        let indentation ({ Indentation = indentation }: Line) = indentation

        let isIndented indentation ({ Indentation = lineIndentation }: Line) = lineIndentation = indentation
        let isIndentedOrMore indentation ({ Indentation = lineIndentation }: Line) = lineIndentation >= indentation

        //let isIndented indentation ({ Indentation = lineIndentation }: Line) = lineIndentation > indentation    // todo - use depht
        //let isIndentedOrMore indentation ({ Indentation = lineIndentation }: Line) = lineIndentation >= indentation // todo - use depht

        let error (position: int) ({ Number = number; Original = line }: Line) = number, position, line

    (* type private BodyLine =
        | InBody of Line
        | Outside of Line

    [<RequireQualifiedAccess>]
    module private BodyLine =
        let select indentation = function
            | { Indentation = lineIndentation } as line when lineIndentation >= indentation -> InBody line
            | line -> Outside line

        let isInBody = function
            | InBody _ -> true
            | _ -> false

        let line = function
            | InBody line
            | Outside line -> line *)

    type private ParseResult<'TucItem> = {
        Item: 'TucItem
        Lines: Line list
    }

    type private Parse<'TucItem> = MF.ConsoleApplication.Output -> IndentationLevel -> Line list -> Result<'TucItem, ParseError>
    type private ParseLines<'TucItem> = MF.ConsoleApplication.Output -> IndentationLevel -> Line list -> Result<ParseResult<'TucItem>, ParseError>

    let private (|LineDepth|_|) (Depth depth): Line -> _ = function
        | { Depth = (Depth lineDepth) } as line when lineDepth = depth -> Some line
        | _ -> None

    let private (|IndentedLine|_|) (Indentation indentation): Line -> _ = function
        | { Indentation = (Indentation lineIndentation) } as line when lineIndentation = indentation -> Some line
        | _ -> None

    let private (|HasDomainType|_|) name (domainTypes: DomainTypes) =
        domainTypes |> Map.tryFind (TypeName name)

    let private (|LineContent|_|) content: Line -> _ = function
        | { Content = lineContent } when lineContent = content -> Some ()
        | _ -> None

    [<RequireQualifiedAccess>]
    module private KeyWord =
        let (|Participants|_|): Line -> _ = function
            | { Content = "participants"; Depth = Depth 0 } -> Some ()
            | _ -> None

    [<RequireQualifiedAccess>]
    module private Participants =
        let private parseService serviceName serviceDomain alias indentation line domainTypes =
            let service = Service >> Ok

            match domainTypes with
            | HasDomainType serviceName (Record _ as componentType) ->
                service {
                    Domain = serviceDomain
                    Context = serviceName
                    Alias = alias
                    ServiceType = DomainType componentType
                }
            | HasDomainType serviceName (SingleCaseUnion { ConstructorName = "Initiator" } as serviceType) ->
                service {
                    Domain = serviceDomain
                    Context = serviceName
                    Alias = alias
                    ServiceType = DomainType serviceType
                }
            | _ -> Error <| UndefinedParticipant (line |> Line.error (indentation |> Indentation.size))

        let private parseStream streamName streamDomain alias indentation line domainTypes =
            let stream = ActiveParticipant.Stream >> Ok

            match domainTypes with
            | HasDomainType streamName (Stream _ as componentType) ->
                stream {
                    Domain = streamDomain
                    Context = streamName
                    Alias = alias
                    StreamType = DomainType componentType
                }
            | _ -> Error <| UndefinedParticipant (line |> Line.error (indentation |> Indentation.size))

        let private parseActiveParticipant (domainTypes: DomainTypes) indentation line =
            match line with
            | IndentedLine indentation line ->
                match line |> Line.content with
                | Regex @"^(\w+){1} (\w+){1}$" [ serviceName; serviceDomain ] ->
                    domainTypes |> parseService serviceName serviceDomain serviceName indentation line

                | Regex @"^(\w+){1} (\w+){1} as ""(.+){1}""$" [ serviceName; serviceDomain; alias ] ->
                    domainTypes |> parseService serviceName serviceDomain alias indentation line

                | Regex @"^\[(\w+){1}\] (\w+){1}$" [ streamName; streamDomain ] ->
                    domainTypes |> parseStream streamName streamDomain streamName indentation line

                | Regex @"^\[(\w+){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    domainTypes |> parseStream streamName streamDomain alias indentation line

                | _ -> Error <| InvalidParticipant (line |> Line.error (indentation |> Indentation.size))

            | _ -> Error <| InvalidParticipantIndentation (line |> Line.error (indentation |> Indentation.size))

        let private parseParticipant (domainTypes: DomainTypes) indentationLevel lines line: Result<Participant * Line list, _> =
            let participantIndentation = indentationLevel |> IndentationLevel.indentation

            match line |> Line.content with
            | Regex @"^(\w+){1}$" [ componentName ] ->
                match domainTypes with
                | HasDomainType componentName (Record { Fields = componentFields }) ->
                    result {
                        let componentParticipantIndentation = participantIndentation |> Indentation.goDeeper indentationLevel

                        let componentParticipantLines, lines =
                            lines
                            |> List.splitBy (Line.isIndentedOrMore componentParticipantIndentation)

                        let! componentParticipants =
                            componentParticipantLines
                            |> List.map (parseActiveParticipant domainTypes componentParticipantIndentation)
                            |> Result.sequence

                        if componentParticipants |> List.isEmpty then
                            return! Error <| ComponentWithoutParticipants (line |> Line.error (indentationLevel |> IndentationLevel.size))

                        let! _ =
                            componentParticipants
                            |> List.mapi (fun index participant ->
                                if participant |> ActiveParticipant.name |> FieldName |> componentFields.ContainsKey
                                    then Ok ()
                                    else
                                        let number, position, content =
                                            componentParticipantLines.[index]
                                            |> Line.error (componentParticipantIndentation |> Indentation.size)

                                        let definedFields =
                                            componentFields
                                            |> Map.keys
                                            |> List.map FieldName.value

                                        Error <| UndefinedComponentParticipant (number, position, content, componentName, definedFields)
                            )
                            |> Result.sequence

                        return Component { Name = componentName; Participants = componentParticipants }, lines
                    }
                | _ -> Error <| UndefinedParticipant (line |> Line.error 0)

            | _ ->
                result {
                    let! activeParticipant =
                        line
                        |> parseActiveParticipant domainTypes participantIndentation

                    return Participant activeParticipant, lines
                }

        let rec private parseParticipants domainTypes participants: Parse<Participant list> = fun output indentationLevel -> function
            | [] ->
                match participants with
                | [] -> Error MissingParticipants
                | participants -> Ok (participants |> List.rev)

            | LineDepth (Depth 1) line :: lines ->
                result {
                    let! participant, lines =
                        line
                        |> parseParticipant domainTypes indentationLevel lines

                    return!
                        lines
                        |> parseParticipants domainTypes (participant :: participants) output indentationLevel
                }

            | line :: _ ->
                Error <| WrongParticipantIndentation (line |> Line.error (indentationLevel |> IndentationLevel.size))

        let parse domainTypes: ParseLines<Participant list> = fun output indentationLevel -> function
            | [] -> Error MissingParticipants

            | KeyWord.Participants :: lines ->
                result {
                    let participantLines, lines =
                        lines
                        |> List.splitBy (Line.isIndentedOrMore (indentationLevel |> IndentationLevel.indentation))

                    let! participants =
                        participantLines
                        |> parseParticipants domainTypes [] output indentationLevel

                    return { Item = participants; Lines = lines }
                }

            | _ -> Error MissingParticipants

    let private parseTuc (output: MF.ConsoleApplication.Output) domainTypes indentationLevel lines =
        result {
            let! { Item = participants; Lines = lines } =
                lines
                |> Participants.parse domainTypes output indentationLevel

            // todo parse lines to parts

            return {
                Participants = participants
                Parts = []
            }
        }

    let private assertLinesIndentation indentationLevel (lines: RawLine list) =
            let indentationLevel = indentationLevel |> IndentationLevel.size

            lines
            |> List.filter (fun { Indentation = (Indentation indentation) } -> indentation % indentationLevel <> 0)
            |> function
                | [] -> Ok ()
                | wrongLines -> Error <| WrongIndentationLevel (indentationLevel, wrongLines |> List.map RawLine.valuei)

    let parse (output: MF.ConsoleApplication.Output) (domainTypes: DomainType list) file = result {
        if output.IsVerbose() then output.Title <| sprintf "Parse %A" file

        let domainTypes: DomainTypes =
            domainTypes
            |> List.map (fun (DomainType t) -> t |> ResolvedType.name, t)
            |> Map.ofList

        let rawLines =
            file
            |> File.ReadAllLines
            |> Seq.mapi RawLine.parse
            |> Seq.filter (RawLine.isEmpty >> not)
            |> Seq.toList

        let! indentationLevel =
            rawLines
            |> List.tryPick (function
                | { Indentation = indentation } when indentation |> Indentation.size > 0 ->  Some (IndentationLevel indentation)
                | _ -> None
            )
            |> Result.ofOption MissingIndentation

        if output.IsVerbose() then
            output.Message <| sprintf "[Tuc] Current indentation level is <c:magenta>%d</c>" (indentationLevel |> IndentationLevel.size)

        do! rawLines |> assertLinesIndentation indentationLevel

        (*
            lines   // todo remove
            |> List.take 5
            |> List.map Line.format
            |> output.Messages ""
            |> output.NewLine
         *)

        return!
            rawLines
            |> List.map (Line.ofRawLine indentationLevel)
            |> parseTuc output domainTypes indentationLevel
    }
