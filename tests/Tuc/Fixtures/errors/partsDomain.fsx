#load "../CoreTypes.fsx"
open CoreTypes

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
}

and WorkResult =
    | Success
    | Error

type StreamComponent = {
    StreamListener: StreamListener
}

type Person = {
    Name: string
}

type PersonDatabase = PersonDatabase of Database<Person>
