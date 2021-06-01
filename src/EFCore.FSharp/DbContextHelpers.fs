module EntityFrameworkCore.FSharp.DbContextHelpers

open System.Linq
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore
open System.Threading.Tasks

let private awaitValueTask (x: ValueTask<_>) = Async.AwaitTask(x.AsTask())

let findEntity<'a when 'a: not struct> (ctx: DbContext) (key: obj) = ctx.Set<'a>().Find(key)

let tryFindEntity<'a when 'a: not struct> (ctx: DbContext) (key: obj) =
    let result = findEntity<'a> ctx key
    if isNull (box result) then None else Some result

let findEntityAsync<'a when 'a: not struct> (ctx: DbContext) (key: obj) =
    async { return! ctx.Set<'a>().FindAsync(key) |> awaitValueTask }

let findEntityTaskAsync<'a when 'a: not struct> (ctx: DbContext) (key: obj) = ctx.Set<'a>().FindAsync(key)

let tryFindEntityAsync<'a when 'a: not struct> (ctx: DbContext) (key: obj) =
    async {
        let! result = findEntityAsync<'a> ctx key
        return if isNull (box result) then None else Some result
    }

let tryFindEntityTaskAsync<'a when 'a: not struct> (ctx: DbContext) (key: obj) =
    let result = findEntityTaskAsync<'a> ctx key

    result
        .AsTask()
        .ContinueWith(fun (t: Task<'a>) -> if isNull (box t.Result) then None else Some t.Result)


/// Helper method for saving an updated record type
let updateEntity (ctx: DbContext) (key: 'a -> 'b) (entity: 'a when 'a: not struct) =
    let currentEntity = findEntity<'a> ctx (key entity)

    ctx
        .Entry(currentEntity)
        .CurrentValues.SetValues(entity :> obj)

    entity

let updateEntityAsync (ctx: DbContext) (key: 'a -> obj) (entity: 'a when 'a: not struct) =
    async { return updateEntity ctx key entity }

let updateEntityRange (ctx: DbContext) (key: 'a -> obj) (entities: 'a seq when 'a: not struct) =
    entities
    |> Seq.map (fun e -> updateEntity ctx key e)

let updateEntityRangeAsync (ctx: DbContext) (key: 'a -> obj) (entities: 'a seq when 'a: not struct) =
    async { return updateEntityRange ctx key entities }

let saveChanges' (ctx: #DbContext) = ctx.SaveChanges()

let saveChanges ctx = saveChanges' ctx |> ignore

let saveChangesAsync' (ctx: #DbContext) =
    async { return! ctx.SaveChangesAsync() |> Async.AwaitTask }

let saveChangesAsync ctx = saveChangesAsync' ctx |> Async.Ignore

let saveChangesTaskAsync' (ctx: #DbContext) = ctx.SaveChangesAsync()
let saveChangesTaskAsync ctx = saveChangesTaskAsync' ctx :> Task

let addEntity' (ctx: #DbContext) (entity: 'a when 'a: not struct) = ctx.Set<'a>().Add entity

let addEntity ctx entity = addEntity' ctx entity |> ignore


let addEntityAsync' (ctx: #DbContext) (entity: 'a when 'a: not struct) =
    async { return! ctx.Set<'a>().AddAsync(entity) |> awaitValueTask }

let addEntityAsync ctx entity =
    addEntityAsync' ctx entity |> Async.Ignore

let addEntityTaskAsync' (ctx: #DbContext) (entity: 'a when 'a: not struct) = ctx.Set<'a>().AddAsync(entity).AsTask()
let addEntityTaskAsync ctx entity = (addEntityTaskAsync' ctx entity) :> Task

let addEntityRange' (ctx: #DbContext) (entities: 'a seq when 'a: not struct) = ctx.Set<'a>().AddRange entities

let addEntityRange (ctx: #DbContext) (entities: 'a seq when 'a: not struct) = addEntityRange' ctx entities |> ignore

let addEntityRangeAsync' (ctx: #DbContext) (entities: 'a seq when 'a: not struct) =
    async {
        return!
            ctx.Set<'a>().AddRangeAsync(entities)
            |> Async.AwaitTask
    }

let addEntityRangeAsync (ctx: #DbContext) (entities: 'a seq when 'a: not struct) =
    addEntityRangeAsync' ctx entities |> Async.Ignore

let attachEntity' (ctx: #DbContext) (entity: 'a when 'a: not struct) = ctx.Set<'a>().Attach entity
let attachEntity (ctx: #DbContext) (entity: 'a when 'a: not struct) = attachEntity' ctx entity |> ignore

let attachEntityRange' (ctx: #DbContext) (entities: 'a seq when 'a: not struct) = ctx.Set<'a>().AttachRange(entities)

let attachEntityRange (ctx: #DbContext) (entities: 'a seq when 'a: not struct) =
    attachEntityRange' ctx entities |> ignore

let removeEntity' (ctx: #DbContext) (entity: 'a when 'a: not struct) = ctx.Set<'a>().Remove entity
let removeEntity (ctx: #DbContext) (entity: 'a when 'a: not struct) = removeEntity' ctx entity |> ignore

let removeEntityRange' (ctx: #DbContext) (entities: 'a seq when 'a: not struct) = ctx.Set<'a>().RemoveRange(entities)

let removeEntityRange (ctx: #DbContext) (entities: 'a seq when 'a: not struct) =
    removeEntityRange' ctx entities |> ignore

let toListAsync (dbset: #IQueryable<_>) =
    async {
        let! list = dbset.ToListAsync() |> Async.AwaitTask
        return list |> List.ofSeq
    }

let toListTaskAsync (dbset: #IQueryable<_>) = dbset.ToListAsync()


let tryFirstAsync (dbset: #IQueryable<_>) =
    async {
        let! ret = dbset.FirstOrDefaultAsync() |> Async.AwaitTask
        return FSharpUtilities.OptionOfNullableObj ret
    }

let tryFirstTaskAsync dbset = tryFirstAsync dbset |> Async.StartAsTask


let tryFirst (dbset: #IQueryable<_>) =
    dbset.FirstOrDefault()
    |> FSharpUtilities.OptionOfNullableObj

let tryFilterFirstAsync predicate (dbSet: #IQueryable<_>) =
    async {
        let pred = FSharpUtilities.exprToLinq predicate

        let! ret =
            dbSet.FirstOrDefaultAsync(predicate = pred)
            |> Async.AwaitTask

        return FSharpUtilities.OptionOfNullableObj ret
    }

let tryFilterFirstTaskAsync predicate (dbSet: #IQueryable<_>) =
    tryFilterFirstAsync predicate dbSet
    |> Async.StartAsTask

let tryFilterFirst predicate (dbSet: #IQueryable<_>) =
    let pred = FSharpUtilities.exprToLinq predicate
    let ret = dbSet.FirstOrDefault(predicate = pred)
    FSharpUtilities.OptionOfNullableObj ret

type IQueryable<'T> with
    member this.TryFirstAsync() = this |> tryFirstAsync
    member this.TryFirstTaskAsync() = this |> tryFirstTaskAsync
    member this.TryFirst() = this |> tryFirst

    member this.TryFirstAsync expr =
        async {
            let! ret =
                this.FirstOrDefaultAsync(predicate = expr)
                |> Async.AwaitTask

            return FSharpUtilities.OptionOfNullableObj ret
        }

    member this.TryFirstTaskAsync expr =
        this.TryFirstAsync(expr) |> Async.StartAsTask

    member this.TryFirst expr =
        this.FirstOrDefault(predicate = expr)
        |> FSharpUtilities.OptionOfNullableObj
