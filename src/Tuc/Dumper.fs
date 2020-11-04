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
        |> List.iter (Parsed.value >> formatParticipant indentSize >> output.Message)
        |> output.NewLine

        tuc.Parts
        |> List.iter (Parsed.value >> formatPart indentSize >> output.Message)
        |> output.NewLine

    [<RequireQualifiedAccess>]
    module FormatParsed =
        let private formatLocation parsedType (parsedLocation: ParsedLocation) =
            sprintf " - <c:dark-yellow>%s</c> <c:yellow>%s</c> at <c:magenta>%03i</c> -> <c:magenta>%03i</c>: <c:cyan>%s</c> [<c:magenta>%d</c>]  <c:gray>// %s</c>"
                parsedLocation.Location.Uri
                (parsedLocation.Location.Range |> Range.lineString)
                (parsedLocation.Location.Range |> Range.startPosition |> Position.character)
                (parsedLocation.Location.Range |> Range.endPosition |> Position.character)
                parsedLocation.Value
                parsedLocation.Value.Length
                parsedType

        let private format formatValue findPositions parsed =
            let value =
                match parsed with
                | Parsed.KeyWordWithoutValue { KeyWord = { Value = value } } -> value
                | p -> p |> Parsed.value |> formatValue

            let positions = parsed |> findPositions

            sprintf "%s%s" value (positions |> List.formatLines "" id)

        let value<'a> (formatValue: 'a -> string) =
            format formatValue (function
                | Parsed.KeyWord k ->
                    [
                        k.KeyWord |> formatLocation "KeyWord"
                        k.ValueLocation |> formatLocation "Value"
                    ]
                | Parsed.KeyWordWithoutValue k ->
                    [
                        k.KeyWord |> formatLocation "KeyWord"
                    ]
                | Parsed.ParticipantDefinition p ->
                    [
                        Some (p.Context |> formatLocation "Context")
                        p.Domain |> Option.map (formatLocation "Domain")
                        p.Alias |> Option.map (formatLocation "Alias")
                    ]
                    |> List.choose id
                | Parsed.ComponentDefinition c ->
                    [
                        yield c.Context |> formatLocation "Context"
                        yield c.Domain |> formatLocation "Domain"

                        yield! c.Participants |> List.choose (fun p ->
                            match p with
                            | Parsed.ParticipantDefinition p ->
                                [
                                    Some (p.Context |> formatLocation "Context")
                                    p.Domain |> Option.map (formatLocation "Domain")
                                    p.Alias |> Option.map (formatLocation "Alias")
                                ]
                                |> List.choose id
                                |> List.formatLines "" id
                                |> Some
                            | _ -> None
                        )
                    ]
                | _ -> []
            )

        let rec tucPart showIgnored (formatValue: TucPart -> string) =
            let subType = function
                | Section _ -> "Section"
                | Group _ -> "Group"
                | If _ -> "If"
                | Loop _ -> "Loop"
                | Lifeline _ -> "Lifeline"
                | ServiceMethodCall _ -> "ServiceMethodCall"
                | PostData _ -> "PostData"
                | ReadData _ -> "ReadData"
                | PostEvent _ -> "PostEvent"
                | ReadEvent _ -> "ReadEvent"
                | HandleEventInStream _ -> "HandleEventInStream"
                | Do _ -> "Do"
                | LeftNote _ -> "LeftNote"
                | Note _ -> "Note"
                | RightNote _ -> "RightNote"

            format formatValue (function
                | Parsed.KeyWord k ->
                    [
                        k.KeyWord |> formatLocation "KeyWord"
                        k.ValueLocation |> formatLocation "Value"
                    ]
                | Parsed.KeyWordWithBody k ->
                    [
                        yield k.KeyWord |> formatLocation "KeyWord"
                        yield k.ValueLocation |> formatLocation "Value"
                        yield! k.Body |> List.map (tucPart showIgnored formatValue)
                    ]
                | Parsed.KeyWordIf k ->
                    [
                        yield k.IfLocation |> formatLocation "KeyWord"
                        yield k.ConditionLocation |> formatLocation "Condition"
                        yield! k.Body |> List.map (tucPart showIgnored formatValue)

                        match k.ElseLocation, k.ElseBody with
                        | Some elseLocation, Some body ->
                            yield elseLocation |> formatLocation "KeyWord"
                            yield! body |> List.map (tucPart showIgnored formatValue)
                        | _ -> ()
                    ]
                | Parsed.Lifeline p ->
                    [
                        yield p.ParticipantLocation |> formatLocation "Lifeline"
                        yield! p.Execution |> List.map (tucPart showIgnored formatValue)
                    ]
                | Parsed.MethodCall m ->
                    [
                        yield m.ServiceLocation |> formatLocation "Service"
                        yield m.MethodLocation |> formatLocation "Method"
                        yield! m.Execution |> List.map (tucPart showIgnored formatValue)
                    ]
                | Parsed.HandleEvent h ->
                    [
                        yield h.StreamLocation |> formatLocation "Stream"
                        yield h.ServiceLocation |> formatLocation "Service"
                        yield h.MethodLocation |> formatLocation "Method"
                        yield! h.Execution |> List.map (tucPart showIgnored formatValue)
                    ]
                | Parsed.PostData p ->
                    [
                        yield! p.DataLocation |> List.map (formatLocation "Data")
                        yield p.OperatorLocation |> formatLocation "Operator"
                        yield p.DataObjectLocation |> formatLocation "DataObject"
                    ]
                | Parsed.ReadData r ->
                    [
                        yield r.DataObjectLocation |> formatLocation "DataObject"
                        yield r.OperatorLocation |> formatLocation "Operator"
                        yield! r.DataLocation |> List.map (formatLocation "Data")
                    ]
                | Parsed.Ignored v ->
                    [
                        if showIgnored then
                            yield sprintf " - <c:gray>// ignored %s</c>" (v |> subType)
                    ]
                | _ -> []
            )

    let detailedParsedTuc (output: MF.ConsoleApplication.Output) (tuc: ParsedTuc) =
        tuc.Name
        |> FormatParsed.value (TucName.value >> sprintf "Tuc: %s")
        |> output.Message
        |> output.NewLine

        tuc.ParticipantsKeyWord
        |> FormatParsed.value string
        |> output.Message
        |> output.NewLine

        tuc.Participants
        |> List.iter (FormatParsed.value (formatParticipant indentSize) >> tee (ignore >> output.NewLine) >> output.Message)
        |> output.NewLine

        tuc.Parts
        |> List.iter (FormatParsed.tucPart (output.IsVerbose()) (formatPart indentSize) >> tee (ignore >> output.NewLine) >> output.Message)
        |> output.NewLine
