namespace EntityFrameworkCore.FSharp

open System
open System.Globalization
open System.Reflection

module ToStringInstanceHelper =


    let private formatProviderField: IFormatProvider = CultureInfo.InvariantCulture

    let ToStringWithCulture (objectToConvert: #obj) =

        if isNull objectToConvert then
            raise
            <| ArgumentNullException("objectToConvert")

        let t = objectToConvert.GetType()
        let method = t.GetMethod("ToString", [| typeof<IFormatProvider> |])

        if isNull method then
            objectToConvert.ToString()
        else
            method.Invoke(objectToConvert, [| formatProviderField :> obj |]) :?> string
