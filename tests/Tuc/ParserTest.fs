module MF.TucConsole.Test.Tuc.Parser

open Expecto
open System.IO

open MF.Tuc

[<AutoOpen>]
module Common =
    open MF.Tuc.Parser
    open MF.Puml

    let (</>) a b = Path.Combine(a, b)

    let expectFile expected actualLines description =
        Expect.isTrue (expected |> File.Exists) description

        let expectedLines = expected |> File.ReadAllLines |> List.ofSeq
        let actualLines = actualLines |> List.ofSeq

        let separator = String.replicate 50 "."

        Expect.equal
            (actualLines |> List.length)
            (expectedLines |> List.length)
            (sprintf "%s\nActual:\n%s\n%s\n%s"
                description
                separator
                (actualLines |> List.mapi (fun i line -> sprintf "% 3i| %s" i line) |> String.concat "\n")
                separator
            )

        expectedLines
        |> List.iteri (fun i expectedLine ->
            Expect.equal actualLines.[i] expectedLine (sprintf "%s - error at line: #%d" description i)
        )

    let orFail formatError = function
        | Ok ok -> ok
        | Error error -> error |> formatError |> failtestf "%s"

    type Case = {
        Description: string
        Tuc: string
        Expected: Result<string, ParseError list>
    }

    let case path description tuc expected =
        {
            Description = description
            Tuc = path </> tuc
            Expected = expected |> Result.map ((</>) path)
        }

    let test output domainTypes { Tuc = tuc; Expected = expected; Description = description } =
        let parsedTucs =
            tuc
            |> Parser.parse output domainTypes

        match expected, parsedTucs with
        | Ok expected, Ok actual ->
            let puml =
                actual
                |> Generate.puml output description
                |> orFail PumlError.format
                |> Puml.value

            expectFile expected (puml.TrimEnd().Split "\n") description

        | Error expected, Error actual -> Expect.equal actual expected description
        | Error _, Ok success -> failtestf "%s - Error was expected, but it results in ok.\n%A" description success
        | Ok _, Error error -> failtestf "%s - Success was expected, but it results in error.\n%A" description error

module Domain =
    open MF.Domain
    open ErrorHandling

    let parseDomainTypes output domain =
        result {
            let! resolvedTypes =
                domain
                |> Parser.parse output
                |> List.singleton
                |> Resolver.resolve output

            return!
                resolvedTypes
                |> Checker.check output
        }
        |> orFail (List.map TypeName.value >> String.concat "\n  - " >> sprintf "Unresolved types:\n%s")

[<RequireQualifiedAccess>]
module Parts =
    let path = "./Tuc/Fixtures/parts"

    let case = case path

    let provider: Case list =
        [
            case "Sections" "section.tuc" (Ok "section.puml")

            case "Initiator's lifeline" "lifeline.tuc" (Ok "lifeline.puml")

            case "Notes" "note.tuc" (Ok "note.puml")
            case "Note without a caller" "note-without-caller.tuc" (Error [ NoteWithoutACaller (5, 0, @"""Note without caller""") ])

            case "Do" "do.tuc" (Ok "do.puml")
            case "Do without a caller" "do-without-caller.tuc" (Error [ DoWithoutACaller (5, 0, "do Some stuff") ])

            case "Handle event in stream" "handle-event-in-stream.tuc" (Ok "handle-event-in-stream.puml")

            case "Read event from stream" "read-event.tuc" (Ok "read-event.puml")
            case "Post event to stream" "post-event.tuc" (Ok "post-event.puml")

            case "Call a service method" "service-method-call.tuc" (Ok "service-method-call.puml")

            case "Loops" "loop.tuc" (Ok "loop.puml")
            case "Groups" "group.tuc" (Ok "group.puml")
            case "If" "if.tuc" (Ok "if.puml")
        ]

