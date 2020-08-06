namespace MF.Domain

open FSharp.Compiler.SourceCodeServices

[<RequireQualifiedAccess>]
module Dump =
    open TypeResolvers

    type private Dump<'Type> = MF.ConsoleApplication.Output -> 'Type -> unit
    type private DumpMany<'Type> = MF.ConsoleApplication.Output -> 'Type seq -> unit

    type private Format<'Type> = 'Type -> string

    let rec formatType: Format<FSharpType> = function
        | IsScalarType scalarType -> scalarType.TypeDefinition.DisplayName

        | abbreviationType when abbreviationType.IsAbbreviation && abbreviationType.HasTypeDefinition ->
            let generics =
                match abbreviationType.GenericArguments |> Seq.toList with
                | [] -> ""
                | [ generic ] -> generic |> formatType |> sprintf "%s "
                | generics -> generics |> List.map formatType |> String.concat "; " |> sprintf "%s "

            sprintf "%s%s" generics abbreviationType.TypeDefinition.DisplayName

        | IsFunctionType functionType ->
            functionType.GenericArguments |> Seq.map formatType |> String.concat " -> "

        | IsTuple tupleType ->
            tupleType.GenericArguments |> Seq.map formatType |> String.concat " * "

        | IsGeneric genericType ->
            "'" + genericType.GenericParameter.FullName

        | withtoutTypeDefinition when withtoutTypeDefinition.HasTypeDefinition |> not ->
            [
                if withtoutTypeDefinition.IsAbbreviation then yield withtoutTypeDefinition.IsAbbreviation |> sprintf "IsAbbreviation: %A"
                if withtoutTypeDefinition.IsAnonRecordType then yield withtoutTypeDefinition.IsAnonRecordType |> sprintf "IsAnonRecordType: %A"
                if withtoutTypeDefinition.IsFunctionType then yield withtoutTypeDefinition.IsFunctionType |> sprintf "IsFunctionType: %A"
                if withtoutTypeDefinition.IsGenericParameter then yield withtoutTypeDefinition.IsGenericParameter |> sprintf "IsGenericParameter: %A"
                if withtoutTypeDefinition.IsStructTupleType then yield withtoutTypeDefinition.IsStructTupleType |> sprintf "IsStructTupleType: %A"
                if withtoutTypeDefinition.IsTupleType then yield withtoutTypeDefinition.IsTupleType |> sprintf "IsTupleType: %A"
                if withtoutTypeDefinition.IsUnresolved then yield withtoutTypeDefinition.IsUnresolved |> sprintf "IsUnresolved: %A"
            ]
            |> String.concat "; "
            |> sprintf "<c:yellow>%A (%A)</c>" withtoutTypeDefinition

        | t ->
            let generics =
                t.GenericArguments
                |> Seq.map formatType
                |> Seq.toList
                |> function
                    | [] -> ""
                    | generics -> generics |> String.concat ", " |> sprintf "<%s>"

            [
                if t.IsAbbreviation then yield t.IsAbbreviation |> sprintf "IsAbbreviation: %A"
                if t.IsAnonRecordType then yield t.IsAnonRecordType |> sprintf "IsAnonRecordType: %A"
                if t.IsFunctionType then yield t.IsFunctionType |> sprintf "IsFunctionType: %A"
                if t.IsGenericParameter then yield t.IsGenericParameter |> sprintf "IsGenericParameter: %A"
                if t.IsStructTupleType then yield t.IsStructTupleType |> sprintf "IsStructTupleType: %A"
                if t.IsTupleType then yield t.IsTupleType |> sprintf "IsTupleType: %A"
                if t.IsUnresolved then yield t.IsUnresolved |> sprintf "IsUnresolved: %A"
            ]
            |> String.concat "; "
            |> function | "" -> "" | opts -> sprintf " <c:red>(%s)</c>" opts
            |> sprintf "%s%s%s" t.TypeDefinition.DisplayName generics

    let private dumpFields: DumpMany<FSharpField> = fun output fields ->
        if fields |> Seq.isEmpty |> not then
            fields |> Seq.length |> sprintf "Fields [%d]" |> output.Section

            fields
            |> Seq.iter (fun f ->
                [[
                    f.FullName
                    f.DisplayName
                    f.FieldType |> formatType
                ]]
                |> output.Table [
                    "FullName"
                    "DisplayName"
                    "FieldType"
                ]
            )

    let private dumpMemberOrFunctionOrValues: DumpMany<FSharpMemberOrFunctionOrValue> = fun output mfvs ->
        if mfvs |> Seq.isEmpty |> not then
            mfvs |> Seq.length |> sprintf "MemberOrFunctionOrValue [%d]" |> output.Section

            mfvs
            |> Seq.map (fun mfv -> [
                mfv.FullName
                mfv.DisplayName
            ])
            |> Seq.toList
            |> output.Table [
                "FullName"
                "DisplayName"
            ]

    let private dumpUnionCases: DumpMany<FSharpUnionCase> = fun output unionCases ->
        if unionCases |> Seq.isEmpty |> not then
            unionCases |> Seq.length |> sprintf "e.UnionCases [%d]" |> output.Section

            unionCases
            |> Seq.iter (fun uc ->
                [[
                    uc.FullName
                    uc.DisplayName
                    uc.HasFields |> sprintf "%A"
                    uc.ReturnType |> formatType
                ]]
                |> output.Table [
                    "FullName"
                    "DisplayName"
                    "HasFields"
                    "Returns"
                ]

                uc.UnionCaseFields |> dumpFields output
            )

    let rec private dumpEntities parent: DumpMany<FSharpEntity> = fun output entities ->
        if entities |> Seq.isEmpty |> not then
            entities |> Seq.length |> sprintf "%sEntities [%d]" parent |> output.Section

            entities
            |> Seq.iter (fun e ->
                let accessPath = try e.AccessPath |> sprintf "%A" with e -> e.Message |> sprintf "<c:red>%s</c>"
                let displayName = try e.DisplayName |> sprintf "%A" with e -> e.Message |> sprintf "<c:red>%s</c>"
                let fullName = try e.FullName |> sprintf "%A" with e -> e.Message |> sprintf "<c:red>%s</c>"

                [[
                    fullName
                    accessPath
                    displayName
                ]]
                |> output.Table [
                    "FullName"
                    "AccessPath"
                    "DisplayName"
                ]

                e.FSharpFields |> dumpFields output
                e.MembersFunctionsAndValues |> dumpMemberOrFunctionOrValues output
                e.NestedEntities |> dumpEntities (e.FullName + ".") output
                e.UnionCases |> dumpUnionCases output

                "=" |> String.replicate 60 |> sprintf "<c:gray>%s</c>" |> output.Message
                output.NewLine()
            )

    let parsedDomain output (ParsedDomain results) =
        results.AssemblySignature.Entities
        |> dumpEntities "" output

    //
    // Dump parsed types
    //

    type private ParsedTypeDump =
        | NameOnly
        | FullType

    type private FormatParsedType<'Type> = ParsedTypeDump * 'Type -> string

    let private formatTypeName: Format<TypeName> = fun (TypeName t) -> t

    let rec private formatParsedType dump: Format<ResolvedType> = function
        | ScalarType scalar -> scalar |> ScalarType.name |> TypeName.value |> sprintf "%s<scalar>"
        | SingleCaseUnion singleCaseUnion -> (dump, singleCaseUnion) |> formatSingleCaseUnion
        | DiscriminatedUnion discriminatedUnion -> (dump, discriminatedUnion) |> formatDiscriminatedUnion
        | Record record -> (dump, record) |> formatRecord
        | Stream stream -> (dump, stream) |> formatStream
        | Unresolved unresolved -> unresolved |> TypeName.value |> sprintf "%s<unresolved>"

    and private formatSingleCaseUnion: FormatParsedType<SingleCaseUnion> =
        function
        | NameOnly, { Name = name } -> name |> formatTypeName
        | FullType, singleCaseUnion ->
            sprintf "<c:cyan>%s</c> of <c:yellow>%s</c> (<c:gray>%s</c>)"
                (singleCaseUnion.Name |> formatTypeName)
                singleCaseUnion.ConstructorName
                (singleCaseUnion.ConstructorArgument |> TypeDefinition.value)

    and private formatDiscriminatedUnion: FormatParsedType<DiscriminatedUnion> =
        function
        | NameOnly, { Name = name } -> name |> formatTypeName
        | FullType, discriminatedUnion ->
            sprintf "<c:cyan>%s</c> of%s"
                (discriminatedUnion.Name |> formatTypeName)
                (discriminatedUnion.Cases |> List.map (fun c -> formatUnionCase (FullType, c)) |> String.concat "\n    | " |> (+) "\n    | ")

    and private formatUnionCase: FormatParsedType<UnionCase> =
        function
        | NameOnly, { Name = name } -> name |> formatTypeName
        | FullType, unionCase ->
            sprintf "<c:cyan>%s</c> of <c:yellow>(%s)</c>"
                (unionCase.Name |> formatTypeName)
                (unionCase.Argument |> TypeDefinition.value)

    and private formatRecord: FormatParsedType<Record> =
        function
        | NameOnly, { Name = name } -> name |> formatTypeName
        | FullType, record ->
            sprintf "<c:cyan>%s</c> of <c:yellow>{ %s }</c>%s"
                (record.Name |> formatTypeName)
                (record.Fields
                    |> Map.toList
                    |> List.map (fun (field, fieldType) ->
                        sprintf "%s: %s"
                            (field |> FieldName.value)
                            (fieldType |> TypeDefinition.value)
                    )
                    |> String.concat "; "
                )
                (
                    match record.Methods |> Map.toList with
                    | [] -> ""
                    | methods ->
                        methods
                        |> List.map formatMethod
                        |> String.concat "\n    - "
                        |> (+) "\n    - "
                )

    and private formatMethod: Format<FieldName * FunctionDefinition> =
        fun (FieldName name, func) ->
            sprintf "fun <c:cyan>%s</c> = <c:dark-yellow>%s</c>"
                name
                (Function func |> TypeDefinition.value)

    and private formatStream: FormatParsedType<Stream> =
        function
        | NameOnly, { Name = name } -> name |> formatTypeName
        | FullType, stream ->
            sprintf "<c:dark-cyan>[%s]</c> of <c:yellow>%s</c>"
                (stream.Name |> formatTypeName)
                (stream.EventType |> TypeName.value)

    let parsedType (output: MF.ConsoleApplication.Output) =
        formatParsedType FullType
        >> output.Message
        >> output.NewLine
