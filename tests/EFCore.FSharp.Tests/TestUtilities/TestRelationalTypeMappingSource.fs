namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open System
open System.Collections.Generic
open System.Data
open System.Linq
open System.Linq.Expressions
open Microsoft.EntityFrameworkCore.ChangeTracking
open Microsoft.EntityFrameworkCore.Storage

type internal IntArrayTypeMapping =
    inherit RelationalTypeMapping 

    new () = {
        inherit RelationalTypeMapping(
            "some_int_array_mapping",
            (typeof<int[]>)
        )
    }

    new (parameters) = { inherit RelationalTypeMapping(parameters) }

    override this.Clone parameters =
        parameters |> IntArrayTypeMapping :> RelationalTypeMapping

type internal TestStringTypeMapping (storeType, dbType, unicode, size, fixedLength) =
    inherit StringTypeMapping(
        storeType,
        dbType,
        unicode,
        size
    )


type TestRelationalTypeMappingSource(dependencies, relationalDependencies) =
    inherit RelationalTypeMappingSource(dependencies, relationalDependencies)

    

    let _string = StringTypeMapping("just_string(2000)") :> RelationalTypeMapping

    let _binary = ByteArrayTypeMapping("just_binary(max)", dbType = Nullable<DbType>(DbType.Binary)) :> RelationalTypeMapping

    let _rowversion = ByteArrayTypeMapping("rowversion", dbType = Nullable<DbType>(DbType.Binary), size = Nullable<int>(8)) :> RelationalTypeMapping

    let _defaultIntMapping = IntTypeMapping("default_int_mapping", dbType = Nullable<DbType>(DbType.Int32)) :> RelationalTypeMapping

    let _defaultLongMapping = LongTypeMapping("default_long_mapping", dbType = Nullable<DbType>(DbType.Int64)) :> RelationalTypeMapping

    let _defaultShortMapping = ShortTypeMapping("default_short_mapping", dbType = Nullable<DbType>(DbType.Int16)) :> RelationalTypeMapping

    let _defaultByteMapping = ByteTypeMapping("default_byte_mapping", dbType = Nullable<DbType>(DbType.Byte)) :> RelationalTypeMapping

    let _defaultBoolMapping = BoolTypeMapping("default_bool_mapping") :> RelationalTypeMapping

    let _someIntMapping = IntTypeMapping("some_int_mapping") :> RelationalTypeMapping

    let _intArray = IntArrayTypeMapping() :> RelationalTypeMapping

    let _defaultDecimalMapping = DecimalTypeMapping("default_decimal_mapping") :> RelationalTypeMapping

    let _defaultDateTimeMapping = DateTimeTypeMapping("default_datetime_mapping", dbType = Nullable<DbType>(DbType.DateTime2)) :> RelationalTypeMapping

    let _defaultDoubleMapping = DoubleTypeMapping("default_double_mapping") :> RelationalTypeMapping

    let _defaultDateTimeOffsetMapping = DateTimeOffsetTypeMapping("default_datetimeoffset_mapping") :> RelationalTypeMapping

    let _defaultFloatMapping = FloatTypeMapping("default_float_mapping") :> RelationalTypeMapping

    let _defaultGuidMapping = GuidTypeMapping("default_guid_mapping") :> RelationalTypeMapping

    let _defaultTimeSpanMapping = TimeSpanTypeMapping("default_timespan_mapping") :> RelationalTypeMapping

    let _simpleMappings =
        [
            (typeof<int>, _defaultIntMapping)
            (typeof<Int64>, _defaultLongMapping)
            (typeof<DateTime>, _defaultDateTimeMapping)
            (typeof<Guid>, _defaultGuidMapping)
            (typeof<bool>, _defaultBoolMapping)
            (typeof<byte>, _defaultByteMapping)
            (typeof<double>, _defaultDoubleMapping)
            (typeof<DateTimeOffset>, _defaultDateTimeOffsetMapping)
            (typeof<char>, _defaultIntMapping)
            (typeof<Int16>, _defaultShortMapping)
            (typeof<float>, _defaultFloatMapping)
            (typeof<decimal>, _defaultDecimalMapping)
            (typeof<TimeSpan>, _defaultTimeSpanMapping)
            (typeof<string>, _string)
            (typeof<int[]>, _intArray)
        ] |> dict :?> IReadOnlyDictionary<Type, RelationalTypeMapping>

    let _simpleNameMappings =
        [
            ("some_int_mapping", _someIntMapping)
            ("some_string(max)", _string)
            ("some_binary(max)", _binary)
            ("money", _defaultDecimalMapping)
            ("dec", _defaultDecimalMapping)
        ] |> dict :?> IReadOnlyDictionary<string, RelationalTypeMapping>

    override this.FindMapping (mappingInfo : inref<RelationalTypeMappingInfo>) =
        let clrType = mappingInfo.ClrType
        let isClrTypeNull = isNull clrType

        let storeTypeName = mappingInfo.StoreTypeName
        let isStoreTypeNameNull = isNull storeTypeName
        
        match clrType with
        | t when t = typeof<string> ->
            let isAnsi = mappingInfo.IsUnicode.GetValueOrDefault();
            let isFixedLength = mappingInfo.IsFixedLength.HasValue && mappingInfo.IsFixedLength.Value;
            let baseName =
                match isAnsi, isFixedLength with
                | true, true -> "ansi_string_fixed"
                | true, false -> "ansi_string"
                | false, true -> "just_string_fixed"
                | false, false -> "just_string"

            let size =
                if mappingInfo.Size.HasValue then
                    mappingInfo.Size
                else
                    if mappingInfo.IsKeyOrIndex then
                        let s = if isAnsi then 900 else 450
                        s |> Nullable
                    else
                        Nullable<int>()
 
            let name =
                if isStoreTypeNameNull then
                    let sizeStr = if size.HasValue then string size.Value else "max"
                    sprintf "%s(%s)" baseName sizeStr
                else
                    storeTypeName

            let dbType =
                if isAnsi then
                    Nullable<DbType>(DbType.AnsiString)
                else
                    Nullable<DbType>()

            TestStringTypeMapping(
                name,
                dbType,
                (not isAnsi),
                size,
                isFixedLength) :> RelationalTypeMapping
            
        | t when t = typeof<byte[]> ->
            if mappingInfo.IsRowVersion.GetValueOrDefault() then
                _rowversion
            else
                let size =
                    if mappingInfo.Size.HasValue then
                        mappingInfo.Size
                    else
                        if mappingInfo.IsKeyOrIndex then
                            Nullable<int>(900)
                        else
                            Nullable<int>()

                let name =
                    if isNull storeTypeName then
                        let sizeStr =
                            if size.HasValue then string size.Value else "max"
                        sprintf "just_binary(%s)" sizeStr
                    else
                        storeTypeName

                ByteArrayTypeMapping(name, Nullable<DbType>(DbType.Binary), size) :> RelationalTypeMapping

        
        | _ ->

            let success, mapping = _simpleMappings.TryGetValue clrType

            match (isClrTypeNull, success) with
            | false, true ->
                if (not isStoreTypeNameNull) && not (mapping.StoreType.Equals(storeTypeName, StringComparison.Ordinal)) then
                    mapping.Clone(storeTypeName, mapping.Size)
                else
                    mapping
            | _ ->

                let successFromName, mappingFromName = _simpleNameMappings.TryGetValue storeTypeName
                if (not isStoreTypeNameNull) && (clrType = null || mappingFromName.ClrType = clrType) && successFromName then
                    mappingFromName
                else
                    null
