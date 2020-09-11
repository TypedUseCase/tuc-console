TUC
===

## TUC
> **T**yped **U**se-**C**ase

![tuc-logo](https://raw.githubusercontent.com/MortalFlesh/tuc-console/master/docs/assets/tuc-logo.png)

It is basically a use case definition, for which this console application can generate [PlantUML](https://plantuml.com/) diagram, where all services are domain specific type safe.

## Motivation
We have a DDD based micro service architecture, where most of the services have an asynchronous communication between them (mostly through event streams) with a domain specific ubiquitous language.

And we need to document the use-cases done by those services.

For now, we use a [PlantUML](https://plantuml.com/) directly, but it is **a lot** of work, so we decided to create a *language* to help us with that - **TUC**.

## Table of Contents
- [Domain](/tuc-console/domain/)
    - [How does it work?](/tuc-console/domain/#how-does-it-work)
    - [Common Types](/tuc-console/domain/#common-types)
    - [Domain example](/tuc-console/domain/#domain-example)
    - [Share types between domains](/tuc-console/domain/#share-types-between-domains)
- [TUC](/tuc-console/tuc/)
    - [Syntax](/tuc-console/tuc/#syntax)
    - [Structure](/tuc-console/tuc/#structure)
    - [Example](/tuc-console/tuc/example.html)
