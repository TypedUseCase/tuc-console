module MF.TucConsole.Test.Tuc.Parser

open Expecto
open System.IO

open MF.Tuc

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
module Event =
    open MF.Domain

    let path = "./Tuc/Fixtures/event"

    type Case = {
        Description: string
        Tuc: string
        Expected: Result<string, ParseError>
    }

    let case description tuc expected =
        {
            Description = description
            Tuc = path </> tuc
            Expected = expected |> Result.map ((</>) path)
        }

    let provider: Case list =
        [
            case "Valid cases" "valid.tuc" (Ok "valid.puml")

            case "InteractionEvent with typo" "wrong-interaction-event.tuc" (Error <|
                WrongEvent (
                    10,
                    8,
                    "        InteractionEvents -> [InteractionStream]",
                    ["InteractionEvent"]
                )
            )

            case "Confirmed with typo" "wrong-confirmed.tuc" (Error <|
                WrongEvent (
                    10,
                    24,
                    "        InteractionEvent.Confirmes -> [InteractionStream]",
                    ["Confirmed"; "Rejected"; "Interaction"; "Other"]
                )
            )

            case "Interaction is too deep" "wrong-interaction-too-deep.tuc" (Error <|
                WrongEvent (
                    10,
                    36,
                    "        InteractionEvent.Interaction.Interaction -> [InteractionStream]",
                    []
                )
            )

            case "Interaction with undefined rejection" "wrong-rejection.tuc" (Error <|
                WrongEvent (
                    10,
                    46,
                    "        InteractionEvent.Rejected.UserRejected.Boo -> [InteractionStream]",
                    ["Foo"; "Bar"]
                )
            )
        ]

    open MF.Tuc.Parser
    open MF.Puml

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
        | Error _, Ok success -> failtestf "Error was expected, but it results in ok.\n%A" success
        | Ok _, Error error -> failtestf "Success was expected, but it results in error.\n%A" error

[<Tests>]
let parserTests =
    let output = MF.ConsoleApplication.Output.console

    testList "Tuc.Parser" [
        testCase "should parse events" <| fun _ ->
            let domainTypes =
                Event.path </> "domain.fsx"
                |> Domain.parseDomainTypes output

            Event.provider |> List.iter (Event.test output domainTypes)
    ]
