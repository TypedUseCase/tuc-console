# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased
- Add `DomainName` to the domain types
    - `Record`
    - `SingleCaseUnion`
    - `DiscriminatedUnion`
    - `Stream`

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
