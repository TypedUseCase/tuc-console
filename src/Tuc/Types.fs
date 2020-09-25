namespace MF.Tuc

open MF.Domain

type TucName = TucName of string

[<RequireQualifiedAccess>]
module TucName =
    let value (TucName name) = name

[<RequireQualifiedAccess>]
type EventError =
    | Empty
    | WrongFormat

type Data = Data of string

type Event = {
    Original: string
    Path: string list
}

[<RequireQualifiedAccess>]
module Event =
    open MF.TucConsole

    let ofString = function
        | String.IsEmpty ->
            Error EventError.Empty
        | wrongFormat when wrongFormat.Contains " " || wrongFormat.StartsWith "." || wrongFormat.EndsWith "." ->
            Error EventError.WrongFormat
        | event ->
            Ok {
                Original = event
                Path = event.Split "." |> List.ofSeq
            }

    let private path { Path = path } = path

    let lastInPath =
        path
        >> List.rev
        >> List.head

    let value = path >> String.concat "."

    // @see https://plantuml.com/link
    let link = function
        | { Path = [ single ] } -> single
        | { Path = _ } as event -> sprintf "[[{%s}%s]]" (event |> value) (event |> lastInPath)

type Tuc = {
    Name: TucName
    Participants: Participant list
    Parts: TucPart list
}

and Participant =
    | Component of ParticipantComponent
    | Participant of ActiveParticipant

and ParticipantComponent = {
    Name: string
    Participants: ActiveParticipant list
}

and ActiveParticipant =
    | Service of ServiceParticipant
    | DataObject of DataObjectParticipant
    | Stream of StreamParticipant

and ServiceParticipant = {
    Domain: DomainName
    Context: string
    Alias: string
    ServiceType: DomainType
}

and DataObjectParticipant = {
    Domain: DomainName
    Context: string
    Alias: string
    DataObjectType: DomainType
}

and StreamParticipant = {
    Domain: DomainName
    Context: string
    Alias: string
    StreamType: DomainType
}

and TucPart =
    | Section of Section
    | Group of Group
    | If of If
    | Loop of Loop
    | Lifeline of Lifeline
    | ServiceMethodCall of ServiceMethodCall
    | PostData of PostData
    | ReadData of ReadData
    | PostEvent of PostEvent
    | ReadEvent of ReadEvent
    | HandleEventInStream of HandleEventInStream
    | Do of Do
    | LeftNote of Note
    | Note of CallerNote
    | RightNote of Note

and Section = {
    Value: string
}

and Group = {
    Name: string
    Body: TucPart list
}

and Loop = {
    Condition: string
    Body: TucPart list
}

and If = {
    Condition: string
    Body: TucPart list
    Else: (TucPart list) option
}

and Lifeline = {
    Initiator: ActiveParticipant
    Execution: TucPart list
}

and ServiceMethodCall = {
    Caller: ActiveParticipant
    Service: ActiveParticipant
    Method: MethodDefinition
    Execution: TucPart list
}

and PostData = {
    Caller: ActiveParticipant
    DataObject: ActiveParticipant
    Data: Data
}

and ReadData = {
    Caller: ActiveParticipant
    DataObject: ActiveParticipant
    Data: Data
}

and PostEvent = {
    Caller: ActiveParticipant
    Stream: ActiveParticipant
    Event: Event
}

and ReadEvent = {
    Caller: ActiveParticipant
    Stream: ActiveParticipant
    Event: Event
}

and HandleEventInStream = {
    Stream: ActiveParticipant
    Service: ActiveParticipant
    Handler: HandlerMethodDefinition
    Execution: TucPart list
}

and Do = {
    Caller: ActiveParticipant
    Actions: string list
}

and CallerNote = {
    Caller: ActiveParticipant
    Lines: string list
}

and Note = {
    Lines: string list
}

[<RequireQualifiedAccess>]
module Tuc =
    let name ({ Name = name }: Tuc) = name

[<RequireQualifiedAccess>]
module Participant =
    let active = function
        | Component { Participants = participants } -> participants
        | Participant participant -> [ participant ]

[<RequireQualifiedAccess>]
module ActiveParticipant =
    let name = function
        | Service { ServiceType = t }
        | DataObject { DataObjectType = t }
        | Stream { StreamType = t } -> t |> DomainType.nameValue
