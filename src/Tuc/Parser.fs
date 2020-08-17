namespace MF.Tuc

open MF.TucConsole
open MF.Domain

type ParseError =
    // Tuc file
    | MissingTucName
    | TucMustHaveName of lineNumber: int * position: int * line: string
    | MissingParticipants
    | MissingIndentation
    | WrongIndentation of lineNumber: int * position: int * line: string
    | WrongIndentationLevel of indentationLevel: int * lines: string list
    | TooMuchIndented of lineNumber: int * position: int * line: string

    // Participants
    | WrongParticipantIndentation of lineNumber: int * position: int * line: string
    | ComponentWithoutParticipants of lineNumber: int * position: int * line: string
    | UndefinedComponentParticipant of lineNumber: int * position: int * line: string * componentName: string * definedFields: string list
    | InvalidParticipantIndentation of lineNumber: int * position: int * line: string
    | InvalidParticipant of lineNumber: int * position: int * line: string
    | UndefinedParticipant of lineNumber: int * position: int * line: string

    // Parts
    | MissingUseCase of TucName
    | SectionWithoutName of lineNumber: int * position: int * line: string
    | IsNotInitiator of lineNumber: int * position: int * line: string
    | CalledUndefinedMethod of lineNumber: int * position: int * line: string * service: string * definedMethods: string list
    | MethodCalledWithoutACaller of lineNumber: int * position: int * line: string
    | EventPostedWithoutACaller of lineNumber: int * position: int * line: string
    | EventReadWithoutACaller of lineNumber: int * position: int * line: string
    | WrongEventPostedToStream of lineNumber: int * position: int * line: string * stream: string * definedEvent: string
    | WrongEventReadFromStream of lineNumber: int * position: int * line: string * stream: string * definedEvent: string
    | MissingEventHandlerMethodCall of lineNumber: int * position: int * line: string
    | InvalidMultilineNote of lineNumber: int * position: int * line: string
    | InvalidMultilineLeftNote of lineNumber: int * position: int * line: string
    | InvalidMultilineRightNote of lineNumber: int * position: int * line: string
    | DoWithoutACaller of lineNumber: int * position: int * line: string
    | DoMustHaveActions of lineNumber: int * position: int * line: string
    | IfWithoutCondition of lineNumber: int * position: int * line: string
    | IfMustHaveBody of lineNumber: int * position: int * line: string
    | ElseOutsideOfIf of lineNumber: int * position: int * line: string
    | ElseMustHaveBody of lineNumber: int * position: int * line: string
    | GroupWithoutName of lineNumber: int * position: int * line: string
    | GroupMustHaveBody of lineNumber: int * position: int * line: string
    | LoopWithoutCondition of lineNumber: int * position: int * line: string
    | LoopMustHaveBody of lineNumber: int * position: int * line: string
    | NoteWithoutACaller of lineNumber: int * position: int * line: string
    | UnknownPart of lineNumber: int * position: int * line: string

