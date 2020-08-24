// common types

type Stream<'Event> = Stream of 'Event list
type StreamHandler<'Event> = StreamHandler of ('Event -> unit)

// domain types

type MainService = Initiator

type Service = {
    Method: Input -> Output
}

and Input = Input
and Output = Output

type InteractionStream = InteractionStream of Stream<InteractionEvent>

and InteractionEvent =
    | Confirmed of ConfirmedEvent
    | Rejected of RejectedEvent

and ConfirmedEvent = ConfirmedEvent

and RejectedEvent =
    | Expired
    | Rejected

type StreamListener = {
    OnInteractionEvent: StreamHandler<InteractionEvent>
}
