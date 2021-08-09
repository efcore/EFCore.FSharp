namespace EntityFrameworkCore.FSharp

open System
open System.Reflection
open System.Text
open Microsoft.FSharp.Reflection

module internal rec SharedTypeExtensions =

    let builtInTypeNames =
        [
            (typeof<bool>, "bool")
            (typeof<byte>, "byte")
            (typeof<sbyte>, "sbyte")
            (typeof<char>, "char")
            (typeof<int16>, "Int16")
            (typeof<int>, "int")
            (typeof<int64>, "Int64")
            (typeof<uint16>, "UInt16")
            (typeof<uint32>, "UInt32")
            (typeof<uint64>, "UInt64")
            (typeof<decimal>, "decimal")
            (typeof<float>, "float")
            (typeof<double>, "double")
            (typeof<string>, "string")
            (typeof<obj>, "obj")
        ]
        |> readOnlyDict

    let processType (t:Type) useFullName (sb:StringBuilder) =
        if t.IsGenericType then
            let genericArguments = t.GetGenericArguments()
            processGenericType t genericArguments (genericArguments.Length) useFullName sb
        elif t.IsArray then
            processArrayType t useFullName sb
        else
            match builtInTypeNames.TryGetValue t with
            | (true, builtInName) -> sb.Append(builtInName)
            | _ ->
                let name = if useFullName then t.FullName else t.Name
                sb.Append(name)

    let rec processGenericType t genericArguments length useFullName (sb:StringBuilder) =
        let offset = if t.IsNested then t.DeclaringType.GetGenericArguments().Length else 0

        if useFullName then
            if t.IsNested then
                processGenericType t.DeclaringType genericArguments offset useFullName sb |> ignore
                sb.Append("+") |> ignore
            else
                sb.Append(t.Namespace).Append(".") |> ignore


        let genericPartIndex = t.Name.IndexOf("`")

        if genericPartIndex <= 0 then
            sb.Append(t.Name)
        else
            sb.Append(t.Name, 0, genericPartIndex).Append("<") |> ignore

            for i = offset to length do
                processType genericArguments.[i] useFullName sb |> ignore
                if (i+1) <> length then
                    sb.Append(',')  |> ignore
                    if (not (genericArguments.[i+1].IsGenericParameter)) then
                        sb.Append(' ') |> ignore

            sb.Append(">")

    let processArrayType (t:Type) useFullName (sb:StringBuilder) =
        let mutable innerType = t
        while (innerType.IsArray) do
            innerType <- innerType.GetElementType()

        processType innerType useFullName sb |> ignore

        while (innerType.IsArray) do
            sb
                .Append('[')
                .Append(',', innerType.GetArrayRank() - 1)
                .Append(']')  |> ignore
            innerType <- innerType.GetElementType()
        sb

    let rec getNamespaces (t: Type) =
        seq {
            if builtInTypeNames.ContainsKey(t) |> not then
                yield t.Namespace

                if t.IsGenericType then
                    for typeArgument in t.GenericTypeArguments do
                        for ns in (getNamespaces typeArgument) do
                            yield ns
        }

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
            if nullable && t.IsValueType then
                typedefof<Nullable<_>>.MakeGenericType(t)
            else unwrapNullableType t

    let makeOptional (optional : bool) (t : Type) =
        if isOptionType t = optional then
            t
        else
            if optional then
                typedefof<Option<_>>.MakeGenericType(t)
            else unwrapOptionType t

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

    let isSignedInteger (t:Type) =
        let t' = t |> unwrapNullableType |> unwrapOptionType
        t' = typeof<int>
            || t' = typeof<int64>
            || t' = typeof<int16>
            || t' = typeof<sbyte>

    let displayName (t:Type) useFullName =
        let sb = StringBuilder()
        processType t useFullName sb |> ignore
        sb.ToString()

    let isSingleCaseUnion t =
      FSharpType.IsUnion t
      && FSharpType.GetUnionCases(t)
         |> Array.length
         |> ((=)1)

    let unwrapSingleCaseUnion t =
      let case = FSharpType.GetUnionCases(t)
                 |> Array.exactlyOne
      let field = case.GetFields() |> Array.head
      field.PropertyType
