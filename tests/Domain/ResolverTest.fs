module MF.TucConsole.Test.Domain.Parser

open Expecto
open System.IO

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

[<RequireQualifiedAccess>]
module Domain =
    open MF.Domain

    let path = "./Domain/Fixtures"

    type Case = {
        Description: string
        Domain: string
        Expected: Result<ResolvedType list, TypeName list>
    }

    let case description domain expected =
        {
            Description = description
            Domain = path </> domain
            Expected = expected
        }

    let provider: Case list =
        let unit = Type (TypeName "unit")
        let string = Type (TypeName "string")
        let int = Type (TypeName "int")

        let recordTypes = [
            Record {
                Domain = None
                Name = TypeName "Name"
                Fields = Fields.ofList [
                    FieldName "FirstName", string
                    FieldName "Middle", Option string
                    FieldName "LastName", string
                ]
                Methods = Fields.empty
                Handlers = Fields.empty
            }

            SingleCaseUnion { Domain = None; Name = TypeName "Id"; ConstructorName = "UUID"; ConstructorArgument = unit }

            Record {
                Domain = None
                Name = TypeName "ContractCreatedEvent"
                Fields = Fields.ofList [
                    FieldName "ContractId", Type (TypeName "Id")
                    FieldName "Intent", Type (TypeName "Intent")
                    FieldName "Texts", Type (TypeName "Text") |> List
                ]
                Methods = Fields.empty
                Handlers = Fields.empty
            }

            Record {
                Domain = None
                Name = TypeName "Intent"
                Fields = Fields.ofList [
                    FieldName "Purpose", string
                    FieldName "Scope", string
                ]
                Methods = Fields.empty
                Handlers = Fields.empty
            }

            SingleCaseUnion { Domain = None; Name = TypeName "Text"; ConstructorName = "Text"; ConstructorArgument = string }

            Record {
                Domain = None
                Name = TypeName "Service"
                Fields = Fields.empty
                Methods = Fields.ofList [
                    FieldName "FirstMethod", { Argument = Type (TypeName "SingleAttribute"); Returns = Type (TypeName "MethodResult") }
                    FieldName "SecondMethod", { Argument = Type (TypeName "SingleAttribute"); Returns = Function { Argument = Type (TypeName "Name"); Returns = Type (TypeName "MethodResult" )} }
                    FieldName "ThirdMethod", { Argument = Tuple [ Type (TypeName "SingleAttribute"); Type (TypeName "Name") ]; Returns = unit }
                ]
                Handlers = Fields.empty
            }

            SingleCaseUnion { Domain = None; Name = TypeName "SingleAttribute"; ConstructorName = "SingleAttribute"; ConstructorArgument = string }

            DiscriminatedUnion { Domain = None; Name = TypeName "MethodResult"; Cases = [
                { Name = TypeName "Ok"; Argument = unit }
                { Name = TypeName "Error"; Argument = unit }
            ]}
        ]

        [
            case "Empty domain" "empty.fsx" (Ok [])

            case "Options" "options.fsx" (Ok [
                SingleCaseUnion { Domain = None; Name = TypeName "MaybeName"; ConstructorName = "MaybeName"; ConstructorArgument = Option string }
            ])

            case "Single Case Unions" "singleCaseUnions.fsx" (Ok [
                SingleCaseUnion { Domain = None; Name = TypeName "FirstType"; ConstructorName = "FirstType"; ConstructorArgument = unit }

                SingleCaseUnion { Domain = None; Name = TypeName "SecondType"; ConstructorName = "SecondTypeCtr"; ConstructorArgument = string }

                SingleCaseUnion { Domain = None; Name = TypeName "ThirdType"; ConstructorName = "ThirdType"; ConstructorArgument = Tuple [ string; string ] }

                SingleCaseUnion { Domain = None; Name = TypeName "FourthType"; ConstructorName = "FourthType"; ConstructorArgument = Tuple [ string; int ] }
            ])

            case "Discriminated unions" "discriminatedUnions.fsx" (Ok [
                DiscriminatedUnion { Domain = None; Name = TypeName "FirstType"; Cases = [
                    { Name = TypeName "CaseA"; Argument = unit }
                    { Name = TypeName "CaseB"; Argument = unit }
                ] }

                DiscriminatedUnion { Domain = None; Name = TypeName "SecondType"; Cases = [
                    { Name = TypeName "CaseA"; Argument = string }
                    { Name = TypeName "CaseB"; Argument = unit }
                    { Name = TypeName "CaseC"; Argument = Type (TypeName "FirstType") }
                ] }

                DiscriminatedUnion { Domain = None; Name = TypeName "ThirdType"; Cases = [
                    { Name = TypeName "First"; Argument = Type (TypeName "FirstType") }
                    { Name = TypeName "Second"; Argument = Type (TypeName "SecondType") }
                    { Name = TypeName "Fourth"; Argument = Type (TypeName "FourthType") }
                ] }

                DiscriminatedUnion { Domain = None; Name = TypeName "FourthType"; Cases = [
                    { Name = TypeName "CaseFooBar"; Argument = Tuple [ Type (TypeName "Foo"); Type (TypeName "Bar") ] }
                    { Name = TypeName "CaseFun"; Argument = Function { Argument = Type (TypeName "Foo"); Returns = Type (TypeName "Bar") }  }
                ] }

                SingleCaseUnion { Domain = None; Name = TypeName "Foo"; ConstructorName = "Foo"; ConstructorArgument = unit }
                SingleCaseUnion { Domain = None; Name = TypeName "Bar"; ConstructorName = "Bar"; ConstructorArgument = unit }

                DiscriminatedUnion { Domain = None; Name = TypeName "RecursiveType"; Cases = [
                    { Name = TypeName "Other"; Argument = string }
                    { Name = TypeName "Self"; Argument = Type (TypeName "RecursiveType") }
                ] }
            ])

            case "Functions" "functions.fsx" (Ok [
                SingleCaseUnion { Domain = None; Name = TypeName "Function"; ConstructorName = "Function"; ConstructorArgument = Function {
                    Argument = string
                    Returns = string
                } }

                SingleCaseUnion { Domain = None; Name = TypeName "SecondFunction"; ConstructorName = "SecondFunction"; ConstructorArgument = Function {
                    Argument = Tuple [ int; string ]
                    Returns = Function {
                        Argument = unit
                        Returns = string
                    }
                } }

                SingleCaseUnion { Domain = None; Name = TypeName "GenericFunction"; ConstructorName = "GenericFunction"; ConstructorArgument = Function {
                    Argument = string
                    Returns = Function {
                        Argument = GenericParameter (TypeName "Input")
                        Returns = GenericParameter (TypeName "Output")
                    }
                } }

                SingleCaseUnion { Domain = None; Name = TypeName "ComplexFunction"; ConstructorName = "ComplexFunction"; ConstructorArgument = Function {
                    Argument = GenericParameter (TypeName "Input") |> List
                    Returns = Function {
                        Argument = string |> Option
                        Returns = GenericType {
                            Type = TypeName "Async"
                            Argument = GenericType {
                                Type = TypeName "Result"
                                Argument = Tuple [ GenericParameter (TypeName "Input"); string ]
                            }
                        }
                    }
                } }
            ])

            case "Records" "records.fsx" (Ok recordTypes)

            case "With load" "with-load.fsx" (Ok [
                yield! recordTypes

                yield Record {
                    Domain = None
                    Name = TypeName "Contract"
                    Fields = Fields.ofList [
                        FieldName "Text", string
                        FieldName "Intent", Type (TypeName "Intent")
                    ]
                    Methods = Fields.empty
                    Handlers = Fields.empty
                }
            ])

            case "Generics" "generics.fsx" (Ok [
                SingleCaseUnion { Domain = None; Name = TypeName "Stream"; ConstructorName = "Stream"; ConstructorArgument = GenericParameter (TypeName "Event") |> List }

                SingleCaseUnion { Domain = None; Name = TypeName "StreamHandler"; ConstructorName = "StreamHandler"; ConstructorArgument = Function { Argument = GenericParameter (TypeName "Event"); Returns = unit } }

                DiscriminatedUnion { Domain = None; Name = TypeName "InteractionEvent"; Cases = [
                    { Name = TypeName "Confirmation"; Argument = unit }
                    { Name = TypeName "Rejection"; Argument = unit }
                ] }

                Stream { Domain = None; Name = TypeName "InteractionCollectorStream"; EventType = TypeName "InteractionEvent" }

                Record {
                    Domain = None
                    Name = TypeName "PersonIdentificationEngine"
                    Fields = Fields.empty
                    Methods = Fields.empty
                    Handlers = Fields.ofList [
                        FieldName "OnInteractionEvent", { Name = TypeName "StreamHandler"; Handles = Type (TypeName "InteractionEvent") }
                    ]
                }

                SingleCaseUnion { Domain = None; Name = TypeName "Method"; ConstructorName = "Method"; ConstructorArgument = Function {
                    Argument = Type (TypeName "Input") |> List |> Option
                    Returns = GenericType {
                        Type = TypeName "Async"
                        Argument = GenericType {
                            Type = TypeName "Result"
                            Argument = Tuple [ Type (TypeName "Output") |> Option; string ]
                        }
                    } |> List
                } }

                SingleCaseUnion { Domain = None; Name = TypeName "Input"; ConstructorName = "Input"; ConstructorArgument = string }
                SingleCaseUnion { Domain = None; Name = TypeName "Output"; ConstructorName = "Output"; ConstructorArgument = string }
            ])
        ]

    let test output { Domain = domain; Expected = expected; Description = description } =
        let resolvedDomain =
            domain
            |> Parser.parse output
            |> List.singleton
            |> Resolver.resolve output

        match expected, resolvedDomain with
        | Ok expected, Ok actual -> Expect.equal (actual |> List.sort) (expected |> List.sort) description
        | Error expected, Error actual -> Expect.equal actual expected description
        | Error _, Ok success -> failtestf "Error was expected, but it results in ok.\n%A" success
        | Ok _, Error error -> failtestf "Success was expected, but it results in error.\n%A" error

[<Tests>]
let parserTests =
    let output = MF.ConsoleApplication.Output.console

    testList "Domain.Resolver" [
        testCase "should parse and resolve types" <| fun _ ->
            Domain.provider |> List.iter (Domain.test output)
    ]
