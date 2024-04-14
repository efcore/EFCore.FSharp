namespace EntityFrameworkCore.FSharp.Internal

open System
open System.Collections.Generic
open System.Linq
open System.Linq.Expressions
open System.Numerics
open System.Text
open Microsoft.EntityFrameworkCore.Internal
open System.Globalization
open EntityFrameworkCore.FSharp
open EntityFrameworkCore.FSharp.SharedTypeExtensions
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open System.Security

type FSharpHelper(relationalTypeMappingSource: IRelationalTypeMappingSource) =
    let _builtInTypes =
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
        |> dict

    let _keywords = [|
        "abstract"
        "and"
        "as"
        "asr"
        "assert"
        "atomic"
        "base"
        "begin"
        "break"
        "checked"
        "class"
        "component"
        "const"
        "constraint"
        "constructor"
        "continue"
        "default"
        "delegate"
        "do"
        "done"
        "downcast"
        "downto"
        "eager"
        "elif"
        "else if"
        "else"
        "end"
        "event"
        "exception"
        "extern"
        "external"
        "false"
        "finally"
        "fixed"
        "for"
        "fun"
        "function"
        "functor"
        "global"
        "if"
        "in"
        "include"
        "inherit"
        "inline"
        "interface"
        "internal"
        "land"
        "lazy"
        "let!"
        "let"
        "lor"
        "lsl"
        "lsr"
        "lxor"
        "match"
        "member"
        "method"
        "mixin"
        "mod"
        "module"
        "mutable"
        "namespace"
        "new"
        "not struct"
        "not"
        "null"
        "object"
        "of"
        "open"
        "or"
        "override"
        "parallel"
        "private"
        "process"
        "protected"
        "public"
        "pure"
        "rec"
        "return!"
        "return"
        "sealed"
        "select"
        "sig"
        "static"
        "struct"
        "tailcall"
        "then"
        "to"
        "trait"
        "true"
        "try"
        "type"
        "upcast"
        "use!"
        "use"
        "val"
        "virtual"
        "void"
        "volatile"
        "when"
        "while"
        "with"
        "yield!"
        "yield"
    |]

    let ensureDecimalPlaces (number: double) =

        let literal = number.ToString("G17", CultureInfo.InvariantCulture)

        if Double.IsNaN number then
            "Double.NaN"
        elif Double.IsPositiveInfinity number then
            "Double.PositiveInfinity"
        elif Double.IsNegativeInfinity number then
            "Double.NegativeInfinity"

        elif
            literal.Contains('.')
            || literal.Contains('E')
            || literal.Contains('e')
        then
            literal
        else
            literal
            + ".0"

    let literalAsObj l =
        if l = "null" then
            l
        else
            l
            + " :> obj"

    let shouldUseFullName t = false


    member private this.ReferenceFullName (t: Type) (useFullName: Nullable<bool>) =

        let useFullName' =
            match
                (useFullName
                 |> Option.ofNullable)
            with
            | Some x -> x
            | None ->
                if t.IsNested then
                    shouldUseFullName t.DeclaringType
                else
                    shouldUseFullName t


        match _builtInTypes.TryGetValue t with
        | true, value -> value
        | _ ->
            if
                t
                |> isNullableType
            then
                sprintf
                    "Nullable<%s>"
                    (this.ReferenceFullName
                        (t
                         |> unwrapNullableType)
                        useFullName)
            elif
                t
                |> isOptionType
            then
                sprintf
                    "%s option"
                    (this.ReferenceFullName
                        (t
                         |> unwrapOptionType)
                        useFullName)
            else
                let builder = StringBuilder()

                let returnName () =
                    let name = displayName t useFullName' true

                    builder.Append(name)
                    |> string

                if t.IsArray then
                    builder
                        .Append(this.ReferenceFullName (t.GetElementType()) (Nullable(false)))
                        .Append("[")
                    |> ignore

                    match t.GetArrayRank() with
                    | 1 ->
                        builder.Append("]")
                        |> ignore
                    | n ->
                        (',', n)
                        |> String
                        |> builder.Append
                        |> ignore

                    builder
                    |> string
                elif t.IsNested then
                    builder
                        .Append(this.ReferenceFullName (t.DeclaringType) (Nullable(false)))
                        .Append(".")
                    |> ignore

                    returnName ()
                else
                    returnName ()

    member private this.literalString(value: string) =
        "\""
        + value
            .Replace("\\", @"\\")
            .Replace("\0", @"\0")
            .Replace("\"", "\\\"")
            .Replace("\n", @"\n")
            .Replace("\r", @"\r")
        + "\""

    member private this.literalBoolean(value: bool) = if value then "true" else "false"

    member private this.literalByte(value: byte) = sprintf "(byte %d)" value

    member private this.literalChar(value: char) =

        let value' =
            match value with
            | '\\' -> @"\\"
            | '\n' -> @"\n"
            | '\r' -> @"\r"
            | '\'' -> @"\'"
            | _ -> value.ToString()

        sprintf "\'%s\'" value'

    member private this.literalDateOnly(value: DateOnly) =
        String.Format(
            CultureInfo.InvariantCulture,
            "DateOnly({0}, {1}, {2})",
            value.Year,
            value.Month,
            value.Day
        )

    member private this.literalDateTime(value: DateTime) =

        String.Format(
            CultureInfo.InvariantCulture,
            "new DateTime({0}, {1}, {2}, {3}, {4}, {5}, {6}, DateTimeKind.{7})",
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            value.Second,
            value.Millisecond,
            value.Kind
        )
        + (if value.Ticks % 10_000L = 0L then
               ""
           else
               String.Format(CultureInfo.InvariantCulture, ".AddTicks({0})", value.Ticks % 10000L))

    member private this.literalDateTimeOffset(value: DateTimeOffset) =
        sprintf
            "DateTimeOffset(%s, %s)"
            (value.DateTime
             |> this.literalDateTime)
            (value.Offset
             |> this.literalTimeSpan)

    member private this.literalDecimal(value: decimal) = sprintf "%fm" value

    member private this.literalDouble(value: double) = ensureDecimalPlaces value

    member private this.literalFloat32(value: float32) = sprintf "(float32 %f)" value

    member private this.literalGuid(value: Guid) = sprintf "Guid(\"%A\")" value

    member private this.literalInt(value: int) = sprintf "%d" value

    member private this.literalInt64(value: Int64) = sprintf "%dL" value

    member private this.literalSByte(value: sbyte) = sprintf "(sbyte %d)" value

    member private this.literalInt16(value: Int16) = sprintf "%ds" value

    member private this.literaTimeOnly(value: TimeOnly) =

        let result =
            if value.Millisecond = 0 then
                String.Format(
                    CultureInfo.InvariantCulture,
                    "TimeOnly({0}, {1}, {2})",
                    value.Hour,
                    value.Minute,
                    value.Second
                )
            else
                String.Format(
                    CultureInfo.InvariantCulture,
                    "TimeOnly({0}, {1}, {2}, {3}, {4})",
                    value.Hour,
                    value.Minute,
                    value.Second,
                    value.Millisecond
                )

        if value.Ticks % 10_000L = 0L then
            result
        else
            result
            + String.Format(CultureInfo.InvariantCulture, ".AddTicks({0})", value.Ticks % 10000L)

    member private this.literalTimeSpan(value: TimeSpan) =
        if value.Ticks % 10_000L = 0L then
            String.Format(
                CultureInfo.InvariantCulture,
                "TimeSpan(%d, %d, %d, %d, %d)",
                value.Days,
                value.Hours,
                value.Minutes,
                value.Seconds,
                value.Milliseconds
            )
        else
            String.Format(CultureInfo.InvariantCulture, "TimeSpan({0}L)", value.Ticks)

    member private this.literalUInt32(value: UInt32) = sprintf "%du" value

    member private this.literalUInt64(value: UInt64) = sprintf "%duL" value

    member private this.literalUInt16(value: UInt16) = sprintf "%dus" value

    member private this.literalBigInteger(value: BigInteger) =
        sprintf
            """BigInteger.Parse("%s", NumberFormatInfo.InvariantInfo)"""
            (value.ToString(NumberFormatInfo.InvariantInfo))


    member private this.literalByteArray(values: byte[]) =
        let v =
            values
            |> Seq.map this.literalByte

        sprintf "[| %s |]" (String.Join("; ", v))

    member private this.literalStringArray(values: string[]) =
        let v =
            values
            |> Seq.map this.literalString

        sprintf "[| %s |]" (String.Join("; ", v))

    member private this.literalArray(values: Array) =
        let v =
            values.Cast<obj>()
            |> Seq.map this.unknownLiteral

        sprintf "[| %s |]" (String.Join("; ", v))

    member private this.literalType(t: Type, useFullName: Nullable<bool>) =
        let value = this.ReferenceFullName t (Nullable(false))
        sprintf "typeof<%s>" value


    member private this.literalList
        (values: IReadOnlyList<obj>)
        (vertical: bool)
        (isObjType: bool)
        =

        let values' =
            if isObjType then
                values
                |> Seq.map this.unknownLiteral
                |> Seq.map literalAsObj
            else
                values
                |> Seq.map this.unknownLiteral

        if not vertical then
            sprintf
                "[| %s |]"
                (values'
                 |> join "; ")
        else

            stringBuffer {
                "[|"
                indent { values' }
                "|]"
            }


    member private this.handleArguments args (sb: IndentedStringBuilder) =

        sb.Append("(")
        |> ignore

        if (this.handleList args false sb) then
            sb.Append(")")
            |> ignore

            true
        else
            false

    member private this.handleList exps simple sb =
        let mutable separator = String.Empty

        let results =
            exps
            |> Seq.map (fun e ->
                sb.Append(separator)
                |> ignore

                let result = this.handleExpression e simple sb

                separator <- ", "
                result
            )

        results
        |> Seq.forall (fun r -> r = true)

    member private this.handleExpression
        (expression: Expression)
        simple
        (sb: IndentedStringBuilder)
        =
        match expression.NodeType with
        | ExpressionType.NewArrayInit ->

            sb.Append("[| ")
            |> ignore

            this.handleList (expression :?> NewArrayExpression).Expressions true sb
            |> ignore

            sb.Append(" |]")
            |> ignore

            true
        | ExpressionType.Convert ->
            sb.Append("(")
            |> ignore

            let result = this.handleExpression (expression :?> UnaryExpression).Operand false sb

            sb.Append($" :?> {this.ReferenceFullName expression.Type (Nullable(true))})")
            |> ignore

            result
        | ExpressionType.New ->
            sb.Append(this.ReferenceFullName expression.Type (Nullable(true)))
            |> ignore

            this.handleArguments ((expression :?> NewExpression).Arguments) sb

        | ExpressionType.Call ->

            let mutable exitEarly = false
            let callExpr = expression :?> MethodCallExpression

            if callExpr.Method.IsStatic then
                sb.Append(this.ReferenceFullName callExpr.Method.DeclaringType (Nullable(true)))
                |> ignore
            else if (not (this.handleExpression callExpr.Object false sb)) then
                exitEarly <- true

            if exitEarly then
                false
            else
                sb.Append($".{callExpr.Method.Name}")
                |> ignore

                this.handleArguments callExpr.Arguments sb

        | ExpressionType.Constant ->
            let value = (expression :?> ConstantExpression).Value

            let valueToWrite =
                if
                    simple
                    && (value.GetType()
                        |> isNumeric)
                then
                    value
                    |> string
                else
                    this.unknownLiteral (value)

            sb.Append(valueToWrite)
            |> ignore

            true

        | ExpressionType.MemberAccess ->
            let memberExpression = expression :?> MemberExpression

            let appendAndReturn () =
                sb.Append($".{memberExpression.Member.Name}")
                |> ignore

                true

            if
                memberExpression.Expression
                |> isNull
            then
                sb.Append(
                    this.ReferenceFullName memberExpression.Member.DeclaringType (Nullable(true))
                )
                |> ignore

                appendAndReturn ()
            elif
                this.handleExpression memberExpression.Expression false sb
                |> not
            then
                false
            else
                appendAndReturn ()
        | ExpressionType.Add ->
            let binaryExpression = expression :?> BinaryExpression

            if
                this.handleExpression binaryExpression.Left false sb
                |> not
            then
                false
            else
                sb.Append(" + ")
                |> ignore

                this.handleExpression binaryExpression.Right false sb
        | _ -> false


    member private this.getSimpleEnumValue t name =
        (this.ReferenceFullName t (Nullable(false)))
        + "."
        + name

    member private this.getFlags(flags: Enum) =
        let t = flags.GetType()
        let defaultValue = Enum.ToObject(t, 0uy) :?> Enum

        Enum.GetValues(t)
        |> Seq.cast<Enum>
        |> Seq.except [| defaultValue |]
        |> Seq.filter flags.HasFlag
        |> Seq.toList

    member private this.getCompositeEnumValue t flags =
        let allValues =
            flags
            |> this.getFlags
            |> HashSet

        allValues
        |> Seq.iter (fun a ->
            let decomposedValues = this.getFlags a

            if decomposedValues.Length > 1 then
                decomposedValues
                |> Seq.filter (fun v -> not (obj.Equals(v, a)))
                |> allValues.ExceptWith
        )

        let folder previous current =
            if String.IsNullOrEmpty previous then
                this.getSimpleEnumValue t (Enum.GetName(t, current))
            else
                previous
                + " | "
                + this.getSimpleEnumValue t (Enum.GetName(t, current))

        allValues
        |> Seq.fold folder ""


    member private this.literalEnum(value: Enum, useFullName: bool) =
        let t = value.GetType()
        let name = Enum.GetName(t, value)

        if isNull name then
            this.getCompositeEnumValue t value
        else
            this.getSimpleEnumValue t name

    member private this.isLetterChar cat =
        match cat with
        | UnicodeCategory.UppercaseLetter -> true
        | UnicodeCategory.LowercaseLetter -> true
        | UnicodeCategory.TitlecaseLetter -> true
        | UnicodeCategory.ModifierLetter -> true
        | UnicodeCategory.OtherLetter -> true
        | UnicodeCategory.LetterNumber -> true
        | _ -> false


    member private this.isIdentifierPartCharacter ch =
        if ch < 'a' then
            if ch < 'A' then
                ch >= '0'
                && ch <= '9'
            else
                ch <= 'Z'
                || ch = '_'
        elif ch <= 'z' then
            true
        elif
            ch
            <= '\u007F'
        then
            false
        else
            let cat =
                ch
                |> CharUnicodeInfo.GetUnicodeCategory

            if
                cat
                |> this.isLetterChar
            then
                true
            else
                match cat with
                | UnicodeCategory.DecimalDigitNumber -> true
                | UnicodeCategory.ConnectorPunctuation -> true
                | UnicodeCategory.NonSpacingMark -> true
                | UnicodeCategory.SpacingCombiningMark -> true
                | UnicodeCategory.Format -> true
                | _ -> false

    member private this.isIdentifierStartCharacter ch =
        if ch < 'a' then
            if ch < 'A' then
                false
            else
                ch <= 'Z'
                || ch = '_'
        elif ch <= 'z' then
            true
        elif
            ch
            <= '\u007F'
        then
            false
        else
            ch
            |> CharUnicodeInfo.GetUnicodeCategory
            |> this.isLetterChar

    member private this.handleScope (scope: ICollection<string>) (sb: StringBuilder) =
        if
            scope
            |> Seq.isEmpty
        then
            sb
            |> string
        else
            let baseId =
                sb
                |> string

            let mutable uniqueId =
                sb
                |> string

            let mutable qualifier = 0

            while scope
                  |> Seq.contains uniqueId do
                qualifier <-
                    qualifier
                    + 1

                uniqueId <- sprintf "%s%d" baseId qualifier

            uniqueId
            |> scope.Add

            uniqueId

    member private this.IdentifierWithScope (name: string) (scope: ICollection<string>) =

        let sb = StringBuilder()
        let mutable partStart = 0

        for i = partStart to (name.Length
                              - 1) do
            if
                name.[i]
                |> this.isIdentifierPartCharacter
                |> not
            then
                if
                    partStart
                    <> i
                then
                    sb.Append(
                        name.Substring(
                            partStart,
                            (i
                             - partStart)
                        )
                    )
                    |> ignore

                partStart <- i + 1

        if
            partStart
            <> name.Length
        then
            sb.Append(name.Substring(partStart))
            |> ignore

        if
            sb.Length = 0
            || sb.[0]
               |> this.isIdentifierStartCharacter
               |> not
        then
            sb.Insert(0, "_")
            |> ignore

        let identifier =
            sb
            |> this.handleScope scope

        if
            _keywords
            |> Seq.contains identifier
        then
            sprintf "``%s``" identifier
        else
            identifier

    member private this.buildFragment
        (
            fragment: MethodCallCodeFragment,
            typeQualified,
            instanceIdentifier,
            (indent: int)
        ) =
        let builder = IndentedStringBuilder()
        let mutable current = fragment

        let processArg (arg: obj) (sb: IndentedStringBuilder) =
            match arg with
            | :? NestedClosureCodeFragment as n ->
                let f: string = this.buildNestedFragment (n, indent)
                sb.AppendLine(f)
            | _ -> sb.Append(this.unknownLiteral arg)

        if typeQualified then
            if
                isNull instanceIdentifier
                || isNull fragment.MethodInfo
                || notNull fragment.ChainedCall
            then
                raise (ArgumentException DesignStrings.CannotGenerateTypeQualifiedMethodCall)

            builder.Append $"%s{fragment.DeclaringType}.%s{fragment.Method}(%s{instanceIdentifier}"
            |> ignore

            for a in fragment.Arguments do
                builder.Append(", ")
                |> processArg a
                |> ignore

            builder.Append(")")
            |> ignore

        else
            if notNull instanceIdentifier then
                builder.Append(instanceIdentifier)
                |> ignore

                if notNull current.ChainedCall then
                    builder.AppendLine().IncrementIndent()
                    |> ignore

            while notNull current do
                builder.Append(sprintf ".%s(" current.Method)
                |> ignore

                for i in
                    [
                        0 .. current.Arguments.Count
                             - 1
                    ] do
                    if i <> 0 then
                        builder.Append(", ")
                        |> ignore

                    builder
                    |> processArg current.Arguments.[i]
                    |> ignore

                builder.Append(")")
                |> ignore

                if notNull current.ChainedCall then
                    builder.AppendLine()
                    |> ignore

                current <- current.ChainedCall

        builder
        |> string

    member private this.buildNestedFragment(n: NestedClosureCodeFragment, indent: int) =
        let builder = IndentedStringBuilder()

        for i in [ -1 .. (indent + 1) ] do
            builder.IncrementIndent()
            |> ignore

        builder
            .AppendLine()
            .IncrementIndent()
            .AppendLine(sprintf "(fun %s ->" n.Parameter)
        |> ignore

        let lines =
            n.MethodCalls
            |> Seq.map (fun mc -> this.buildFragment (mc, false, n.Parameter, indent + 1))

        builder.IncrementIndent()
        |> ignore

        for l in lines do
            builder.AppendLines(
                l
                + " |> ignore",
                false
            )
            |> ignore

        builder.Append(")")
        |> string


    member private this.unknownLiteral(value: obj) =
        if isNull value then
            "null"
        else
            match value with
            | :? DBNull -> "null"
            | :? Enum as e -> this.literalEnum (e, true)
            | :? bool as e -> this.literalBoolean e
            | :? byte as e -> this.literalByte e
            | :? (byte array) as e -> this.literalByteArray e
            | :? char as e -> this.literalChar e
            | :? DateTime as e -> this.literalDateTime e
            | :? DateTimeOffset as e -> this.literalDateTimeOffset e
            | :? decimal as e -> this.literalDecimal e
            | :? double as e -> this.literalDouble e
            | :? float32 as e -> this.literalFloat32 e
            | :? Guid as e -> this.literalGuid e
            | :? int as e -> this.literalInt e
            | :? Int64 as e -> this.literalInt64 e
            | :? sbyte as e -> this.literalSByte e
            | :? Int16 as e -> this.literalInt16 e
            | :? string as e -> this.literalString e
            | :? TimeSpan as e -> this.literalTimeSpan e
            | :? UInt32 as e -> this.literalUInt32 e
            | :? UInt64 as e -> this.literalUInt64 e
            | :? UInt16 as e -> this.literalUInt16 e
            | :? BigInteger as e -> this.literalBigInteger e
            | :? (string[]) as e -> this.literalStringArray e
            | :? Array as e -> this.literalArray e
            | :? Type as t -> this.ReferenceFullName t (Nullable<bool>())
            | :? NestedClosureCodeFragment as n -> this.buildNestedFragment (n, 0)
            | _ ->

                let literalType = value.GetType()

                let mapping = relationalTypeMappingSource.FindMapping literalType

                if isNull mapping then
                    let t = value.GetType()

                    let type' =
                        if
                            t
                            |> isNullableType
                        then
                            t
                            |> unwrapNullableType
                        elif
                            t
                            |> isOptionType
                        then
                            t
                            |> unwrapOptionType
                        else
                            t

                    invalidOp (
                        type'
                        |> DesignStrings.UnknownLiteral
                    )
                else
                    let builder = IndentedStringBuilder()
                    let expression = mapping.GenerateCodeLiteral(value)

                    let handled = this.handleExpression expression false builder

                    if handled then
                        builder.ToString()
                    else
                        let args = ((expression.ToString()), (displayName literalType false false))

                        args
                        |> DesignStrings.LiteralExpressionNotSupported
                        |> NotSupportedException
                        |> raise


    interface ICSharpHelper with
        member this.Fragment
            (
                fragment: IMethodCallCodeFragment,
                instanceIdentifer: string,
                typeQualified: bool
            ) =
            this.buildFragment (
                fragment :?> MethodCallCodeFragment,
                typeQualified,
                instanceIdentifer,
                0
            )

        member this.Identifier
            (
                name: string,
                scope: ICollection<string>,
                capitalize: Nullable<bool>
            ) : string =
            if isNull scope then
                this.IdentifierWithScope name [||]
            else
                this.IdentifierWithScope name scope

        member this.Lambda(properties: IReadOnlyList<string>, lambdaIdentifier: string) : string =

            let lambdaIdentifier' =
                if String.IsNullOrEmpty lambdaIdentifier then
                    "x"
                else
                    lambdaIdentifier

            let props =
                properties
                |> Seq.map (fun p ->
                    lambdaIdentifier'
                    + "."
                    + p
                )
                |> join ", "

            sprintf "(fun %s -> (%s) :> obj)" lambdaIdentifier' props

        member this.Literal(value: Nullable<'T>) : string = this.unknownLiteral value

        member this.Literal(value: bool) : string = this.literalBoolean value

        member this.Literal(value: byte) : string = this.literalByte value

        member this.Literal(value: char) : string = this.literalChar value

        member this.Literal(value: DateTime) : string = this.literalDateTime value

        member this.Literal(value: DateTimeOffset) : string = this.literalDateTimeOffset value

        member this.Literal(value: decimal) : string = this.literalDecimal value

        member this.Literal(value: float) : string = this.literalDouble value

        member this.Literal(value: Enum, fullName: bool) : string =
            this.literalEnum (value, fullName)

        member this.Literal(value: float32) : string = this.literalFloat32 value

        member this.Literal(value: Guid) : string = this.literalGuid value

        member this.Literal(value: int) : string = this.literalInt value

        member this.Literal(value: int64) : string = this.literalInt64 value

        member this.Literal(value: sbyte) : string = this.literalSByte value

        member this.Literal(value: int16) : string = this.literalInt16 value

        member this.Literal(value: string) : string = this.literalString value

        member this.Literal(value: TimeSpan) = this.literalTimeSpan value

        member this.Literal(value: UInt32) = this.literalUInt32 value

        member this.Literal(value: UInt16) = this.literalUInt16 value

        member this.Literal(value: UInt64) = this.literalUInt64 value

        member this.Literal(values: 'T[], vertical: bool) : string =
            let isObjType = typeof<'T> = typeof<obj>

            this.literalList
                (values
                 |> Seq.cast<obj>
                 |> ResizeArray)
                vertical
                isObjType

        member this.Literal(t: Type, fullName: Nullable<bool>) = this.literalType (t, fullName)

        member this.Namespace(name: string[]) : string =
            let join (ns': string array) = String.Join(".", ns')

            let ns =
                name
                |> Array.filter (
                    String.IsNullOrEmpty
                    >> not
                )
                |> Array.collect (fun n ->
                    n.Split([| '.' |], StringSplitOptions.RemoveEmptyEntries)
                )
                |> Array.map (fun t -> (this :> ICSharpHelper).Identifier(t, null))
                |> Array.filter (
                    String.IsNullOrEmpty
                    >> not
                )
                |> join

            if String.IsNullOrEmpty ns then "_" else ns

        member this.Reference(t: Type, fullName) : string = this.ReferenceFullName t fullName

        member this.UnknownLiteral(value: obj) : string = this.unknownLiteral value

        member this.Arguments(values: IEnumerable<obj>) : string =
            values
            |> Seq.map this.unknownLiteral
            |> join ", "


        member this.Expression(node: Expression, collectedNamespaces: ISet<string>) : string =
            failwith "Not Implemented"

        member this.Fragment(fragment: IMethodCallCodeFragment, indent: int) : string =
            if isNull fragment then
                ""
            else

                let builder = IndentedStringBuilder()

                let appendMethodCall (current: IMethodCallCodeFragment) =
                    builder.Append(".").Append(current.Method)
                    |> ignore

                    if current.TypeArguments.Any() then
                        builder
                            .Append("<")
                            .Append(
                                current.TypeArguments
                                |> join ", "
                            )
                            .Append(">")
                        |> ignore

                    builder.Append("(")
                    |> ignore

                    current.Arguments
                    |> Seq.iteri (fun i arg ->
                        if i <> 0 then
                            builder.Append(", ")
                            |> ignore

                        if arg :? NestedClosureCodeFragment then
                            builder.Append(
                                this.buildNestedFragment (
                                    arg :?> NestedClosureCodeFragment,
                                    indent + 1
                                )
                            )
                            |> ignore
                        else
                            builder.Append(this.unknownLiteral arg)
                            |> ignore
                    )

                if isNull fragment.ChainedCall then
                    appendMethodCall fragment
                else
                    let mutable current = fragment

                    while notNull current do
                        appendMethodCall current
                        |> ignore

                        current <- current.ChainedCall

                builder.ToString()

        member this.Fragment(fragment: NestedClosureCodeFragment, indent: int) : string =

            if fragment.MethodCalls.Count = 1 then
                let m = fragment.MethodCalls.Single() :> IMethodCallCodeFragment
                let x = (this :> ICSharpHelper).Fragment(m, indent)
                $"(fun {fragment.Parameter} -> {fragment.Parameter}{x})"
            else
                failwith "Not Implemented"

        member this.Fragment(fragment: PropertyAccessorCodeFragment) : string =
            (this :> ICSharpHelper).Lambda(fragment.Properties, fragment.Parameter)

        member this.Fragment(fragment: AttributeCodeFragment) : string =
            let attributeName =
                if fragment.Type.Name.EndsWith("Attribute", StringComparison.Ordinal) then
                    fragment.Type.Name.Substring(
                        0,
                        fragment.Type.Name.Length
                        - 9
                    )
                else
                    fragment.Type.Name

            let args =
                fragment.Arguments
                |> Seq.map (fun a -> this.unknownLiteral (a))
                |> join ", "

            let namedArgs =
                fragment.NamedArguments
                |> Seq.map (fun a -> $"{a.Key} = {this.unknownLiteral (a.Value)}")
                |> join ", "

            if
                String.IsNullOrEmpty(args)
                && String.IsNullOrEmpty(namedArgs)
            then
                $"[{attributeName}]"
            elif String.IsNullOrEmpty(args) then
                $"[{attributeName}({namedArgs})]"
            elif String.IsNullOrEmpty(namedArgs) then
                $"[{attributeName}({args})]"
            else
                $"[{attributeName}({args}, {namedArgs})]"

        member this.GetRequiredUsings(``type``: Type) : IEnumerable<string> = getNamespaces ``type``

        member this.Lambda(properties: IEnumerable<IProperty>, lambdaIdentifier: string) : string =
            if properties.Count() = 1 then
                let p = properties.Single()
                $"(fun {lambdaIdentifier} -> {lambdaIdentifier}.{p.Name})"
            else

                stringBuffer {
                    $"(fun {lambdaIdentifier} ->"

                    indent {
                        properties
                        |> Seq.map (fun p -> sprintf "%s.%s" lambdaIdentifier p.Name)
                        |> join ", "
                    }

                    ")"
                }

        member this.Literal(values: obj array2d) : string = failwith "Not Implemented"
        member this.Literal(value: BigInteger) : string = this.literalBigInteger value
        member this.Literal(value: DateOnly) : string = this.literalDateOnly value
        member this.Literal(value: TimeOnly) : string = this.literaTimeOnly value
        member this.Literal(values: List<'T>, vertical: bool) : string = failwith "Not Implemented"

        member this.Literal(values: Dictionary<'TKey, 'TValue>, vertical: bool) : string =
            failwith "Not Implemented"

        member this.Statement(node: Expression, collectedNamespaces: ISet<string>) : string =
            failwith "Not Implemented"

        member this.XmlComment(comment: string, indent: int) : string =
            let lines =
                comment.Split(
                    [|
                        "\n"
                        "\r"
                        "\r\n"
                    |],
                    StringSplitOptions.RemoveEmptyEntries
                )

            stringBuffer {
                "/// <summary>"

                lines
                |> Seq.map (fun l ->
                    "/// "
                    + (SecurityElement.Escape l)
                )

                "/// </summary>"
            }
