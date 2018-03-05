namespace Bricelam.EntityFrameworkCore.FSharp.Internal

open System
open System.Collections.Generic
open System.Globalization
open System.Linq
open System.Reflection
open System.Text
open System.Text.RegularExpressions
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Utilities

open FSharpHelper

module FSharpUtilities = 

    let _primitiveTypeNames =
        [
            (typedefof<bool>, "bool")
            (typedefof<byte>, "byte")
            (typedefof<byte[]>, "byte[]")
            (typedefof<sbyte>, "sbyte")
            (typedefof<char>, "char")
            (typedefof<Int16>, "Int16")
            (typedefof<int>, "int")
            (typedefof<Int64>, "Int64")
            (typedefof<UInt16>, "UInt16")
            (typedefof<UInt32>, "UInt32")
            (typedefof<UInt64>, "UInt64")
            (typedefof<decimal>, "decimal")
            (typedefof<float>, "float")
            (typedefof<double>, "double")
            (typedefof<string>, "string")
            (typedefof<obj>, "obj")
        ] |> dict

    let _fsharpTypeNames =
        [
            ("IEnumerable", "seq")
            ("FSharpList", "list")
            ("FSharpOption", "option")
        ] |> dict

    let escapeString (str: string) =
        str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\t", "\\t")

    let escapeVerbatimString (str: string) =
        str.Replace("\"", "\"\"")

    type LiteralWriter =
        static member GenerateLiteral (value : byte array) =
            "new byte[] {" + String.Join(", ", value) + "}"

        static member GenerateLiteral (value: bool) =
            match value with | true -> "true" | false -> "false"

        static member GenerateLiteral (value: int) =
            value.ToString(CultureInfo.InvariantCulture)

        static member GenerateLiteral (value: Int64) =
            value.ToString(CultureInfo.InvariantCulture) + "L"

        static member GenerateLiteral (value: decimal) =
            value.ToString(CultureInfo.InvariantCulture) + "m"

        static member GenerateLiteral (value: float32) =
            sprintf "(float32 %s)" (value.ToString(CultureInfo.InvariantCulture))

        static member GenerateLiteral (value: double) =
            sprintf "(double %s)" (value.ToString(CultureInfo.InvariantCulture))

        static member GenerateLiteral (value: TimeSpan) =
            sprintf "TimeSpan(%d)" value.Ticks

        static member GenerateLiteral (value: DateTime) =
            sprintf "DateTime(%d, DateTimeKind.%s)" value.Ticks (Enum.GetName(typedefof<DateTimeKind>, value.Kind))

        static member GenerateLiteral (value: DateTimeOffset) =
            sprintf "DateTimeOffset(%d, TimeSpan(%d))" value.Ticks value.Offset.Ticks

        static member GenerateLiteral (value: Guid) =
            sprintf "Guid(%s)" (value |> string)

        static member GenerateLiteral (value:string) =
            sprintf "\"%s\"" (value |> escapeString)

        static member GenerateVerbatimStringLiteral (value:string) =
            sprintf "@\"%s\"" (value |> escapeVerbatimString)      

        static member GenerateLiteral(value: obj) =
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

    let handleMethodCallCodeFragment (sb:StringBuilder) (methodCallCodeFragment: MethodCallCodeFragment) =
        sb.Append("(").Append(methodCallCodeFragment.Method).Append(")")

    let delimitString (str: string) =
        match str.Contains(Environment.NewLine) with
        | true -> str |> escapeVerbatimString |> sprintf "@\"%s\""
        | false -> str |> escapeString |> sprintf "\"%s\""

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
                let args = t.GenericTypeArguments |> Array.map (fun t' -> if isNull t' then failwithf "%s has a null arg" t.Name else t' |> getTypeName)
                match _fsharpTypeNames.TryGetValue genericTypeDefName with
                | true, value -> sprintf "%s %s" (String.Join(", ", args)) value
                | _ -> sprintf "%s<%s>" genericTypeDefName (String.Join(", ", args))
        else
            match _primitiveTypeNames.TryGetValue t with
            | true, value -> value
            | _ -> t.Name

    let generateLiteral(literal:obj) =
        match literal with
        | :? (byte array) as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? bool as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? int as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? Int64 as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? decimal as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? float32 as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? double as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? TimeSpan as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? DateTime as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? DateTimeOffset as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? Guid as literal' -> LiteralWriter.GenerateLiteral(literal')
        | :? string as literal' -> LiteralWriter.GenerateLiteral(literal')
        | _ -> LiteralWriter.GenerateLiteral(literal)
        

    let generate (methodCallCodeFragment: MethodCallCodeFragment) =
        let a' = (methodCallCodeFragment.Arguments |> Seq.map generateLiteral)
        sprintf ".%s(%s)" methodCallCodeFragment.Method (String.Join(", ", a'))
