namespace MF.Tuc

open MF.TucConsole

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
    | UndefinedComponentParticipant of lineNumber: int * position: int * line: string * componentName: string * definedFields: string list * wantedService: string
    | InvalidParticipantIndentation of lineNumber: int * position: int * line: string
    | InvalidParticipant of lineNumber: int * position: int * line: string
    | UndefinedParticipant of lineNumber: int * position: int * line: string

    // Parts
    | MissingUseCase of TucName
    | SectionWithoutName of lineNumber: int * position: int * line: string
    | IsNotInitiator of lineNumber: int * position: int * line: string
    | CalledUndefinedMethod of lineNumber: int * position: int * line: string * service: string * definedMethods: string list
    | CalledUndefinedHandler of lineNumber: int * position: int * line: string * service: string * definedHandlerNames: string list
    | MethodCalledWithoutACaller of lineNumber: int * position: int * line: string
    | EventPostedWithoutACaller of lineNumber: int * position: int * line: string
    | EventReadWithoutACaller of lineNumber: int * position: int * line: string
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

    // Others
    | WrongEventName of lineNumber: int * position: int * line: string * message: string
    | UndefinedEventType of lineNumber: int * position: int * line: string
    | WrongEvent of lineNumber: int * position: int * line: string * definedCases: string list

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

        | UndefinedComponentParticipant (lineNumber, position, line, componentName, definedFields, wantedService) ->
            let error =
                sprintf "This participant is not defined as one of the field of the component %s." componentName
                |> red
                |> formatLineWithError lineNumber position line

            let formattedFields =
                definedFields
                |> List.formatAvailableItems
                    "There are no defined fields"
                    (List.formatLines "  - " id)
                    wantedService

            sprintf "%s\n\n<c:red>Component %s has defined fields: %s</c>"
                error
                componentName
                formattedFields

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

            let serviceHas =
                sprintf "Service %s has" serviceName

            let definedMethods =
                match definedMethods with
                | [] -> sprintf "%s not defined any methods." serviceHas
                | definedMethods ->
                    sprintf "%s defined methods:%s</c>"
                        serviceHas
                        (definedMethods |> List.formatLines "  - " id)

            sprintf "%s\n\n<c:red>%s</c>" error definedMethods

        | CalledUndefinedHandler (lineNumber, position, line, serviceName, definedHandlers) ->
            let error =
                sprintf "There is an undefined handler called on the service %s." serviceName
                |> red
                |> formatLineWithError lineNumber (position + serviceName.Length) line

            let serviceHas =
                sprintf "Service %s has" serviceName

            let definedHandlers =
                match definedHandlers with
                | [] -> sprintf "%s not defined any handlers." serviceHas
                | definedHandlers ->
                    sprintf "%s defined handlers:%s</c>"
                        serviceHas
                        (definedHandlers |> List.formatLines "  - " id)

            sprintf "%s\n\n<c:red>%s</c>" error definedHandlers

        | MissingEventHandlerMethodCall (lineNumber, position, line) ->
            "There must be exactly one handler call which handles the stream. (It must be on the subsequent line, indented by one level)"
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

        // others

        | WrongEventName (lineNumber, position, line, message) ->
            sprintf "There is a wrong event - %s." message
            |> red
            |> formatLineWithError lineNumber position line

        | UndefinedEventType (lineNumber, position, line) ->
            "There is an undefined event."
            |> red
            |> formatLineWithError lineNumber position line

        | WrongEvent (lineNumber, position, line, cases) ->
            let error =
                "There is no such a case for an event."
                |> red
                |> formatLineWithError lineNumber position line

            let defineCases =
                match cases with
                | [] -> "Event does not have defined any more cases."
                | cases ->
                    sprintf "Event has defined cases:%s"
                        (cases |> List.formatLines "  - " id)

            sprintf "%s\n\n<c:red>%s</c>" error defineCases