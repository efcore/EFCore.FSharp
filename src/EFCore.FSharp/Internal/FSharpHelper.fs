namespace EntityFrameworkCore.FSharp.Internal

open System
open System.Collections.Generic
open System.Linq
open System.Linq.Expressions
open System.Numerics
open System.Text
open Microsoft.EntityFrameworkCore.Internal
open System.Globalization
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.SharedTypeExtensions
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Storage

type FSharpHelper(relationalTypeMappingSource : IRelationalTypeMappingSource) =
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
        ] |> dict

    let _keywords =
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
      
    member private this.ReferenceFullName (t: Type) useFullName =

        match _builtInTypes.TryGetValue t with
        | true, value -> value
        | _ ->
            if t |> isNullableType then 
                sprintf "Nullable<%s>" (this.ReferenceFullName (t |> unwrapNullableType) useFullName)
            elif t |> isOptionType then 
                sprintf "%s option" (this.ReferenceFullName (t |> unwrapOptionType) useFullName)
            else
                let builder = StringBuilder()

                if t.IsArray then
                    builder
                        .Append(this.ReferenceFullName (t.GetElementType()) false)
                        .Append("[") |> ignore

                    (',', t.GetArrayRank()) |> String |> builder.Append |> ignore
                    builder.Append("]") |> ignore

                elif t.IsNested then
                    builder
                        .Append(this.ReferenceFullName (t.DeclaringType) false)
                        .Append(".") |> ignore

                let name =
                    t.DisplayName(useFullName)                    

                builder.Append(name) |> string

    member private this.ensureDecimalPlaces (number:string) =
        if number.IndexOf('.') >= 0 then number else number + ".0"

    member private this.literalString(value: string) =
        if value.Contains(Environment.NewLine) || value.Contains('\r') then
            "@\"" + value.Replace("\"", "\"\"") + "\""
        else
            "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

    member private this.literalBoolean(value: bool) =
        if value then "true" else "false"

    member private this.literalByte (value: byte) = sprintf "(byte %d)" value

    member private this.literalByteArray(values: byte[]) =
        let v = values |> Seq.map this.literalByte
        sprintf "[| %s |]" (String.Join("; ", v))

    member private this.literalStringArray(values: string[]) =
        let v = values |> Seq.map this.literalString
        sprintf "[| %s |]" (String.Join("; ", v))

    member private this.literalArray(values: Array) =
        let v = values.Cast<obj>() |> Seq.map this.unknownLiteral
        sprintf "[| %s |]" (String.Join("; ", v))

    member private this.literalChar(value: char) =
        "\'" + (if value = '\'' then "\\'" else value.ToString()) + "\'"

    member private this.literalDateTime(value: DateTime) =
        sprintf "DateTime(%d, %d, %d, %d, %d, %d,%d, DateTimeKind.%A)"
            value.Year value.Month value.Day value.Hour value.Minute value.Second value.Millisecond value.Kind

    member private this.literalTimeSpan(value: TimeSpan) =
        sprintf "TimeSpan(%d, %d, %d, %d, %d)" value.Days value.Hours value.Minutes value.Seconds value.Milliseconds

    member private this.literalDateTimeOffset(value: DateTimeOffset) =
        sprintf "DateTimeOffset(%s, %s)" (value.DateTime |> this.literalDateTime) (value.Offset |> this.literalTimeSpan)
    
    member private this.literalDecimal(value: decimal) =
        sprintf "%fm" value
    
    member private this.literalDouble(value: double) =
        (value.ToString("R", CultureInfo.InvariantCulture)) |> this.ensureDecimalPlaces
    
    member private this.literalFloat32(value: float32) =
        sprintf "(float32 %f)" value
    
    member private this.literalGuid(value: Guid) =
        sprintf "Guid(\"%A\")" value
    
    member private this.literalInt(value: int) =
        sprintf "%d" value
    
    member private this.literalInt64(value: Int64) =
        sprintf "%dL" value
    
    member private this.literalSByte(value: sbyte) =
        sprintf "(sbyte %d)" value

    member private this.literalInt16(value: Int16) =
        sprintf "%ds" value

    member private this.literalUInt32(value: UInt32) =
        sprintf "%du" value
    
    member private this.literalUInt64(value: UInt64) =
        sprintf "%duL" value
    
    member private this.literalUInt16(value: UInt16) =
        sprintf "%dus" value

    member private this.literalBigInteger(value : BigInteger) =
        sprintf """BigInteger.Parse("%s", NumberFormatInfo.InvariantInfo)""" (value.ToString(NumberFormatInfo.InvariantInfo))
    member private this.literalList (values: IReadOnlyList<obj>) (vertical: bool) (sb:IndentedStringBuilder) =

        let values' = values |> Seq.map this.unknownLiteral

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

    member private this.literalArray2D(values: obj[,]) =

        let rowCount = Array2D.length1 values
        let valuesCount = Array2D.length2 values

        let rowContents =
            [0..rowCount]
            |> Seq.map(fun i ->
                let row' = values.[i, 0..valuesCount]
                let entries = row' |> Seq.map this.unknownLiteral
                sprintf "[ %s ]" (String.Join("; ", entries)) )

        sprintf "array2D [ %s ]" (String.Join("; ", rowContents))

    member private this.handleArguments args sb =

        sb |> append "(" |> ignore

        if (this.handleList args false sb) then
            sb |> append ")" |> ignore
            true
        else false

    member private this.handleList exps simple sb =
        let mutable separator = String.Empty

        let results =
            exps
                |> Seq.map(fun e ->
                    sb |> append separator |> ignore

                    let result = this.handleExpression e simple sb

                    separator <- ", "
                    result )

        results |> Seq.forall (fun r -> r = true)

    member private this.handleExpression (expression:Expression) simple (sb:IndentedStringBuilder) =
        match expression.NodeType with
        | ExpressionType.NewArrayInit ->
            
            sb |> append "[| " |> ignore
            this.handleList (expression :?> NewArrayExpression).Expressions true sb |> ignore
            sb |> append " |]" |> ignore

            true
        | ExpressionType.Convert ->
            sb |> append "(" |> ignore
            let result = this.handleExpression (expression :?> UnaryExpression).Operand false sb
            sb |> append " :?> " |> append (this.ReferenceFullName expression.Type true) |> append ")" |> ignore

            result
        | ExpressionType.New ->
            sb |> append (this.ReferenceFullName expression.Type true) |> ignore                
            this.handleArguments ((expression :?> NewExpression).Arguments) sb

        | ExpressionType.Call ->
            
            let mutable exitEarly = false
            let callExpr = expression :?> MethodCallExpression

            if callExpr.Method.IsStatic then
                sb |> append (this.ReferenceFullName callExpr.Method.DeclaringType true) |> ignore
            else
                if (not (this.handleExpression callExpr.Object false sb)) then
                    exitEarly <- true
            
            if exitEarly then
                false
            else
                sb |> append "." |> append callExpr.Method.Name |> ignore
                this.handleArguments callExpr.Arguments sb

        | ExpressionType.Constant ->
            let value = (expression :?> ConstantExpression).Value
            let valueToWrite =
                if simple && (value.GetType() |> isNumeric) then
                    value |> string
                else
                    this.unknownLiteral(value)

            sb |> append valueToWrite |> ignore

            true
        | _ -> false

    
    member private this.getSimpleEnumValue t name =
        (this.ReferenceFullName t false) + "." + name

    member private this.getFlags (flags : Enum) =
        let t = flags.GetType()
        let defaultValue = Enum.ToObject(t, 0uy) :?> Enum
        Enum.GetValues(t)
        |> Seq.cast<Enum>
        |> Seq.except [| defaultValue |]
        |> Seq.filter flags.HasFlag
        |> Seq.toList
    member private this.getCompositeEnumValue t flags =
        let allValues = flags |> this.getFlags |> HashSet

        allValues
        |> Seq.iter(fun a ->
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
                previous + " | " + this.getSimpleEnumValue t (Enum.GetName(t, current))

        allValues |> Seq.fold folder ""


    member private this.literalEnum (value : Enum) =
        let t = value.GetType()
        let name = Enum.GetName(t, value)

        if isNull name then
            this.getCompositeEnumValue t value
        else
            this.getSimpleEnumValue t name

    member private this.LiteralList (vertical: bool) (sb:IndentedStringBuilder) (values: IReadOnlyList<obj>) =
        this.literalList values vertical sb


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
                ch >= '0' && ch <= '9'
            else
                ch <= 'Z' || ch = '_'
        elif ch <= 'z' then
            true
        elif ch <= '\u007F' then
            false
        else
            let cat = ch |> CharUnicodeInfo.GetUnicodeCategory

            if cat |> this.isLetterChar then
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
                ch <= 'Z' || ch = '_'
        elif ch <= 'z' then
            true
        elif ch <= '\u007F' then
            false
        else
            ch |> CharUnicodeInfo.GetUnicodeCategory |> this.isLetterChar

    member private this.handleScope (scope:ICollection<string>) (sb:StringBuilder) =
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

    member private this.IdentifierWithScope (name:string) (scope:ICollection<string>) =

        let sb = StringBuilder()
        let mutable partStart = 0

        for i = partStart to (name.Length - 1) do
            if name.[i] |> this.isIdentifierPartCharacter |> not then
                if partStart <> i then
                    sb.Append(name.Substring(partStart, (i - partStart))) |> ignore

                partStart <- i + 1

        if partStart <> name.Length then
            sb.Append(name.Substring(partStart)) |> ignore

        if sb.Length = 0 || sb.[0] |> this.isIdentifierStartCharacter |> not then
            sb.Insert(0, "_") |> ignore

        let identifier = sb |> this.handleScope scope

        if _keywords |> Seq.contains identifier then
            sprintf"``%s``" identifier
        else
            identifier

    member private this.buildFragment (f: MethodCallCodeFragment) (b: StringBuilder) : StringBuilder =
        let args = f.Arguments |> Seq.map this.unknownLiteral |> join ", "

        let result = sprintf ".%s(%s)" f.Method args

        b.Append(result) |> ignore

        if isNull f.ChainedCall then
            b
        else        
            this.buildFragment f.ChainedCall b

    member private this.unknownLiteral (value: obj) =
        if isNull value then
            "null"
        else
            match value with
            | :? DBNull -> "null"
            | :? Enum as e -> this.literalEnum e
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
            | _ ->

                let literalType = value.GetType()
                let mapping = relationalTypeMappingSource.FindMapping literalType

                if isNull mapping then
                    let t = value.GetType()
                    let type' =
                        if t |> isNullableType then t |> unwrapNullableType
                        elif t |> isOptionType then t |> unwrapOptionType
                        else t
                    invalidOp (type' |> DesignStrings.UnknownLiteral)
                else
                    let builder = new IndentedStringBuilder()
                    let expression = mapping.GenerateCodeLiteral(value)
                    let handled = this.handleExpression expression false builder

                    if handled then
                        builder.ToString()
                    else
                        let args = (
                                (expression.ToString()),
                                (literalType.DisplayName(false))
                            )

                        args |> DesignStrings.LiteralExpressionNotSupported |> NotSupportedException |> raise
                         


    interface ICSharpHelper with
        member this.Fragment (fragment: MethodCallCodeFragment) =
            this.buildFragment fragment (StringBuilder()) |> string

        member this.Identifier(name: string, scope: ICollection<string>): string = 
            if isNull scope then
                this.IdentifierWithScope name [||]
            else
                this.IdentifierWithScope name scope

        member this.Lambda(properties: IReadOnlyList<string>): string = 
            StringBuilder()
                .Append("(fun x -> ")
                .Append("(")
                .Append(String.Join(", ", (properties |> Seq.map(fun p -> "x." + p))))
                .Append(") :> obj)") |> string

        member this.Literal(values: obj [,]): string = 
            this.literalArray2D values

        member this.Literal(value: Nullable<'T>): string = 
            this.unknownLiteral value

        member this.Literal(value: bool): string = 
            this.literalBoolean value

        member this.Literal(value: byte): string = 
            this.literalByte value

        member this.Literal(value: char): string = 
            this.literalChar value

        member this.Literal(value: DateTime): string = 
            this.literalDateTime value

        member this.Literal(value: DateTimeOffset): string = 
            this.literalDateTimeOffset value

        member this.Literal(value: decimal): string = 
            this.literalDecimal value

        member this.Literal(value: float): string = 
            this.literalDouble value

        member this.Literal(value: Enum): string = 
            this.literalEnum value

        member this.Literal(value: float32): string = 
            this.literalFloat32 value

        member this.Literal(value: Guid): string = 
            this.literalGuid value

        member this.Literal(value: int): string = 
            this.literalInt value

        member this.Literal(value: int64): string = 
            this.literalInt64 value

        member this.Literal(value: sbyte): string = 
            this.literalSByte value

        member this.Literal(value: int16): string = 
            this.literalInt16 value

        member this.Literal(value: string): string = 
            this.literalString value

        member this.Literal(value: TimeSpan) =
            this.literalTimeSpan value

        member this.Literal(value: UInt32) =
            this.literalUInt32 value

        member this.Literal(value: UInt16) =
            this.literalUInt16 value

        member this.Literal(value: UInt64) =
            this.literalUInt64 value

        member this.Literal(values: 'T [], vertical: bool): string = 
            this.literalList (values |> Seq.cast<obj> |> ResizeArray) vertical (IndentedStringBuilder())

        member this.Namespace(name: string []): string = 
            let join (ns': string array) = String.Join(".", ns')
            
            let ns =
                name
                |> Array.filter(String.IsNullOrEmpty >> not)
                |> Array.collect(fun n -> n.Split([|'.'|], StringSplitOptions.RemoveEmptyEntries))
                |> Array.map(fun t -> (this :> ICSharpHelper).Identifier(t, null))
                |> Array.filter(String.IsNullOrEmpty >> not)
                |> join
            
            if String.IsNullOrEmpty ns then "_" else ns

        member this.Reference(t: Type): string = 
            this.ReferenceFullName t false

        member this.UnknownLiteral(value: obj): string = 
            this.unknownLiteral value

        