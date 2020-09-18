namespace MF.Domain

open FSharp.Compiler.SourceCodeServices
open MF.TucConsole

[<RequireQualifiedAccess>]
module Resolver =
    open MF.TucConsole.Option.Operators
    open TypeResolvers

    type private ResolvedTypes = Map<DomainName option * TypeName, ResolvedType>

    type private Collect<'In, 'Out> = MF.ConsoleApplication.Output -> 'In list -> 'Out
    type private CollectMany<'In, 'Out> = MF.ConsoleApplication.Output -> 'In list -> 'Out list
    type private CollectResolvedTypes = MF.ConsoleApplication.Output -> FSharpEntity option -> ResolvedTypes -> FSharpEntity list -> ResolvedTypes

    /// Cast IList or other Enumerable to list and pass to a function
    let inline private (|>>) input f =
        input
        |> Seq.toList
        |> f

    let rec private resolveTypeDefinition: FSharpType -> TypeDefinition = function
        | IsScalarType scalar -> Type (TypeName scalar.TypeDefinition.DisplayName)
        | IsListType listItem -> List (listItem |> resolveTypeDefinition)
        | IsOptionType optionValue -> Option (optionValue |> resolveTypeDefinition)

        | abbreviationType when abbreviationType.IsAbbreviation && abbreviationType.HasTypeDefinition ->
            abbreviationType
            |> Dump.formatType
            |> failwithf "[ResolveError] IsAbbreviation: %s"

        | IsFunctionType functionType ->
            match functionType.GenericArguments |> Seq.toList |> List.map resolveTypeDefinition with
            | [] -> failwithf "[ResolveError] Invalid function definition, without any arguments and return value."
            | [ argument; returns ] -> Function { Argument = argument; Returns = returns }
            | _ -> failwithf "[ResolveError] Invalid function definition, with more then one argument and return value."

        | IsTuple tupleType ->
            Tuple (tupleType.GenericArguments |> Seq.toList |> List.map resolveTypeDefinition)

        | IsGeneric genericType ->
            GenericParameter (TypeName genericType.GenericParameter.DisplayName)

        | withtoutTypeDefinition when withtoutTypeDefinition.HasTypeDefinition |> not ->
            withtoutTypeDefinition
            |> Dump.formatType
            |> failwithf "[ResolveError] WithoutTypeDefinition: %s"

        | IsTypeWithoutArgs t ->
            Type (TypeName t.TypeDefinition.DisplayName)

        | IsGenericHandler dataType as handlerType ->
            Handler {
                Name = TypeName handlerType.TypeDefinition.DisplayName
                Handles = dataType |> resolveTypeDefinition
            }

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
            |> failwithf "[ResolveError] Type: %s"

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
        |> List.choose (function
            | nameless when nameless.IsNameGenerated -> failwithf "[ResolveError][Record] %A has a nameless field with type: %A." parent (nameless.FieldType |> Dump.formatType)
            | named ->
                match named.FieldType |> resolveTypeDefinition with
                | Handler _ -> None
                | field -> Some (FieldName named.DisplayName => field)
        )

    let private collectRecordMethods (TypeName parent): CollectMany<FSharpField, FieldName * FunctionDefinition> = fun output fields ->
        let methods =
            fields
            |> List.filter (fun field -> field.FieldType.IsFunctionType)

        if output.IsVerbose() then
            methods |> Seq.length |> sprintf " - %s.Methods [%d]" parent |> output.Message

        methods
        |> List.map (function
            | nameless when nameless.IsNameGenerated -> failwithf "[ResolveError][Record] %A has a nameless field with type: %A." parent (nameless.FieldType |> Dump.formatType)
            | named ->
                match named.FieldType |> resolveTypeDefinition with
                | Function func -> FieldName named.DisplayName => func
                | unexpected -> failwithf "[ResolveError][Record] Unexpected function type %A" unexpected
        )

    let private collectRecordHandlers (TypeName parent): CollectMany<FSharpField, FieldName * HandlerDefinition> = fun output fields ->
        let handlers =
            fields
            |> List.filter (fun field -> not field.FieldType.IsFunctionType)

        if output.IsVerbose() then
            handlers |> Seq.length |> sprintf " - %s.Handlers [%d]" parent |> output.Message

        handlers
        |> List.choose (function
            | nameless when nameless.IsNameGenerated -> failwithf "[ResolveError][Record] %A has a nameless field with type: %A." parent (nameless.FieldType |> Dump.formatType)
            | named ->
                match named.FieldType |> resolveTypeDefinition with
                | Handler handler -> Some (FieldName named.DisplayName => handler)
                | _ -> None
        )

    let private collectUnionCases parentDomain parent: CollectMany<FSharpUnionCase, ResolvedType> = fun output cases ->
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
                [
                    Stream {
                        Domain = parentDomain
                        Name = TypeName stream.DisplayName
                        EventType = eventType
                    }
                ]

            | notAStream ->
                failwithf "[ResolveError][UnionCase] Type %A seems to be a stream but has not a specific generic type Stream<'Event> in it.\nIt has: %A\n"
                    stream.DisplayName
                    (notAStream |> TypeDefinition.value)

        | [ singleCaseUnion ] ->
            [
                SingleCaseUnion {
                    Domain = parentDomain
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
                    Domain = parentDomain
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

    let private addResolvedTypes (resolvedTypes: ResolvedTypes) newResolvedTypes: ResolvedTypes =
        newResolvedTypes
        |> List.fold (fun resolvedTypes parsedType ->
            resolvedTypes
            |> Map.add
                ((parsedType |> ResolvedType.domain, parsedType |> ResolvedType.name))
                parsedType
        ) resolvedTypes

    let rec private collectEntities: CollectResolvedTypes = fun output parent acc entities ->
        if output.IsVerbose() then
            let parent = parent |> Option.map (fun p -> p.DisplayName) |> Option.defaultValue ""
            entities |> Seq.length |> sprintf "%s.Entities [%d]" parent |> output.Section

        let collectEntities = collectEntities output

        match entities with
        | [] -> acc

        | globalEntity :: entities when globalEntity.AccessPath = "global" ->
            globalEntity.NestedEntities
            |>> (@) entities
            |> collectEntities (Some globalEntity) acc

        | record :: entities when record.IsFSharpRecord ->
            let domainName = record.AccessPath |> DomainName.parse
            let typeName = TypeName record.DisplayName

            if record.NestedEntities |> Seq.isEmpty |> not then
                failwithf "[ResolveError][Entity] Record nested entities: %A" record.NestedEntities

            let fields = record.FSharpFields |> Seq.toList

            let acc =
                [
                    Record {
                        Domain = domainName
                        Name = typeName
                        Fields =
                            fields
                            |> collectRecordFields typeName output
                            |> Map.ofList
                        Methods =
                            fields
                            |> collectRecordMethods typeName output
                            |> Map.ofList
                        Handlers =
                            fields
                            |> collectRecordHandlers typeName output
                            |> Map.ofList
                    }
                ]
                |> addResolvedTypes acc

            entities |> collectEntities parent acc

        | unionCase :: entities when unionCase.IsFSharpUnion ->
            let domainName = unionCase.AccessPath |> DomainName.parse
            let typeName = TypeName unionCase.DisplayName

            if unionCase.NestedEntities |> Seq.isEmpty |> not then
                failwithf "[ResolveError][Entity] UnionCase nested entities: %A" unionCase.NestedEntities

            let acc =
                unionCase.UnionCases
                |>> collectUnionCases domainName typeName output
                |> addResolvedTypes acc

            entities |> collectEntities parent acc

        | e :: entities ->
            if output.IsVerbose() then
                e.DisplayName
                |> sprintf " <c:yellow>/?\\ type %A has nested entities, but is not a Record or UnionCase</c>"
                |> output.Message

                if e.MembersFunctionsAndValues |> Seq.isEmpty |> not then
                    output.Message <| sprintf "[ResolveWarning][Entity] Unexpected Entity members, functions and values: %A" e.MembersFunctionsAndValues

                if e.FSharpFields |> Seq.isEmpty |> not then
                    output.Message <| sprintf "[ResolveWarning][Entity] Unexpected Entity FSharpFields: %A" e.FSharpFields

                if e.UnionCases |> Seq.isEmpty |> not then
                    output.Message <| sprintf "[ResolveWarning][Entity] Unexpected Entity UnionCases: %A" e.UnionCases

            e.NestedEntities
            |>> (@) entities
            |> collectEntities (Some e) acc

    let private debugResolvedTypes (output: MF.ConsoleApplication.Output) resolvedTypes =
        if output.IsVerbose() then
            resolvedTypes
            |> List.map (fun ((domain, typeName), t) ->
                [
                    typeName |> TypeName.value
                    domain |> Option.map DomainName.value |> Option.defaultValue "-"
                    t |> ResolvedType.getType
                ]
            )
            |> List.sort
            |> function
                | [] -> output.Error "There are no resolved types in given domain."
                | resolvedTypes ->
                    [ "Type Name"; "Domain Name"; "Resolved Type" ]
                    :: resolvedTypes
                    |> output.Options (sprintf "Resolved (or scalar) types [%d]:" (resolvedTypes |> List.length))

    let resolve output (parsedDomains: ParsedDomain list): Result<ResolvedType list, _> =
        let emptyResolvedTypes: ResolvedTypes = Map.empty

        // resolve all scalar types in advance
        let resolvedTypesWithScalars =
            ScalarType.all
            |> List.map ScalarType
            |> addResolvedTypes emptyResolvedTypes

        // resolve all entites of the parsed domains
        let allResolvedTypes =
            parsedDomains
            |> List.collect (fun (ParsedDomain parsedResult) ->
                parsedResult.AssemblySignature.Entities
                |>> id
            )
            |> collectEntities output None resolvedTypesWithScalars

        let resolvedTypes =
            allResolvedTypes
            |> Map.toList
            |> tee (debugResolvedTypes output)
            |> List.map snd

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
