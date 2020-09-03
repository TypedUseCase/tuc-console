Domain
======

[Home](/tuc-console/) / Domain

---

> Domain is a DDD model, written in the F# as `.fsx` (_F# script_).

It should only contain `Types` (_at least for now_).

## Table of contents
- [How does it work?](/tuc-console/domain/#how-does-it-work)
- [Common Types](/tuc-console/domain/#common-types)
    - [Initiator](/tuc-console/domain/#initiator)
    - [Data Object](/tuc-console/domain/#data-object)
    - [Stream](/tuc-console/domain/#stream)
    - [Handler](/tuc-console/domain/#handler)
- [Domain example](/tuc-console/domain/#domain-example)
- [Share types between domains](/tuc-console/domain/#share-types-between-domains)

## How does it work?
It uses a [Fsharp.Compiler.Service](https://github.com/fsharp/FSharp.Compiler.Service) under the hood.

* compiler parses a `.fsx` files
* then a Domain Resolver resolves a domain types for Tuc
* there is also a Domain Checker, which checks the basic mistakes and shows types, which are not defined or supported

### Domain types
You can use whatever type you want, but abbreviation.

Abbreviation is not supported, since the compiler just replace the Abbreviation with the real type, it doesn't mean anything in the Domain.

```fs
type Id = string    // this is abbreviation for string

type Id = Id of string  // this is correct

// or use just an Id, if you don't want to specify a concrete type
type Id = Id
```

```fs
type Event = Event // this is ok

type SomethingCreatedEvent = Event   // this is abbreviation for Event

type SomethingCreatedEvent = SomethingCreatedEvent of Event // this is correct
```

## Common types
> _Common types_ helps a **Resolver** to resolve a type with specific purpose or usage.

### Initiator
> Initiator is the _main_ service, which occurs in the use-case.

```fs
type MyMainService = Initiator
```

### Data Object
> Data object is defined as a list of Data.

It allows you to [post data into the data object](/tuc-console/tuc/parts.html#post-data) and [read data from data object](/tuc-console/tuc/parts.html#read-data).

```fs
type DataObject<'Data> = DataObject of 'Data list
```

Data Objects are used as a type of your concrete data object.

For example, when you have a database of Persons, it might be as following:
```fs
type Database<'Entity> = Database of 'Entity list

type PersonDatabase = PersonDatabase of Database<Person>

and Person = {
    Name: string
}
```

### Stream
> Stream is defined as a list of Events. Stream type is one of a specific Data Objects.

It allows you to [post events into the stream](/tuc-console/tuc/parts.html#post-event), [read events from stream](/tuc-console/tuc/parts.html#read-event) or [handle an event in stream](/tuc-console/tuc/parts.html#handle-event-in-stream).

```fs
type Stream<'Event> = Stream of 'Event list
```

Stream are used as a type of your concrete stream.

For example, when you have a stream of Interactions, it might be as following:
```fs
type InteractionStream = InteractionStream of Stream<InteractionEvent>

and InteractionEvent =
    | Confirmed
    | Rejected
```

### Handler
> Handler is a generic function, which handles a data.

It must be a generic type with exactly one generic parameter and it must have a `Handler` suffix.

Handlers allow you to handle a Data from DataObjects, by a [special tuc syntax](/tuc-console/tuc/parts.html#handle-event-in-stream).

```fs
type Handler<'Data> = Handler of ('Data -> unit)
```

So for example you can have a `StreamHandler`, which would handle an Event in a [Stream](#stream).
```fs
type StreamHandler<'Event> = StreamHandler of ('Event -> unit)
```

Handler is just a function, so you need a Service, to _contain_ such handler.
```fs
type StreamListenerService = {
    OnInteractionEvent = StreamHandler<InteractionEvent>
}
```
In `StreamListenerService` record, there now would be a Handler `OnInteractionEvent`, which can handle an `InteractionEvent` from a `Stream` of such event.

## Domain example
```fs
// Common types

type Id = UUID

type Stream<'Event> = Stream of 'Event list
type StreamHandler<'Event> = StreamHandler of ('Event -> unit)

// Types

type InteractionEvent =
    | Confirmation
    | Rejection

type InteractionResult =
    | Accepted
    | Error

type IdentityMatchingSet = {
    Contact: Contact
}

and Contact = {
    Email: Email option
    Phone: Phone option
}

and Email = Email of string
and Phone = Phone of string

type Person =
    | Known of PersonId
    | Unknown

and PersonId = PersonId of Id

// Streams

type InteractionCollectorStream = InteractionCollectorStream of Stream<InteractionEvent>

// Services

type GenericService = Initiator

type InteractionCollector = {
    PostInteraction: InteractionEvent -> InteractionResult
}

type PersonIdentificationEngine = {
    OnInteractionEvent: InteractionEvent -> unit
}

type PersonAggregate = {
    IdentifyPerson: IdentityMatchingSet -> Person
}
```

## Share types between domains
> There is often a situation, where you need to _share_ some types between multiple domains (DTOs, Common Types, ...).

You can use a `#load` key word in `.fsx` file.

**TIP**: You should avoid loading one domain from another, since domains should have its boundaries.

```fs
// common.fsx

type Id = UUID

type Stream<'Event> = Stream of 'Event list
type StreamHandler<'Event> = StreamHandler of ('Event -> unit)
```

```fs
// cars-persons-shared.fsx

#load "common.fsx"
open Common

type CarDto = {
    Id: Id
    Name: string
    Type: string
}

type PersonDto = {
    Id: Id
    Name: string
    Address: string
}
```

```fs
// carsDomain.fsx

#load "cars-persons-share.fsx"
open ``Cars-persons-share``

open Common

type CarFinder = {
    FindCar: Id -> CarDto
}
```

```fs
// personsDomain.fsx

#load "cars-persons-share.fsx"
open ``Cars-persons-share``

open Common

type PersonEvent =
    | BoughtCar of PersonBoughtCarEvent

and PersonBoughtCarEvent = {
    PersonId: Id
    Car: CarDto
}

type PersonStream = PersonStream of Stream<PersonEvent>
```
