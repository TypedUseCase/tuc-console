namespace MF.Puml

open MF.TucConsole
open MF.Domain
open MF.Tuc
open ErrorHandling

type PumlError =
    | NoTucProvided

[<RequireQualifiedAccess>]
module PumlError =
    let format = function
        | NoTucProvided -> "There is no tuc to generate a puml from."

[<RequireQualifiedAccess>]
module Generate =
    type private PumlPart = PumlPart of string
    type private Generate<'TucPart> = MF.ConsoleApplication.Output -> 'TucPart -> PumlPart list
    type private GenerateTuc = MF.ConsoleApplication.Output -> Map<string, ActiveParticipant> -> PumlPart list -> Tuc list -> Map<string, ActiveParticipant> * PumlPart list

    let private indentSize = 4

    let private indent size =
        " " |> String.replicate size

    [<RequireQualifiedAccess>]
    module private PumlPart =
        let emptyLine = PumlPart ""
        let endBody indentation = PumlPart (sprintf "%send" indentation)

        let value (PumlPart part) = part

        let map f = value >> f >> PumlPart

        let combine parts =
            parts
            |> List.map value
            |> String.concat "\n"
            |> PumlPart

        let indent size = map ((+) (indent size))
        let indentMany size = List.map (indent size)

    [<RequireQualifiedAccess>]
    module private Participant =
        let private generateActive: Generate<ActiveParticipant> = fun output -> function
            | Service { Domain = domain; Context = context; Alias = alias; ServiceType = DomainType.Initiator } ->
                [ PumlPart (sprintf "actor %A as %s <<%s>>" (alias |> Format.format) context (domain |> DomainName.value)) ]

            | Service { Domain = domain; Context = context; Alias = alias } ->
                [ PumlPart (sprintf "participant %A as %s <<%s>>" (alias |> Format.format) context (domain |> DomainName.value)) ]

            | DataObject { Domain = domain; Context = context; Alias = alias } ->
                [ PumlPart (sprintf "database %A as %s <<%s>>" (alias |> Format.format) context (domain |> DomainName.value)) ]

            | Stream { Domain = domain; Context = context; Alias = alias } ->
                [ PumlPart (sprintf "collections %A as %s <<%s>>" (alias |> Format.format) context (domain |> DomainName.value)) ]

        let generate: Generate<Participant> = fun output -> function
            | Component { Name = name; Participants = participants } ->
                [
                    yield PumlPart (sprintf "box %A" name)

                    yield!
                        participants
                        |> List.collect (
                            generateActive output
                            >> PumlPart.indentMany indentSize
                        )

                    yield PumlPart "end box"
                ]

            | Participant active -> active |> generateActive output

        let activate participant =
            PumlPart (participant |> ActiveParticipant.name |> sprintf "activate %s")

        let deactivate participant =
            PumlPart (participant |> ActiveParticipant.name |> sprintf "deactivate %s")

    [<RequireQualifiedAccess>]
    module private Part =
        let rec generate mainActor indentation: Generate<TucPart> = fun output ->
            let currentIndentation = indent indentation
            let deeper = indentation + indentSize

            let generate indentation =
                generate mainActor indentation output

            function
            | Section { Value = section } ->
                [
                    PumlPart.emptyLine
                    PumlPart (sprintf "== %s ==" section)
                    PumlPart.emptyLine
                ]

            | Group { Name = group; Body = body } ->
                [
                    yield PumlPart (sprintf "%sgroup %s" currentIndentation group)
                    yield! body |> List.collect (generate deeper)
                    yield PumlPart.endBody currentIndentation
                ]

            | If { Condition = condition; Body = body; Else = elseBody } ->
                [
                    yield PumlPart (sprintf "%salt %s" currentIndentation condition)
                    yield! body |> List.collect (generate deeper)

                    yield!
                        match elseBody with
                        | Some elseBody ->
                            [
                                yield PumlPart (sprintf "%selse" currentIndentation)
                                yield! elseBody |> List.collect (generate deeper)
                            ]
                        | _ -> []

                    yield PumlPart.endBody currentIndentation
                ]

            | Loop { Condition = condition; Body = body } ->
                [
                    yield PumlPart (sprintf "%sloop %s" currentIndentation condition)
                    yield! body |> List.collect (generate deeper)
                    yield PumlPart.endBody currentIndentation
                ]

            | Lifeline { Initiator = initiator; Execution = execution } ->
                [
                    if Some initiator <> mainActor then
                        yield initiator |> Participant.activate |> PumlPart.indent indentation

                    yield! execution |> List.collect (generate indentation)

                    if Some initiator <> mainActor then
                        yield initiator |> Participant.deactivate |> PumlPart.indent indentation
                ]

            | ServiceMethodCall { Caller = caller; Service = service; Method = method; Execution = execution } ->
                let arguments, returns =
                    method.Function
                    |> FunctionDefinition.fold

                let callMethod =
                    sprintf "%s -> %s ++: %s(%s)"
                        (caller |> ActiveParticipant.name)
                        (service |> ActiveParticipant.name)
                        (method.Name |> FieldName.value)
                        (arguments |> List.map TypeDefinition.value |> String.concat ", ")
                    |> PumlPart

                let methodReturns =
                    sprintf "%s --> %s --: %s"
                        (service |> ActiveParticipant.name)
                        (caller |> ActiveParticipant.name)
                        (returns |> TypeDefinition.value)
                    |> PumlPart

                [
                    yield callMethod |> PumlPart.indent indentation
                    yield! execution |> List.collect (generate deeper)
                    yield methodReturns |> PumlPart.indent indentation
                ]

            | PostData { Caller = caller; DataObject = dataObject; Data = Data data } ->
                let postData =
                    sprintf "%s ->> %s: %s"
                        (caller |> ActiveParticipant.name)
                        (dataObject |> ActiveParticipant.name)
                        data
                    |> PumlPart

                [
                    postData |> PumlPart.indent indentation
                ]

            | ReadData { Caller = caller; DataObject = dataObject; Data = Data data } ->
                let readData =
                    sprintf "%s ->> %s: %s"
                        (dataObject |> ActiveParticipant.name)
                        (caller |> ActiveParticipant.name)
                        data
                    |> PumlPart

                [
                    readData |> PumlPart.indent indentation
                ]

            | PostEvent { Caller = caller; Stream = stream; Event = event } ->
                let postEvent =
                    sprintf "%s ->> %s: %s"
                        (caller |> ActiveParticipant.name)
                        (stream |> ActiveParticipant.name)
                        (event |> Event.lastInPath)
                    |> PumlPart

                [
                    postEvent |> PumlPart.indent indentation
                ]

            | ReadEvent { Caller = caller; Stream = stream; Event = event } ->
                let readEvent =
                    sprintf "%s ->> %s: %s"
                        (stream |> ActiveParticipant.name)
                        (caller |> ActiveParticipant.name)
                        (event |> Event.lastInPath)
                    |> PumlPart

                [
                    readEvent |> PumlPart.indent indentation
                ]

            | HandleEventInStream { Stream = stream; Service = service; Handler = handlerMethod; Execution = execution } ->
                let handlerCalled =
                    sprintf "%s ->> %s: %s(%s)"
                        (stream |> ActiveParticipant.name)
                        (service |> ActiveParticipant.name)
                        (handlerMethod.Name |> FieldName.value)
                        (handlerMethod.Handler.Handles |> TypeDefinition.value)
                    |> PumlPart

                [
                    yield handlerCalled |> PumlPart.indent indentation

                    yield service |> Participant.activate |> PumlPart.indent deeper
                    yield! execution |> List.collect (generate deeper)
                    yield service |> Participant.deactivate |> PumlPart.indent deeper
                ]

            | Do { Caller = caller; Actions = [ action ]} ->
                [
                    PumlPart (sprintf "hnote over %s" (caller |> ActiveParticipant.name)) |> PumlPart.indent indentation
                    PumlPart (sprintf "do: %s" (action |> Format.format)) |> PumlPart.indent indentation
                    PumlPart "end hnote" |> PumlPart.indent indentation
                ]

            | Do { Caller = caller; Actions = actions } ->
                [
                    yield PumlPart (sprintf "hnote over %s" (caller |> ActiveParticipant.name)) |> PumlPart.indent indentation
                    yield PumlPart "do:" |> PumlPart.indent indentation
                    yield! actions |> List.map (Format.format >> PumlPart) |> PumlPart.indentMany deeper
                    yield PumlPart "end hnote" |> PumlPart.indent indentation
                ]

            | LeftNote { Lines = [ line ] } ->
                [
                    PumlPart (sprintf "note left: %s" (line |> Format.format)) |> PumlPart.indent indentation
                ]

            | LeftNote { Lines = lines } ->
                [
                    yield PumlPart "note left" |> PumlPart.indent indentation
                    yield! lines |> List.map (Format.format >> PumlPart) |> PumlPart.indentMany indentation
                    yield PumlPart "end note" |> PumlPart.indent indentation
                ]

            | Note { Caller = caller; Lines = [ line ] } ->
                [
                    PumlPart (sprintf "note over %s: %s" (caller |> ActiveParticipant.name) (line |> Format.format)) |> PumlPart.indent indentation
                ]

            | Note { Caller = caller; Lines = lines } ->
                [
                    yield PumlPart (sprintf "note over %s" (caller |> ActiveParticipant.name)) |> PumlPart.indent indentation
                    yield! lines |> List.map (Format.format >> PumlPart) |> PumlPart.indentMany indentation
                    yield PumlPart "end note" |> PumlPart.indent indentation
                ]

            | RightNote { Lines = [ line ] } ->
                [
                    PumlPart (sprintf "note right: %s" (line |> Format.format)) |> PumlPart.indent indentation
                ]

            | RightNote { Lines = lines } ->
                [
                    yield PumlPart "note right" |> PumlPart.indent indentation
                    yield! lines |> List.map (Format.format >> PumlPart) |> PumlPart.indentMany indentation
                    yield PumlPart "end note" |> PumlPart.indent indentation
                ]

    let rec private generate: GenerateTuc = fun output activatedMainInitiators acc -> function
        | [] -> activatedMainInitiators, acc
        | tuc :: tucs ->
            let name =
                tuc.Name
                |> TucName.value
                |> sprintf "== %s =="
                |> PumlPart

            let mainInitiator =
                tuc.Participants
                |> List.collect Participant.active
                |> List.tryPick (function
                    | Service { ServiceType = DomainType.Initiator } as actor -> Some actor
                    | _ -> None
                )

            let parts = [
                PumlPart.combine [
                    yield name
                    yield PumlPart.emptyLine

                    yield! tuc.Participants |> List.collect (Participant.generate output)
                    yield PumlPart.emptyLine

                    match mainInitiator with
                    | Some mainInitiator ->
                        if activatedMainInitiators |> Map.containsKey (mainInitiator |> ActiveParticipant.name) |> not
                            then yield mainInitiator |> Participant.activate
                    | _ -> ()

                    yield! tuc.Parts |> List.collect (Part.generate mainInitiator 0 output)
                    yield PumlPart.emptyLine
                ]
            ]

            let activatedMainInitiators =
                match mainInitiator with
                | Some mainInitiator -> activatedMainInitiators |> Map.add (mainInitiator |> ActiveParticipant.name) mainInitiator
                | _ -> activatedMainInitiators

            tucs
            |> generate output activatedMainInitiators (acc @ parts)

    let private createPuml style pumlName = function
        | [] -> Error NoTucProvided
        | parts ->
            [
                yield sprintf "@startuml %s" pumlName
                yield ""

                match style with
                | Some style -> yield style
                | _ -> ()

                yield parts |> PumlPart.combine |> PumlPart.value

                yield ""
                yield "@enduml"
                yield ""
            ]
            |> String.concat "\n"
            |> Puml
            |> Ok

    let puml (output: MF.ConsoleApplication.Output) style pumlName (tucs: Tuc list) =
        let activatedMainInitiators, parts =
            tucs
            |> generate output Map.empty []

        parts
        @ (activatedMainInitiators |> Map.toList |> List.map (snd >> Participant.deactivate))
        |> createPuml style pumlName

    open PlantUml.Net

    type ImageFormat =
        | Png
        | Svg
        | Eps
        | Pdf
        | Vdx
        | Xmi
        | Scxml
        | Html
        | Ascii
        | AsciiUnicode
        | LaTeX

    [<RequireQualifiedAccess>]
    module ImageFormat =
        let formats =
            [ "Png"; "Svg"; "Eps"; "Pdf"; "Vdx"; "Xmi"; "Scxml"; "Html"; "Ascii"; "AsciiUnicode"; "LaTeX" ]

        let parseExtension format =
            match format |> String.trimStart '.' |> String.toLower |> String.replaceAll ["-"; "_"] "" with
            | "png" -> Png
            | "svg" -> Svg
            | "eps" -> Eps
            | "pdf" -> Pdf
            | "vdx" -> Vdx
            | "xmi" -> Xmi
            | "scxml" -> Scxml
            | "html" -> Html
            | "ascii" -> Ascii
            | "asciiunicode" -> AsciiUnicode
            | "latex" -> LaTeX
            | _ ->
                failwithf "Unknown image format %A. Available formats are:\n  - %s"
                    format
                    (formats |> String.concat "\n  - ")

        let outputFormat = function
            | Png -> OutputFormat.Png
            | Svg -> OutputFormat.Svg
            | Eps -> OutputFormat.Eps
            | Pdf -> OutputFormat.Pdf
            | Vdx -> OutputFormat.Vdx
            | Xmi -> OutputFormat.Xmi
            | Scxml -> OutputFormat.Scxml
            | Html -> OutputFormat.Html
            | Ascii -> OutputFormat.Ascii
            | AsciiUnicode -> OutputFormat.Ascii_Unicode
            | LaTeX -> OutputFormat.LaTeX

        let extension = function
            | Png -> "png"
            | Svg -> "svg"
            | Eps -> "eps"
            | Pdf -> "pdf"
            | Vdx -> "vdx"
            | Xmi -> "xmi"
            | Scxml -> "scxml"
            | Html -> "html"
            | Ascii -> "ascii"
            | AsciiUnicode -> "ascii_Unicode"
            | LaTeX -> "laTeX"

    let image imageFormat (Puml puml) = asyncResult {
        let factory = RendererFactory()
        let renderer = factory.CreateRenderer(PlantUmlSettings())

        let! image =
            renderer.RenderAsync(puml, imageFormat |> ImageFormat.outputFormat)
            |> AsyncResult.ofTaskCatch (fun e -> e.Message)

        return PumlImage image
    }
