namespace Tuc

open Tuc.Console
open Tuc.Domain

[<RequireQualifiedAccess>]
module Dump =

    type private Format<'Type> = 'Type -> string

    let private indentSize = 4

    let private indent size =
        " " |> String.replicate size

    let private formatActiveParticipant: Format<ActiveParticipant> = function
        | ActiveParticipant.Service { Domain = domain; Context = context; Alias = alias } ->
            sprintf "<c:cyan>%s</c>(<c:gray>%s.%s</c>)"
                alias
                (domain |> DomainName.value)
                context

        | ActiveParticipant.DataObject { Domain = domain; Context = context; Alias = alias } ->
            sprintf "<c:magenta>[</c><c:cyan>%s</c>(<c:gray>%s.%s</c>)<c:magenta>]</c>"
                alias
                (domain |> DomainName.value)
                context

        | ActiveParticipant.Stream { Domain = domain; Context = context; Alias = alias } ->
            sprintf "<c:purple>[</c><c:cyan>%s</c>(<c:gray>%s.%s</c>)<c:purple>]</c>"
                alias
                (domain |> DomainName.value)
                context

    let private formatParticipant indentation: Format<Participant> = function
        | Participant.Component { Name = name; Participants = participants } ->
            sprintf "<c:purple>%s</c>:%s"
                name
                (participants |> List.formatLines (indent indentation) formatActiveParticipant)

        | Participant.Participant participant -> participant |> formatActiveParticipant

    let rec private formatPart indentation: Format<TucPart> = function
        | Section { Value = section } -> sprintf "\nSection <c:purple>%s</c>\n" section

        | Group { Name = group; Body = body } ->
            sprintf "Group <c:purple>%s</c>%s"
                group
                (body |> List.formatLines (indent indentation) (formatPart (indentation + indentSize)))

        | If { Condition = condition; Body = body; Else = elseBody } ->
            sprintf "if <c:magenta>%s</c>%s%s"
                condition
                (body |> List.formatLines (indent indentation) (formatPart (indentation + indentSize)))
                (
                    match elseBody with
                    | Some body ->
                        body
                        |> List.formatLines (indent indentation) (formatPart (indentation + indentSize))
                        |> sprintf "\n%selse%s" (indent (indentation - indentSize))
                    | _ -> ""
                )

        | Loop { Condition = condition; Body = body } ->
            sprintf "loop <c:magenta>%s</c>%s"
                condition
                (body |> List.formatLines (indent indentation) (formatPart (indentation + indentSize)))

        | Lifeline { Initiator = initiator; Execution = execution } ->
            sprintf "%s  <c:gray>// lifeline</c>%s"
                (initiator |> formatActiveParticipant)
                (execution |> List.formatLines (indent indentation) (formatPart (indentation + indentSize)))

        | ServiceMethodCall { Caller = caller; Service = service; Method = method; Execution = execution } ->
            sprintf "-> %s.<c:yellow>%s</c>()  <c:gray>// Called by</c> %s%s\n%s<- <c:yellow>%s</c>"
                (service |> formatActiveParticipant)
                (method.Name |> FieldName.value)
                (caller |> formatActiveParticipant)
                (execution |> List.formatLines (indent indentation) (formatPart (indentation + indentSize)))
                (indent (indentation - 4))
                (method.Function.Returns |> TypeDefinition.value)

        | PostData { Caller = caller; DataObject = dataObject; Data = { Original = data } } ->
            sprintf "<c:gray>post:</c> <c:dark-yellow>%s</c> -> %s  <c:gray>// Called by</c> %s"
                data
                (dataObject |> formatActiveParticipant)
                (caller |> formatActiveParticipant)

        | ReadData { Caller = caller; DataObject = dataObject; Data = { Original = data } } ->
            sprintf "<c:gray>read:</c> <c:dark-yellow>%s</c> <- %s  <c:gray>// Called by</c> %s"
                data
                (dataObject |> formatActiveParticipant)
                (caller |> formatActiveParticipant)

        | PostEvent { Caller = caller; Stream = stream; Event = (Event { Original = event }) } ->
            sprintf "<c:gray>post:</c> <c:yellow>%s</c> -> %s  <c:gray>// Called by</c> %s"
                event
                (stream |> formatActiveParticipant)
                (caller |> formatActiveParticipant)

        | ReadEvent { Caller = caller; Stream = stream; Event = (Event { Original = event }) } ->
            sprintf "<c:gray>read:</c> <c:yellow>%s</c> <- %s  <c:gray>// Called by</c> %s"
                event
                (stream |> formatActiveParticipant)
                (caller |> formatActiveParticipant)

        | HandleEventInStream { Stream = stream; Service = service; Handler = handlerMethod; Execution = execution } ->
            sprintf "%s\n%s%s.<c:yellow>%s</c>()%s"
                (stream |> formatActiveParticipant)
                (indent indentation)
                (service |> formatActiveParticipant)
                (handlerMethod.Name |> FieldName.value)
                (execution |> List.formatLines (indent indentation) (formatPart (indentation + indentSize)))

        | Do { Actions = [ action ]} -> sprintf "<c:dark-yellow>Do:</c> %s" action
        | Do { Actions = actions } -> sprintf "<c:dark-yellow>Do:</c>%s" (actions |> List.formatLines (indent indentation) id)

        | LeftNote { Lines = lines } -> sprintf "Left note:<c:gray>%s</c>" (lines |> List.formatLines (indent indentation) id)
        | Note { Lines = lines } -> sprintf "Note:<c:gray>%s</c>" (lines |> List.formatLines (indent indentation) id)
        | RightNote { Lines = lines } -> sprintf "Right note:<c:gray>%s</c>" (lines |> List.formatLines (indent indentation) id)

    let parsedTuc (output: MF.ConsoleApplication.Output) (tuc: ParsedTuc) =
        tuc.Name
        |> Parsed.value
        |> TucName.value
        |> sprintf "Tuc: %s"
        |> output.Section

        tuc.Participants
        |> List.iter (Parsed.value >> ParsedParticipant.participant >> formatParticipant indentSize >> output.Message)
        |> output.NewLine

        tuc.Parts
        |> List.iter (Parsed.value >> formatPart indentSize >> output.Message)
        |> output.NewLine

    let private formatParsedValue formatValue parsed =
        let value = parsed |> Parsed.value |> formatValue

        let positions =
            let formatLocation parsed value uri range =
                sprintf "<c:gray>%s</c>: <c:cyan>%s</c> from <c:magenta>%d</c> to <c:magenta>%d</c> at <c:yellow>%s</c> in <c:dark-yellow>%s</c>"
                    parsed
                    value
                    (range |> Range.startPosition |> Position.character)
                    (range |> Range.endPosition |> Position.character)
                    (range |> Range.lineString)
                    uri

            match parsed with
            | Parsed.KeyWord k ->
                [
                    formatLocation "KeyWord" (k.KeyWord |> string) k.ValueLocation.Uri k.KeyWordRange
                    formatLocation "Value" value k.ValueLocation.Uri k.ValueLocation.Range
                ]
            | Parsed.Participant p ->
                [
                    yield formatLocation "Value" value p.ValueLocation.Uri p.ValueLocation.Range

                    match p.DomainRange with
                    | Some domainRange -> yield formatLocation "Domain" "..." p.ValueLocation.Uri domainRange
                    | _ -> ()

                    match p.AliasRange with
                    | Some aliasRange -> yield formatLocation "Alias" "..." p.ValueLocation.Uri aliasRange
                    | _ -> ()
                ]
            | Parsed.Value v ->
                [
                    formatLocation "Value" value v.Location.Uri v.Location.Range
                ]

        sprintf "%s\n" (positions |> List.formatLines " - " id)

    let detailedParsedTuc (output: MF.ConsoleApplication.Output) (tuc: ParsedTuc) =
        tuc.Name
        |> formatParsedValue TucName.value
        |> output.Message
        |> output.NewLine

        tuc.Participants
        |> List.iter (formatParsedValue (ParsedParticipant.participant >> formatParticipant indentSize) >> output.Message)
        |> output.NewLine

        // todo:
        tuc.Parts
        |> List.iter (Parsed.value >> formatPart indentSize >> output.Message)
        |> output.NewLine
