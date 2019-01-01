namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Reflection

module SharedTypeExtensions =
    
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
        match t |> isNullableType with
        | true -> t |> Nullable.GetUnderlyingType
        | false -> t

    let unwrapOptionType (t:Type) =
        match t |> isOptionType with
        | true -> t.GenericTypeArguments.[0]
        | false -> t

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
            match isNullable with
            | true -> unwrapNullableType t
            | false -> t

        if not (underlyingNonNullableType.GetTypeInfo()).IsEnum then
            t
        else
            let underlyingEnumType = Enum.GetUnderlyingType(underlyingNonNullableType)
            match isNullable with
            | true -> makeNullable true underlyingEnumType
            | false -> underlyingEnumType

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

