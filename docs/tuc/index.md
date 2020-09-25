Tuc
===

[Home](/tuc-console/) / Tuc

---

> **T**yped **U**se-**C**ase

## Table of contents
- [Syntax](/tuc-console/tuc/#syntax)
- [Structure](/tuc-console/tuc/#structure)
    - [Tuc Name](/tuc-console/tuc/#tuc-name)
    - [Participants](/tuc-console/tuc/#participants)
    - [Parts](/tuc-console/tuc/#use-case-parts)
- [Example](/tuc-console/tuc/example.html)

## Syntax
> We use a custom syntax to create a [PlantUML](https://plantuml.com/) diagram with ease.

Syntax is meant to be as **light-weight** as possible. It is also a **white-space significant**, so no other boilerplate symbols are needed.

### Indentation
Nested structure is indented by spaces.

**The first indented line** specifies the **indentation level** for the **entire file**.

> You determine number of spaces on your own, but you need to stick with it in the entire file.

```tuc
MainInitiator
    // here is the body (lifeline) of the initiator, currently indented by 4 spaces
```

### Comments
You can comment your tuc file with the simple syntax of `//`.
Everything behind `//` is ignored and the parser won't do anything with it (_at the moment_).

```tuc
// this is comment
```

There are no special multi-line comments.
```tuc
// this
// is
// multi-line
// comment
```

## Structure
Single tuc definition **must** consist of 3 parts:
* [Tuc Name](/tuc-console/tuc/#tuc-name)
* [Participants](/tuc-console/tuc/#participants)
* [Use-case Parts](/tuc-console/tuc/#use-case-parts)

Single tuc definition
```tuc
tuc Example     // tuc name

participants    // participants
    MyService MyDomain

MyService       // use-case parts
    do something
```

Tuc file can contain one or more tuc definitions.

### Tuc name
This is a start of a tuc definition. (_It will be a section in puml result._)

`tuc {NAME OF YOUR TYPED-USE-CASE}`

Example:
```tuc
tuc My use case definition
```

### Participants
All participants of the use case **must** be defined.

**NOTE**: Order in this definition determines the order of participants in puml result.

Their definition **must** start with `participants` key word.
Then there are participants defined as a name of the `Record` (or [`Initiator`](/tuc-console/domain/#initiator)) in the Domain Types and its Domain.

The minimal participants definition.
```tuc
participants
    MyService MyDomainName
```

**NOTE**: Participants are the first indented line(s) in the tuc file, so they determine the indentation level of the entire file.

Read more about [participants here](/tuc-console/tuc/participants.html).

### Use-Case Parts
There **must** be at lease one part of the use-case.

There is currently 13 available parts:
* [Lifeline](/tuc-console/tuc/parts.html#lifeline)
* [Section](/tuc-console/tuc/parts.html#section)
* [Service Method Call](/tuc-console/tuc/parts.html#service-method-call)
* [Post Data](/tuc-console/tuc/parts.html#post-data)
* [Read Data](/tuc-console/tuc/parts.html#read-data)
* [Post Event](/tuc-console/tuc/parts.html#post-event)
* [Read Event](/tuc-console/tuc/parts.html#read-event)
* [Handle Event In Stream](/tuc-console/tuc/parts.html#handle-event-in-stream)
* [Group](/tuc-console/tuc/parts.html#group)
* [If](/tuc-console/tuc/parts.html#if)
* [Loop](/tuc-console/tuc/parts.html#loop)
* [Do](/tuc-console/tuc/parts.html#do)
* [Left Note](/tuc-console/tuc/parts.html#left-note)
* [Note](/tuc-console/tuc/parts.html#note)
* [Right Note](/tuc-console/tuc/parts.html#right-note)

Read more about [parts here](/tuc-console/tuc/parts.html).