[<RequireQualifiedAccess>]
module ParseErrors =
    let path = "./Tuc/Fixtures/errors"

    let case = case path

    let provider: Case list =
        [
            // Tuc file
            case "MissingTucName" "MissingTucName.tuc" (Error [ MissingTucName ])
            case "TucMustHaveName" "TucMustHaveName.tuc" (Error [ TucMustHaveName (1, 0, "tuc") ])
            case "MissingParticipants"  "MissingParticipants.tuc"  (Error [ MissingParticipants ])
            case "MissingIndentation" "MissingIndentation.tuc" (Error [ MissingIndentation ])
            case "WrongIndentationLevel" "WrongIndentationLevel.tuc" (Error [ WrongIndentationLevel (4, [
                "  6|    do nothing"
                "  8|  \"Also here\""
                " 12|      \"> And here is it wrong\""
            ]) ])
            case "TooMuchIndented" "TooMuchIndented.tuc" (Error [ TooMuchIndented (6, 4, "        \"This is too much indented\"") ])

            // Participants
            case "WrongParticipantIndentation" "WrongParticipantIndentation.tuc" (Error [ WrongParticipantIndentation (4, 4, "        StreamListener parts") ])
            case "WrongParticipantIndentation in component" "WrongParticipantIndentation-in-component.tuc" (Error [ WrongParticipantIndentation (4, 8, "            StreamListener parts") ])
            case "ComponentWithoutParticipants" "ComponentWithoutParticipants.tuc" (Error [ ComponentWithoutParticipants (4, 4, "    StreamComponent parts") ])
            case "UndefinedComponentParticipant" "UndefinedComponentParticipant.tuc" (Error [
                UndefinedComponentParticipant (4, 8, "        GenericService parts", "StreamComponent", ["StreamListener"], "GenericService")
                UndefinedComponentParticipant (5, 8, "        Service parts", "StreamComponent", ["StreamListener"], "Service")
            ])
            case "WrongComponentParticipantDomain" "WrongComponentParticipantDomain.tuc" (Error [ WrongComponentParticipantDomain (5, 8, "        Service wrongDomain", "Parts") ])
            case "InvalidParticipant" "InvalidParticipant.tuc" (Error [ InvalidParticipant (3, 4, "    GenericService domain foo bar") ])
            case "UndefinedParticipantInDomain in participant definition" "UndefinedParticipantInDomain.tuc" (Error [ UndefinedParticipantInDomain (3, 4, "    ServiceNotInDomain parts", "Parts") ])
            case "UndefinedParticipantInDomain in participant definition" "UndefinedParticipant-in-participants.tuc" (Error [ UndefinedParticipantInDomain (3, 4, "    UndefinedParticipantDefinition parts", "Parts") ])
            case "UndefinedParticipant in parts" "UndefinedParticipant-in-parts.tuc" (Error [ UndefinedParticipant (6, 4, "    UndefinedParticipant.Foo") ])

            // parts
            case "MissingUseCase" "MissingUseCase.tuc" (Error [ MissingUseCase (TucName "without a use-case") ])
            case "SectionWithoutName" "SectionWithoutName.tuc" (Error [ SectionWithoutName (5, 0, "section") ])
            case "IsNotInitiator" "IsNotInitiator.tuc" (Error [ IsNotInitiator (5, 0, "StreamListener") ])
            case "CalledUndefinedMethod" "CalledUndefinedMethod.tuc" (Error [ CalledUndefinedMethod (7, 4, "    Service.UndefinedMethod", "Service", ["DoSomeWork"]) ])
            case "CalledUndefinedHandler" "CalledUndefinedHandler.tuc" (Error [ CalledUndefinedHandler (7, 4, "    StreamListener.UndefinedHandler", "StreamListener", ["ReadEvent"]) ])
            case "MethodCalledWithoutACaller" "MethodCalledWithoutACaller.tuc" (Error [ MethodCalledWithoutACaller (5, 0, "Service.Method") ])
            case "EventPostedWithoutACaller" "EventPostedWithoutACaller.tuc" (Error [ EventPostedWithoutACaller (5, 0, "InputEvent -> [InputStream]") ])
            case "EventReadWithoutACaller" "EventReadWithoutACaller.tuc" (Error [ EventReadWithoutACaller (5, 0, "[InputStream] -> InputEvent") ])
            case "MissingEventHandlerMethodCall" "MissingEventHandlerMethodCall.tuc" (Error [ MissingEventHandlerMethodCall (5, 0, "[InputStream]") ])
            case "InvalidMultilineNote" "InvalidMultilineNote.tuc" (Error [ InvalidMultilineNote (6, 4, "    \"\"\"") ])
            case "InvalidMultilineLeftNote" "InvalidMultilineLeftNote.tuc" (Error [ InvalidMultilineLeftNote (5, 0, "\"<\"") ])
            case "InvalidMultilineRightNote" "InvalidMultilineRightNote.tuc" (Error [ InvalidMultilineRightNote (5, 0, "\">\"") ])
            case "DoWithoutACaller" "DoWithoutACaller.tuc" (Error [ DoWithoutACaller (5, 0, "do well.. nothing") ])
            case "DoMustHaveActions" "DoMustHaveActions.tuc" (Error [ DoMustHaveActions (6, 4, "    do") ])
            case "IfWithoutCondition" "IfWithoutCondition.tuc" (Error [ IfWithoutCondition (5, 0, "if") ])
            case "IfMustHaveBody" "IfMustHaveBody.tuc" (Error [ IfMustHaveBody (5, 0, "if true") ])
            case "ElseOutsideOfIf" "ElseOutsideOfIf.tuc" (Error [ ElseOutsideOfIf (5, 0, "else") ])
            case "ElseMustHaveBody" "ElseMustHaveBody.tuc" (Error [ ElseMustHaveBody (8, 4, "    else") ])
            case "GroupWithoutName" "GroupWithoutName.tuc" (Error [ GroupWithoutName (5, 0, "group") ])
            case "GroupMustHaveBody" "GroupMustHaveBody.tuc" (Error [ GroupMustHaveBody (5, 0, "group Without a body") ])
            case "LoopWithoutCondition" "LoopWithoutCondition.tuc" (Error [ LoopWithoutCondition (5, 0, "loop") ])
            case "LoopMustHaveBody" "LoopMustHaveBody.tuc" (Error [ LoopMustHaveBody (5, 0, "loop always") ])
            case "NoteWithoutACaller" "NoteWithoutACaller.tuc" (Error [ NoteWithoutACaller (5, 0, "\"note without a caller\"") ])
            case "UnknownPart" "UnknownPart.tuc" (Error [ UnknownPart (5, 0, "basically whaterver here") ])
        ]

