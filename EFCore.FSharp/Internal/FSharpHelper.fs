namespace Bricelam.EntityFrameworkCore.FSharp.Internal

open System
open System.Collections.Generic
open System.Linq
open System.Reflection
open System.Text
open Microsoft.EntityFrameworkCore.Internal
open System.Globalization
open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.SharedTypeExtensions
open Microsoft.EntityFrameworkCore.Design

module FSharpHelper =
    open System.Linq.Expressions

    let private _builtInTypes =
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
        ] |> dict

    let private _keywords =
        [|
            "abstract";
            "and";
            "as";
            "asr";
            "assert";
            "atomic";
            "base";
            "begin";
            "break";
            "checked";
            "class";
            "component";
            "const";
            "constraint";
            "constructor";
            "continue";
            "default";
            "delegate";
            "do";
            "done";
            "downcast";
            "downto";
            "eager";
            "elif";
            "else if";
            "else";
            "end";
            "event";
            "exception";
            "extern";
            "external";
            "false";
            "finally";
            "fixed";
            "for";
            "fun";
            "function";
            "functor";
            "global";
            "if";
            "in";
            "include";
            "inherit";
            "inline";
            "interface";
            "internal";
            "land";
            "lazy";
            "let!";
            "let";
            "lor";
            "lsl";
            "lsr";
            "lxor";
            "match";
            "member";
            "method";
            "mixin";
            "mod";
            "module";
            "mutable";
            "namespace";
            "new";
            "not struct";
            "not";
            "null";
            "object";
            "of";
            "open";
            "or";
            "override";
            "parallel";
            "private";
            "process";
            "protected";
            "public";
            "pure";
            "rec";
            "return!";
            "return";
            "sealed";
            "select";
            "sig";
            "static";
            "struct";
            "tailcall";
            "then";
            "to";
            "trait";
            "true";
            "try";
            "type";
            "upcast";
            "use!";
            "use";
            "val";
            "virtual";
            "void";
            "volatile"
            "when";
            "while";
            "with";
            "yield!";
            "yield";
          |]
          
    let rec ReferenceFullName (t: Type) useFullName =

        match _builtInTypes.TryGetValue t with
        | true, value -> value
        | _ ->
            if t |> isNullableType then sprintf "Nullable<%s>" (ReferenceFullName (t |> unwrapNullableType) useFullName)
            elif t |> isOptionType then sprintf "%s option" (ReferenceFullName (t |> unwrapOptionType) useFullName)
            else
                let builder = StringBuilder()

                if t.IsArray then
                    builder.Append(ReferenceFullName (t.GetElementType()) useFullName).Append("[") |> ignore
                    (',', t.GetArrayRank()) |> String |> builder.Append |> ignore
                    builder.Append("]") |> ignore

                elif t.IsNested then
                    builder.Append(ReferenceFullName (t.DeclaringType) useFullName).Append(".") |> ignore

                let name =
                    match useFullName with
                    | true -> t.DisplayName()
                    | false -> t.ShortDisplayName()

                builder.Append(name) |> string

    let Reference t =
        ReferenceFullName t false

    let private ensureDecimalPlaces (number:string) =
        if number.IndexOf('.') >= 0 then number else number + ".0"

    module rec LiteralWriter =

        let LiteralEnum(value: Enum) = Reference(value.GetType()) + "." + (value |> string)

        let LiteralString(value: string) =
            if value.Contains(Environment.NewLine) then
                "@\"" + value.Replace("\"", "\"\"") + "\""
            else
                "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

        let LiteralBoolean(value: bool) =
            if value then "true" else "false"

        let LiteralByte (value: byte) = sprintf "(byte %d)" value

        let LiteralByteArray(values: byte[]) =
            let v = values |> Seq.map LiteralByte
            sprintf "[| %s |]" (String.Join("; ", v))

        let LiteralStringArray(values: string[]) =
            let v = values |> Seq.map LiteralString
            sprintf "[| %s |]" (String.Join("; ", v))

        let LiteralArray(values: Array) =
            let v = values.Cast<obj>() |> Seq.map UnknownLiteral
            sprintf "[| %s |]" (String.Join("; ", v))

        let LiteralChar(value: char) =
            "\'" + (if value = '\'' then "\\'" else value.ToString()) + "\'"

        let LiteralDateTime(value: DateTime) =
            sprintf "DateTime(%d, %d, %d, %d, %d, %d,%d, DateTimeKind.%A)"
                value.Year value.Month value.Day value.Hour value.Minute value.Second value.Millisecond value.Kind

        let LiteralTimeSpan(value: TimeSpan) =
            sprintf "TimeSpan(%d, %d, %d, %d, %d)" value.Days value.Hours value.Minutes value.Seconds value.Milliseconds

        let LiteralDateTimeOffset(value: DateTimeOffset) =
            sprintf "DateTimeOffset(%s, %s)" (value.DateTime |> LiteralDateTime) (value.Offset |> LiteralTimeSpan)
        
        let LiteralDecimal(value: decimal) =
            sprintf "%fm" value
        
        let LiteralDouble(value: double) =
            (value.ToString("R", CultureInfo.InvariantCulture)) |> ensureDecimalPlaces
        
        let LiteralFloat32(value: float32) =
            sprintf "(float32 %f)" value
        
        let LiteralGuid(value: Guid) =
            sprintf "Guid(\"%A\")" value
        
        let LiteralInt(value: int) =
            sprintf "%d" value
        
        let LiteralInt64(value: Int64) =
            sprintf "%dL" value
        
        let LiteralSByte(value: sbyte) =
            sprintf "(sbyte %d)" value

        let LiteralInt16(value: Int16) =
            sprintf "%ds" value

        let LiteralUInt32(value: UInt32) =
            sprintf "%du" value
        
        let LiteralUInt64(value: UInt64) =
            sprintf "%duL" value
        
        let LiteralUInt16(value: UInt16) =
            sprintf "%dus" value

        let LiteralList (values: IReadOnlyList<obj>) (vertical: bool) (sb:IndentedStringBuilder) =

            let values' = values |> Seq.map UnknownLiteral

            if not vertical then
                let line = sprintf "[| %s |]" (String.Join("; ", values'))
                sb |> append line
            else
                sb
                    |> append "[|"
                    |> indent
                    |> ignore

                values' |> Seq.iter(fun line -> sb |> appendLine line |> ignore)

                sb

            |> string

        let LiteralArray2D(values: obj[,]) =

            let rowCount = Array2D.length1 values
            let valuesCount = Array2D.length2 values

            let rowContents =
                [0..rowCount]
                |> Seq.map(fun i ->
                    let row' = values.[i, 0..valuesCount]
                    let entries = row' |> Seq.map Literal
                    sprintf "[ %s ]" (String.Join("; ", entries)) )

            sprintf "array2D [ %s ]" (String.Join("; ", rowContents))

        let HandleArguments args sb =

            sb |> append "(" |> ignore

            match (HandleList args false sb) with
            | true ->
                sb |> append ")" |> ignore
                true
            | false -> false

        let HandleList exps simple sb =
            let mutable separator = String.Empty

            let results =
                exps
                    |> Seq.map(fun e ->
                        sb |> append separator |> ignore

                        let result = HandleExpression e simple sb

                        separator <- ", "
                        result )

            results |> Seq.forall (fun r -> r = true)

        let rec HandleExpression (expression:Expression) simple (sb:IndentedStringBuilder) =
            match expression.NodeType with
            | ExpressionType.NewArrayInit ->
                
                sb |> append "[| " |> ignore
                HandleList (expression :?> NewArrayExpression).Expressions true sb |> ignore
                sb |> append " |]" |> ignore

                true
            | ExpressionType.Convert ->
                sb |> append "(" |> ignore
                let result = HandleExpression (expression :?> UnaryExpression).Operand false sb
                sb |> append " :?> " |> append (ReferenceFullName expression.Type true) |> append ")" |> ignore

                result
            | ExpressionType.New ->
                sb |> append (ReferenceFullName expression.Type true) |> ignore                
                HandleArguments ((expression :?> NewExpression).Arguments) sb

            | ExpressionType.Call ->
                
                let mutable exitEarly = false
                let callExpr = expression :?> MethodCallExpression

                if callExpr.Method.IsStatic then
                    sb |> append (ReferenceFullName callExpr.Method.DeclaringType true) |> ignore
                else
                    if (not (HandleExpression callExpr.Object false sb)) then
                        exitEarly <- true
                
                match exitEarly with
                | true -> false
                | false ->
                    sb |> append "." |> append callExpr.Method.Name |> ignore
                    HandleArguments callExpr.Arguments sb

            | ExpressionType.Constant ->
                let value = (expression :?> ConstantExpression).Value
                let valueToWrite =
                    if simple && (value.GetType() |> isNumeric) then
                        value |> string
                    else
                        LiteralWriter.UnknownLiteral(value)

                sb |> append valueToWrite |> ignore

                true
            | _ -> false

        let UnknownLiteral (value: obj) =
            if isNull value then
                "null"
            else
                match value with
                | :? DBNull -> "null"
                | :? Enum as e -> LiteralEnum e
                | :? bool as e -> LiteralBoolean e
                | :? byte as e -> LiteralByte e
                | :? (byte array) as e -> LiteralByteArray e                
                | :? char as e -> LiteralChar e
                | :? DateTime as e -> LiteralDateTime e
                | :? DateTimeOffset as e -> LiteralDateTimeOffset e
                | :? decimal as e -> LiteralDecimal e
                | :? double as e -> LiteralDouble e
                | :? float32 as e -> LiteralFloat32 e
                | :? Guid as e -> LiteralGuid e
                | :? int as e -> LiteralInt e
                | :? Int64 as e -> LiteralInt64 e
                | :? sbyte as e -> LiteralSByte e
                | :? Int16 as e -> LiteralInt16 e
                | :? string as e -> LiteralString e
                | :? TimeSpan as e -> LiteralTimeSpan e
                | :? UInt32 as e -> LiteralUInt32 e
                | :? UInt64 as e -> LiteralUInt64 e
                | :? UInt16 as e -> LiteralUInt16 e
                | :? (string[]) as e -> LiteralStringArray e
                | :? Array as e -> LiteralArray e
                | _ ->
                    let t = value.GetType()
                    let type' =
                        if t |> isNullableType then t |> unwrapNullableType
                        elif t |> isOptionType then t |> unwrapOptionType
                        else t
                    invalidOp (type' |> DesignStrings.UnknownLiteral)

        let Literal value =
            value |> UnknownLiteral

    let Lambda (properties: IReadOnlyList<string>) =
        StringBuilder()
            .Append("(fun x -> ")
            .Append("(").Append(String.Join(", ", (properties |> Seq.map(fun p -> "x." + p)))).Append(" :> obj)")
            .Append(")") |> string

    let Literal (o:obj) =
        LiteralWriter.Literal o

    let LiteralList (vertical: bool) (sb:IndentedStringBuilder) (values: IReadOnlyList<obj>) =
        LiteralWriter.LiteralList values vertical sb

    let Literal2DArray (values: obj[,]) =
        LiteralWriter.LiteralArray2D values

    let UnknownLiteral (o:obj) =
        LiteralWriter.UnknownLiteral o

    let private isLetterChar cat =
        match cat with
        | UnicodeCategory.UppercaseLetter -> true
        | UnicodeCategory.LowercaseLetter -> true
        | UnicodeCategory.TitlecaseLetter -> true
        | UnicodeCategory.ModifierLetter -> true
        | UnicodeCategory.OtherLetter -> true
        | UnicodeCategory.LetterNumber -> true
        | _ -> false


    let private isIdentifierPartCharacter ch =
        if ch < 'a' then
            if ch < 'A' then
                ch >= '0' && ch <= '9'
            else
                ch <= 'Z' || ch = '_'
        elif ch <= 'z' then
            true
        elif ch <= '\u007F' then
            false
        else
            let cat = ch |> CharUnicodeInfo.GetUnicodeCategory

            if cat |> isLetterChar then
                true
            else
                match cat with
                | UnicodeCategory.DecimalDigitNumber -> true
                | UnicodeCategory.ConnectorPunctuation -> true
                | UnicodeCategory.NonSpacingMark -> true
                | UnicodeCategory.SpacingCombiningMark -> true
                | UnicodeCategory.Format -> true
                | _ -> false

    let private isIdentifierStartCharacter ch =
        if ch < 'a' then
            if ch < 'A' then
                false
            else
                ch <= 'Z' || ch = '_'
        elif ch <= 'z' then
            true
        elif ch <= '\u007F' then
            false
        else
            ch |> CharUnicodeInfo.GetUnicodeCategory |> isLetterChar

    let private handleScope (scope:ICollection<string>) (sb:StringBuilder) =
        if scope |> Seq.isEmpty then
            sb |> string
        else
            let baseId = sb |> string
            let mutable uniqueId = sb |> string
            let mutable qualifier = 0

            while scope |> Seq.contains uniqueId do
                qualifier <- qualifier + 1
                uniqueId <- sprintf "%s%d" baseId qualifier

            uniqueId |> scope.Add
            uniqueId

    let IdentifierWithScope (name:string) (scope:ICollection<string>) =

        let sb = StringBuilder()
        let mutable partStart = 0

        for i = partStart to (name.Length - 1) do
            if name.[i] |> isIdentifierPartCharacter |> not then
                if partStart <> i then
                    sb.Append(name.Substring(partStart, (i - partStart))) |> ignore

                partStart <- i + 1

        if partStart <> name.Length then
            sb.Append(name.Substring(partStart)) |> ignore

        if sb.Length = 0 || sb.[0] |> isIdentifierStartCharacter |> not then
            sb.Insert(0, "_") |> ignore

        let identifier = sb |> handleScope scope

        if _keywords |> Seq.contains identifier then
            sprintf"``%s``" identifier
        else
            identifier

    let Identifier (name:string) =
         IdentifierWithScope name [||]

    let Namespace ([<ParamArray>]name: string array) =

         let join (ns': string array) = String.Join(".", ns')

         let ns =
             name
             |> Array.filter(String.IsNullOrEmpty >> not)
             |> Array.collect(fun n -> n.Split([|'.'|], StringSplitOptions.RemoveEmptyEntries))
             |> Array.map(Identifier)
             |> Array.filter(String.IsNullOrEmpty >> not)
             |> join

         if String.IsNullOrEmpty ns then "_" else ns

    let rec private buildFragment (f: MethodCallCodeFragment) (b: StringBuilder) : StringBuilder =
        let args = f.Arguments |> Seq.map UnknownLiteral |> join ", "

        let result = sprintf ".%s(%s)" f.Method args

        b.Append(result) |> ignore

        match f.ChainedCall |> isNull with
            | false -> buildFragment f.ChainedCall b
            | true -> b

    let Fragment (fragment: MethodCallCodeFragment) =
        buildFragment fragment (StringBuilder()) |> string