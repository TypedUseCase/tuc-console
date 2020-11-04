namespace Tuc

open Tuc.Domain

//
// Common types for a parsed information
//

/// Position in a text document expressed as zero-based line and zero-based character offset.
/// A position is between two characters like an ‘insert’ cursor in a editor.
type Position = {
    /// Line position in a document (zero-based).
    Line: int

    /// Character offset on a line in a document (zero-based). Assuming that the line is
    /// represented as a string, the `character` value represents the gap between the
    /// `character` and `character + 1`.
    ///
    /// If the character value is greater than the line length it defaults back to the
    /// line length.
    Character: int
}

[<RequireQualifiedAccess>]
module Position =
    let line { Line = line } = line
    let character { Character = char } = char

/// A range in a text document expressed as (zero-based) start and end positions.
/// A range is comparable to a selection in an editor. Therefore the end position is exclusive.
///
/// If you want to specify a range that contains a line including the line ending character(s)
/// then use an end position denoting the start of the next line. For example:
///
/// ```fsharp
/// {
///     Start = { Line = 5; Character = 23 }
///     End = { Line = 6; Character = 0 }
/// }
/// ```
type Range = {
    /// The range's start position.
    Start: Position

    /// The range's end position.
    End: Position
}

[<RequireQualifiedAccess>]
module Range =
    let singleLine line (fromChar, toChar) =
        {
            Start = { Line = line; Character = fromChar }
            End = { Line = line; Character = toChar }
        }

    let startPosition { Start = startPosition } = startPosition
    let endPosition { End = endPosition } = endPosition
    let lineString range =
        [
            range |> startPosition
            range |> endPosition
        ]
        |> List.map (Position.line >> sprintf "#%03i")
        |> List.distinct
        |> String.concat "-"

    /// Indent a range by an indent size (for both start and end positions)
    let indent indent range =
        {
            Start = { Line = range.Start.Line; Character = range.Start.Character + indent }
            End = { Line = range.End.Line; Character = range.End.Character + indent }
        }

    /// Creates a new Range from the end of a given range, moved by an offset with a given length.
    let fromEnd offset length range =
        let endPosition = range |> endPosition
        let line = endPosition |> Position.line
        let newStart = (endPosition |> Position.character) + offset

        singleLine line (newStart, newStart + length)

type DocumentUri = string

/// Represents a location inside a resource, such as a line inside a text file.
type Location = {
    Uri: DocumentUri
    Range: Range
}

type ParsedLocation = {
    Value: string
    Location: Location
}

[<RequireQualifiedAccess>]
module ParsedLocation =
    let startChar { Location = { Range = range } } =
        range |> Range.startPosition |> Position.character

    let endChar { Location = { Range = range } } =
        range |> Range.endPosition |> Position.character

[<RequireQualifiedAccess>]
module Location =
    let create uri range =
        {
            Uri = uri
            Range = range
        }

    let uri { Uri = uri } = uri

//
// Tuc parsed types
//

[<RequireQualifiedAccess>]
type Parsed<'Type> =
    | KeyWord of ParsedKeyWord<'Type>
    | KeyWordWithoutValue of ParsedKeyWordWithoutValue
    | KeyWordWithBody of ParsedKeyWordWithBody<'Type>
    | KeyWordIf of ParsedKeyWordIf<'Type>
    | ParticipantDefinition of ParsedParticipantDefinition<'Type>
    | ComponentDefinition of ParsedComponentDefinition<'Type>
    | Lifeline of ParsedLifeline<'Type>
    | MethodCall of ParsedMethodCall<'Type>
    | IncompleteHandleEvent of ParsedIncompleteHandleEvent<'Type>
    | HandleEvent of ParsedHandleEvent<'Type>
    | PostData of ParsedPostData<'Type>
    | ReadData of ParsedReadData<'Type>
    | Ignored of 'Type

and ParsedKeyWord<'Type> = {
    Value: 'Type
    KeyWord: ParsedLocation
    ValueLocation: ParsedLocation
}

