namespace MF.Domain

type TypeName = TypeName of string

type ParsedType =
    | SingleCaseUnion of SingleCaseUnion
    | UnionCase of UnionCase
    | Record of RecordType
    | DiscriminatedUnion of DiscriminatedUnionType
    | Service of ServiceType
    | Stream of StreamType

and SingleCaseUnion = {
    Name: TypeName
    ConstructorName: string
    ConstructorArguments: string list
}

and UnionCase = {
    Name: TypeName
    Arguments: ParsedType list
}

and RecordType = {
    Name: TypeName
    Members: Map<string, ParsedType>
}

and FunctionType = {
    Name: TypeName
    Arguments: ParsedType list
    Returns: ParsedType option
}

and ServiceType = {
    Name: TypeName
    Methods: FunctionType list
}

and StreamType = {
    Name: TypeName
    EventType: ParsedType
}

and DiscriminatedUnionType = {
    Name: TypeName
    Cases: UnionCase list
}

module internal Example =
(* type Email = Email of string *)
    let email = SingleCaseUnion {
        Name = TypeName "Email"
        ConstructorName = "Email"
        ConstructorArguments = [ "string" ]
    }
(* type Phone = Phone of string *)
    let phone = SingleCaseUnion {
        Name = TypeName "Phone"
        ConstructorName = "Phone"
        ConstructorArguments = [ "string" ]
    }

(*
type InteractionEvent =
    | Confirmation
    | Rejection
 *)
    let interactionEvent = DiscriminatedUnion {
        Name = TypeName "InteractionEvent"
        Cases = [
            { Name = TypeName "Confirmation"; Arguments = [] }
            { Name = TypeName "Rejection"; Arguments = [] }
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
        Members = Map.ofList [
            "Email", email
            "Phone", phone
        ]
    }
    let indentityMatchingSet = Record {
        Name = TypeName "IdentityMatchingSet"
        Members = Map.ofList [
            "Contact", contact
        ]
    }

(* type PersonId = PersonId of System.Guis *)
    let personId = SingleCaseUnion {
        Name = TypeName "PersonId"
        ConstructorName = "PersonId"
        ConstructorArguments = [ "System.Guid" ]
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
            { Name = TypeName "Complete"; Arguments = [ personId; indentityMatchingSet(* ; personAttributes *) ] }
            { Name = TypeName "Incomplete"; Arguments = [ personId ] }
            { Name = TypeName "Inconsistent"; Arguments = [ personId ] }
            { Name = TypeName "Nonexisting"; Arguments = [  ] }
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
            { Name = TypeName "Accepted"; Arguments = [] }
            { Name = TypeName "Error"; Arguments = [] }
        ]
    }

(*
type GenericService = Initiator
 *)
    let genericService = Service {
        Name = TypeName "GenericService"
        Methods = []
    }

(*
type InteractionCollector = {
    PostInteraction: InteractionEvent -> CommandResult
}
 *)
    let postInteractionMethod = {
        Name = TypeName "PostInteraction"
        Arguments = [ interactionEvent ]
        Returns = Some commandResult
    }

    let interactionCollector = Service {
        Name = TypeName "InteractionCollector"
        Methods = [ postInteractionMethod ]
    }

(*
type PersonIdentificationEngine = {
    OnInteractionEvent: InteractionEvent -> unit
}
 *)
    let onInteractionEventMethod = {
        Name = TypeName "OnInteractionEvent"
        Arguments = [ interactionEvent ]
        Returns = None
    }

    let personIdentificationEngine = Service {
        Name = TypeName "PersonIdentificationEngine"
        Methods = [ onInteractionEventMethod ]
    }

(*
type PersonAggregate = {
    IdentifyPerson: IdentityMatchingSet -> Person
}
 *)
    let identifyPersonMethod = {
        Name = TypeName "IdentifyPerson"
        Arguments = [ indentityMatchingSet ]
        Returns = Some person
    }

    let personAggregate = Service {
        Name = TypeName "PersonAggregate"
        Methods = [ identifyPersonMethod ]
    }

(*
type InteractionCollectorStream = InteractionEvent list
 *)
    let interactionCollectorStream = Stream {
        Name = TypeName "InteractionCollectorStream"
        EventType = interactionEvent
    }
