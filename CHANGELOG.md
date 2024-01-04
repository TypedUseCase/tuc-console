# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased

## 1.7.0 - 2023-01-04
- Use net8.0
- Update dependencies

## 1.6.0 - 2021-08-12
- Update dependencies

## 1.5.0 - 2021-06-10
- Use net5.0
- Use F# 5.0
- Update dependencies

## 1.4.0 - 2020-11-13
- Add more information to parsed tuc types
- Add diagnostics to tuc parse
- Use `Tuc.Parser` as an external library
- Update `Tuc.Parser`

## 1.3.0 - 2020-11-03
- Use `Tuc.DomainResolver` as an external library
- Use `Tuc.` namespace for the whole application
- Parse only `.fsx` files with Domain types (it must end with `Domain.fsx`).

## 1.2.0 - 2020-09-25
- Fix wrong error occurrence when there is undefined participant in component.
- Allow to generate a dir with all sub tucs of a multi-tuc file.
- Enhance an output for generating tuc
- Generate method arguments of service-method-call as multiline, when there are more than one
- Add tooltip for post/read events with longer path
- Allow to use FQ name for data and use deeper data name in read/post data

## 1.1.0 - 2020-09-21
- Allow Modules, Functions, etc in Domain files.
- Do not track `://` in links as `//` comments
- Allow all supported image output formats
- Allow to generate multiple tuc files at once by `tuc:generate` command

## 1.0.0 - 2020-09-03
- Fix `style` option of `tuc:generate` command
- Add `DataObject` as participant

## 0.3.0 - 2020-09-03
- Add `DomainName` to the domain types
    - `Record`
    - `SingleCaseUnion`
    - `DiscriminatedUnion`
    - `Stream`
- Parse/Check `Domain` of participants in tuc
    - Component must have domain and it's participants must be in the same domain
- Change tuc comment back to `//` from `#`, so it won't conflict with `#colorHash` in notes, etc.
- Transform *italic* in tuc files
- Fix Read/Post event by FQ name for events with only one case
- Show only last event in path in puml result.
- Allow to style puml
- Remove internal cache in Domain Resolver

## 0.2.0 - 2020-09-01
- Parse left and right notes in tuc
- Add tuc part
    - `ReadEvent` from stream
- Show better error message for undefined component field
- Rename a `only-parse` (`-p`) option to `only-resolved` (`-r`) in `domain:check` command
- Add domain type for `Handler`
- Allow to use FQ name for events and use deeper event name
- Allow to check multiple .tuc files in a dir at once
- Parse .tuc with more errors, if possible
- Make a `Do` as a `hnote` in `puml`
- Change tuc comment from `//` to `#`, so it won't conflict with `//italic//` in notes, etc.

## 0.1.0 - 2020-08-12
- Initial implementation