and ParsedKeyWordWithoutValue = {
    KeyWord: ParsedLocation
}

and ParsedKeyWordWithBody<'Type> = {
    Value: 'Type
    KeyWord: ParsedLocation
    ValueLocation: ParsedLocation
    Body: Parsed<TucPart> list
}

and ParsedKeyWordIf<'Type> = {
    Value: 'Type
    IfLocation: ParsedLocation
    ConditionLocation: ParsedLocation
    ElseLocation: ParsedLocation option
    Body: Parsed<TucPart> list
    ElseBody: (Parsed<TucPart> list) option
}

and ParsedParticipantDefinition<'Type> = {
    Value: 'Type
    Context: ParsedLocation
    Domain: ParsedLocation option
    Alias: ParsedLocation option
}

and ParsedComponentDefinition<'Type> = {
    Value: 'Type
    Context: ParsedLocation
    Domain: ParsedLocation
    Participants: Parsed<ActiveParticipant> list
}

and ParsedLifeline<'Type> = {
    Value: 'Type
    ParticipantLocation: ParsedLocation
    Execution: Parsed<TucPart> list
}

and ParsedMethodCall<'Type> = {
    Value: 'Type
    ServiceLocation: ParsedLocation
    MethodLocation: ParsedLocation
    Execution: Parsed<TucPart> list
}

and ParsedIncompleteHandleEvent<'Type> = {
    Value: 'Type
    ServiceLocation: ParsedLocation
    MethodLocation: ParsedLocation
    Execution: Parsed<TucPart> list
}

and ParsedHandleEvent<'Type> = {
    Value: 'Type
    StreamLocation: ParsedLocation
    ServiceLocation: ParsedLocation
    MethodLocation: ParsedLocation
    Execution: Parsed<TucPart> list
}

and ParsedPostData<'Type> = {
    Value: 'Type
    DataLocation: ParsedLocation list
    OperatorLocation: ParsedLocation
    DataObjectLocation: ParsedLocation
}

and ParsedReadData<'Type> = {
    Value: 'Type
    DataObjectLocation: ParsedLocation
    OperatorLocation: ParsedLocation
    DataLocation: ParsedLocation list
}

