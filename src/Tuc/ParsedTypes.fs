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
type KeyWord =
    | TucName
    | Section
    | Group
    | Loop
    | Do
    | If
    | Else

type ParsedKeyWord<'Type> = {
    KeyWord: KeyWord
    KeyWordRange: Range
    Value: 'Type
    ValueLocation: Location
}

[<RequireQualifiedAccess>]
type ParticipantType =
    | Component
    | Service
    | DataObject

type ParsedParticipantDefinition<'Type> = {
    Value: 'Type
    ValueLocation: Location
    DomainRange: Range option
    AliasRange: Range option
}

type ParsedValue<'Type> = {
    Value: 'Type
    Location: Location
}

[<RequireQualifiedAccess>]
type Parsed<'Type> =
    | KeyWord of ParsedKeyWord<'Type>
    | Participant of ParsedParticipantDefinition<'Type>
    | Value of ParsedValue<'Type>

[<RequireQualifiedAccess>]
module Parsed =
    let value = function
        | Parsed.KeyWord { Value = value }
        | Parsed.Participant { Value = value }
        | Parsed.Value { Value = value } -> value

    let map (f: 'a -> 'b) = function
        | Parsed.KeyWord p -> Parsed.KeyWord { KeyWord = p.KeyWord; KeyWordRange = p.KeyWordRange; Value = p.Value |> f; ValueLocation = p.ValueLocation }
        | Parsed.Participant p -> Parsed.Participant { Value = p.Value |> f; ValueLocation = p.ValueLocation; DomainRange = p.DomainRange; AliasRange = p.AliasRange }
        | Parsed.Value p -> Parsed.Value { Value = p.Value |> f; Location = p.Location }

type ParsedTucName = Parsed<TucName>
type ParsedData = Parsed<Data>
type ParsedEvent = Parsed<Event>

type ParsedTuc = {
    Name: ParsedTucName
    Participants: Parsed<ParsedParticipant> list
    Parts: ParsedTucPart list
}

(* and ParsedParticipant = Parsed<Participant>
and ParsedParticipantComponent = Parsed<ParticipantComponent> *)

and ParsedParticipant =
    | Component of ParsedParticipantComponent
    | Participant of ActiveParticipant

and ParsedParticipantComponent = {
    Name: string
    Participants: ParsedActiveParticipant list
}
and ParsedActiveParticipant = Parsed<ActiveParticipant>

and ParsedServiceParticipant = Parsed<ServiceParticipant>
and ParsedDataObjectParticipant = Parsed<DataObjectParticipant>
and ParsedStreamParticipant = Parsed<StreamParticipant>

and ParsedTucPart = Parsed<TucPart>

[<RequireQualifiedAccess>]
module ParsedParticipant =
    let participant: ParsedParticipant -> Participant = function
        | Component c ->
            Participant.Component {
                Name = c.Name
                Participants = c.Participants |> List.map Parsed.value
            }
        | Participant p -> Participant.Participant p

[<RequireQualifiedAccess>]
module ParsedTuc =
    let tuc (parsed: ParsedTuc): Tuc =
        {
            Name = parsed.Name |> Parsed.value
            Participants = parsed.Participants |> List.map (Parsed.value >> ParsedParticipant.participant)
            Parts = parsed.Parts |> List.map Parsed.value
        }

(* and ParsedTucPart =
    | ParsedSection of ParsedSection
    | ParsedGroup of ParsedGroup
    | ParsedIf of ParsedIf
    | ParsedLoop of ParsedLoop
    | ParsedLifeline of ParsedLifeline
    | ParsedServiceMethodCall of ParsedServiceMethodCall
    | ParsedPostData of ParsedPostData
    | ParsedReadData of ParsedReadData
    | ParsedPostEvent of ParsedPostEvent
    | ParsedReadEvent of ParsedReadEvent
    | ParsedHandleEventInStream of ParsedHandleEventInStream
    | ParsedDo of ParsedDo
    | ParsedLeftNote of ParsedNote
    | ParsedNote of ParsedCallerNote
    | ParsedRightNote of ParsedNote

and ParsedSection = Parsed<Section>
and ParsedGroup = Parsed<Group>
and ParsedIf = Parsed<If>
and ParsedLoop = Parsed<Loop>
and ParsedLifeline = Parsed<Lifeline>
and ParsedServiceMethodCall = Parsed<ServiceMethodCall>
and ParsedPostData = Parsed<PostData>
and ParsedReadData = Parsed<ReadData>
and ParsedPostEvent = Parsed<PostEvent>
and ParsedReadEvent = Parsed<ReadEvent>
and ParsedHandleEventInStream = Parsed<HandleEventInStream>
and ParsedDo = Parsed<Do>
and ParsedNote = Parsed<Note>
and ParsedCallerNote = Parsed<CallerNote>
 *)