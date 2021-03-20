module EntityFrameworkCore.FSharp.DbContextHelpers

open Microsoft.EntityFrameworkCore

/// Helper method for saving an updated record type
let updateEntity (ctx: #DbContext) (key: 'a -> obj) (entity : 'a when 'a : not struct) =
    let currentEntity = ctx.Set<'a>("").Find (key entity)
    ctx.Entry(currentEntity).CurrentValues.SetValues(entity :> obj)
    ctx.SaveChanges() |> ignore
    entity