[<RequireQualifiedAccess>]
module Parsed =
    let value = function
        | Parsed.KeyWord { Value = value }
        | Parsed.KeyWordWithBody { Value = value }
        | Parsed.KeyWordIf { Value = value }
        | Parsed.ParticipantDefinition { Value = value }
        | Parsed.ComponentDefinition { Value = value }
        | Parsed.Lifeline { Value = value }
        | Parsed.MethodCall { Value = value }
        | Parsed.HandleEvent { Value = value }
        | Parsed.PostData { Value = value }
        | Parsed.ReadData { Value = value }
        | Parsed.Ignored value
            -> value

        | Parsed.KeyWordWithoutValue { KeyWord = { Value = value } } -> failwithf "KeyWord %A does not have a value." value
        | Parsed.IncompleteHandleEvent _ -> failwithf "It is not allowed to get a value out of a Parsed.IncompleteHandleEvent, use ParsedIncompleteHandleEvent.complete first."

    let map (f: 'a -> 'b) = function
        | Parsed.KeyWord k -> Parsed.KeyWord { KeyWord = k.KeyWord; ValueLocation = k.ValueLocation; Value = k.Value |> f }
        | Parsed.KeyWordWithoutValue k -> Parsed.KeyWordWithoutValue { KeyWord = k.KeyWord }
        | Parsed.KeyWordWithBody k -> Parsed.KeyWordWithBody { KeyWord = k.KeyWord; ValueLocation = k.ValueLocation; Body = k.Body; Value = k.Value |> f }
        | Parsed.KeyWordIf k -> Parsed.KeyWordIf { IfLocation = k.IfLocation; ConditionLocation = k.ConditionLocation; ElseLocation = k.ElseLocation; Body = k.Body; ElseBody = k.ElseBody; Value = k.Value |> f }
        | Parsed.ParticipantDefinition p -> Parsed.ParticipantDefinition { Context = p.Context; Domain = p.Domain; Alias = p.Alias; Value = p.Value |> f }
        | Parsed.ComponentDefinition c -> Parsed.ComponentDefinition { Context = c.Context; Domain = c.Domain; Participants = c.Participants; Value = c.Value |> f }
        | Parsed.Lifeline l -> Parsed.Lifeline { ParticipantLocation = l.ParticipantLocation; Execution = l.Execution; Value = l.Value |> f }
        | Parsed.MethodCall m -> Parsed.MethodCall { ServiceLocation = m.ServiceLocation; MethodLocation = m.MethodLocation; Execution = m.Execution; Value = m.Value |> f }
        | Parsed.IncompleteHandleEvent h -> Parsed.IncompleteHandleEvent { ServiceLocation = h.ServiceLocation; MethodLocation = h.MethodLocation; Execution = h.Execution; Value = h.Value |> f }
        | Parsed.HandleEvent h -> Parsed.HandleEvent { StreamLocation = h.StreamLocation; ServiceLocation = h.ServiceLocation; MethodLocation = h.MethodLocation; Execution = h.Execution; Value = h.Value |> f }
        | Parsed.PostData h -> Parsed.PostData { DataLocation = h.DataLocation; OperatorLocation = h.OperatorLocation; DataObjectLocation = h.DataObjectLocation; Value = h.Value |> f }
        | Parsed.ReadData h -> Parsed.ReadData { DataLocation = h.DataLocation; OperatorLocation = h.OperatorLocation; DataObjectLocation = h.DataObjectLocation; Value = h.Value |> f }
        | Parsed.Ignored t -> Parsed.Ignored (t |> f)

[<RequireQualifiedAccess>]
module ParsedIncompleteHandleEvent =
    let complete streamLocation (handleEvent: ParsedIncompleteHandleEvent<_>) =
        Parsed.HandleEvent {
            Value = handleEvent.Value
            StreamLocation = streamLocation
            ServiceLocation = handleEvent.ServiceLocation
            MethodLocation = handleEvent.MethodLocation
            Execution = handleEvent.Execution
        }

type ParsedTucName = Parsed<TucName>
type ParsedData = Parsed<Data>
type ParsedEvent = Parsed<Event>

type ParsedTuc = {
    Name: ParsedTucName
    ParticipantsKeyWord: Parsed<unit>
    Participants: ParsedParticipant list
    Parts: ParsedTucPart list
}

and ParsedParticipant = Parsed<Participant>
and ParsedActiveParticipant = Parsed<ActiveParticipant>

and ParsedServiceParticipant = Parsed<ServiceParticipant>
and ParsedDataObjectParticipant = Parsed<DataObjectParticipant>
and ParsedStreamParticipant = Parsed<StreamParticipant>

and ParsedTucPart = Parsed<TucPart>

[<RequireQualifiedAccess>]
module ParsedTuc =
    let tuc (parsed: ParsedTuc): Tuc =
        {
            Name = parsed.Name |> Parsed.value
            Participants = parsed.Participants |> List.map Parsed.value
            Parts = parsed.Parts |> List.map Parsed.value
        }

(* and ParsedTucPart =
    | Section   -> Keyword<ok>
    | Group     -> Keyword<ok>
    | If        -> Keyword<ok>
    | Loop      -> Keyword<ok>
    | Lifeline  -> Lifeline<ok>
    | ServiceMethodCall -> MethodCall<ok>
    | PostData  -> PostData<ok>
    | ReadData  ->
    | PostEvent -> PostData<ok>
    | ReadEvent ->
    | HandleEventInStream -> HandleEvent<ok>
    | Do        -> Ignore<ok>
    | LeftNote  -> Ignore<ok>
    | Note      -> Ignore<ok>
    | RightNote -> Ignore<ok>
 *)