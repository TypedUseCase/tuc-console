namespace MF.Domain

type FieldName = FieldName of string

[<RequireQualifiedAccess>]
module FieldName =
    let value (FieldName name) = name

type TypeName = TypeName of string

[<RequireQualifiedAccess>]
module TypeName =
    let value (TypeName name) = name

type TypeDefinition =
    | Type of TypeName
    | Function of FunctionDefinition
    | Tuple of TypeDefinition list
    | Option of TypeDefinition
    | List of TypeDefinition
    | GenericParameter of TypeName
    | GenericType of GenericDefinition

and FunctionDefinition = {
    Arguments: TypeDefinition list
    Returns: TypeDefinition
}

and GenericDefinition = {
    Type: TypeName
    Argument: TypeDefinition
}

type MethodDefinition = {
    Name: FieldName
    Function: FunctionDefinition
}

[<RequireQualifiedAccess>]
module TypeDefinition =
    let rec value = function
        | Type name -> name |> TypeName.value
        | Function { Arguments = args; Returns = returns } ->
            sprintf "%s -> %s"
                (args |> List.map value |> String.concat " -> ")
                (returns |> value)
        | Tuple values -> values |> List.map value |> String.concat " * "
        | Option ofType -> ofType |> value |> sprintf "%s option"
        | List ofType -> ofType |> value |> sprintf "%s list"
        | GenericParameter parameter -> parameter |> TypeName.value |> sprintf "'%s"
        | GenericType { Type = gType; Argument = argument } ->
            sprintf "%s<%s>"
                (gType |> TypeName.value)
                (argument |> value)

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
    Name: TypeName
    ConstructorName: string
    ConstructorArgument: TypeDefinition
}

and DiscriminatedUnion = {
    Name: TypeName
    Cases: UnionCase list
}

and UnionCase = {
    Name: TypeName
    Argument: TypeDefinition
}

and Record = {
    Name: TypeName
    Fields: Map<FieldName, TypeDefinition>
    Methods: Map<FieldName, FunctionDefinition>
}

and Stream = {
    Name: TypeName
    EventType: TypeName
}

[<RequireQualifiedAccess>]
module ScalarType =
    let name = function
        | String -> TypeName "string"
        | Int -> TypeName "int"
        | Float -> TypeName "float"
        | Bool -> TypeName "bool"
        | Unit -> TypeName "unit"

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

type ParseDomainError =
    | UnresolvedTypes of TypeName list
    | UndefinedTypes of TypeName list

module internal Example =
(* type Email = Email of string *)
    let email = SingleCaseUnion {
        Name = TypeName "Email"
        ConstructorName = "Email"
        ConstructorArgument = Type (TypeName "string")
    }
(* type Phone = Phone of string *)
    let phone = SingleCaseUnion {
        Name = TypeName "Phone"
        ConstructorName = "Phone"
        ConstructorArgument = Type (TypeName "string")
    }

(*
type InteractionEvent =
    | Confirmation
    | Rejection
 *)
    let interactionEvent = DiscriminatedUnion {
        Name = TypeName "InteractionEvent"
        Cases = [
            { Name = TypeName "Confirmation"; Argument = Type (TypeName "unit") }
            { Name = TypeName "Rejection"; Argument = Type (TypeName "unit") }
        ]
    }

(*
type IdentityMatchingSet = {
  Contact: Contact
}
and Contact = {
    Email: Email option
    Phone: Phone option
}
 *)
    let contact = Record {
        Name = TypeName "Contact"
        Fields = Map.ofList [
            FieldName "Email", Type (email |> ResolvedType.name)
            FieldName "Phone", Type (phone |> ResolvedType.name)
        ]
        Methods = Map.empty
    }
    let indentityMatchingSet = Record {
        Name = TypeName "IdentityMatchingSet"
        Fields = Map.ofList [
            FieldName "Contact", Type (contact |> ResolvedType.name)
        ]
        Methods = Map.empty
    }

(* type PersonId = PersonId of Id *)
    let personId = SingleCaseUnion {
        Name = TypeName "PersonId"
        ConstructorName = "PersonId"
        ConstructorArgument = Type (TypeName "Id")
    }
    (*
type Person =
  | Complete of PersonId * IdentityMatchingSet * PersonAttributes
  | Incomplete of PersonId
  | Inconsistent of PersonId
  | Nonexisting
     *)
    let person = DiscriminatedUnion {
        Name = TypeName "Person"
        Cases = [
            { Name = TypeName "Complete"; Argument = Tuple [ Type (personId |> ResolvedType.name); Type (indentityMatchingSet |> ResolvedType.name) (* ; personAttributes *) ] }
            { Name = TypeName "Incomplete"; Argument = Type (personId |> ResolvedType.name) }
            { Name = TypeName "Inconsistent"; Argument = Type (personId |> ResolvedType.name) }
            { Name = TypeName "Nonexisting"; Argument = Type (TypeName "unit") }
        ]
    }

(*
type CommandResult =
  | Accepted
  | Error
 *)
    let commandResult = DiscriminatedUnion {
        Name = TypeName "CommandResult"
        Cases = [
            { Name = TypeName "Accepted"; Argument = Type (TypeName "unit") }
            { Name = TypeName "Error"; Argument = Type (TypeName "unit") }
        ]
    }

(*
type GenericService = Initiator
 *)
    let genericService = Record {
        Name = TypeName "GenericService"
        Fields = Map.empty
        Methods = Map.empty
    }

(*
type InteractionCollector = {
    PostInteraction: InteractionEvent -> CommandResult
}
 *)
    let postInteraction = {
        Arguments = [ Type (interactionEvent |> ResolvedType.name) ]
        Returns = Type (commandResult |> ResolvedType.name)
    }

    let interactionCollector = Record {
        Name = TypeName "InteractionCollector"
        Fields = Map.empty
        Methods = Map.ofList [
            FieldName "PostInteraction", postInteraction
        ]
    }

    let postInteractionMethod = {
        Name = FieldName "PostInteraction"
        Function = postInteraction
    }

(*
type PersonIdentificationEngine = {
    OnInteractionEvent: InteractionEvent -> unit
}
 *)
    let onInteractionEvent = {
        Arguments = [ Type (interactionEvent |> ResolvedType.name) ]
        Returns = Type (TypeName "unit")
    }

    let personIdentificationEngine = Record {
        Name = TypeName "PersonIdentificationEngine"
        Fields = Map.empty
        Methods = Map.ofList [
            FieldName "OnInteractionEvent", onInteractionEvent
        ]
    }

    let onInteractionEventMethod = {
        Name = FieldName "OnInteractionEvent"
        Function = onInteractionEvent
    }

(*
type PersonAggregate = {
    IdentifyPerson: IdentityMatchingSet -> Person
}
 *)
    let identifyPerson = {
        Arguments = [ Type (indentityMatchingSet |> ResolvedType.name) ]
        Returns = Type (person |> ResolvedType.name)
    }

    let personAggregate = Record {
        Name = TypeName "PersonAggregate"
        Fields = Map.empty
        Methods = Map.ofList [
            FieldName "IdentifyPerson", identifyPerson
        ]
    }

    let identifyPersonMethod = {
        Name = FieldName "IdentifyPerson"
        Function = identifyPerson
    }

(*
type InteractionCollectorStream = InteractionEvent list
 *)
    let interactionCollectorStream = Stream {
        Name = TypeName "InteractionCollectorStream"
        EventType = interactionEvent |> ResolvedType.name
    }
