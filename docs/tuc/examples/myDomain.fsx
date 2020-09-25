// common types

type Id = Id

type Stream<'Event> = Stream of 'Event list
type StreamHandler<'Event> = StreamHandler of ('Event -> unit)

type Database<'Entity> = Database of 'Entity list
type Cache<'CachedData> = Cache of 'CachedData list

// domain types

type MainService = Initiator

type Service = {
    Method: Input -> Output
    MethodWithMoreArgs: Id -> int * string -> Input -> Output
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

type InteractionDatabase = InteractionDatabase of Database<InteractionEntity>

and InteractionEntity = {
    Id: Id
    InteractionData: string
}

type InteractionCache = InteractionCache of Cache<InteractionEvent>
