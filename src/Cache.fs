namespace MF.TucConsole

module ConcurrentCache =
    open System.Collections.Concurrent

    type Key<'UniqueData> = Key of 'UniqueData

    [<RequireQualifiedAccess>]
    module Key =
        let value (Key key) = key

    type private Storage<'UniqueData, 'Cache> = ConcurrentDictionary<Key<'UniqueData>, 'Cache>
    type Cache<'UniqueData, 'Cache> = private Cache of Storage<'UniqueData, 'Cache>

    let private create<'UniqueData, 'Cache> () =
        Storage<'UniqueData, 'Cache>()
        |> Cache

    let private setCache (Cache storage) key cache =
        storage.AddOrUpdate(key, cache, fun _ _ -> cache)
        |> ignore

    let private addOrUpdateCache (Cache storage) (update: Key<'a> -> 'b -> 'b -> 'b) key newCache =
        storage.AddOrUpdate(key, newCache, (fun key currentCache -> update key currentCache newCache))
        |> ignore

    let private getCache (Cache storage) key =
        match storage.TryGetValue key with
        | true, cache -> Some cache
        | _ -> None

    let private getAllKeys (Cache storage) =
        storage.Keys

    let private getAllValues (Cache storage) =
        storage.Values

    let private countAll (Cache storage) =
        storage.Count

    [<RequireQualifiedAccess>]
    module Cache =
        let empty = create

        let iter f (Cache storage) =
            storage
            |> Seq.map (fun kv -> kv.Key, kv.Value)
            |> Seq.iter f

        let length = countAll

        let tryFind key storage =
            key |> getCache storage

        let set key value storage =
            value |> setCache storage key

        let update key value update storage =
            value |> addOrUpdateCache storage update key

        let keys storage =
            storage
            |> getAllKeys
            |> List.ofSeq

        let values storage =
            storage
            |> getAllValues
            |> List.ofSeq

        let items storage =
            storage
            |> values
            |> List.zip (storage |> keys)

        let tryRemove key (Cache storage) =
            storage.TryRemove(key)
            |> ignore

        let clear (Cache storage) =
            storage.Clear()
