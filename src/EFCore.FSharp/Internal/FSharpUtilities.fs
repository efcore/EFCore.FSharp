namespace EntityFrameworkCore.FSharp.Internal

open System
open System.Globalization
open System.Reflection
open System.Text
open Microsoft.EntityFrameworkCore.Design

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

module FSharpUtilities =

    let private _primitiveTypeNames =
        [
            (typeof<bool>, "bool")
            (typeof<byte>, "byte")
            (typeof<byte[]>, "byte[]")
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

    let private _fsharpTypeNames =
        [
            ("IEnumerable", "seq")
            ("FSharpList", "list")
            ("FSharpOption", "option")
            ("List", "ResizeArray")
        ] |> dict

    let private escapeString (str: string) =
        str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\t", "\\t")

    let private escapeVerbatimString (str: string) =
        str.Replace("\"", "\"\"")

    let private generateLiteralByteArray (value : byte array) =
        "new byte[] {" + String.Join(", ", value) + "}"

    let private generateLiteralStringArray (value: string array) =
        "[| " + (value |> Array.fold (fun c n -> c + "\"" + n + "\"; ") "") + "|]"

    let private generateLiteralBool (value: bool) =
        if value then "true" else "false"

    let private generateLiteralInt32 (value: int) =
        value.ToString(CultureInfo.InvariantCulture)

    let private generateLiteralInt64 (value: Int64) =
        value.ToString(CultureInfo.InvariantCulture) + "L"

    let private generateLiteralDecimal (value: decimal) =
        value.ToString(CultureInfo.InvariantCulture) + "m"

    let private generateLiteralFloat32 (value: float32) =
        sprintf "(float32 %s)" (value.ToString(CultureInfo.InvariantCulture))

    let private generateLiteralDouble (value: double) =
        sprintf "(double %s)" (value.ToString(CultureInfo.InvariantCulture))

    let private generateLiteralTimeSpan (value: TimeSpan) =
        sprintf "TimeSpan(%d)" value.Ticks

    let private generateLiteralDateTime (value: DateTime) =
        sprintf "DateTime(%d, DateTimeKind.%s)" value.Ticks (Enum.GetName(typedefof<DateTimeKind>, value.Kind))

    let private generateLiteralDateTimeOffset (value: DateTimeOffset) =
        sprintf "DateTimeOffset(%d, TimeSpan(%d))" value.Ticks value.Offset.Ticks

    let private generateLiteralGuid (value: Guid) =
        sprintf "Guid(%s)" (value |> string)

    let private generateLiteralString (value:string) =
        sprintf "\"%s\"" (value |> escapeString)

    let private generateLiteralVerbatimString (value:string) =
        sprintf "@\"%s\"" (value |> escapeVerbatimString)

    let private generateLiteralObject (value: obj) =
        let valType = value.GetType()
        if valType.GetTypeInfo().IsEnum then
            sprintf "%s.%s" valType.Name (Enum.Format(valType, value, "G"))
        else
            String.Format(CultureInfo.InvariantCulture, "{0}", value)


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

    let isKeyword str =
        _keywords |> Seq.contains str

    let handleMethodCallCodeFragment (sb:StringBuilder) (methodCallCodeFragment: MethodCallCodeFragment) =
        sb.Append("(").Append(methodCallCodeFragment.Method).Append(")")

    let delimitString (str: string) =
        if str.Contains(Environment.NewLine) then
            str |> escapeVerbatimString |> sprintf "@\"%s\""
        else
            str |> escapeString |> sprintf "\"%s\""

    let rec getTypeName (t:Type) =
        if isNull t then
            failwith "t is null"
        elif t.IsArray then
            sprintf "%s[]" (t.GetElementType() |> getTypeName)
        elif t.GetTypeInfo().IsGenericType then
            if t |> isNullableType then sprintf "Nullable<%s>" (t |> unwrapNullableType |> getTypeName)
            elif t |> isOptionType then sprintf "%s option" (t |> unwrapOptionType |> getTypeName)
            else
                let genericTypeDefName = t.Name.Substring(0, t.Name.IndexOf('`'))
                let args =
                    t.GenericTypeArguments
                    |> Array.map (fun t' -> if isNull t' then failwithf "%s has a null arg" t.Name else t' |> getTypeName)
                    |> join ", "

                match _fsharpTypeNames.TryGetValue genericTypeDefName with
                | true, value -> sprintf "%s %s" args value
                | _ -> sprintf "%s<%s>" genericTypeDefName args
        else
            match _primitiveTypeNames.TryGetValue t with
            | true, value -> value
            | _ -> t.Name

    let generateLiteral(literal:obj) =
        match literal with
        | :? (byte array) as literal' -> generateLiteralByteArray(literal')
        | :? (string array) as literal' -> generateLiteralStringArray(literal')
        | :? bool as literal' -> generateLiteralBool(literal')
        | :? int as literal' -> generateLiteralInt32(literal')
        | :? Int64 as literal' -> generateLiteralInt64(literal')
        | :? decimal as literal' -> generateLiteralDecimal(literal')
        | :? float32 as literal' -> generateLiteralFloat32(literal')
        | :? double as literal' -> generateLiteralDouble(literal')
        | :? TimeSpan as literal' -> generateLiteralTimeSpan(literal')
        | :? DateTime as literal' -> generateLiteralDateTime(literal')
        | :? DateTimeOffset as literal' -> generateLiteralDateTimeOffset(literal')
        | :? Guid as literal' -> generateLiteralGuid(literal')
        | :? string as literal' -> generateLiteralString(literal')        
        | _ -> generateLiteralObject(literal)


    let generate (methodCallCodeFragment: MethodCallCodeFragment) =
        let parameters =
            methodCallCodeFragment.Arguments
            |> Seq.map generateLiteral
            |> join ", "

        sprintf ".%s(%s)" methodCallCodeFragment.Method parameters
