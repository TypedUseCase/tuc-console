namespace MF.Domain

open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open MF.TucConsole

[<RequireQualifiedAccess>]
module Resolver =
    open MF.TucConsole.ConcurrentCache
    open MF.TucConsole.Option.Operators
    open TypeResolvers

    let private resolvedTypes: Cache<TypeName, ResolvedType> = Cache.empty()

    type private Collect<'In, 'Out> = MF.ConsoleApplication.Output -> 'In list -> 'Out
    type private CollectMany<'In, 'Out> = MF.ConsoleApplication.Output -> 'In list -> 'Out list

    let private cacheResolvedTypes =
        List.iter (fun parsedType ->
            resolvedTypes
            |> Cache.set
                (parsedType |> ResolvedType.name |> Key)
                parsedType
        )

    /// Cast IList or other Enumerable to list and pass to a function, which parse types and then cache resolved result
    let inline private (|>>) input f =
        input
        |> Seq.toList
        |> f
        |> tee cacheResolvedTypes

    let rec private resolveTypeDefinition: FSharpType -> TypeDefinition = function
        | IsScalarType scalar -> Type (TypeName scalar.TypeDefinition.DisplayName)
        | IsListType listItem -> List (listItem |> resolveTypeDefinition)
        | IsOptionType optionValue -> Option (optionValue |> resolveTypeDefinition)

        | abbreviationType when abbreviationType.IsAbbreviation && abbreviationType.HasTypeDefinition ->
            abbreviationType
            |> Dump.formatType
            |> failwithf "ResolveError.abbreviation: %s"

        | IsFunctionType functionType ->
            match functionType.GenericArguments |> Seq.toList |> List.map resolveTypeDefinition |> List.rev with
            | [] -> failwithf "Invalid function definition, without any arguments and return value."
            | returns :: arguments ->
                Function { Arguments = arguments |> List.rev; Returns = returns }

        | IsTuple tupleType ->
            Tuple (tupleType.GenericArguments |> Seq.toList |> List.map resolveTypeDefinition)

        | IsGeneric genericType ->
            GenericParameter (TypeName genericType.GenericParameter.DisplayName)

        | withtoutTypeDefinition when withtoutTypeDefinition.HasTypeDefinition |> not ->
            withtoutTypeDefinition
            |> Dump.formatType
            |> failwithf "ResolveError.noDef: %s"

        | IsTypeWithoutArgs t ->
            Type (TypeName t.TypeDefinition.DisplayName)

        | IsTypeWithGenericArgs t ->
            GenericType {
                Type = TypeName t.TypeDefinition.DisplayName;
                Argument =
                    match t.GenericArguments |> Seq.toList |> List.map resolveTypeDefinition with
                    | [ single ] -> single
                    | tuple -> Tuple tuple
            }

        | t ->
            t
            |> Dump.formatType
            |> failwithf "ResolveError: %s"

    let private collectUnionCaseFields (TypeName parent): Collect<FSharpField, TypeDefinition> = fun output fields ->
        if output.IsVerbose() then
            fields |> Seq.length |> sprintf " - %s.Fields [%d]" parent |> output.Message

        match fields |> List.map (fun field -> field.FieldType |> resolveTypeDefinition) with
        | [] -> Type (Unit |> ScalarType.name)
        | [ field ] -> field
        | fields -> Tuple fields

    let private collectRecordFields (TypeName parent): CollectMany<FSharpField, FieldName * TypeDefinition> = fun output fields ->
        let fields =
            fields
            |> List.filter (fun field -> not field.FieldType.IsFunctionType)

        if output.IsVerbose() then
            fields |> Seq.length |> sprintf " - %s.Fields [%d]" parent |> output.Message

        fields
        |> List.map (function
            | nameless when nameless.IsNameGenerated -> failwithf "Record %A has a nameless field with type: %A." parent (nameless.FieldType |> Dump.formatType)
            | named -> (FieldName named.DisplayName) => (named.FieldType |> resolveTypeDefinition)
        )

    let private collectRecordMethods (TypeName parent): CollectMany<FSharpField, FieldName * FunctionDefinition> = fun output fields ->
        let methods =
            fields
            |> List.filter (fun field -> field.FieldType.IsFunctionType)

        if output.IsVerbose() then
            methods |> Seq.length |> sprintf " - %s.Methods [%d]" parent |> output.Message

        methods
        |> List.map (function
            | nameless when nameless.IsNameGenerated -> failwithf "Record %A has a nameless field with type: %A." parent (nameless.FieldType |> Dump.formatType)
            | named ->
                match named.FieldType |> resolveTypeDefinition with
                | Function func -> FieldName named.DisplayName => func
                | unexpected -> failwithf "Unexpected function type %A" unexpected
        )

    let private collectUnionCases parent: CollectMany<FSharpUnionCase, ResolvedType> = fun output cases ->
        if output.IsVerbose() then
            cases |> Seq.length |> sprintf " - %s.UnionCases [%d]" (parent |> TypeName.value) |> output.Message

        match cases with
        | [] -> []

        | [ stream ] when stream.DisplayName.EndsWith "Stream" && stream.DisplayName.Length > "Stream".Length ->
            let argument =
                stream.UnionCaseFields
                |> Seq.toList
                |> collectUnionCaseFields (TypeName stream.DisplayName) output

            match argument with
            | GenericType { Type = TypeName "Stream"; Argument = Type eventType } ->
                [ Stream { Name = TypeName stream.DisplayName; EventType = eventType }]

            | notAStream ->
                failwithf "Type %A seems to be a stream but has not a specific generic type Stream<'Event> in it.\nIt has: %A\n"
                    stream.DisplayName
                    (notAStream |> TypeDefinition.value)

        | [ singleCaseUnion ] ->
            [
                SingleCaseUnion {
                    Name = parent
                    ConstructorName = singleCaseUnion.DisplayName
                    ConstructorArgument =
                        singleCaseUnion.UnionCaseFields
                        |> Seq.toList
                        |> collectUnionCaseFields (TypeName singleCaseUnion.DisplayName) output
                }
            ]
        | discriminatedUnionCases ->
            [
                DiscriminatedUnion {
                    Name = parent
                    Cases =
                        discriminatedUnionCases
                        |> List.map (fun c ->
                            let caseName = TypeName c.DisplayName

                            {
                                Name = caseName
                                Argument =
                                    c.UnionCaseFields
                                    |> Seq.toList
                                    |> collectUnionCaseFields caseName output
                            }
                        )
                }
            ]

    let rec private collectEntities (parent: FSharpEntity option): CollectMany<FSharpEntity, ResolvedType> = fun output entities ->
        if output.IsVerbose() then
            let parent = parent |> Option.map (fun p -> p.DisplayName) |> Option.defaultValue ""
            entities |> Seq.length |> sprintf "%s.Entities [%d]" parent |> output.Section

        entities
        |> List.collect (function
            | globalEntity when globalEntity.AccessPath = "global" ->
                globalEntity.NestedEntities
                |>> collectEntities (Some globalEntity) output

            | record when record.IsFSharpRecord ->
                let typeName = TypeName record.DisplayName

                if record.NestedEntities |> Seq.isEmpty |> not then
                    failwithf "Record nested entities: %A" record.NestedEntities

                [
                    Record {
                        Name = typeName
                        Fields =
                            record.FSharpFields
                            |> Seq.toList
                            |> collectRecordFields typeName output
                            |> Map.ofList
                        Methods =
                            record.FSharpFields
                            |> Seq.toList
                            |> collectRecordMethods typeName output
                            |> Map.ofList
                    }
                ]

            | unionCase when unionCase.IsFSharpUnion ->
                let typeName = TypeName unionCase.DisplayName

                if unionCase.NestedEntities |> Seq.isEmpty |> not then
                    failwithf "UnionCase nested entities: %A" unionCase.NestedEntities

                unionCase.UnionCases
                |>> collectUnionCases typeName output

            | e ->
                e.DisplayName
                |> sprintf " <c:yellow>/?\\ type %A has nested entities, but is not a Record or UnionCase</c>"
                |> output.Message

                if e.MembersFunctionsAndValues |> Seq.isEmpty |> not then
                    failwithf "[Resolver] Unexpected Entity members, functions and values: %A" e.MembersFunctionsAndValues

                if e.FSharpFields |> Seq.isEmpty |> not then
                    failwithf "[Resolver] Unexpected Entity FSharpFields: %A" e.FSharpFields

                if e.UnionCases |> Seq.isEmpty |> not then
                    failwithf "[Resolver] Unexpected Entity UnionCases: %A" e.UnionCases

                [
                    yield! e.NestedEntities |>> collectEntities (Some e) output
                ]
        )

    let resolve output (parsedDomains: ParsedDomain list): Result<ResolvedType list, _> =
        resolvedTypes |> Cache.clear

        // resolve all scalar types in advance
        ScalarType.all |>> List.map ScalarType |> ignore

        parsedDomains
        |> List.iter (fun (ParsedDomain parsedResult) ->
            parsedResult.AssemblySignature.Entities
            |>> collectEntities None output
            |> ignore
        )

        if output.IsVerbose() then
            resolvedTypes
            |> Cache.items
            |> List.map (fun (Key typeName, t) ->
                [
                    typeName |> TypeName.value
                    t |> ResolvedType.getType
                ]
            )
            |> List.sort
            |> function
                | [] -> output.Error "There are no resolved types in given domain."
                | resolvedTypes ->
                    resolvedTypes
                    |> output.Options (sprintf "Resolved (or scalar) types [%d]:" (resolvedTypes |> List.length))

        let resolvedTypes =
            resolvedTypes
            |> Cache.values

        match resolvedTypes |> List.filter (function Unresolved _ -> true | _ -> false) with
        | [] ->
            resolvedTypes
            |> List.filter (function ScalarType _ -> false | _ -> true)
            |> List.sortBy ResolvedType.name
            |> Ok

        | unresolvedTypes ->
            unresolvedTypes
            |> List.map ResolvedType.name
            |> Error