[<RequireQualifiedAccess>]
module ParseError =
    let private formatLine lineNumber line =
        sprintf "<c:gray>% 3i|</c> %s" lineNumber line

    let private errorAtPostion position error =
        sprintf "%s<c:red>^---</c> %s"
            (" " |> String.replicate (position + "999| ".Length))
            error

    let private formatLineWithError baseIndentation lineNumber position line error =
        sprintf "%s\n%s"
            (line |> formatLine lineNumber)
            (error |> errorAtPostion (baseIndentation + position))

    let private red = sprintf "<c:red>%s</c>"

    let format baseIndentation =
        let formatLineWithError =
            formatLineWithError baseIndentation

        function
        // Tuc file
        | MissingTucName ->
            red "There is no tuc name defined."

        | TucMustHaveName (lineNumber, position, line) ->
            "Tuc must have a name."
            |> red
            |> formatLineWithError lineNumber (position + "tuc ".Length) line

        | MissingParticipants ->
            red "There are no participants defined in the tuc file. (Or the \"participants\" keyword is wrongly written or indented)"

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

        | TooMuchIndented (lineNumber, position, line) ->
            "This line is too much indented from the current context."
            |> red
            |> formatLineWithError lineNumber position line

        // Participants

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

        // Parts

        | MissingUseCase (TucName name) ->
            name
            |> sprintf "<c:red>There is no use-case defined in the tuc</c> <c:cyan>%s</c><c:red>.</c>"

        | SectionWithoutName (lineNumber, position, line) ->
            "Section must have a name."
            |> red
            |> formatLineWithError lineNumber (position + "section ".Length) line

        | IsNotInitiator (lineNumber, position, line) ->
            "Only Initiator service can have a lifeline."
            |> red
            |> formatLineWithError lineNumber position line

        | MethodCalledWithoutACaller (lineNumber, position, line) ->
            "Method can be called only in the lifeline of a caller."
            |> red
            |> formatLineWithError lineNumber position line

        | EventPostedWithoutACaller (lineNumber, position, line) ->
            "Event can be posted only in the lifeline of a caller."
            |> red
            |> formatLineWithError lineNumber position line

        | EventReadWithoutACaller (lineNumber, position, line) ->
            "Event can be read only in the lifeline of a caller."
            |> red
            |> formatLineWithError lineNumber position line

        | CalledUndefinedMethod (lineNumber, position, line, serviceName, definedMethods) ->
            let error =
                sprintf "There is an undefined method called on the service %s." serviceName
                |> red
                |> formatLineWithError lineNumber (position + serviceName.Length) line

            sprintf "%s\n\n<c:red>Service %s has defined methods:%s</c>"
                error
                serviceName
                (definedMethods |> List.formatLines "  - " id)

        | WrongEventPostedToStream (lineNumber, position, line, streamName, definedEventType) ->
            let error =
                sprintf "There is a wrong event posted to the %s." streamName
                |> red
                |> formatLineWithError lineNumber (position + streamName.Length) line

            sprintf "%s\n\n<c:red>%s is a Stream of %s</c>"
                error
                streamName
                definedEventType

        | WrongEventReadFromStream (lineNumber, position, line, streamName, definedEventType) ->
            let error =
                sprintf "There is a wrong event read from the %s." streamName
                |> red
                |> formatLineWithError lineNumber (position + streamName.Length) line

            sprintf "%s\n\n<c:red>%s is a Stream of %s</c>"
                error
                streamName
                definedEventType

        | MissingEventHandlerMethodCall (lineNumber, position, line) ->
            "There must be exactly one method call which handles the stream."
            |> red
            |> formatLineWithError lineNumber position line

        | InvalidMultilineNote (lineNumber, position, line) ->
            "Invalid multiline note. (It must start and end on the same level with \"\"\")"
            |> red
            |> formatLineWithError lineNumber position line

        | InvalidMultilineLeftNote (lineNumber, position, line) ->
            "Invalid multiline left note. (It must start and end on the same level with \"<\")"
            |> red
            |> formatLineWithError lineNumber position line

        | InvalidMultilineRightNote (lineNumber, position, line) ->
            "Invalid multiline right note. (It must start and end on the same level with \">\")"
            |> red
            |> formatLineWithError lineNumber position line

        | DoMustHaveActions (lineNumber, position, line) ->
            "Do must have an action on the same line, or there must be at least one action indented on the subsequent line."
            |> red
            |> formatLineWithError lineNumber position line

        | DoWithoutACaller (lineNumber, position, line) ->
            "Do can be only in the lifeline of a caller."
            |> red
            |> formatLineWithError lineNumber position line

        | IfWithoutCondition (lineNumber, position, line) ->
            "If must have a condition."
            |> red
            |> formatLineWithError lineNumber (position + "if ".Length) line

        | IfMustHaveBody (lineNumber, position, line) ->
            "If must have a body. (It must be indented)"
            |> red
            |> formatLineWithError lineNumber position line

        | ElseOutsideOfIf (lineNumber, position, line) ->
            "There must be an If before an Else."
            |> red
            |> formatLineWithError lineNumber position line

        | ElseMustHaveBody (lineNumber, position, line) ->
            "Else must have a body. (It must be indented)"
            |> red
            |> formatLineWithError lineNumber position line

        | GroupWithoutName (lineNumber, position, line) ->
            "Group must have a name."
            |> red
            |> formatLineWithError lineNumber (position + "group ".Length) line

        | GroupMustHaveBody (lineNumber, position, line) ->
            "Group must have a body. (It must be indented)"
            |> red
            |> formatLineWithError lineNumber position line

        | LoopWithoutCondition (lineNumber, position, line) ->
            "Loop must have a condition."
            |> red
            |> formatLineWithError lineNumber (position + "loop ".Length) line

        | LoopMustHaveBody (lineNumber, position, line) ->
            "Loop must have a body. (It must be indented)"
            |> red
            |> formatLineWithError lineNumber position line

        | NoteWithoutACaller (lineNumber, position, line) ->
            "Note can be only in the lifeline of a caller."
            |> red
            |> formatLineWithError lineNumber position line

        | UnknownPart (lineNumber, position, line) ->
            "There is an unknown part or an undefined participant. (Or wrongly indented)"
            |> red
            |> formatLineWithError lineNumber position line

