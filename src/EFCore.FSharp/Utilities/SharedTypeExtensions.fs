namespace EntityFrameworkCore.FSharp

open System
open System.Reflection

module internal SharedTypeExtensions =
    
    let isValidEntityType (t:Type) =
            t.GetTypeInfo().IsClass

    let isNullableType (t:Type) =
        let typeInfo = t.GetTypeInfo()
        typeInfo.IsGenericType
        && typeInfo.GetGenericTypeDefinition() = typedefof<Nullable<_>>

    let isOptionType (t:Type) =
        let typeInfo = t.GetTypeInfo()
        typeInfo.IsGenericType
        && typeInfo.GetGenericTypeDefinition() = typedefof<Option<_>>

    let unwrapNullableType (t:Type) =
        if isNullableType t then Nullable.GetUnderlyingType t else t

    let unwrapOptionType (t:Type) =
        if isOptionType t then t.GenericTypeArguments.[0] else t

    let makeNullable (nullable : bool) (t : Type) =
        if isNullableType t = nullable then
            t
        else
            if nullable then
                typedefof<Nullable<_>>.MakeGenericType(t)
            else unwrapNullableType t

    let unwrapEnumType (t:Type) =
        let isNullable = isNullableType t

        let underlyingNonNullableType =
            if isNullable then unwrapNullableType t else t

        if not (underlyingNonNullableType.GetTypeInfo()).IsEnum then
            t
        else
            let underlyingEnumType = Enum.GetUnderlyingType(underlyingNonNullableType)
            if isNullable then
                makeNullable true underlyingEnumType
            else
                underlyingEnumType

    let isInteger (t:Type) = 
        let t' = t |> unwrapNullableType |> unwrapOptionType
        t' = typeof<int>
            || t' = typeof<int64>
            || t' = typeof<int16>
            || t' = typeof<byte>
            || t' = typeof<uint32>
            || t' = typeof<uint64>
            || t' = typeof<uint16>
            || t' = typeof<sbyte>
            || t' = typeof<char>

    let isNumeric (t:Type) =
        let t' = t |> unwrapNullableType |> unwrapOptionType

        (isInteger t')
            || t' = typeof<decimal>
            || t' = typeof<float>
            || t' = typeof<float32>

