module EntityFrameworkCore.FSharp.DbContextHelpers

open System.Linq
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore
open System.Threading.Tasks

let private awaitValueTask (x: ValueTask<_>) = Async.AwaitTask (x.AsTask())

let findEntity<'a when 'a : not struct> (ctx: DbContext) (key: obj)=
    ctx.Set<'a>().Find(key)

let tryFindEntity<'a when 'a : not struct> (ctx: DbContext) (key: obj)=
    let result = findEntity<'a> ctx key
    if isNull (box result) then None else Some result

let findEntityAsync<'a when 'a : not struct> (ctx: DbContext) (key: obj) =
    async {
        return! ctx.Set<'a>().FindAsync(key) |> awaitValueTask
    }

let tryFindEntityAsync<'a when 'a : not struct> (ctx: DbContext) (key: obj) =
    async {
        let! result = findEntityAsync<'a> ctx key
        return if isNull (box result) then None else Some result
    }

/// Helper method for saving an updated record type
let updateEntity (ctx: DbContext) (key: 'a -> obj) (entity : 'a when 'a : not struct) =
    let currentEntity = findEntity<'a> ctx (key entity)
    ctx.Entry(currentEntity).CurrentValues.SetValues(entity :> obj)
    entity

let updateEntityAsync (ctx: DbContext) (key: 'a -> obj) (entity : 'a when 'a : not struct) =
    async {
        return updateEntity ctx key entity
    }

let updateEntityRange  (ctx: DbContext) (key: 'a -> obj) (entities : 'a seq when 'a : not struct) =
    entities |> Seq.map(fun e -> updateEntity ctx key e)

let updateEntityRangeAsync  (ctx: DbContext) (key: 'a -> obj) (entities : 'a seq when 'a : not struct) =
    async {
        return updateEntityRange ctx key entities
    }

let saveChanges (ctx: DbContext) =
    ctx.SaveChanges()

let saveChangesAsync (ctx: DbContext) =
    async {
        return! ctx.SaveChangesAsync() |> Async.AwaitTask
    }

let addEntity (ctx: DbContext) (entity : 'a when 'a : not struct) =
    ctx.Set<'a>().Add entity

let addEntityAsync (ctx: DbContext) (entity : 'a when 'a : not struct) =
    async {
        return! ctx.Set<'a>().AddAsync(entity) |> awaitValueTask
    }

let addEntityRange (ctx: DbContext) (entities : 'a seq when 'a : not struct) =
    ctx.Set<'a>().AddRange entities

let addEntityRangeAsync (ctx: DbContext) (entities : 'a seq when 'a : not struct) =
    async {
        return! ctx.Set<'a>().AddRangeAsync(entities) |> Async.AwaitTask
    }

let attachEntity (ctx: DbContext) (entity : 'a when 'a : not struct) =
    ctx.Set<'a>().Attach entity

let attachEntityRange (ctx: DbContext) (entities : 'a seq when 'a : not struct) =
    ctx.Set<'a>().AttachRange(entities)

let removeEntity (ctx: DbContext) (entity : 'a when 'a : not struct) =
    ctx.Set<'a>().Remove entity

let removeEntityRange (ctx: DbContext) (entities : 'a seq when 'a : not struct) =
    ctx.Set<'a>().RemoveRange(entities)


let toListAsync (dbset: #IQueryable<_>) = async {
    let! list = dbset.ToListAsync() |> Async.AwaitTask
    return list |> List.ofSeq
}

let tryFirstAsync (dbset: #IQueryable<_>) = async {
    let! ret = dbset.FirstOrDefaultAsync() |> Async.AwaitTask
    return FSharpUtilities.OptionOfNullableObj ret
}

let tryFirst (dbset: #IQueryable<_>) =
    dbset.FirstOrDefault() |> FSharpUtilities.OptionOfNullableObj

let tryFilterFirstAsync predicate (dbSet: #IQueryable<_>) = async {
    let pred = FSharpUtilities.exprToLinq predicate
    let! ret = dbSet.FirstOrDefaultAsync(predicate = pred) |> Async.AwaitTask
    return FSharpUtilities.OptionOfNullableObj ret
}

let tryFilterFirst predicate (dbSet: #IQueryable<_>) =
    let pred = FSharpUtilities.exprToLinq predicate
    let ret = dbSet.FirstOrDefault(predicate = pred)
    FSharpUtilities.OptionOfNullableObj ret

type IQueryable<'T> with
    member this.TryFirstAsync () = this |> tryFirstAsync
    member this.TryFirst () = this |> tryFirst
    member this.TryFirstAsync expr = async {
        let! ret = this.FirstOrDefaultAsync(predicate = expr) |> Async.AwaitTask
        return FSharpUtilities.OptionOfNullableObj ret
    }
    member this.TryFirst expr =
        this.FirstOrDefault(predicate = expr)
         |> FSharpUtilities.OptionOfNullableObj
