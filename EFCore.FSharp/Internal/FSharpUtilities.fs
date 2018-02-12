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

module FSharpUtilities = 

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
        | true -> str |> escapeVerbatimString
        | false -> str |> escapeString

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
