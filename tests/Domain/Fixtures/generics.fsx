type Stream<'Event> = Stream of 'Event list

type StreamHandler<'Event> = StreamHandler of ('Event -> unit)

type InteractionEvent =
    | Confirmation
    | Rejection

type InteractionCollectorStream = InteractionCollectorStream of Stream<InteractionEvent>

type PersonIdentificationEngine = {
    OnInteractionEvent: StreamHandler<InteractionEvent>
}

type Method = Method of ((Input list) option -> Async<Result<Output option, string>> list)

and Input = Input of string
and Output = Output of string
