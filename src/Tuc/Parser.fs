namespace MF.TUC.Parser

open MF.TucConsole.Utils
open MF.Domain

type Tuc = {
    Participants: ParticipantItem list
    Parts: TucPart list
}

and ParticipantItem =
    | Component of ParticipantComponent
    | Participant of Participant

and ParticipantComponent = {
    Name: string
    Participants: Participant list
}

and Participant =
    | Service of ServiceParticipant
    | Stream of StreamParticipant

and ServiceParticipant = {
    Domain: string
    Context: string
    Alias: string
    Type: ResolvedType
}

and StreamParticipant = {
    Domain: string
    Context: string
    Alias: string
    Type: ResolvedType
}

and TucPart =
    | Section of Section
    | Group of Group
    | If of If
    | Loop of Loop
    | Lifeline of Lifeline
    | ServiceMethodCall of ServiceMethodCall
    | PostEvent of PostEvent
    | HandleEventInStream of HandleEventInStream
    | Do of Do
    | LeftNote of Note
    | Note of Note
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
    Initiator: Participant
    Execution: TucPart list
}

and ServiceMethodCall = {
    Caller: Participant
    Service: Participant
    Method: MethodDefinition
    Returns: ResolvedType
    Execution: TucPart list
}

and PostEvent = {
    Caller: Participant
    Event: ResolvedType
    Stream: Participant
}

and HandleEventInStream = {
    Stream: Participant
    Service: Participant
    Method: MethodDefinition
    Execution: TucPart list
}

and Do = {
    Actions: string list
}

and Note = {
    Lines: string list
}

module Example =
    open MF.Domain.Example

(*
participants
  consentManager
    GenericService consents as "Generic Service"
    InteractionCollector consents "Interaction Collector"
  [InteractionCollectorStream] consents //domenu ma service, komponenta ne
  PersonIdentificationEngine consents "PID"
  PersonAggregate consents as "Person Aggregate"
 *)
    let genericServiceParticipant = Service { Domain = "consents"; Context = "GenericService"; Alias = "Generic Service"; Type = genericService }
    let interactionCollectorParticipant = Service { Domain = "consents"; Context = "InteractionCollector"; Alias = "Interaction Collector"; Type = interactionCollector }
    let interactionCollectorStreamParticipant = Stream { Domain = "consents"; Context = "InteractionCollectorStream"; Alias = "InteractionCollectorStream"; Type = interactionCollectorStream }
    let personIdentificationEngineParticipant = Service { Domain = "consents"; Context = "PersonIdentificationEngine"; Alias = "PID"; Type = personIdentificationEngine }
    let personAggregateParticipant = Service { Domain = "consents"; Context = "PersonAggregate"; Alias = "Person Aggregate"; Type = personAggregate }

    let participants: ParticipantItem list = [
        Component ({ Name = "consentManager"; Participants = [
            genericServiceParticipant
            interactionCollectorParticipant
        ]})
        Participant interactionCollectorStreamParticipant
        Participant personIdentificationEngineParticipant
        Participant personAggregateParticipant
    ]

(* section Section 1 *)
    let section1 = Section { Value = "Section 1" }

(*
group Skupina Bozich Funkci
  GenericService
    InteractionCollector.PostInteraction
    loop until result is processed
      do process result
 *)
    let parts = [
        Group { Name = "Skupina Bozich Funkci"; Body = [
            Lifeline { Initiator = genericServiceParticipant; Execution = [
                ServiceMethodCall { Caller = genericServiceParticipant; Service = interactionCollectorParticipant; Method = postInteractionMethod; Returns = commandResult; Execution = [] }
                Loop { Condition = "until result is processed"; Body = [
                    Do { Actions = [ "process result" ] }
                ] }
            ] }
        ] }
    ]

(*
GenericService
  InteractionCollector.PostInteraction
    do vyroba udalosti z dat osoby
    InteractionEvent -> [InteractionCollectorStream] "poznamka" //note k modelu
*)
    let ``send event to stream`` =
        Lifeline { Initiator = genericServiceParticipant; Execution = [
            ServiceMethodCall { Caller = genericServiceParticipant; Service = interactionCollectorParticipant; Method = postInteractionMethod; Returns = commandResult; Execution = [
                Do { Actions = [ "vyroba udalosti z dat osoby" ] }
                PostEvent { Caller = interactionCollectorParticipant; Event = interactionEvent; Stream = interactionCollectorStreamParticipant }
                RightNote { Lines = [ "poznamka" ] }
            ] }
        ] }

(*
GenericService
  InteractionCollector.PostInteraction
    [InteractionCollectorStream]
      PersonIdentificationEngine.OnInteractionEvent
        """
        poznamka
        na vic radku
        """
        PersonAggregate.IdentifyPerson
          do
            prvni step
            druhy step
          if PersonFound
            do return Person
          else
            do return Error
 *)
    let ``handle event in stream`` =
        Lifeline { Initiator = genericServiceParticipant; Execution = [
            HandleEventInStream { Stream = interactionCollectorStreamParticipant; Service = personIdentificationEngineParticipant; Method = onInteractionEventMethod; Execution = [
                Note { Lines = [
                    "poznamka"
                    "na vic radku"
                ] }

                ServiceMethodCall { Caller = personIdentificationEngineParticipant; Service = personAggregateParticipant; Method = identifyPersonMethod; Returns = person; Execution = [
                    Do { Actions = [
                        "prvni step"
                        "druhy step"
                    ] }

                    If {
                        Condition = "PersonFound"
                        Body = [
                            Do { Actions = [ "return Person" ] }
                        ]
                        Else = Some [
                            Do { Actions = [ "return Error" ] }
                        ]
                    }
                ] }
                RightNote { Lines = [ "poznamka" ] }
            ] }
        ] }

module Parser =
    let parse () =
        ()
