namespace MF.Domain

type FieldName = FieldName of string

[<RequireQualifiedAccess>]
module FieldName =
    let value (FieldName name) = name

type DomainName = DomainName of string

[<RequireQualifiedAccess>]
module DomainName =
    open MF.TucConsole

    let value (DomainName name) = name

    let parse = function
        | Regex "^(.+?)Domain$" [ domainName ] -> Some (DomainName domainName)
        | _ -> None

type TypeName = TypeName of string

[<RequireQualifiedAccess>]
module TypeName =
    let value (TypeName name) = name

type TypeDefinition =
    | Type of TypeName
    | Function of FunctionDefinition
    | Handler of HandlerDefinition
    | Tuple of TypeDefinition list
    | Option of TypeDefinition
    | List of TypeDefinition
    | GenericParameter of TypeName
    | GenericType of GenericDefinition

and FunctionDefinition = {
    Argument: TypeDefinition
    Returns: TypeDefinition
}

and HandlerDefinition = {
    Name: TypeName
    Handles: TypeDefinition
}

and GenericDefinition = {
    Type: TypeName
    Argument: TypeDefinition
}

type MethodDefinition = {
    Name: FieldName
    Function: FunctionDefinition
}

type HandlerMethodDefinition = {
    Name: FieldName
    Handler: HandlerDefinition
}

type Fields<'FieldType> = Map<FieldName, 'FieldType>

[<RequireQualifiedAccess>]
module Fields =
    let empty: Fields<_> = Map.empty
    let ofList fields: Fields<_> = Map.ofList fields

type ResolvedType =
    | ScalarType of ScalarType
    | SingleCaseUnion of SingleCaseUnion
    | DiscriminatedUnion of DiscriminatedUnion
    | Record of Record
    | Stream of Stream
    | Unresolved of TypeName

and ScalarType =
    | String
    | Int
    | Float
    | Bool
    | Unit

and SingleCaseUnion = {
    Domain: DomainName option
    Name: TypeName
    ConstructorName: string
    ConstructorArgument: TypeDefinition
}

and DiscriminatedUnion = {
    Domain: DomainName option
    Name: TypeName
    Cases: UnionCase list
}

and UnionCase = {
    Name: TypeName
    Argument: TypeDefinition
}

and Record = {
    Domain: DomainName option
    Name: TypeName
    Fields: Fields<TypeDefinition>
    Methods: Fields<FunctionDefinition>
    Handlers: Fields<HandlerDefinition>
}

and Stream = {
    Domain: DomainName option
    Name: TypeName
    EventType: TypeName
}

[<RequireQualifiedAccess>]
module UnionCase =
    let name ({ Name = name }: UnionCase) = name

[<RequireQualifiedAccess>]
module SingleCaseUnion =
    let name ({ Name = name }: SingleCaseUnion) = name

[<RequireQualifiedAccess>]
module ScalarType =
    let name = function
        | String -> TypeName "string"
        | Int -> TypeName "int"
        | Float -> TypeName "float"
        | Bool -> TypeName "bool"
        | Unit -> TypeName "unit"

    let parse = function
        | "string" -> Some String
        | "int" -> Some Int
        | "float" -> Some Float
        | "bool" -> Some Bool
        | "unit" -> Some Unit
        | _ -> None

    let isScalar = function
        | "string"
        | "int"
        | "float"
        | "bool"
        | "unit" -> true
        | _ -> false

    let all =
        [
            String
            Int
            Float
            Bool
            Unit
        ]

[<RequireQualifiedAccess>]
module ResolvedType =
    let name = function
        | ScalarType scalar -> scalar |> ScalarType.name
        | SingleCaseUnion { Name = name }
        | Record { Name = name }
        | DiscriminatedUnion { Name = name }
        | Stream { Name = name }
        | Unresolved name -> name

    let getType = function
        | ScalarType _ -> "ScalarType"
        | SingleCaseUnion _ -> "SingleCaseUnion"
        | Record _ -> "Record"
        | DiscriminatedUnion _ -> "DiscriminatedUnion"
        | Stream _ -> "Stream"
        | Unresolved _ -> "Unresolved"

[<RequireQualifiedAccess>]
module TypeDefinition =
    let rec value = function
        | Type name -> name |> TypeName.value
        | Function { Argument = args; Returns = returns } -> sprintf "%s -> %s" (args |> value) (returns |> value)
        | Handler { Name = name; Handles = handles } -> sprintf "%s<%s>" (name |> TypeName.value) (handles |> value)
        | Tuple values -> values |> List.map value |> String.concat " * "
        | Option ofType -> ofType |> value |> sprintf "%s option"
        | List ofType -> ofType |> value |> sprintf "%s list"
        | GenericParameter parameter -> parameter |> TypeName.value |> sprintf "'%s"
        | GenericType { Type = gType; Argument = argument } -> sprintf "%s<%s>" (gType |> TypeName.value) (argument |> value)

    let (|IsScalar|_|) = function
        | Type (TypeName name) -> name |> ScalarType.parse
        | _ -> None

[<RequireQualifiedAccess>]
module FunctionDefinition =
    let fold: FunctionDefinition -> _ = fun { Argument = a; Returns = r } ->
        let rec folder acc = function
            | Function { Argument = a; Returns = Function _ as f } -> f |> folder (a :: acc)
            | Function { Argument = a; Returns = r } -> r :: a :: acc
            | t -> t :: acc

        match folder [ a ] r with
        | [] -> failwithf "[Logic] Unexpected case"
        | returns :: args -> (args |> List.rev), returns

type DomainType = DomainType of ResolvedType

[<RequireQualifiedAccess>]
module DomainType =
    let name (DomainType resolvedType) = resolvedType |> ResolvedType.name
    let nameValue = name >> TypeName.value

    let (|Initiator|_|) = function
        | DomainType (SingleCaseUnion { ConstructorName = "Initiator" }) -> Some ()
        | _ -> None

    let (|Stream|_|) = function
        | DomainType (Stream { EventType = TypeName eventType } ) -> Some eventType
        | _ -> None

//
// Errors
//

type ParseDomainError =
    | UnresolvedTypes of TypeName list
    | UndefinedTypes of TypeName list
