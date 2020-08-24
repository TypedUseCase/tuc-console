namespace MF.Domain

module private TypeResolvers =
    open FSharp.Compiler.SourceCodeServices

    type TypeResolver = FSharpType -> FSharpType option

    let (|IsScalarType|_|): TypeResolver = function
        | abbreviationType when abbreviationType.IsAbbreviation && abbreviationType.HasTypeDefinition && abbreviationType.TypeDefinition.DisplayName |> ScalarType.isScalar ->
            Some abbreviationType
        | _ -> None

    let (|IsListType|_|): TypeResolver = function
        | listType when listType.IsAbbreviation && listType.HasTypeDefinition && listType.TypeDefinition.DisplayName = "list" ->
            match listType.GenericArguments |> Seq.toList with
            | [ listItem ] -> Some listItem
            | _ -> None
        | _ -> None

    let (|IsOptionType|_|): TypeResolver = function
        | option when option.IsAbbreviation && option.HasTypeDefinition && option.TypeDefinition.DisplayName = "option" ->
            match option.GenericArguments |> Seq.toList with
            | [ optionValue ] -> Some optionValue
            | _ -> None
        | _ -> None

    let (|IsFunctionType|_|): TypeResolver = function
        | functionType when functionType.IsFunctionType -> Some functionType
        | _ -> None

    let (|IsTuple|_|): TypeResolver = function
        | tupleType when tupleType.IsTupleType -> Some tupleType
        | _ -> None

    let (|IsGeneric|_|): TypeResolver = function
        | genericType when genericType.IsGenericParameter -> Some genericType
        | _ -> None

    let (|IsTypeWithoutArgs|_|): TypeResolver = function
        | t when t.HasTypeDefinition && t.GenericArguments |> Seq.isEmpty -> Some t
        | _ -> None

    let (|IsTypeWithGenericArgs|_|): TypeResolver = function
        | t when t.HasTypeDefinition && t.GenericArguments |> Seq.isEmpty |> not -> Some t
        | _ -> None

    let (|IsGenericHandler|_|): TypeResolver = function
        | IsTypeWithGenericArgs t when t.TypeDefinition.DisplayName.EndsWith "Handler" ->
            match t.GenericArguments |> Seq.toList with
            | [] -> failwithf "[GenericHandler] There is no generic argument in the Handler."
            | [ data ] -> Some data
            | _ -> failwithf "[GenericHandler] There is more then one generic argument in the Handler."
        | _ -> None
