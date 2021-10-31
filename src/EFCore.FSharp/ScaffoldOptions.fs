namespace EntityFrameworkCore.FSharp

/// <summary>A type representing how scaffolded objects should be generated</summary>
type ScaffoldTypesAs =
    /// <summary>Objects should be generated as a standard F# record</summary>
    | RecordType
    /// <summary>Objects should be generated similar to how a C# class behaves</summary>
    | ClassType

/// <summary>A type representing how nullable columns in scaffolded databases should be represented</summary>
type ScaffoldNullableColumnsAs =
    /// <summary>A nullable column of type <c>'a</c> should be represented as <c>'a option</c></summary>
    | OptionTypes
    /// <summary>A nullable column of type <c>'a</c> should be represented as <c>Nullable&lt;'a&gt;</c></summary>
    | NullableTypes

[<AllowNullLiteral>]
type ScaffoldOptions() =
    member val ScaffoldTypesAs = RecordType with get, set
    member val ScaffoldNullableColumnsAs = OptionTypes with get, set

    static member Default = ScaffoldOptions()
