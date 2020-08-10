namespace MF.Tuc

open MF.Domain

type TucName = TucName of string

[<RequireQualifiedAccess>]
module TucName =
    let value (TucName name) = name

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
    | Stream of StreamParticipant

and ServiceParticipant = {
    Domain: string
    Context: string
    Alias: string
    ServiceType: DomainType
}

and StreamParticipant = {
    Domain: string
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
    Initiator: ActiveParticipant
    Execution: TucPart list
}

and ServiceMethodCall = {
    Caller: ActiveParticipant
    Service: ActiveParticipant
    Method: MethodDefinition
    Execution: TucPart list
}

and PostEvent = {
    Caller: ActiveParticipant
    Stream: ActiveParticipant
}

and HandleEventInStream = {
    Stream: ActiveParticipant
    Service: ActiveParticipant
    Method: MethodDefinition
    Execution: TucPart list
}

and Do = {
    Actions: string list
}

and Note = {
    Lines: string list
}

[<RequireQualifiedAccess>]
module ActiveParticipant =
    let name = function
        | Service { ServiceType = t }
        | Stream { StreamType = t } -> t |> DomainType.nameValue

module internal Example =
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
    let genericServiceParticipant = Service { Domain = "consents"; Context = "GenericService"; Alias = "Generic Service"; ServiceType = DomainType genericService }
    let interactionCollectorParticipant = Service { Domain = "consents"; Context = "InteractionCollector"; Alias = "Interaction Collector"; ServiceType = DomainType interactionCollector }
    let interactionCollectorStreamParticipant = Stream { Domain = "consents"; Context = "InteractionCollectorStream"; Alias = "InteractionCollectorStream"; StreamType = DomainType interactionCollectorStream }
    let personIdentificationEngineParticipant = Service { Domain = "consents"; Context = "PersonIdentificationEngine"; Alias = "PID"; ServiceType = DomainType personIdentificationEngine }
    let personAggregateParticipant = Service { Domain = "consents"; Context = "PersonAggregate"; Alias = "Person Aggregate"; ServiceType = DomainType personAggregate }

    let participants: Participant list = [
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
                ServiceMethodCall { Caller = genericServiceParticipant; Service = interactionCollectorParticipant; Method = postInteractionMethod; Execution = [
                    // empty body
                ] }

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
            ServiceMethodCall { Caller = genericServiceParticipant; Service = interactionCollectorParticipant; Method = postInteractionMethod; Execution = [
                Do { Actions = [ "vyroba udalosti z dat osoby" ] }
                PostEvent { Caller = interactionCollectorParticipant; Stream = interactionCollectorStreamParticipant }
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
            ServiceMethodCall { Caller = genericServiceParticipant; Service = interactionCollectorParticipant; Method = postInteractionMethod; Execution = [
                HandleEventInStream { Stream = interactionCollectorStreamParticipant; Service = personIdentificationEngineParticipant; Method = onInteractionEventMethod; Execution = [
                    Note { Lines = [
                        "poznamka"
                        "na vic radku"
                    ] }

                    ServiceMethodCall { Caller = personIdentificationEngineParticipant; Service = personAggregateParticipant; Method = identifyPersonMethod; Execution = [
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
        ] }
