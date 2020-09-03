Tuc Participants
================

[Home](/tuc-console/) / [Tuc](/tuc-console/tuc/) / Participants

---

All participants of the use case **must** be defined.

**NOTE**: Order in this definition determines the order of participants in puml result.

* Their definition **must** start with `participants` key word.
* Then there are participants defined as a name of the `Record` in the Domain Types and its Domain.

The minimal participants definition.
```tuc
participants
    MyService MyDomainName
```

## Participants:
- [Active Participant](#active-participant)
- [Component](#component)
- [Data Object](#data-object)
- [Stream](#stream)
- [Formatting](#formatting)

---

## Active Participant
> In order to use a participant in the use-case, it must be an _active_ participant.

**NOTE**: Every participant is active, unless it is a [Component](#component).

### Syntax
```tuc
participants                                    // keyword starting a participant section
    {TypeName} {DomainName} ( as "{Alias}" )    // active participant
```

### Type Name
> Participant's Domain type name.

* It **must** be defined in [Domain Types](/tuc-console/domain/#domain-types) as a `Record` or as the [Initiator](/tuc-console/domain/#initiator).
* So it **must** contain only chars, available for F# type name.

**TIP**: Use a PascalCase for type name.

### Domain Name
> Domain, where a participant is defined.

* It **must** be defined and it **must** be the same as the domain file name.

**TIP**: Use a PascalCase for domain name.

### Alias
> Optional part of participant definition.

* If it is set, it **must** start with `as` keyword and be in `"`.
* It **may** contain all chars, spaces, ... (_it **must not** contain another `"` in the name, not even escaped_)

Alias is used as a participant title in the puml file.
If alias is not set, type name will be used instead.

### Example
Let's say, we have domain `Example`.

Domain types will be in `exampleDomain.fsx` (or `ExampleDomain.fsx`).

```fs
// exampleDomain.fsx

type MainService = Initiator

type Service = {
    DoSomeWork: Input -> Output
}
```

```tuc
participants
    MainService Example as "Main Service"
    Service Example
```

In puml:
```puml
actor "Main Service" as MainService <<Example>>
participant "Service" as Service <<Example>>
```

**NOTE**: `Initiator` will be declared as an `actor` in the puml.

## Component
> Component is used, when you need to _group_ participants together.

Component **can not** be used anywhere in the use-case - its only purpose is _grouping_ participants in the puml.

### Syntax
```tuc
participants                                            // keyword starting a participant section
    {ComponentTypeName} {DomainName}                    // component participant
        {TypeName} ( {DomainName} ) ( as "{Alias}" )    // active participant, from component
```

* It **must** be defined in the Domain types as a `Record`, containing its services as fields.
* It **must** have its domain defined and it **must** have at lease one active participant defined.
* Active participant of the Component **may not** have its Domain defined, since it **must** be the same as the Component's domain.

```fs
// exampleDomain.fsx

type MainService = Initiator

type ServiceA = {
    DoSomeWork: Input -> Output
}

type ServiceB = {
    DoSomeWork: Input -> Output
}

type ServiceComponent = {
    ServiceA: ServiceA
    ServiceB: ServiceB
}
```

```tuc
participants
    MainService Example as "Main Service"   // this is Active Participant
    ServiceComponent Example                // this is a Component
        ServiceA Example                    // this is Active Participant of the Component
        ServiceB as "Service B"             // this is Active Participant of the Component
```

In puml:
```puml
actor "Main Service" as MainService <<Example>>
box "ServiceComponent"
    participant "ServiceA" as ServiceA <<Example>>
    participant "Service B" as ServiceB <<Example>>
end box
```

## Data Object
> Data object is a special kind of an Active Participant and it stands for a Database, Stream, Storage, ...

**NOTE**: It **may** also be used in the Component.

### Syntax
```tuc
participants                                    // keyword starting a participant section
    [{TypeName}] {DomainName} ( as "{Alias}" )  // active participant
```

* It has the same syntax as an Active Participant (_also in the Component_), but its Type name **must** be in `[ ]`.
* It **must** be defined as a Generic type in Domain types, with exactly one Generic Parameter.

Example of different data objects (_you can use whatever names you want_).
```fs
// myDomain.fsx

// Data object types
type Database<'Entity> = Database of 'Entity list
type Cache<'CachedData> = Cache of 'CachedData list

// Domain specific data object service
type InteractionDatabase = InteractionDatabase of Database<InteractionEntity>

and InteractionEntity = InteractionEntity

type InteractionCache = InteractionCache of Cache<InteractionEntity>
```

```tuc
participants
    [InteractionDatabase] My as "Interaction DB"
    [InteractionCache] My
```

In puml:
```puml
database "Interaction DB" as InteractionDatabase <<My>>
database "InteractionCache" as InteractionCache <<My>>
```

## Stream
> Stream is a specific kind of a Data object.

### Syntax
* It has the same syntax as the Data Object, but it **must** have `Stream` suffix.

```tuc
participants                                            // keyword starting a participant section
    [{TypeName}Stream] {DomainName} ( as "{Alias}" )    // active participant
```

**NOTE**: It has a [special definition in the domain types](/tuc-console/domain/#stream).

### Example
```fs
// myDomain.fsx

// Data object types
type Stream<'Event> = Stream of 'Event list

// Domain specific data object service
type InteractionStream = InteractionStream of Stream<InteractionEvent>

and InteractionEvent =
    | Confirmed
    | Rejected
```

```tuc
participants
    [InteractionStream] My as "Interaction Stream"
```

In puml:
```puml
collections "Interaction Stream" as InteractionStream <<My>>
```

## Formatting
Participant's Alias **may** contain a formatting, supported by a [PlantUML](https://plantuml.com/sequence-diagram#ezoic-pub-ad-placeholder-141)

```tuc
participants
    MainService Domain as "**bold**"
    MainService Domain as "*italics*"
    MainService Domain as "--stroked--"
    MainService Domain as "__underlined__"
    MainService Domain as "~~waved~~"
    MainService Domain as "<back:cadetblue><size:18>formatted</size></back>"
    MainService Domain as "<u:red>This</u> is <color #118888>displayed</color> **<color purple> with </color> <s:red>some</strike> formatting**"
```
