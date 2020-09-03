namespace MF.Tuc.Parser

open MF.TucConsole
open MF.Tuc
open MF.Domain

[<RequireQualifiedAccess>]
module private Assert =
    open ErrorHandling
    open ParserPatterns

    let isInitiator line indentation = function
        | DomainType (SingleCaseUnion { ConstructorName = "Initiator" }) -> Ok ()
        | _ -> Error <| IsNotInitiator (line |> Line.error indentation)

    let definedComponentParticipants
        indentationLevel
        line
        componentName
        componentParticipantIndentation
        (componentParticipantLines: Line list)
        (componentFields: Map<FieldName, _>)
        componentParticipants

        = result {
            if componentParticipants |> List.isEmpty then
                return! Error [
                    ComponentWithoutParticipants (line |> Line.error (indentationLevel |> IndentationLevel.indentation))
                ]

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

                            Error <| UndefinedComponentParticipant (number, position, content, componentName, definedFields, (participant |> ActiveParticipant.name))
                )
                |> Validation.ofResults
                |> Validation.toResult

            return ()
        }

    let rec private assertIsOfEventType (output: MF.ConsoleApplication.Output) indentation line (DomainTypes domainTypes) domain eventType (event: Event) =
        let isDebug = output.IsDebug()
        if isDebug then
            output.Message (String.replicate 60 "." |> sprintf "<c:yellow>%s</c>")
            output.Message <| sprintf "Event: %A" event

        let currentPosition currentPath =
            (indentation |> Indentation.size)
            + (currentPath |> String.concat ".").Length
            |> Indentation

        let casesNames cases =
            cases
            |> List.map (fst >> TypeName.value)

        let caseTypeByName indentation cases eventName =
            cases
            |> List.tryFind (fst >> TypeName.value >> (=) eventName)
            |> Option.map snd
            |> Result.ofOption (Errors.wrongEvent indentation line (cases |> casesNames))

        let debugChecking eventName =
            if isDebug then output.Message <| sprintf "\n<c:purple>Checking</c> <c:yellow>%s</c>" eventName

        let rec assertType cases currentPath = function
            | [] -> Error <| Errors.wrongEventName indentation line EventError.Empty

            | [ eventName ] ->
                if isDebug then output.Message "<c:gray>// the end of path</c>"

                eventName
                |> tee debugChecking
                |> caseTypeByName (currentPosition currentPath) cases
                |> Result.map ignore

            | eventName :: path ->
                result {
                    eventName |> debugChecking
                    let currentPath = eventName :: currentPath

                    let! case =
                        eventName
                        |> caseTypeByName (currentPosition currentPath) cases

                    let cases =
                        match case with
                        | DomainType (DiscriminatedUnion { Cases = cases }) ->
                            cases
                            |> List.choose (function
                                | { Name = name; Argument = TypeDefinition.IsScalar scalar } ->
                                    Some (name, DomainType (ScalarType scalar))

                                | { Name = name; Argument = (Type arg) } ->
                                    domainTypes
                                    |> Map.tryFind (domain, arg)
                                    |> Option.map (fun argType -> name, DomainType argType)

                                | _ -> None
                            )

                        | DomainType (SingleCaseUnion { ConstructorName = name; ConstructorArgument = TypeDefinition.IsScalar scalar }) ->
                            [ TypeName name, DomainType (ScalarType scalar) ]

                        | DomainType (SingleCaseUnion { ConstructorName = name; ConstructorArgument = (Type arg) }) ->
                            domainTypes
                            |> Map.tryFind (domain, arg)
                            |> Option.map (fun argType -> TypeName name, DomainType argType)
                            |> Option.toList

                        | _ -> []

                    if isDebug then
                        output.Message <| sprintf " -> [Ok] <c:gray>go deeper to [%s] ...</c>" (cases |> casesNames |> String.concat "; ")

                    return! path |> assertType cases currentPath
                }

        event.Path |> assertType [ eventType |> DomainType.name, eventType ] []

    let event (output: MF.ConsoleApplication.Output) indentation line domainTypes expectedEventTypeName domain eventName = result {
        let! event =
            eventName
            |> Event.ofString
            |> Result.mapError (Errors.wrongEventName indentation line)

        let eventType =
            match domainTypes with
            | HasDomainType domain expectedEventTypeName eventType -> eventType
            | _ -> failwithf "[Assert] Undefined Event Type %A expected." expectedEventTypeName

        do! event |> assertIsOfEventType output indentation line domainTypes domain eventType

        return event
    }

    let data (output: MF.ConsoleApplication.Output) indentation line expectedDataTypeName dataName = result {
        if expectedDataTypeName <> dataName then
            return! Error <| Errors.wrongData indentation line dataName expectedDataTypeName

        return Data dataName
    }