[<RequireQualifiedAccess>]
module Parser =
    open System
    open System.IO
    open ErrorHandling

    type private DomainTypes = DomainTypes of Map<TypeName, ResolvedType>

    type private Depth = Depth of int
    type private Indentation = Indentation of int
    type private IndentationLevel = IndentationLevel of Indentation

    [<RequireQualifiedAccess>]
    module private Depth =
        let value (Depth depth) = depth

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

        let error (Indentation position) ({ Number = number; Original = line }: Line) = number, position, line

    [<RequireQualifiedAccess>]
    module private Errors =
        let calledUndefinedMethod indentation service definedMethods line =
            let n, p, c = line |> Line.error indentation
            CalledUndefinedMethod (n, p, c, service, definedMethods)

        let wrongEventPostedToStream indentation stream eventType line =
            let n, p, c = line |> Line.error indentation
            WrongEventPostedToStream (n, p, c, stream, eventType)

        let wrongEventReadFromStream indentation stream eventType line =
            let n, p, c = line |> Line.error indentation
            WrongEventReadFromStream (n, p, c, stream, eventType)

    type private ParseResult<'TucItem> = {
        Item: 'TucItem
        Lines: Line list
    }

    type private ParseLines<'TucItem> = MF.ConsoleApplication.Output -> IndentationLevel -> Line list -> Result<ParseResult<'TucItem>, ParseError>

    let private (|LineDepth|_|) (Depth depth): Line -> _ = function
        | { Depth = (Depth lineDepth) } as line when lineDepth = depth -> Some line
        | _ -> None

    let private (|IndentedLine|_|) (Indentation indentation): Line -> _ = function
        | { Indentation = (Indentation lineIndentation) } as line when lineIndentation = indentation -> Some line
        | _ -> None

    let private (|HasDomainType|_|) name (DomainTypes domainTypes) =
        domainTypes
        |> Map.tryFind (TypeName name)
        |> Option.map DomainType

    let private (|LineContent|_|) content: Line -> _ = function
        | { Content = lineContent } when lineContent = content -> Some ()
        | _ -> None

    [<RequireQualifiedAccess>]
    module private KeyWord =
        let (|Tuc|_|): Line -> _ = function
            | { Tokens = "tuc" :: name; Depth = Depth 0 } -> Some (name |> String.concat " ")
            | _ -> None

        let (|Participants|_|): Line -> _ = function
            | { Content = "participants"; Depth = Depth 0 } -> Some ()
            | _ -> None

        let (|Section|_|): Line -> _ = function
            | { Tokens = "section" :: section; Depth = Depth 0 } -> Some (section |> String.concat " ")
            | _ -> None

        let (|SingleLineDo|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = "do" :: action } -> Some (action |> String.concat " ")
            | _ -> None

        let (|SingleLineNote|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Content = note } when note.StartsWith '"' && note.EndsWith '"' -> Some (note.Trim '"')
            | _ -> None

        let (|SingleLineLeftNote|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Content = note } when note.StartsWith @"""<" && note.EndsWith '"' -> Some (note.Trim('"').TrimStart('<').Trim())
            | _ -> None

        let (|SingleLineRightNote|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Content = note } when note.StartsWith @""">" && note.EndsWith '"' -> Some (note.Trim('"').TrimStart('>').Trim())
            | _ -> None

        let (|If|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = "if" :: condition } -> Some (condition |> String.concat " ")
            | _ -> None

        let (|Else|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ "else" ] } -> Some ()
            | _ -> None

        let (|Group|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = "group" :: group } -> Some (group |> String.concat " ")
            | _ -> None

        let (|Loop|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = "loop" :: condition } -> Some (condition |> String.concat " ")
            | _ -> None

    [<RequireQualifiedAccess>]
    module private Participants =
        type private ParseParticipants = MF.ConsoleApplication.Output -> DomainTypes -> IndentationLevel -> Line list -> Result<Participant list, ParseError>

        let private parseService serviceName serviceDomain alias indentation line domainTypes =
            let service = Service >> Ok

            match domainTypes with
            | HasDomainType serviceName (DomainType (Record _ as componentType)) ->
                service {
                    Domain = serviceDomain
                    Context = serviceName
                    Alias = alias
                    ServiceType = DomainType componentType
                }
            | HasDomainType serviceName (DomainType.Initiator as serviceType) ->
                service {
                    Domain = serviceDomain
                    Context = serviceName
                    Alias = alias
                    ServiceType = serviceType
                }
            | _ -> Error <| UndefinedParticipant (line |> Line.error indentation)

        let private parseStream streamName streamDomain alias indentation line domainTypes =
            let stream = ActiveParticipant.Stream >> Ok

            match domainTypes with
            | HasDomainType streamName (DomainType (Stream _ as componentType)) ->
                stream {
                    Domain = streamDomain
                    Context = streamName
                    Alias = alias
                    StreamType = DomainType componentType
                }
            | _ -> Error <| UndefinedParticipant (line |> Line.error indentation)

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

                | _ -> Error <| InvalidParticipant (line |> Line.error indentation)

            | _ -> Error <| InvalidParticipantIndentation (line |> Line.error indentation)

        let private parseParticipant (domainTypes: DomainTypes) indentationLevel lines line: Result<Participant * Line list, _> =
            let participantIndentation = indentationLevel |> IndentationLevel.indentation

            match line |> Line.content with
            | Regex @"^(\w+){1}$" [ componentName ] ->
                match domainTypes with
                | HasDomainType componentName (DomainType (Record { Fields = componentFields })) ->
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
                            return! Error <| ComponentWithoutParticipants (line |> Line.error (indentationLevel |> IndentationLevel.indentation))

                        let! _ =
                            componentParticipants
                            |> List.mapi (fun index participant ->
                                if participant |> ActiveParticipant.name |> FieldName |> componentFields.ContainsKey
                                    then Ok ()
                                    else
                                        let number, position, content =
                                            componentParticipantLines.[index]
                                            |> Line.error componentParticipantIndentation

                                        let definedFields =
                                            componentFields
                                            |> Map.keys
                                            |> List.map FieldName.value

                                        Error <| UndefinedComponentParticipant (number, position, content, componentName, definedFields)
                            )
                            |> Result.sequence

                        return Component { Name = componentName; Participants = componentParticipants }, lines
                    }
                | _ -> Error <| UndefinedParticipant (line |> Line.error (indentationLevel |> IndentationLevel.indentation))

            | _ ->
                result {
                    let! activeParticipant =
                        line
                        |> parseActiveParticipant domainTypes participantIndentation

                    return Participant activeParticipant, lines
                }

        let rec private parseParticipants participants: ParseParticipants = fun output domainTypes indentationLevel -> function
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
                        |> parseParticipants (participant :: participants) output domainTypes indentationLevel
                }

            | line :: _ ->
                Error <| WrongParticipantIndentation (line |> Line.error (indentationLevel |> IndentationLevel.indentation))

        let parse domainTypes: ParseLines<Participant list> = fun output indentationLevel -> function
            | [] -> Error MissingParticipants

            | KeyWord.Participants :: lines ->
                result {
                    let participantLines, lines =
                        lines
                        |> List.splitBy (Line.isIndentedOrMore (indentationLevel |> IndentationLevel.indentation))

                    let! participants =
                        participantLines
                        |> parseParticipants [] output domainTypes indentationLevel

                    return { Item = participants; Lines = lines }
                }

            | _ -> Error MissingParticipants

    [<RequireQualifiedAccess>]
    module private Parts =
        type private Participants = Participants of Map<string, ActiveParticipant>
        type private ParseParts = MF.ConsoleApplication.Output -> TucName -> Participants -> IndentationLevel -> Line list -> Result<TucPart list, ParseError>

        let private (|IsParticipant|_|) (Participants participants) token =
            participants |> Map.tryFind token

        let private (|IsStreamParticipant|_|) (Participants participants) = function
            | Regex @"^\[(.*Stream){1}\]$" [ stream ] -> participants |> Map.tryFind stream
            | _ -> None

        let private (|IsMethodCall|_|) = function
            | Regex @"^(\w+){1}\.(\w+){1}$" [ service; method ] -> Some (service, method)
            | _ -> None

        let private (|IsPostEvent|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ event; "->"; stream ] } when stream.StartsWith "[" && stream.EndsWith "]" ->
                Some (event, stream.Trim('[').Trim(']'))
            | _ -> None

        let private (|IsReadEvent|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ stream; "->"; event ] } when stream.StartsWith "[" && stream.EndsWith "]" ->
                Some (stream.Trim('[').Trim(']'), event)
            | _ -> None

        let private (|IsMultilineDo|_|) = function
            | "do" -> Some ()
            | _ -> None

        let private (|IsMultilineNote|_|) = function
            | @"""""""" -> Some ()
            | _ -> None

        let private (|IsMultilineLeftNote|_|) = function
            | @"""<""" -> Some ()
            | _ -> None

        let private (|IsMultilineRightNote|_|) = function
            | @""">""" -> Some ()
            | _ -> None

        let private assertIsInitiator line indentation = function
            | DomainType (SingleCaseUnion { ConstructorName = "Initiator" }) -> Ok ()
            | _ -> Error <| IsNotInitiator (line |> Line.error indentation)

        let rec private parsePart
            (output: MF.ConsoleApplication.Output)
            participants
            indentationLevel
            indentation
            caller
            lines
            line: Result<TucPart * Line list, _> =

            let parsePart =
                parsePart output participants indentationLevel

            /// Parses an execution of a caller, it goes as deep as possible, starting at 1 level deeper than current indenation.
            let parseExecution caller lines = result {
                let executionIndentation =
                    indentation
                    |> Indentation.goDeeper indentationLevel

                let executionLines, lines =
                    lines
                    |> List.splitBy (Line.isIndentedOrMore executionIndentation)

                let! execution =
                    executionLines
                    |> parseBodyParts [] (parsePart executionIndentation (Some caller))

                return execution, lines
            }

            /// Parses a body of a "block", it goes as deep as possible, starting at given depth, relative to a current indentation.
            let parseBody depth lines = result {
                let bodyIndentation =
                    indentation
                    |> Indentation.goDeeperBy depth indentationLevel

                let bodyLines, lines =
                    lines
                    |> List.splitBy (Line.isIndentedOrMore bodyIndentation)

                let! body =
                    bodyLines
                    |> parseBodyParts [] (parsePart bodyIndentation caller)

                return body, lines
            }

            match line with
            | IndentedLine indentation { Tokens = [ singleToken ] } as line ->
                match singleToken with
                | IsParticipant participants (Service { ServiceType = serviceType } as participant) ->
                    result {
                        do! serviceType |> assertIsInitiator line indentation

                        let! execution, lines = lines |> parseExecution participant

                        let part = Lifeline {
                            Initiator = participant
                            Execution = execution
                        }

                        return part, lines
                    }

                | IsStreamParticipant participants (ActiveParticipant.Stream s as stream) ->
                    result {
                        let! execution, lines = lines |> parseExecution stream

                        let! methodCall =
                            match execution with
                            | [ ServiceMethodCall methodCall ] when methodCall.Caller = stream ->
                                Ok methodCall
                            | _ ->
                                Error <| MissingEventHandlerMethodCall (line |> Line.error indentation)

                        // todo - parsovat Handlery zvlast a tady je kontrolovat
                        // todo - upravit error pro chybejici handler, aby vic definoval, co se stalo
                        //      -> chybi handler
                        //      -> je tam vic nez jen handler

                        let part = HandleEventInStream {
                            Stream = stream
                            Service = methodCall.Service
                            Method = methodCall.Method
                            Execution = methodCall.Execution
                        }

                        return part, lines
                    }

                | IsMethodCall (serviceName, methodName) ->
                    match caller, serviceName with
                    | None, _ ->
                        Error <| MethodCalledWithoutACaller (line |> Line.error indentation)

                    | Some caller, IsParticipant participants (Service { ServiceType = (DomainType (Record { Methods = methods } )) } as service) ->
                        result {
                            let definedMethodNames =
                                methods
                                |> Map.keys
                                |> List.map FieldName.value

                            let methodName = (FieldName methodName)

                            let! method =
                                methods
                                |> Map.tryFind methodName
                                |> Result.ofOption (line |> Errors.calledUndefinedMethod indentation serviceName definedMethodNames)

                            let! execution, lines = lines |> parseExecution service

                            let part = ServiceMethodCall {
                                Caller = caller
                                Service = service
                                Method = { Name = methodName; Function = method }
                                Execution = execution
                            }

                            return part, lines
                        }

                    | _ ->
                        Error <| UndefinedParticipant (line |> Line.error indentation)

                | IsMultilineNote ->
                    match caller with
                    | None ->
                        Error <| NoteWithoutACaller (line |> Line.error indentation)

                    | Some caller ->
                        result {
                            let noteLines, lines =
                                lines
                                |> List.splitBy (function
                                    | IndentedLine indentation { Content = @"""""""" } -> false
                                    | _ -> true
                                )

                            let! lines =
                                match lines with
                                | IndentedLine indentation { Content = @"""""""" } :: lines -> Ok lines
                                | _ -> Error <| InvalidMultilineNote (line |> Line.error indentation)

                            return Note { Caller = caller; Lines = noteLines |> List.map Line.content }, lines
                        }

                | IsMultilineLeftNote ->
                    result {
                        let noteLines, lines =
                            lines
                            |> List.splitBy (function
                                | IndentedLine indentation { Content = @"""<""" } -> false
                                | _ -> true
                            )

                        let! lines =
                            match lines with
                            | IndentedLine indentation { Content = @"""<""" } :: lines -> Ok lines
                            | _ -> Error <| InvalidMultilineLeftNote (line |> Line.error indentation)

                        return LeftNote { Lines = noteLines |> List.map Line.content }, lines
                    }

                | IsMultilineRightNote ->
                    result {
                        let noteLines, lines =
                            lines
                            |> List.splitBy (function
                                | IndentedLine indentation { Content = @""">""" } -> false
                                | _ -> true
                            )

                        let! lines =
                            match lines with
                            | IndentedLine indentation { Content = @""">""" } :: lines -> Ok lines
                            | _ -> Error <| InvalidMultilineRightNote (line |> Line.error indentation)

                        return RightNote { Lines = noteLines |> List.map Line.content }, lines
                    }

                | IsMultilineDo ->
                    match caller with
                    | None ->
                        Error <| DoWithoutACaller (line |> Line.error indentation)

                    | Some caller ->
                        result {
                            let actionIndentation =
                                indentation
                                |> Indentation.goDeeper indentationLevel

                            let actionLines, lines =
                                lines
                                |> List.splitBy (Line.isIndented actionIndentation)

                            if actionLines |> List.isEmpty then
                                return! Error <| DoMustHaveActions (line |> Line.error indentation)

                            return Do { Caller = caller; Actions = actionLines |> List.map Line.content }, lines
                        }

                | "if" -> Error <| IfWithoutCondition (line |> Line.error indentation)
                | "else" -> Error <| ElseOutsideOfIf (line |> Line.error indentation)
                | "group" -> Error <| GroupWithoutName (line |> Line.error indentation)
                | "loop" -> Error <| LoopWithoutCondition (line |> Line.error indentation)

                | _ ->
                    Error <| UnknownPart (line |> Line.error indentation)

            | KeyWord.SingleLineDo indentation action ->
                match caller with
                | None -> Error <| DoWithoutACaller (line |> Line.error indentation)
                | Some caller -> Ok (Do { Caller = caller; Actions = [ action ] }, lines)

            | KeyWord.SingleLineLeftNote indentation note ->
                Ok (LeftNote { Lines = [ note ] }, lines)

            | KeyWord.SingleLineRightNote indentation note ->
                Ok (RightNote { Lines = [ note ] }, lines)

            | KeyWord.SingleLineNote indentation note ->
                match caller with
                | None -> Error <| NoteWithoutACaller (line |> Line.error indentation)
                | Some caller -> Ok (Note { Caller = caller; Lines = [ note ] }, lines)

            | KeyWord.If indentation condition ->
                match condition with
                | String.IsEmpty -> Error <| IfWithoutCondition (line |> Line.error indentation)
                | condition ->
                    result {
                        let! body, lines = lines |> parseBody (Depth 1)

                        if body |> List.isEmpty then
                            return! Error <| IfMustHaveBody (line |> Line.error indentation)

                        let! elseBody, lines =
                            match lines with
                            | KeyWord.Else indentation :: lines ->
                                result {
                                    let! body, lines = lines |> parseBody (Depth 1)

                                    if body |> List.isEmpty then
                                        return! Error <| ElseMustHaveBody (line |> Line.error indentation)

                                    return Some body, lines
                                }
                            | lines -> Ok (None, lines)

                        let part = If {
                            Condition = condition
                            Body = body
                            Else = elseBody
                        }

                        return part, lines
                    }

            | KeyWord.Group indentation groupName ->
                match groupName with
                | String.IsEmpty -> Error <| GroupWithoutName (line |> Line.error indentation)
                | groupName ->
                    result {
                        let! body, lines = lines |> parseBody (Depth 1)

                        if body |> List.isEmpty then
                            return! Error <| GroupMustHaveBody (line |> Line.error indentation)

                        let part = Group {
                            Name = groupName
                            Body = body
                        }

                        return part, lines
                    }

            | KeyWord.Loop indentation condition ->
                match condition with
                | String.IsEmpty -> Error <| LoopWithoutCondition (line |> Line.error indentation)
                | condition ->
                    result {
                        let! body, lines = lines |> parseBody (Depth 1)

                        if body |> List.isEmpty then
                            return! Error <| LoopMustHaveBody (line |> Line.error indentation)

                        let part = Loop {
                            Condition = condition
                            Body = body
                        }

                        return part, lines
                    }

            | IsPostEvent indentation (eventName, streamName) ->
                match caller, streamName with
                | None, _ ->
                    Error <| EventPostedWithoutACaller (line |> Line.error indentation)

                | Some caller, IsParticipant participants (ActiveParticipant.Stream { StreamType = DomainType.Stream eventType } as stream) ->
                    result {
                        if eventName <> eventType then
                            return! line |> Errors.wrongEventPostedToStream indentation streamName eventType |> Error

                        let part = PostEvent {
                            Caller = caller
                            Stream = stream
                        }

                        return part, lines
                    }
                | _ ->
                    let participantIndentation =
                        Indentation ((indentation |> Indentation.size) + " -> ".Length + eventName.Length)

                    Error <| UndefinedParticipant (line |> Line.error participantIndentation)

            | IsReadEvent indentation (streamName, eventName) ->
                match caller, streamName with
                | None, _ ->
                    Error <| EventReadWithoutACaller (line |> Line.error indentation)

                | Some caller, IsParticipant participants (ActiveParticipant.Stream { StreamType = DomainType.Stream eventType } as stream) ->
                    result {
                        if eventName <> eventType then
                            return! line |> Errors.wrongEventReadFromStream indentation streamName eventType |> Error

                        let part = ReadEvent {
                            Caller = caller
                            Stream = stream
                        }

                        return part, lines
                    }
                | _ ->
                    let participantIndentation =
                        Indentation ((indentation |> Indentation.size) + " -> ".Length + eventName.Length)

                    Error <| UndefinedParticipant (line |> Line.error participantIndentation)

            | lineWithUnknownPart ->
                Error <| UnknownPart (lineWithUnknownPart |> Line.error indentation)

        and private parseBodyParts body parsePart = function
            | [] -> body |> List.rev |> Ok
            | line :: lines ->
                result {
                    let! part, lines =
                        line
                        |> parsePart lines

                    return!
                        lines
                        |> parseBodyParts (part :: body) parsePart
                }

        let rec private parseParts parts depth: ParseParts = fun output tucName participants indentationLevel lines ->
            let currentIndentation =
                Indentation ((depth |> Depth.value) * (indentationLevel |> IndentationLevel.size))

            let parseParts parts depth lines =
                parseParts parts depth output tucName participants indentationLevel lines

            match lines with
            | [] ->
                match parts with
                | [] -> Error <| MissingUseCase tucName
                | parts -> Ok (parts |> List.rev)

            | KeyWord.Section section as line :: lines ->
                match section with
                | String.IsEmpty -> Error <| SectionWithoutName (line |> Line.error (Indentation 0))
                | section ->
                    lines
                    |> parseParts (Section { Value = section } :: parts) depth

            | LineDepth depth line :: lines ->
                result {
                    let! part, lines =
                        line
                        |> parsePart output participants indentationLevel currentIndentation None lines

                    return! lines |> parseParts (part :: parts) depth
                }

            | line :: _ ->
                Error <| TooMuchIndented (line |> Line.error (indentationLevel |> IndentationLevel.indentation))

        let parse tucName participants: ParseLines<TucPart list> = fun output indentationLevel lines ->
            match lines with
            | [] -> Error <| MissingUseCase tucName

            | lines ->
                result {
                    let participants =
                        participants
                        |> List.collect Participant.active
                        |> List.map (fun participant -> participant |> ActiveParticipant.name, participant)
                        |> Map.ofList
                        |> Participants

                    let! parts =
                        lines
                        |> parseParts [] (Depth 0) output tucName participants indentationLevel

                    return { Item = parts; Lines = [] }
                }

    let private parseTuc (output: MF.ConsoleApplication.Output) domainTypes indentationLevel lines =
        result {
            let! name, lines =
                match lines with
                | (KeyWord.Tuc name as line) :: lines ->
                    match name with
                    | String.IsEmpty -> Error <| TucMustHaveName (line |> Line.error (Indentation 0))
                    | name -> Ok (TucName name, lines)

                | _ ->
                    Error <| MissingTucName

            let! { Item = participants; Lines = lines } =
                lines
                |> Participants.parse domainTypes output indentationLevel

            let! { Item = parts } =
                lines
                |> Parts.parse name participants output indentationLevel

            return {
                Name = name
                Participants = participants
                Parts = parts
            }
        }

    let private assertLinesIndentation indentationLevel (lines: RawLine list) =
            let indentationLevel = indentationLevel |> IndentationLevel.size

            lines
            |> List.filter (fun { Indentation = (Indentation indentation) } -> indentation % indentationLevel <> 0)
            |> function
                | [] -> Ok ()
                | wrongLines -> Error <| WrongIndentationLevel (indentationLevel, wrongLines |> List.map RawLine.valuei)

    let rec private parseLines (output: MF.ConsoleApplication.Output) domainTypes indentationLevel tucAcc = function
        | [] ->
            match tucAcc with
            | [] -> Error MissingTucName
            | tuc -> Ok (tuc |> List.rev)

        | lines ->
            result {
                let mutable isCurrentTuc = true
                let currentTucLines, lines =
                    lines
                    |> List.splitBy (function
                        | KeyWord.Tuc _ ->
                            if isCurrentTuc
                            then
                                isCurrentTuc <- false
                                true
                            else
                                false

                        | _ -> true
                    )

                if output.IsVeryVerbose() then
                    output.Section "Current tuc lines"
                    currentTucLines
                    |> List.map Line.format
                    |> output.Messages ""
                    |> output.NewLine

                let! tuc =
                    currentTucLines
                    |> parseTuc output domainTypes indentationLevel

                return! lines |> parseLines output domainTypes indentationLevel (tuc :: tucAcc)
            }

    let parse (output: MF.ConsoleApplication.Output) domainTypes file = result {
        if output.IsVerbose() then output.Title <| sprintf "Parse %A" file

        let domainTypes =
            domainTypes
            |> List.map (fun (DomainType t) -> t |> ResolvedType.name, t)
            |> Map.ofList
            |> DomainTypes

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

        return!
            rawLines
            |> List.map (Line.ofRawLine indentationLevel)
            |> parseLines output domainTypes indentationLevel []
    }
