namespace Tuc.Parser

open Tuc
open Tuc.Console
open Tuc.Domain

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
    module private KeyWords =
        let (|Tuc|_|): Line -> _ = function
            | { Tokens = "tuc" :: name; Depth = Depth 0 } as line ->
                Some (
                    name |> String.concat " ",
                    Range.singleLine (line.Number - 1) (0, "tuc".Length)
                )
            | _ -> None

        let (|Participants|_|): Line -> _ = function
            | { Content = "participants"; Depth = Depth 0 } as line ->
                Some (
                    Range.singleLine (line.Number - 1) (0, "participants".Length)
                )
            | _ -> None

        let (|Section|_|): Line -> _ = function
            | { Tokens = "section" :: section; Depth = Depth 0 } as line ->
                Some (
                    section |> String.concat " ",
                    Range.singleLine (line.Number - 1) (0, "section".Length)
                )
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
            | IndentedLine indentation { Tokens = "if" :: condition } as line ->
                Some (
                    condition |> String.concat " ",
                    Range.singleLine (line.Number - 1) (0, "if".Length) |> Range.indent (line.Indentation |> Indentation.size)
                )
            | _ -> None

        let (|Else|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = [ "else" ] } as line ->
                Some (
                    Range.singleLine (line.Number - 1) (0, "else".Length) |> Range.indent (line.Indentation |> Indentation.size)
                )
            | _ -> None

        let (|Group|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = "group" :: group } as line ->
                Some (
                    group |> String.concat " ",
                    Range.singleLine (line.Number - 1) (0, "group".Length) |> Range.indent (line.Indentation |> Indentation.size)
                )
            | _ -> None

        let (|Loop|_|) indentation: Line -> _ = function
            | IndentedLine indentation { Tokens = "loop" :: condition } as line ->
                Some (
                    condition |> String.concat " ",
                    Range.singleLine (line.Number - 1) (0, "loop".Length) |> Range.indent (line.Indentation |> Indentation.size)
                )
            | _ -> None

    [<RequireQualifiedAccess>]
    module private Participants =
        type private ParseParticipants = MF.ConsoleApplication.Output -> DomainTypes -> IndentationLevel -> Line list -> Result<ParsedParticipant list, ParseError list>

        [<RequireQualifiedAccess>]
        module private Participants =
            let (|Component|_|): Line -> _ = function
                | { Content = Regex @"^(\w+){1} (\w+){1}$" [ componentName; domainName ] } as line ->
                    let componentNameRange = line |> Line.findRangeForWord componentName
                    let domainNameRange = componentNameRange |> Range.fromEnd 1 (domainName.Length)

                    Some ((componentName, componentNameRange), (domainName, domainNameRange))
                | _ -> None

        let private findParticipantLocations location participant domainName alias line =
            let contextValue = participant |> ActiveParticipant.value

            let context = {
                Value = contextValue
                Location = line |> Line.findRangeForWord contextValue |> location
            }

            let domain = maybe {
                let domain = domainName |> DomainName.value
                let offset = context |> ParsedLocation.endChar

                let! range =
                    match line |> Line.tryFindRangeForWordAfter offset domain with
                    | Some range -> Some range
                    | _ -> line |> Line.tryFindRangeForWordAfter offset (domain |> String.lcFirst)

                return {
                    Value = domain
                    Location = range |> location
                }
            }

            let alias = maybe {
                let! range =
                    match domain with
                    | Some domain -> line |> Line.tryFindRangeForWordAfter (domain |> ParsedLocation.endChar) alias
                    | _ -> line |> Line.tryFindRangeForWordAfter (context |> ParsedLocation.endChar) alias

                return {
                    Value = alias
                    Location = range |> location
                }
            }

            context, domain, alias

        let private parseService location serviceName serviceDomain alias indentation line domainTypes =
            let service (service: ServiceParticipant) =
                let context, domain, alias =
                    line |> findParticipantLocations location (Service service) service.Domain service.Alias

                Ok (Parsed.ParticipantDefinition {
                    Value = Service service
                    Context = context
                    Domain = domain
                    Alias = alias
                })

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

        let private parseDataObject location streamName dataObjectDomain alias indentation line domainTypes =
            let dataObject (dataObject: DataObjectParticipant) =
                let context, domain, alias =
                    line |> findParticipantLocations location (DataObject dataObject) dataObject.Domain dataObject.Alias

                Ok (Parsed.ParticipantDefinition {
                    Value = DataObject dataObject
                    Context = context
                    Domain = domain
                    Alias = alias
                })

            match domainTypes with
            | HasDomainType (Some dataObjectDomain) streamName (DomainType.DataObject dataObjectDomain componentType) ->
                dataObject {
                    Domain = dataObjectDomain
                    Context = streamName
                    Alias = alias
                    DataObjectType = DomainType componentType
                }
            | _ -> Error <| Errors.undefinedParticipantInDomain indentation line dataObjectDomain

        let private parseStream location streamName streamDomain alias indentation line domainTypes =
            let stream (stream: StreamParticipant) =
                let context, domain, alias =
                    line |> findParticipantLocations location (ActiveParticipant.Stream stream) stream.Domain stream.Alias

                Ok (Parsed.ParticipantDefinition {
                    Value = ActiveParticipant.Stream stream
                    Context = context
                    Domain = domain
                    Alias = alias
                })

            match domainTypes with
            | HasDomainType (Some streamDomain) streamName (DomainType.Stream streamDomain componentType) ->
                stream {
                    Domain = streamDomain
                    Context = streamName
                    Alias = alias
                    StreamType = DomainType componentType
                }
            | _ -> Error <| Errors.undefinedParticipantInDomain indentation line streamDomain

        let private parseActiveParticipant location (domainTypes: DomainTypes) indentation line =
            match line with
            | IndentedLine indentation line ->
                match line |> Line.content with
                // Service
                | Regex @"^(\w+){1} (\w+){1}$" [ serviceName; serviceDomain ] ->
                    domainTypes |> parseService location serviceName (DomainName.create serviceDomain) serviceName indentation line

                | Regex @"^(\w+){1} (\w+){1} as ""(.+){1}""$" [ serviceName; serviceDomain; alias ] ->
                    domainTypes |> parseService location serviceName (DomainName.create serviceDomain) alias indentation line

                // Stream
                | Regex @"^\[(\w+Stream){1}\] (\w+){1}$" [ streamName; streamDomain ] ->
                    domainTypes |> parseStream location streamName (DomainName.create streamDomain) streamName indentation line

                | Regex @"^\[(\w+Stream){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    domainTypes |> parseStream location streamName (DomainName.create streamDomain) alias indentation line

                // Data Object
                | Regex @"^\[(\w+){1}\] (\w+){1}$" [ dataObjectName; dataObjectDomain ] ->
                    domainTypes |> parseDataObject location dataObjectName (DomainName.create dataObjectDomain) dataObjectName indentation line

                | Regex @"^\[(\w+){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    domainTypes |> parseDataObject location streamName (DomainName.create streamDomain) alias indentation line

                | _ -> Error <| InvalidParticipant (line |> Line.error indentation)

            | _ -> Error <| WrongParticipantIndentation (line |> Line.error indentation)

        let private parseComponentActiveParticipant location (domainTypes: DomainTypes) indentation componentDomain line =
            match line with
            | IndentedLine indentation line ->
                match line |> Line.content with
                // Service
                | Regex @"^(\w+){1}$" [ serviceName ] ->
                    domainTypes |> parseService location serviceName componentDomain serviceName indentation line

                | Regex @"^(\w+){1} (\w+){1}$" [ serviceName; serviceDomain ] ->
                    if componentDomain |> DomainName.eq serviceDomain
                    then domainTypes |> parseService location serviceName componentDomain serviceName indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^(\w+){1} (\w+){1} as ""(.+){1}""$" [ serviceName; serviceDomain; alias ] ->
                    if componentDomain |> DomainName.eq serviceDomain
                    then domainTypes |> parseService location serviceName componentDomain alias indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^(\w+){1} as ""(.+){1}""$" [ serviceName; alias ] ->
                    domainTypes |> parseService location serviceName componentDomain alias indentation line

                // Stream
                | Regex @"^\[(\w+Stream){1}\]$" [ streamName ] ->
                    domainTypes |> parseStream location streamName componentDomain streamName indentation line

                | Regex @"^\[(\w+Stream){1}\] (\w+){1}$" [ streamName; streamDomain ] ->
                    if componentDomain |> DomainName.eq streamDomain
                    then domainTypes |> parseStream location streamName componentDomain streamName indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+Stream){1}\] (\w+){1} as ""(.+){1}""$" [ streamName; streamDomain; alias ] ->
                    if componentDomain |> DomainName.eq streamDomain
                    then domainTypes |> parseStream location streamName componentDomain alias indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+Stream){1}\] as ""(.+){1}""$" [ streamName; alias ] ->
                    domainTypes |> parseStream location streamName componentDomain alias indentation line

                // Data Object
                | Regex @"^\[(\w+){1}\]$" [ dataObjectName ] ->
                    domainTypes |> parseDataObject location dataObjectName componentDomain dataObjectName indentation line

                | Regex @"^\[(\w+){1}\] (\w+){1}$" [ dataObjectName; dataObjectDomain ] ->
                    if componentDomain |> DomainName.eq dataObjectDomain
                    then domainTypes |> parseDataObject location dataObjectName componentDomain dataObjectName indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+){1}\] (\w+){1} as ""(.+){1}""$" [ dataObjectName; dataObjectDomain; alias ] ->
                    if componentDomain |> DomainName.eq dataObjectDomain
                    then domainTypes |> parseDataObject location dataObjectName componentDomain alias indentation line
                    else Error <| Errors.wrongComponentParticipantDomain indentation line componentDomain

                | Regex @"^\[(\w+){1}\] as ""(.+){1}""$" [ dataObjectName; alias ] ->
                    domainTypes |> parseDataObject location dataObjectName componentDomain alias indentation line

                | _ -> Error <| InvalidParticipant (line |> Line.error indentation)

            | _ -> Error <| WrongParticipantIndentation (line |> Line.error indentation)
            |> Result.mapError (function
                | UndefinedParticipantInDomain (lineNumber, position, line, domain) -> UndefinedComponentParticipantInDomain (lineNumber, position, line, domain)
                | error -> error
            )

        let private parseParticipant location (domainTypes: DomainTypes) indentationLevel lines line: Result<ParsedParticipant * Line list, ParseError list> =
            let participantIndentation = indentationLevel |> IndentationLevel.indentation

            match line with
            | Participants.Component ((componentName, componentRange), (domainName, domainRange)) ->
                let domainName = DomainName.create domainName

                let parsedComponentResult =
                    match domainTypes with
                    | HasDomainType (Some domainName) componentName (DomainType.Component domainName componentFields) ->
                        result {
                            let componentParticipantIndentation = participantIndentation |> Indentation.goDeeper indentationLevel

                            let componentParticipantLines, lines =
                                lines
                                |> List.splitBy (Line.isIndentedOrMore componentParticipantIndentation)

                            let! componentParticipants =
                                componentParticipantLines
                                |> List.map (parseComponentActiveParticipant location domainTypes componentParticipantIndentation domainName)
                                |> Validation.ofResults
                                |> Validation.toResult

                            do!
                                componentParticipants
                                |> List.map Parsed.value
                                |> Assert.definedComponentParticipants indentationLevel line
                                    componentName
                                    componentParticipantIndentation
                                    componentParticipantLines
                                    componentFields

                            return
                                Parsed.ComponentDefinition {
                                    Value = Component {
                                        Name = componentName
                                        Participants = componentParticipants |> List.map Parsed.value
                                    }
                                    Context = {
                                        Value = componentName
                                        Location = componentRange |> location
                                    }
                                    Domain = {
                                        Value = domainName |> DomainName.value
                                        Location = domainRange |> location
                                    }
                                    Participants = componentParticipants
                                },
                                lines
                        }

                    | _ ->
                        Error [
                            Errors.undefinedParticipantInDomain (indentationLevel |> IndentationLevel.indentation) line domainName
                        ]

                match parsedComponentResult with
                | Ok parsedComponent -> Ok parsedComponent

                | Error ([ UndefinedParticipantInDomain _ ]) ->
                    // since component syntax is the same as active participant without an alias, we must also try to parse active participant
                    // but it is only possible, when it is an Undefind participant in domain, otherwise it is just a component error
                    result {
                        let! activeParticipant =
                            line
                            |> parseActiveParticipant location domainTypes participantIndentation
                            |> Validation.ofResult

                        return activeParticipant |> Parsed.map Participant, lines
                    }

                | Error componentError -> Error componentError

            | _ ->
                result {
                    let! activeParticipant =
                        line
                        |> parseActiveParticipant location domainTypes participantIndentation
                        |> Validation.ofResult

                    return activeParticipant |> Parsed.map Participant, lines
                }

        let rec private parseParticipants location participants: ParseParticipants = fun output domainTypes indentationLevel -> function
            | [] ->
                match participants with
                | [] -> Error [ MissingParticipants ]
                | participants -> Ok (participants |> List.rev)

            | LineDepth (Depth 1) line :: lines ->
                result {
                    let! participant, lines =
                        line
                        |> parseParticipant location domainTypes indentationLevel lines

                    return!
                        lines
                        |> parseParticipants location (participant :: participants) output domainTypes indentationLevel
                }

            | line :: _ ->
                Error [
                    WrongParticipantIndentation (line |> Line.error (indentationLevel |> IndentationLevel.indentation))
                ]

        let parse location domainTypes: ParseLines<Parsed<unit> * ParsedParticipant list> = fun output indentationLevel -> function
            | [] -> Error [ MissingParticipants ]

            | KeyWords.Participants participantsRange :: lines ->
                result {
                    let participantLines, lines =
                        lines
                        |> List.splitBy (Line.isIndentedOrMore (indentationLevel |> IndentationLevel.indentation))

                    let! participants =
                        participantLines
                        |> parseParticipants location [] output domainTypes indentationLevel

                    let participantsKeyWord = Parsed.KeyWordWithoutValue {
                        KeyWord = {
                            Value = "participants"
                            Location = participantsRange |> location
                        }
                    }

                    return { Item = participantsKeyWord, participants; Lines = lines }
                }

            | _ -> Error [ MissingParticipants ]

    [<RequireQualifiedAccess>]
    module private Parts =
        type private Participants = Participants of Map<string, ActiveParticipant>
        type private ParseParts = MF.ConsoleApplication.Output -> TucName -> Participants -> DomainTypes -> IndentationLevel -> Line list -> Result<ParsedTucPart list, ParseError>

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

        let private dataLocations offset location line data =
            let rec loop acc = function
                | _, [] -> acc
                | None, [ onlyOne ] ->
                    {
                        Value = onlyOne
                        Location =
                            match offset with
                            | Some offset -> line |> Line.findRangeForWordAfter offset onlyOne |> location
                            | _ -> line |> Line.findRangeForWord onlyOne |> location
                    }
                    :: acc

                | Some previous, [ last ] ->
                    {
                        Value = last
                        Location = line |> Line.findRangeForWordAfter (previous |> ParsedLocation.endChar) last |> location
                    }
                    :: acc

                | None, first :: rest ->
                    let itemLocation = {
                        Value = first + "."
                        Location =
                            match offset with
                            | Some offset -> line |> Line.findRangeForWordAfter offset first |> location
                            | _ -> line |> Line.findRangeForWord first |> location
                    }
                    (Some itemLocation, rest) |> loop (itemLocation :: acc)

                | Some previous, current :: rest ->
                    let itemLocation = {
                        Value = current + "."
                        Location = line |> Line.findRangeForWordAfter (previous |> ParsedLocation.endChar) current |> location
                    }
                    (Some itemLocation, rest) |> loop (itemLocation :: acc)

            (None, data.Path)
            |> loop []
            |> List.rev

        let rec private parsePart
            (output: MF.ConsoleApplication.Output)
            location
            participants
            domainTypes
            indentationLevel
            indentation
            caller
            lines
            line: Result<ParsedTucPart * Line list, _> =

            let currentDepth = indentation |> Depth.ofIndentation indentationLevel

            let parsePart =
                parsePart output location participants domainTypes indentationLevel

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

                        let part =
                            let participantName = participant |> ActiveParticipant.name

                            Parsed.Lifeline {
                                Value = Lifeline {
                                    Initiator = participant
                                    Execution = execution |> List.map Parsed.value
                                }
                                ParticipantLocation = {
                                    Value = participantName
                                    Location = line |> Line.findRangeForWord participantName |> location
                                }
                                Execution = execution
                            }

                        return part, lines
                    }

                | IsStreamParticipant participants (ActiveParticipant.Stream s as stream) ->
                    result {
                        let! execution, lines = lines |> parseExecution stream

                        let! handlerCall =
                            match execution with
                            | [ Parsed.IncompleteHandleEvent ({ Value = HandleEventInStream handlerCall } as p) ] when handlerCall.Stream = stream ->
                                let streamPosition = {
                                    Value = stream |> ActiveParticipant.value
                                    Location = line |> Line.findRangeForWord (stream |> ActiveParticipant.value) |> location
                                }

                                Ok (p |> ParsedIncompleteHandleEvent.complete streamPosition)
                            | _ ->
                                Error <| MissingEventHandlerMethodCall (line |> Line.error indentation)

                        return handlerCall, lines
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

                            let serviceCall = serviceName + "."
                            let serviceCallRange = line |> Line.findRangeForWord serviceCall
                            let serviceLocation = {
                                Value = serviceCall
                                Location = serviceCallRange |> location
                            }

                            let part = Parsed.MethodCall {
                                Value = ServiceMethodCall {
                                    Caller = caller
                                    Service = service
                                    Method = { Name = methodName; Function = method }
                                    Execution = execution |> List.map Parsed.value
                                }
                                ServiceLocation = serviceLocation
                                MethodLocation = {
                                    Value = methodName |> FieldName.value
                                    Location = line |> Line.findRangeForWordAfter (serviceLocation |> ParsedLocation.endChar) (methodName |> FieldName.value) |> location
                                }
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

                            let serviceCall = serviceName + "."
                            let serviceCallRange = line |> Line.findRangeForWord serviceCall
                            let serviceLocation = {
                                Value = serviceCall
                                Location = serviceCallRange |> location
                            }

                            let part = Parsed.IncompleteHandleEvent {
                                Value = HandleEventInStream {
                                    Stream = caller
                                    Service = service
                                    Handler = { Name = handlerName; Handler = handler }
                                    Execution = execution |> List.map Parsed.value
                                }
                                ServiceLocation = serviceLocation
                                MethodLocation = {
                                    Value = handlerName |> FieldName.value
                                    Location = line |> Line.findRangeForWordAfter (serviceLocation |> ParsedLocation.endChar) (handlerName |> FieldName.value) |> location
                                }
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

                            return Note { Caller = caller; Lines = noteLines |> List.map Line.content } |> Parsed.Ignored, lines
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

                        return LeftNote { Lines = noteLines |> List.map Line.content } |> Parsed.Ignored, lines
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

                        return RightNote { Lines = noteLines |> List.map Line.content } |> Parsed.Ignored, lines
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

                            return Do { Caller = caller; Actions = actionLines |> List.map Line.content } |> Parsed.Ignored, lines
                        }

                | "if" -> Error <| IfWithoutCondition (line |> Line.error indentation)
                | "else" -> Error <| ElseOutsideOfIf (line |> Line.error indentation)
                | "group" -> Error <| GroupWithoutName (line |> Line.error indentation)
                | "loop" -> Error <| LoopWithoutCondition (line |> Line.error indentation)

                | _ ->
                    Error <| UnknownPart (line |> Line.error indentation)

            | KeyWords.SingleLineDo indentation action ->
                match caller with
                | None -> Error <| DoWithoutACaller (line |> Line.error indentation)
                | Some caller -> Ok (Do { Caller = caller; Actions = [ action ] } |> Parsed.Ignored, lines)

            | KeyWords.SingleLineLeftNote indentation note ->
                Ok (LeftNote { Lines = [ note ] } |> Parsed.Ignored, lines)

            | KeyWords.SingleLineRightNote indentation note ->
                Ok (RightNote { Lines = [ note ] } |> Parsed.Ignored, lines)

            | KeyWords.SingleLineNote indentation note ->
                match caller with
                | None -> Error <| NoteWithoutACaller (line |> Line.error indentation)
                | Some caller -> Ok (Note { Caller = caller; Lines = [ note ] } |> Parsed.Ignored, lines)

            | KeyWords.If indentation (condition, ifRange) ->
                match condition with
                | String.IsEmpty -> Error <| IfWithoutCondition (line |> Line.error indentation)
                | condition ->
                    result {
                        let! body, lines = lines |> parseBody (Depth 1)

                        if body |> List.isEmpty then
                            return! Error <| IfMustHaveBody (line |> Line.error indentation)

                        let! elseBody, elseRange, lines =
                            match lines with
                            | KeyWords.Else indentation elseRange as elseLine :: lines ->
                                result {
                                    let! body, lines = lines |> parseBody (Depth 1)

                                    if body |> List.isEmpty then
                                        return! Error <| ElseMustHaveBody (elseLine |> Line.error indentation)

                                    return Some body, Some elseRange, lines
                                }
                            | lines -> Ok (None, None, lines)

                        let part = Parsed.KeyWordIf {
                            Value = If {
                                Condition = condition
                                Body = body |> List.map Parsed.value
                                Else = elseBody |> Option.map (List.map Parsed.value)
                            }
                            IfLocation = {
                                Value = "if"
                                Location = ifRange |> location
                            }
                            ConditionLocation = {
                                Value = condition
                                Location = ifRange |> Range.fromEnd 1 condition.Length |> location
                            }
                            ElseLocation = elseRange |> Option.map (fun elseRange ->
                                {
                                    Value = "else"
                                    Location = elseRange |> location
                                }
                            )
                            Body = body
                            ElseBody = elseBody
                        }

                        return part, lines
                    }

            | KeyWords.Group indentation (groupName, groupRange) ->
                match groupName with
                | String.IsEmpty -> Error <| GroupWithoutName (line |> Line.error indentation)
                | groupName ->
                    result {
                        let! body, lines = lines |> parseBody (Depth 1)

                        if body |> List.isEmpty then
                            return! Error <| GroupMustHaveBody (line |> Line.error indentation)

                        let part = Parsed.KeyWordWithBody {
                            Value = Group {
                                Name = groupName
                                Body = body |> List.map Parsed.value
                            }
                            ValueLocation = {
                                Value = groupName
                                Location = groupRange |> Range.fromEnd 1 groupName.Length |> location
                            }
                            KeyWord = {
                                Value = "group"
                                Location = groupRange |> location
                            }
                            Body = body
                        }

                        return part, lines
                    }

            | KeyWords.Loop indentation (condition, loopRange) ->
                match condition with
                | String.IsEmpty -> Error <| LoopWithoutCondition (line |> Line.error indentation)
                | condition ->
                    result {
                        let! body, lines = lines |> parseBody (Depth 1)

                        if body |> List.isEmpty then
                            return! Error <| LoopMustHaveBody (line |> Line.error indentation)

                        let part = Parsed.KeyWordWithBody {
                            Value = Loop {
                                Condition = condition
                                Body = body |> List.map Parsed.value
                            }
                            ValueLocation = {
                                Value = condition
                                Location = loopRange |> Range.fromEnd 1 condition.Length |> location
                            }
                            KeyWord = {
                                Value = "loop"
                                Location = loopRange |> location
                            }
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

                        let dataLocations = data |> dataLocations None location line

                        let operatorLocation = {
                            Value = "->"
                            Location = line |> Line.findRangeForWordAfter (dataLocations |> List.last |> ParsedLocation.endChar) "->" |> location
                        }

                        let part = Parsed.PostData {
                            Value = PostData {
                                Caller = caller
                                DataObject = dataObject
                                Data = data
                            }
                            DataLocation = dataLocations
                            OperatorLocation = operatorLocation
                            DataObjectLocation = {
                                Value = dataObject |> ActiveParticipant.value
                                Location = line |> Line.findRangeForWordAfter (operatorLocation |> ParsedLocation.endChar) (dataObject |> ActiveParticipant.value) |> location
                            }
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

                        let dataObjectLocation = {
                            Value = dataObject |> ActiveParticipant.value
                            Location = line |> Line.findRangeForWord (dataObject |> ActiveParticipant.value) |> location
                        }

                        let operatorLocation = {
                            Value = "->"
                            Location = line |> Line.findRangeForWordAfter (dataObjectLocation |> ParsedLocation.endChar) "->" |> location
                        }

                        let dataLocations = data |> dataLocations (operatorLocation |> ParsedLocation.endChar |> Some) location line

                        let part = Parsed.ReadData {
                            Value = ReadData {
                                Caller = caller
                                DataObject = dataObject
                                Data = data
                            }
                            DataObjectLocation = dataObjectLocation
                            OperatorLocation = operatorLocation
                            DataLocation = dataLocations
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

                        let dataLocations = event |> Event.data |> dataLocations None location line

                        let operatorLocation = {
                            Value = "->"
                            Location = line |> Line.findRangeForWordAfter (dataLocations |> List.last |> ParsedLocation.endChar) "->" |> location
                        }

                        let part = Parsed.PostData {
                            Value = PostEvent {
                                Caller = caller
                                Stream = stream
                                Event = event
                            }
                            DataLocation = dataLocations
                            OperatorLocation = operatorLocation
                            DataObjectLocation = {
                                Value = stream |> ActiveParticipant.value
                                Location = line |> Line.findRangeForWordAfter (operatorLocation |> ParsedLocation.endChar) (stream |> ActiveParticipant.value) |> location
                            }
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

                        let dataObjectLocation = {
                            Value = stream |> ActiveParticipant.value
                            Location = line |> Line.findRangeForWord (stream |> ActiveParticipant.value) |> location
                        }

                        let operatorLocation = {
                            Value = "->"
                            Location = line |> Line.findRangeForWordAfter (dataObjectLocation |> ParsedLocation.endChar) "->" |> location
                        }

                        let dataLocations = event |> Event.data |> dataLocations (operatorLocation |> ParsedLocation.endChar |> Some) location line

                        let part = Parsed.ReadData {
                            Value = ReadEvent {
                                Caller = caller
                                Stream = stream
                                Event = event
                            }
                            DataLocation = dataLocations
                            OperatorLocation = operatorLocation
                            DataObjectLocation = dataObjectLocation
                        }

                        return part, lines

                    }
                | _ ->
                    let participantIndentation =
                        Indentation ((indentation |> Indentation.size) + " -> ".Length + eventName.Length)

                    Error <| UndefinedParticipant (line |> Line.error participantIndentation)

            | lineWithUnknownPart ->
                Error <| UnknownPart (lineWithUnknownPart |> Line.error indentation)

        and private parseBodyParts body parsePart: Line list -> Result<ParsedTucPart list, _> = function
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

        let rec private parseParts location parts depth: ParseParts = fun output tucName participants domainTypes indentationLevel lines ->
            let currentIndentation =
                Indentation ((depth |> Depth.value) * (indentationLevel |> IndentationLevel.size))

            let parseParts parts depth lines =
                parseParts location parts depth output tucName participants domainTypes indentationLevel lines

            match lines with
            | [] ->
                match parts with
                | [] -> Error <| MissingUseCase tucName
                | parts -> Ok (parts |> List.rev)

            | KeyWords.Section (section, sectionRange) as line :: lines ->
                match section with
                | String.IsEmpty -> Error <| SectionWithoutName (line |> Line.error (Indentation 0))
                | section ->
                    let section =
                        Parsed.KeyWord {
                            Value = Section { Value = section }
                            ValueLocation = {
                                Value = section
                                Location = sectionRange |> Range.fromEnd 1 section.Length |> location
                            }
                            KeyWord = {
                                Value = "section"
                                Location = sectionRange |> location
                            }
                        }

                    lines
                    |> parseParts (section :: parts) depth

            | line :: lines ->
                result {
                    let! part, lines =
                        line
                        |> parsePart output location participants domainTypes indentationLevel currentIndentation None lines

                    return! lines |> parseParts (part :: parts) depth
                }

        let parse location tucName participants domainTypes: ParseLines<ParsedTucPart list> = fun output indentationLevel lines ->
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
                        |> parseParts location [] (Depth 0) output tucName participants domainTypes indentationLevel
                        |> Validation.ofResult

                    return { Item = parts; Lines = [] }
                }

    let private parseTuc (output: MF.ConsoleApplication.Output) location domainTypes indentationLevel lines =
        result {
            let! name, lines =
                match lines with
                | (KeyWords.Tuc (name, tucRange) as line) :: lines ->
                    match name with
                    | String.IsEmpty -> Error [ TucMustHaveName (line |> Line.error (Indentation 0)) ]
                    | name ->
                        Ok (
                            Parsed.KeyWord {
                                Value = TucName name
                                ValueLocation = {
                                    Value = name
                                    Location = tucRange |> Range.fromEnd 1 name.Length |> location
                                }
                                KeyWord = {
                                    Value = "tuc"
                                    Location = location tucRange
                                }
                            },
                            lines
                        )

                | _ ->
                    Error [ MissingTucName ]

            let! { Item = participantsKeyWord, participants; Lines = lines } =
                lines
                |> Participants.parse location domainTypes output indentationLevel

            let! { Item = parts } =
                lines
                |> Parts.parse
                    location
                    (name |> Parsed.value)
                    (participants |> List.map Parsed.value)
                    domainTypes
                    output
                    indentationLevel

            return {
                Name = name
                ParticipantsKeyWord = participantsKeyWord
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

    let rec private parseLines (output: MF.ConsoleApplication.Output) location domainTypes indentationLevel (tucAcc: Result<ParsedTuc, ParseError list> list) = function
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
                        | KeyWords.Tuc _ ->
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
                    |> parseTuc output location domainTypes indentationLevel

                return! lines |> parseLines output location domainTypes indentationLevel (tuc :: tucAcc)
            }

    let parse (output: MF.ConsoleApplication.Output) withDiagnostics domainTypes file = result {
        let run name execution = Diagnostics.run withDiagnostics name execution

        let res, _parse = run "Parse" (fun () -> result {
            if output.IsVerbose() then output.Title <| sprintf "Parse %A" file

            let domainTypes =
                domainTypes
                |> List.map (fun (DomainType t) ->
                    (t |> ResolvedType.domain, t |> ResolvedType.name), t
                )
                |> Map.ofList
                |> DomainTypes

            let rawLines, _readFiles = run "Read Files" (fun () ->
                file
                |> File.ReadAllLines
                |> Seq.mapi RawLine.parse
                |> Seq.filter (RawLine.isEmpty >> not)
                |> Seq.toList
            )

            let indentationLevel, _indentationLevel = run "Indentation Level" (fun () ->
                rawLines
                |> List.tryPick (function
                    | { Indentation = indentation } when indentation |> Indentation.size > 0 ->  Some (IndentationLevel indentation)
                    | _ -> None
                )
                |> Result.ofOption [ MissingIndentation ]
            )
            let! indentationLevel = indentationLevel

            if output.IsVerbose() then
                output.Message <| sprintf "[Tuc] Current indentation level is <c:magenta>%d</c>" (indentationLevel |> IndentationLevel.size)

            let assertLines, _assertLines = run "Assert Lines" (fun () ->
                rawLines
                |> assertLinesIndentation indentationLevel
                |> Validation.ofResult
            )
            do! assertLines

            let res, _parseLines = run "Parse Lines" (fun () ->
                rawLines
                |> List.map (Line.ofRawLine indentationLevel)
                |> parseLines output (Location.create file) domainTypes indentationLevel []
            )
            let! res = res

            return res, [_readFiles; _indentationLevel; _assertLines; _parseLines]
        })

        let! res, diagnostics = res

        if withDiagnostics then
            diagnostics @ [ _parse ] |> Diagnostics.showResults output

        return res
    }
