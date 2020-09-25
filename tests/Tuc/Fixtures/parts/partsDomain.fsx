#load "../CoreTypes.fsx"
open CoreTypes

type Id = Id of Id

type GenericService = Initiator

type InputStream = InputStream of Stream<InputEvent>

and InputEvent =
    | EventA of AEvent
    | EventB of BEvent
    | EventC of CEvent

and AEvent = AEvent of Event
and BEvent = BEvent of Event
and CEvent =
    | C1 of Event
    | C2 of Event

and Event = {
    Id: Id
}

type StreamListener = {
    ReadEvent: StreamHandler<InputEvent>
}

type Service = {
    DoSomeWork: Id -> WorkResult
    MethodWithMoreArgs: Id -> string -> InputEvent -> WorkResult
}

and WorkResult =
    | Success
    | Error

type IdCache = IdCache of Cache<Id>
type KeyCache = KeyCache of Cache<Key>

and Key =
    | Id of Id
    | Random
    | Scalar of Scalar

and Scalar =
    | String of string
    | Int of string
