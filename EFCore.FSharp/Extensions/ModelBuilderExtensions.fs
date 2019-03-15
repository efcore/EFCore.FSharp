namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

module Extensions =

    let private genericOptionConverterType = typedefof<OptionConverter<_>>

    type ModelBuilder with

        member this.UseValueConverterForType<'a>(converter : ValueConverter) =
            this.UseValueConverterForType(typeof<'a>, converter)

        member this.UseValueConverterForType(``type`` : Type, converter : ValueConverter) =
            this.Model.GetEntityTypes()
            |> Seq.iter(fun e ->
                let properties = e.ClrType.GetProperties() |> Seq.filter(fun p -> p.PropertyType = ``type``)

                properties
                |> Seq.iter(fun p ->
                    this.Entity(e.Name).Property(p.Name).HasConversion(converter) |> ignore
                )
            )

            this

        member this.UseFSharp () =
            
            let registerOptionTypes () =
            
                let types =
                    this.Model.GetEntityTypes()
                    |> Seq.collect (fun e -> e.ClrType.GetProperties()  |> Seq.filter(fun p -> p.PropertyType = typedefof<Option<_>>))
                    |> Seq.map (fun p -> p.ReflectedType)

                types
                |> Seq.iter(fun t ->

                    let converterType = genericOptionConverterType.MakeGenericType(t)
                    let converter = converterType.GetConstructor([||]).Invoke([||]) :?> ValueConverter

                    this.UseValueConverterForType(t, converter) |> ignore
                )

            registerOptionTypes ()            
            