[<RequireQualifiedAccess>]
module Event =
    let path = "./Tuc/Fixtures/event"

    let case = case path

    let provider: Case list =
        [
            case "Valid cases" "valid.tuc" (Ok "valid.puml")

            case "InteractionEvent with typo" "wrong-interaction-event.tuc" (Error [
                WrongEvent (
                    10,
                    8,
                    "        InteractionEvents -> [InteractionStream]",
                    ["InteractionEvent"]
                )
            ])

            case "Confirmed with typo" "wrong-confirmed.tuc" (Error [
                WrongEvent (
                    10,
                    24,
                    "        InteractionEvent.Confirmes -> [InteractionStream]",
                    ["Confirmed"; "Rejected"; "Interaction"; "Other"]
                )
            ])

            case "Interaction is too deep" "wrong-interaction-too-deep.tuc" (Error [
                WrongEvent (
                    10,
                    36,
                    "        InteractionEvent.Interaction.Interaction -> [InteractionStream]",
                    []
                )
            ])

            case "Interaction with undefined rejection" "wrong-rejection.tuc" (Error [
                WrongEvent (
                    10,
                    46,
                    "        InteractionEvent.Rejected.UserRejected.Boo -> [InteractionStream]",
                    ["Foo"; "Bar"]
                )
            ])
        ]

[<RequireQualifiedAccess>]
module Example =
    let path = "./Tuc/Fixtures/example"

    let case = case path

    let provider: Case list =
        [
            case "Readme example" "definition.tuc" (Ok "result.puml")
        ]

[<RequireQualifiedAccess>]
module MultiTuc =
    let path = "./Tuc/Fixtures/multi-tuc"

    let case = case path

    let provider: Case list =
        [
            case "4 Valid tucs in 1 file" "4-valid.tuc" (Ok "4-valid.puml")

            case "3 Different Errors and 1 correct tuc" "3-different-errors.tuc" (Error [
                UndefinedComponentParticipant (4, 8, "        GenericService tests", "StreamComponent", ["StreamListener"], "GenericService")
                UndefinedComponentParticipant (5, 8, "        Service tests", "StreamComponent", ["StreamListener"], "Service")
                ComponentWithoutParticipants (9, 4, "    StreamComponent tests")
                CalledUndefinedMethod (27, 4, "    Service.UndefinedMethod", "Service", ["DoSomeWork"])
            ])
        ]

[<Tests>]
let parserTests =
    let output = MF.ConsoleApplication.Output.console
    let test domainTypes = List.iter (test output domainTypes)

    testList "Tuc.Parser" [
        testCase "should parse parts" <| fun _ ->
            let domainTypes =
                Parts.path </> "partsDomain.fsx"
                |> Domain.parseDomainTypes output

            Parts.provider |> test domainTypes

        testCase "should parse events" <| fun _ ->
            let domainTypes =
                Event.path </> "testsDomain.fsx"
                |> Domain.parseDomainTypes output

            Event.provider |> test domainTypes

        testCase "should show nice parse errors" <| fun _ ->
            let domainTypes =
                ParseErrors.path </> "partsDomain.fsx"
                |> Domain.parseDomainTypes output

            ParseErrors.provider |> test domainTypes

        testCase "should parse multiple tucs" <| fun _ ->
            let domainTypes =
                MultiTuc.path </> "testsDomain.fsx"
                |> Domain.parseDomainTypes output

            MultiTuc.provider |> test domainTypes

        testCase "should parse readme example" <| fun _ ->
            let domainTypes =
                Example.path </> "consentsDomain.fsx"
                |> Domain.parseDomainTypes output

            Example.provider |> test domainTypes
    ]
