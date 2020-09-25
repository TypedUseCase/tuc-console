namespace MF.Tuc.Parser

open MF.TucConsole
open MF.Tuc
open MF.Domain

[<RequireQualifiedAccess>]
module Parser =
    open System
    open System.IO
    open ErrorHandling
    open ParserPatterns

    type private ParseResult<'TucItem> = {
        Item: 'TucItem
        Lines: Line list
    }

    type private ParseLines<'TucItem> = MF.ConsoleApplication.Output -> IndentationLevel -> Line list -> Result<ParseResult<'TucItem>, ParseError list>

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
        type private ParseParticipants = MF.ConsoleApplication.Output -> DomainTypes -> IndentationLevel -> Line list -> Result<Participant list, ParseError list>

        let private parseService serviceName serviceDomain alias indentation line domainTypes =
            let service = Service >> Ok

            match domainTypes with
            | HasDomainType (Some serviceDomain) serviceName (DomainType.ServiceInDomain serviceDomain componentType) ->
                service {
                    Domain = serviceDomain
                    Context = serviceName
                    Alias = alias
                    ServiceType = DomainType componentType
                }
            | HasDomainType (Some serviceDomain) serviceName (DomainType.InitiatorInDomain serviceDomain as serviceType) ->
                service {
                    Domain = serviceDomain
                    Context = serviceName
                    Alias = alias
                    ServiceType = serviceType
                }
            | _ -> Error <| Errors.undefinedParticipantInDomain indentation line serviceDomain

        let private parseDataObject streamName dataObjectDomain alias indentation line domainTypes =
            let dataObject = ActiveParticipant.DataObject >> Ok

            match domainTypes with
            | HasDomainType (Some dataObjectDomain) streamName (DomainType.DataObject dataObjectDomain componentType) ->
                dataObject {
                    Domain = dataObjectDomain
                    Context = streamName
                    Alias = alias
                    DataObjectType = DomainType componentType
                }
            | _ -> Error <| Errors.undefinedParticipantInDomain indentation line dataObjectDomain

        let private parseStream streamName streamDomain alias indentation line domainTypes =
            let stream = ActiveParticipant.Stream >> Ok

            match domainTypes with
            | HasDomainType (Some streamDomain) streamName (DomainType.Stream streamDomain componentType) ->
                stream {
                    Domain = streamDomain
                    Context = streamName
                    Alias = alias
                    StreamType = DomainType componentType
                }
            | _ -> Error <| Errors.undefinedParticipantInDomain indentation line streamDomain

        let private parseActiveParticipant (domainTypes: DomainTypes) indentation line =
            match line with
            | IndentedLine indentation line ->
                match line |> Line.content with
                // Service
                | Regex @"^(\w+){1} (\w+){1}$" [ serviceName; serviceDomain ] ->
                    domainTypes |> parseService serviceName (DomainName.create serviceDomain) serviceName indentation line

                | Regex @"^(\w+){1} (\w+){1} as ""(.+){1}""$" [ serviceName; serviceDomain; alias ] ->
                    domainTypes |> parseService serviceName (DomainName.create serviceDomain) alias indentation line

                // Stream
                | Regex @"^\[(\w+Stream){1}\] (\w+){1}$" [ streamName; streamDomain ] ->
                    domainTypes |> parseStream streamName (DomainName.create streamDomain) streamName indentation line

                | Regex @"^\[(\w+Stream){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    domainTypes |> parseStream streamName (DomainName.create streamDomain) alias indentation line

                // Data Object
                | Regex @"^\[(\w+){1}\] (\w+){1}$" [ dataObjectName; dataObjectDomain ] ->
                    domainTypes |> parseDataObject dataObjectName (DomainName.create dataObjectDomain) dataObjectName indentation line

                | Regex @"^\[(\w+){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    domainTypes |> parseDataObject streamName (DomainName.create streamDomain) alias indentation line

                | _ -> Error <| InvalidParticipant (line |> Line.error indentation)

            | _ -> Error <| WrongParticipantIndentation (line |> Line.error indentation)

        let private parseComponentActiveParticipant (domainTypes: DomainTypes) indentation componentDomain line =
            match line with
            | IndentedLine indentation line ->
                match line |> Line.content with
                // Service
                | Regex @"^(\w+){1}$" [ serviceName ] ->
                    domainTypes |> parseService serviceName componentDomain serviceName indentation line

                | Regex @"^(\w+){1} (\w+){1}$" [ serviceName; serviceDomain ] ->
                    if componentDomain |> DomainName.eq serviceDomain
                    then domainTypes |> parseService serviceName componentDomain serviceName indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^(\w+){1} (\w+){1} as ""(.+){1}""$" [ serviceName; serviceDomain; alias ] ->
                    if componentDomain |> DomainName.eq serviceDomain
                    then domainTypes |> parseService serviceName componentDomain alias indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^(\w+){1} as ""(.+){1}""$" [ serviceName; alias ] ->
                    domainTypes |> parseService serviceName componentDomain alias indentation line

                // Stream
                | Regex @"^\[(\w+Stream){1}\]$" [ streamName ] ->
                    domainTypes |> parseStream streamName componentDomain streamName indentation line

                | Regex @"^\[(\w+Stream){1}\] (\w+){1}$" [ streamName; streamDomain ] ->
                    if componentDomain |> DomainName.eq streamDomain
                    then domainTypes |> parseStream streamName componentDomain streamName indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+Stream){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    if componentDomain |> DomainName.eq streamDomain
                    then domainTypes |> parseStream streamName componentDomain alias indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+Stream){1}\] as ""(.+){1}""$" [ streamName; alias ] ->
                    domainTypes |> parseStream streamName componentDomain alias indentation line

                // Data Object
                | Regex @"^\[(\w+){1}\]$" [ dataObjectName ] ->
                    domainTypes |> parseDataObject dataObjectName componentDomain dataObjectName indentation line

                | Regex @"^\[(\w+){1}\] (\w+){1}$" [ dataObjectName; dataObjectDomain ] ->
                    if componentDomain |> DomainName.eq dataObjectDomain
                    then domainTypes |> parseDataObject dataObjectName componentDomain dataObjectName indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+){1}\] (\w+){1} as ""(.+){1}""$" [ dataObjectName; dataObjectDomain; alias ] ->
                    if componentDomain |> DomainName.eq dataObjectDomain
                    then domainTypes |> parseDataObject dataObjectName componentDomain alias indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+){1}\] as ""(.+){1}""$" [ dataObjectName; alias ] ->
                    domainTypes |> parseDataObject dataObjectName componentDomain alias indentation line

                | _ -> Error <| InvalidParticipant (line |> Line.error indentation)

            | _ -> Error <| WrongParticipantIndentation (line |> Line.error indentation)
            |> Result.mapError (function
                | UndefinedParticipantInDomain (lineNumber, position, line, domain) -> UndefinedComponentParticipantInDomain (lineNumber, position, line, domain)
                | error -> error
            )

        let private parseParticipant (domainTypes: DomainTypes) indentationLevel lines line: Result<Participant * Line list, ParseError list> =
            let participantIndentation = indentationLevel |> IndentationLevel.indentation

            match line |> Line.content with
            | Regex @"^(\w+){1} (\w+){1}$" [ componentName; domainName ] ->
                let domainName = DomainName.create domainName

                let parsedComponent =
                    match domainTypes with
                    | HasDomainType (Some domainName) componentName (DomainType.Component domainName componentFields) ->
                        result {
                            let componentParticipantIndentation = participantIndentation |> Indentation.goDeeper indentationLevel

                            let componentParticipantLines, lines =
                                lines
                                |> List.splitBy (Line.isIndentedOrMore componentParticipantIndentation)

                            let! componentParticipants =
                                componentParticipantLines
                                |> List.map (parseComponentActiveParticipant domainTypes componentParticipantIndentation domainName)
                                |> Validation.ofResults
                                |> Validation.toResult

                            do!
                                componentParticipants
                                |> Assert.definedComponentParticipants indentationLevel line
                                    componentName
                                    componentParticipantIndentation
                                    componentParticipantLines
                                    componentFields

                            return Component { Name = componentName; Participants = componentParticipants }, lines
                        }

                    | _ ->
                        Error [
                            Errors.undefinedParticipantInDomain (indentationLevel |> IndentationLevel.indentation) line domainName
                        ]

                match parsedComponent with
                | Ok parsedComponent -> Ok parsedComponent

                | Error ([ UndefinedParticipantInDomain _ ]) ->
                    // since component syntax is the same as active participant without an alias, we must also try to parse active participant
                    // but it is only possible, when it is an Undefind participant in domain, otherwise it is just a component error
                    result {
                        let! activeParticipant =
                            line
                            |> parseActiveParticipant domainTypes participantIndentation
                            |> Validation.ofResult

                        return Participant activeParticipant, lines
                    }

                | Error componentError -> Error componentError

            | _ ->
                result {
                    let! activeParticipant =
                        line
                        |> parseActiveParticipant domainTypes participantIndentation
                        |> Validation.ofResult

                    return Participant activeParticipant, lines
                }

        let rec private parseParticipants participants: ParseParticipants = fun output domainTypes indentationLevel -> function
            | [] ->
                match participants with
                | [] -> Error [ MissingParticipants ]
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
                Error [
                    WrongParticipantIndentation (line |> Line.error (indentationLevel |> IndentationLevel.indentation))
                ]

        let parse domainTypes: ParseLines<Participant list> = fun output indentationLevel -> function
            | [] -> Error [ MissingParticipants ]

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

            | _ -> Error [ MissingParticipants ]

    [<RequireQualifiedAccess>]
    module private Parts =
        type private Participants = Participants of Map<string, ActiveParticipant>
        type private ParseParts = MF.ConsoleApplication.Output -> TucName -> Participants -> DomainTypes -> IndentationLevel -> Line list -> Result<TucPart list, ParseError>

        let private (|IsParticipant|_|) (Participants participants) token =
            participants |> Map.tryFind token

        let private (|IsStreamParticipant|_|) (Participants participants) = function
            | Regex @"^\[(.*Stream){1}\]$" [ stream ] -> participants |> Map.tryFind stream
            | _ -> None

        let private (|IsMethodCall|_|) = function
            | Regex @"^(\w+){1}\.(\w+){1}$" [ service; method ] -> Some (service, method)
            | _ -> None

        let private (|IsPostData|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ data; "->"; dataObject ] } when dataObject.StartsWith "[" && dataObject.EndsWith "]" && not (dataObject.EndsWith "Stream]") ->
                Some (data, dataObject.Trim('[').Trim(']'))
            | _ -> None

        let private (|IsReadData|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ dataObject; "->"; data ] } when dataObject.StartsWith "[" && dataObject.EndsWith "]" && not (dataObject.EndsWith "Stream]") ->
                Some (dataObject.Trim('[').Trim(']'), data)
            | _ -> None

        let private (|IsPostEvent|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ event; "->"; stream ] } when stream.StartsWith "[" && stream.EndsWith "Stream]" ->
                Some (event, stream.Trim('[').Trim(']'))
            | _ -> None

        let private (|IsReadEvent|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ stream; "->"; event ] } when stream.StartsWith "[" && stream.EndsWith "Stream]" ->
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

        let rec private parsePart
            (output: MF.ConsoleApplication.Output)
            participants
            domainTypes
            indentationLevel
            indentation
            caller
            lines
            line: Result<TucPart * Line list, _> =

            let currentDepth = indentation |> Depth.ofIndentation indentationLevel

            let parsePart =
                parsePart output participants domainTypes indentationLevel

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
            | DeeperLine currentDepth line ->
                Error <| TooMuchIndented (line |> Line.error indentation)

            | IndentedLine indentation { Tokens = [ singleToken ] } as line ->
                match singleToken with
                | IsParticipant participants (Service { ServiceType = serviceType } as participant) ->
                    result {
                        do! serviceType |> Assert.isInitiator line indentation

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

                        let! handlerCall =
                            match execution with
                            | [ HandleEventInStream handlerCall ] when handlerCall.Stream = stream ->
                                Ok handlerCall
                            | _ ->
                                Error <| MissingEventHandlerMethodCall (line |> Line.error indentation)

                        return HandleEventInStream handlerCall, lines
                    }

                | IsMethodCall (serviceName, methodName) ->
                    match caller, serviceName with
                    | None, _ ->
                        Error <| MethodCalledWithoutACaller (line |> Line.error indentation)

                    | Some (Service _ as caller), IsParticipant participants (Service { ServiceType = (DomainType (Record { Methods = methods } )) } as service) ->
                        result {
                            let methodName = (FieldName methodName)

                            let definedMethodNames =
                                methods
                                |> Map.keys
                                |> List.map FieldName.value

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

                    | Some (ActiveParticipant.Stream _ as caller), IsParticipant participants (Service { ServiceType = (DomainType (Record { Handlers = handlers } )) } as service) ->
                        result {
                            let handlerName = (FieldName methodName)

                            let definedMethodNames =
                                handlers
                                |> Map.keys
                                |> List.map FieldName.value

                            let! handler =
                                handlers
                                |> Map.tryFind handlerName
                                |> Result.ofOption (line |> Errors.calledUndefinedHandler indentation serviceName definedMethodNames)

                            let! execution, lines = lines |> parseExecution service

                            let part = HandleEventInStream {
                                Stream = caller
                                Service = service
                                Handler = { Name = handlerName; Handler = handler }
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
                            | KeyWord.Else indentation as elseLine :: lines ->
                                result {
                                    let! body, lines = lines |> parseBody (Depth 1)

                                    if body |> List.isEmpty then
                                        return! Error <| ElseMustHaveBody (elseLine |> Line.error indentation)

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

            | IsPostData indentation (dataName, dataObjectName) ->
                match caller, dataObjectName with
                | None, _ ->
                    Error <| DataPostedWithoutACaller (line |> Line.error indentation)

                | Some caller, IsParticipant participants (DataObject { DataObjectType = DomainType.DataObjectData (domain, expectedDataType) } as dataObject) ->
                    result {
                        let! data =
                            dataName
                            |> Assert.data output indentation line domainTypes expectedDataType domain

                        let part = PostData {
                            Caller = caller
                            DataObject = dataObject
                            Data = data
                        }

                        return part, lines
                    }
                | _ ->
                    let participantIndentation =
                        Indentation ((indentation |> Indentation.size) + " -> ".Length + dataName.Length)

                    Error <| UndefinedParticipant (line |> Line.error participantIndentation)

            | IsReadData indentation (dataObjectName, dataName) ->
                match caller, dataObjectName with
                | None, _ ->
                    Error <| DataReadWithoutACaller (line |> Line.error indentation)

                | Some caller, IsParticipant participants (DataObject { DataObjectType = DomainType.DataObjectData (domain, expectedDataType) } as dataObject) ->
                    result {
                        let! data =
                            dataName
                            |> Assert.data output indentation line domainTypes expectedDataType domain

                        let part = ReadData {
                            Caller = caller
                            DataObject = dataObject
                            Data = data
                        }

                        return part, lines
                    }
                | _ ->
                    let participantIndentation =
                        Indentation ((indentation |> Indentation.size) + " -> ".Length + dataName.Length)

                    Error <| UndefinedParticipant (line |> Line.error participantIndentation)

            | IsPostEvent indentation (eventName, streamName) ->
                match caller, streamName with
                | None, _ ->
                    Error <| EventPostedWithoutACaller (line |> Line.error indentation)

                | Some caller, IsParticipant participants (ActiveParticipant.Stream { StreamType = DomainType.StreamEvent (domain, expectedEventTypeName) } as stream) ->
                    result {
                        let! event =
                            eventName
                            |> Assert.event output indentation line domainTypes expectedEventTypeName domain

                        let part = PostEvent {
                            Caller = caller
                            Stream = stream
                            Event = event
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

                | Some caller, IsParticipant participants (ActiveParticipant.Stream { StreamType = DomainType.StreamEvent (domain, eventTypeName) } as stream) ->
                    result {
                        let! event =
                            eventName
                            |> Assert.event output indentation line domainTypes eventTypeName domain

                        let part = ReadEvent {
                            Caller = caller
                            Stream = stream
                            Event = event
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

        let rec private parseParts parts depth: ParseParts = fun output tucName participants domainTypes indentationLevel lines ->
            let currentIndentation =
                Indentation ((depth |> Depth.value) * (indentationLevel |> IndentationLevel.size))

            let parseParts parts depth lines =
                parseParts parts depth output tucName participants domainTypes indentationLevel lines

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

            | line :: lines ->
                result {
                    let! part, lines =
                        line
                        |> parsePart output participants domainTypes indentationLevel currentIndentation None lines

                    return! lines |> parseParts (part :: parts) depth
                }

        let parse tucName participants domainTypes: ParseLines<TucPart list> = fun output indentationLevel lines ->
            match lines with
            | [] -> Error [ MissingUseCase tucName ]

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
                        |> parseParts [] (Depth 0) output tucName participants domainTypes indentationLevel
                        |> Validation.ofResult

                    return { Item = parts; Lines = [] }
                }

    let private parseTuc (output: MF.ConsoleApplication.Output) domainTypes indentationLevel lines =
        result {
            let! name, lines =
                match lines with
                | (KeyWord.Tuc name as line) :: lines ->
                    match name with
                    | String.IsEmpty -> Error [ TucMustHaveName (line |> Line.error (Indentation 0)) ]
                    | name -> Ok (TucName name, lines)

                | _ ->
                    Error [ MissingTucName ]

            let! { Item = participants; Lines = lines } =
                lines
                |> Participants.parse domainTypes output indentationLevel

            let! { Item = parts } =
                lines
                |> Parts.parse name participants domainTypes output indentationLevel

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

    let rec private parseLines (output: MF.ConsoleApplication.Output) domainTypes indentationLevel (tucAcc: Result<Tuc, ParseError list> list) = function
        | [] ->
            match tucAcc with
            | [] -> Error [ MissingTucName ]
            | tuc ->
                tuc
                |> List.rev
                |> Validation.ofResults
                |> Result.mapError List.concat

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
                    |> List.map Line.debug
                    |> output.Messages ""
                    |> output.NewLine

                let tuc =
                    currentTucLines
                    |> parseTuc output domainTypes indentationLevel

                return! lines |> parseLines output domainTypes indentationLevel (tuc :: tucAcc)
            }

    let parse (output: MF.ConsoleApplication.Output) domainTypes file = result {
        if output.IsVerbose() then output.Title <| sprintf "Parse %A" file

        let domainTypes =
            domainTypes
            |> List.map (fun (DomainType t) ->
                (t |> ResolvedType.domain, t |> ResolvedType.name), t
            )
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
            |> Result.ofOption [ MissingIndentation ]

        if output.IsVerbose() then
            output.Message <| sprintf "[Tuc] Current indentation level is <c:magenta>%d</c>" (indentationLevel |> IndentationLevel.size)

        do!
            rawLines
            |> assertLinesIndentation indentationLevel
            |> Validation.ofResult

        return!
            rawLines
            |> List.map (Line.ofRawLine indentationLevel)
            |> parseLines output domainTypes indentationLevel []
    }
