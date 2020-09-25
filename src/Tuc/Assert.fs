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

    let rec private assertIsOfType (output: MF.ConsoleApplication.Output) title errorEmptyName errorWrongEvent indentation line (DomainTypes domainTypes) domain dataType data =
        let isDebug = output.IsDebug()
        if isDebug then
            output.Message (String.replicate 60 "." |> sprintf "<c:yellow>%s</c>")
            output.Message <| sprintf "%s: %A" title data

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
            |> Result.ofOption (errorWrongEvent indentation line (cases |> casesNames))

        let debugChecking eventName =
            if isDebug then output.Message <| sprintf "\n<c:purple>Checking</c> <c:yellow>%s</c>" eventName

        let rec assertType cases currentPath = function
            | [] -> Error <| errorEmptyName indentation line

            | [ name ] ->
                if isDebug then output.Message "<c:gray>// the end of path</c>"

                name
                |> tee debugChecking
                |> caseTypeByName (currentPosition currentPath) cases
                |> Result.map ignore

            | name :: path ->
                result {
                    name |> debugChecking
                    let currentPath = name :: currentPath

                    let! case =
                        name
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

        data |> Data.path |> assertType [ dataType |> DomainType.name, dataType ] []

    let event (output: MF.ConsoleApplication.Output) indentation line domainTypes expectedEventTypeName domain eventName = result {
        let! event =
            eventName
            |> Event.ofString
            |> Result.mapError (fun e -> Errors.wrongEventName e indentation line)

        let eventType =
            match domainTypes with
            | HasDomainType domain expectedEventTypeName eventType -> eventType
            | _ -> failwithf "[Assert] Undefined Event Type %A expected." expectedEventTypeName

        do!
            event
            |> Event.data
            |> assertIsOfType output "Event"
                (Errors.wrongEventName EventError.Empty)
                Errors.wrongEvent
                indentation line domainTypes domain
                eventType

        return event
    }

    let data (output: MF.ConsoleApplication.Output) indentation line domainTypes expectedDataTypeName domain dataName = result {
        let! data =
            dataName
            |> Data.ofString
            |> Result.mapError (fun e -> Errors.wrongDataName e indentation line)

        let dataType =
            match domainTypes with
            | HasDomainType domain expectedDataTypeName dataType -> dataType
            | _ -> failwithf "[Assert] Undefined Data Type %A expected." expectedDataTypeName

        do!
            data
            |> assertIsOfType output "Data"
                (Errors.wrongDataName DataError.Empty)
                Errors.wrongData
                indentation line domainTypes domain
                dataType

        return data
    }
