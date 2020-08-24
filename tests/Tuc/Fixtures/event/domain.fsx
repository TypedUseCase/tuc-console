#load "../CoreTypes.fsx"
open CoreTypes

type InteractionEvent =
    | Confirmed of ConfirmedEvent
    | Rejected of RejectedEvent
    | Interaction of Interaction
    | Other

and Interaction = Interaction

and ConfirmedEvent = ConfirmedEvent of string
and RejectedEvent =
    | UserRejected of UserRejectedEvent
    | Expired

and UserRejectedEvent =
    | Foo
    | Bar

type GenericService = Initiator

type InteractionCollector = {
    Post: InteractionEvent -> unit
}

type InteractionStream = InteractionStream of Stream<InteractionEvent>
