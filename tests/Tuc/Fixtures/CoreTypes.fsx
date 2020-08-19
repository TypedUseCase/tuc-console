type Event = Event

type Undefined = Undefined
type NotModeledAtThisStage = NotModeledAtThisStage
type Empty = Empty

type Id = UUID

type YesNo =
  | Yes
  | No

type Stream<'Event> = Stream of 'Event list
type StreamHandler<'Event> = StreamHandler of ('Event -> unit)

type CommandResult =
  | Accepted
  | Error
