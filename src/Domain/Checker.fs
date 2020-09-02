namespace MF.Domain

open MF.TucConsole

[<RequireQualifiedAccess>]
module Checker =
    let rec private allTypeNamesOfDefinition = function
        | Type name -> [ name ]
        | Function { Argument = args; Returns = rets } -> (args |> allTypeNamesOfDefinition) @ (rets |> allTypeNamesOfDefinition)
        | Handler { Name = name; Handles = handles } -> name :: (handles |> allTypeNamesOfDefinition)
        | Tuple names -> names |> List.collect allTypeNamesOfDefinition
        | Option name -> name |> allTypeNamesOfDefinition
        | List name -> name |> allTypeNamesOfDefinition
        | GenericParameter _ -> []
        | GenericType { Type = name; Argument = arg } -> name :: (arg |> allTypeNamesOfDefinition)

    let private allTypeNamesOfResolvedType = function
        | ScalarType _ -> []

        | SingleCaseUnion { Name = name; ConstructorArgument = arg } ->
            name
            :: (arg |> allTypeNamesOfDefinition)

        | DiscriminatedUnion { Name = name; Cases = cases } ->
            name
            :: (cases |> List.collect (fun { Argument = arg } -> (arg |> allTypeNamesOfDefinition)))

        | Record { Name = name; Fields = fields; Methods = methods } ->
            name
            :: (fields |> Map.toList |> List.collect (snd >> allTypeNamesOfDefinition))
            @ (methods |> Map.toList |> List.collect (snd >> Function >> allTypeNamesOfDefinition))

        | Stream { Name = name; EventType = eventType } -> [ name; eventType ]

        | Unresolved (TypeName unresolved) -> failwithf "Can not check unresolved type %A." unresolved

    let check (output: MF.ConsoleApplication.Output) (resolvedTypes: ResolvedType list): Result<DomainType list, _> =
        let allTypeNames =
            resolvedTypes
            |> List.collect allTypeNamesOfResolvedType
            |> List.filterNotIn (ScalarType.all |> List.map ScalarType.name)
            |> List.distinct
            |> List.sort

        let allResolvedTypeNames =
            resolvedTypes
            |> List.map ResolvedType.name
            |> List.distinct
            |> List.sort

        match allTypeNames |> List.filterNotIn allResolvedTypeNames with
        | [] -> Ok (resolvedTypes |> List.map DomainType)
        | undefinedTypes -> Error undefinedTypes
