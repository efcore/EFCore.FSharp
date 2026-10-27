using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.FSharp.Test.TestUtilities;

internal class IntArrayTypeMapping : RelationalTypeMapping
{
    public IntArrayTypeMapping() : base("some_int_array_mapping", typeof(int[])) { }
    private IntArrayTypeMapping(RelationalTypeMappingParameters parameters) : base(parameters) { }
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new IntArrayTypeMapping(parameters);
}

internal class TestStringTypeMapping : StringTypeMapping
{
    public TestStringTypeMapping(string storeType, DbType? dbType, bool unicode, int? size, bool fixedLength)
        : base(storeType, dbType, unicode, size) { }
}

public class TestRelationalTypeMappingSource : RelationalTypeMappingSource
{
    private readonly RelationalTypeMapping _string =
        new StringTypeMapping("just_string(2000)", null);

    private readonly RelationalTypeMapping _binary =
        new ByteArrayTypeMapping("just_binary(max)", dbType: DbType.Binary);

    private readonly RelationalTypeMapping _rowversion =
        new ByteArrayTypeMapping("rowversion", dbType: DbType.Binary, size: 8);

    private readonly RelationalTypeMapping _defaultIntMapping =
        new IntTypeMapping("default_int_mapping", dbType: DbType.Int32);

    private readonly RelationalTypeMapping _defaultLongMapping =
        new LongTypeMapping("default_long_mapping", dbType: DbType.Int64);

    private readonly RelationalTypeMapping _defaultShortMapping =
        new ShortTypeMapping("default_short_mapping", dbType: DbType.Int16);

    private readonly RelationalTypeMapping _defaultByteMapping =
        new ByteTypeMapping("default_byte_mapping", dbType: DbType.Byte);

    private readonly RelationalTypeMapping _defaultBoolMapping =
        new BoolTypeMapping("default_bool_mapping");

    private readonly RelationalTypeMapping _someIntMapping =
        new IntTypeMapping("some_int_mapping");

    private readonly RelationalTypeMapping _intArray =
        new IntArrayTypeMapping();

    private readonly RelationalTypeMapping _defaultDecimalMapping =
        new DecimalTypeMapping("default_decimal_mapping");

    private readonly RelationalTypeMapping _defaultDateTimeMapping =
        new DateTimeTypeMapping("default_datetime_mapping", dbType: DbType.DateTime2);

    private readonly RelationalTypeMapping _defaultDoubleMapping =
        new DoubleTypeMapping("default_double_mapping");

    private readonly RelationalTypeMapping _defaultDateTimeOffsetMapping =
        new DateTimeOffsetTypeMapping("default_datetimeoffset_mapping");

    private readonly RelationalTypeMapping _defaultFloatMapping =
        new FloatTypeMapping("default_float_mapping");

    private readonly RelationalTypeMapping _defaultGuidMapping =
        new GuidTypeMapping("default_guid_mapping");

    private readonly RelationalTypeMapping _defaultTimeSpanMapping =
        new TimeSpanTypeMapping("default_timespan_mapping");

    private readonly IReadOnlyDictionary<Type, RelationalTypeMapping> _simpleMappings;
    private readonly IReadOnlyDictionary<string, RelationalTypeMapping> _simpleNameMappings;

    public TestRelationalTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
        _simpleMappings = new Dictionary<Type, RelationalTypeMapping>
        {
            { typeof(int), _defaultIntMapping },
            { typeof(long), _defaultLongMapping },
            { typeof(DateTime), _defaultDateTimeMapping },
            { typeof(Guid), _defaultGuidMapping },
            { typeof(bool), _defaultBoolMapping },
            { typeof(byte), _defaultByteMapping },
            { typeof(double), _defaultDoubleMapping },
            { typeof(DateTimeOffset), _defaultDateTimeOffsetMapping },
            { typeof(char), _defaultIntMapping },
            { typeof(short), _defaultShortMapping },
            { typeof(float), _defaultFloatMapping },
            { typeof(decimal), _defaultDecimalMapping },
            { typeof(TimeSpan), _defaultTimeSpanMapping },
            { typeof(string), _string },
            { typeof(int[]), _intArray },
        };

        _simpleNameMappings = new Dictionary<string, RelationalTypeMapping>
        {
            { "some_int_mapping", _someIntMapping },
            { "some_string(max)", _string },
            { "some_binary(max)", _binary },
            { "money", _defaultDecimalMapping },
            { "dec", _defaultDecimalMapping },
        };
    }

    protected override RelationalTypeMapping FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        var storeTypeName = mappingInfo.StoreTypeName;

        if (clrType == typeof(string))
        {
            var isAnsi = mappingInfo.IsUnicode.GetValueOrDefault();
            var isFixedLength = mappingInfo.IsFixedLength.HasValue && mappingInfo.IsFixedLength.Value;

            var baseName = (isAnsi, isFixedLength) switch
            {
                (true, true) => "ansi_string_fixed",
                (true, false) => "ansi_string",
                (false, true) => "just_string_fixed",
                (false, false) => "just_string",
            };

            int? size = mappingInfo.Size.HasValue
                ? mappingInfo.Size
                : mappingInfo.IsKeyOrIndex
                    ? (isAnsi ? 900 : 450)
                    : null;

            var name = storeTypeName is null
                ? $"{baseName}({(size.HasValue ? size.Value.ToString() : "max")})"
                : storeTypeName;

            DbType? dbType = isAnsi ? DbType.AnsiString : null;
            return new TestStringTypeMapping(name, dbType, !isAnsi, size, isFixedLength);
        }

        if (clrType == typeof(byte[]))
        {
            if (mappingInfo.IsRowVersion.GetValueOrDefault())
                return _rowversion;

            int? size = mappingInfo.Size.HasValue
                ? mappingInfo.Size
                : mappingInfo.IsKeyOrIndex
                    ? 900
                    : null;

            var name = storeTypeName is null
                ? $"just_binary({(size.HasValue ? size.Value.ToString() : "max")})"
                : storeTypeName;

            return new ByteArrayTypeMapping(name, DbType.Binary, size);
        }

        if (clrType is not null && _simpleMappings.TryGetValue(clrType, out var mapping))
        {
            return storeTypeName is not null && !mapping.StoreType.Equals(storeTypeName, StringComparison.Ordinal)
                ? mapping.WithStoreTypeAndSize(storeTypeName, mapping.Size)
                : mapping;
        }

        if (storeTypeName is not null && _simpleNameMappings.TryGetValue(storeTypeName, out var namedMapping))
        {
            if (clrType is null || namedMapping.ClrType == clrType)
                return namedMapping;
        }

        return null;
    }
}
