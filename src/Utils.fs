namespace Tuc.Console

[<RequireQualifiedAccess>]
module FileSystem =
    open System.IO

    let private writeContent (writer: StreamWriter) content =
        writer.WriteLine(sprintf "%s" content)

    let writeSeqToFile (filePath: string) (data: string seq) =
        File.WriteAllLines(filePath, data)

    let writeToFile (filePath: string) data =
        File.WriteAllText(filePath, data)

    let appendToFile (filePath: string) data =
        File.AppendAllText(filePath, data)

    let readLines (filePath: string) =
        File.ReadAllLines(filePath)
        |> Seq.toList

    let readContent (filePath: string) =
        File.ReadAllText(filePath)

    let tryReadContent (filePath: string) =
        if File.Exists filePath then File.ReadAllText(filePath) |> Some
        else None

    let getAllDirs = function
        | [] -> []
        | directories -> directories |> List.collect (Directory.EnumerateDirectories >> List.ofSeq)

    let rec getAllFiles = function
        | [] -> []
        | directories -> [
            yield! directories |> Seq.collect Directory.EnumerateFiles
            yield! directories |> Seq.collect Directory.EnumerateDirectories |> List.ofSeq |> getAllFiles
        ]

[<RequireQualifiedAccess>]
module Option =
    module Operators =
        let (=>) key value = (key, value)

[<RequireQualifiedAccess>]
module String =
    open System

    let toLower (value: string) =
        value.ToLower()

    let ucFirst (value: string) =
        match value |> Seq.toList with
        | [] -> ""
        | first :: rest -> (string first).ToUpper() :: (rest |> List.map string) |> String.concat ""

    let split (separator: string) (value: string) =
        value.Split(separator) |> Seq.toList

    let replaceAll (replace: string list) replacement (value: string) =
        replace
        |> List.fold (fun (value: string) toRemove ->
            value.Replace(toRemove, replacement)
        ) value

    let remove toRemove = replaceAll toRemove ""

    let append suffix string =
        sprintf "%s%s" string suffix

    let trimEnd (char: char) (string: string) =
        string.TrimEnd char

    let trimStart (char: char) (string: string) =
        string.TrimStart char

    let trim (char: char) (string: string) =
        string.Trim char

    let contains (subString: string) (string: string) =
        string.Contains(subString)

    let startsWith (prefix: string) (string: string) =
        string.StartsWith(prefix)

    let (|IsEmpty|_|): string -> _ = function
        | empty when empty |> String.IsNullOrEmpty -> Some ()
        | _ -> None

[<RequireQualifiedAccess>]
module Directory =
    open System.IO

    let ensure (path: string) =
        if path |> Directory.Exists |> not then Directory.CreateDirectory(path) |> ignore

[<RequireQualifiedAccess>]
module Path =
    open System.IO

    let fileName = String.split "/" >> List.rev >> List.head

    let fileNameWithoutExtension: string -> string = Path.GetFileNameWithoutExtension

    let dirName path =
        let file = path |> fileName
        path.Substring(0, path.Length - file.Length)

    module Operators =
        let (/) a b = Path.Combine(a, b)

[<AutoOpen>]
module Regexp =
    open System.Text.RegularExpressions

    // http://www.fssnip.net/29/title/Regular-expression-active-pattern
    let (|Regex|_|) pattern input =
        let m = Regex.Match(input, pattern)
        if m.Success then Some (List.tail [ for g in m.Groups -> g.Value ])
        else None

[<RequireQualifiedAccess>]
module List =
    /// see https://stackoverflow.com/questions/32363848/fastest-way-to-reduce-a-list-based-on-another-list-using-f
    let filterNotIn excluding list =
        let toExclude = set excluding
        list |> List.filter (toExclude.Contains >> not)

    let filterNotInBy f excluding list =
        let toExclude = set excluding
        list |> List.filter (f >> toExclude.Contains >> not)

    let filterInBy f including list =
        let toInclude = set including
        list |> List.filter (f >> toInclude.Contains)

    let formatLines linePrefix f = function
        | [] -> ""
        | lines ->
            let newLineWithPrefix = "\n" + linePrefix

            lines
            |> List.map f
            |> String.concat newLineWithPrefix
            |> (+) newLineWithPrefix

    let formatAvailableItems onEmpty onItems wantedItem definedItems =
        let normalizeItem =
            String.toLower

        let similarDefinedItem =
            definedItems
            |> List.tryFind (normalizeItem >> (=) (wantedItem |> normalizeItem))

        let availableItems =
            definedItems
            |> List.map (function
                | similarItem when (Some similarItem) = similarDefinedItem -> sprintf "%s  <--- maybe this one here?" similarItem
                | item -> item
            )

        match availableItems with
        | [] -> onEmpty
        | items -> items |> onItems

    /// It splits a list by a true/false result of the given function, when the first false occures, it will left all other items in false branch
    /// Example: [ 2; 4; 6; 7; 8; 9; 10 ] |> List.splitBy isEven results in ( [ 2; 4; 6 ], [ 7; 8; 9; 10 ] )
    let splitBy f list =
        let rec splitter trueBranch falseBranch f = function
            | [] -> trueBranch |> List.rev, falseBranch
            | i :: rest ->
                let trueBranch, falseBranch, rest =
                    if i |> f
                        then i :: trueBranch, falseBranch, rest
                        else trueBranch, falseBranch @ i :: rest, []

                rest |> splitter trueBranch falseBranch f

        list |> splitter [] [] f

[<RequireQualifiedAccess>]
module Map =
    let keys map =
        map
        |> Map.toList
        |> List.map fst

[<AutoOpen>]
module Utils =
    let tee f a =
        f a
        a
