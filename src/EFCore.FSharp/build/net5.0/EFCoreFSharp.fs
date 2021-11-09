module EFCoreFSharp

open Microsoft.EntityFrameworkCore.Design

[<assembly: DesignTimeServicesReference("EntityFrameworkCore.FSharp.EFCoreFSharpServices, EntityFrameworkCore.FSharp")>]
do ()
