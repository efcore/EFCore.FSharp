namespace Bricelam.EntityFrameworkCore.FSharp.Internal

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open System
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Internal

module FSharpHelper =

    let private _builtInTypes = new Dictionary<Type, string>()
    _builtInTypes.Add(typedefof<bool>, "bool")
    _builtInTypes.Add(typedefof<byte>, "byte")
    _builtInTypes.Add(typedefof<sbyte>, "sbyte")
    _builtInTypes.Add(typedefof<char>, "char")
    _builtInTypes.Add(typedefof<Int16>, "Int16")
    _builtInTypes.Add(typedefof<int>, "int")
    _builtInTypes.Add(typedefof<Int64>, "Int64")
    _builtInTypes.Add(typedefof<UInt16>, "UInt16")
    _builtInTypes.Add(typedefof<UInt32>, "UInt32")
    _builtInTypes.Add(typedefof<UInt64>, "UInt64")
    _builtInTypes.Add(typedefof<decimal>, "decimal")
    _builtInTypes.Add(typedefof<float>, "float")
    _builtInTypes.Add(typedefof<double>, "double")
    _builtInTypes.Add(typedefof<string>, "string")
    _builtInTypes.Add(typedefof<obj>, "obj")

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

    let private isNullableType (t:Type) =
        let typeInfo = t.GetTypeInfo()
        (not typeInfo.IsValueType)
        || typeInfo.IsGenericType
        && typeInfo.GetGenericTypeDefinition() = typedefof<Nullable<_>>

    let private isOptionType (t:Type) =
        let typeInfo = t.GetTypeInfo()
        (not typeInfo.IsValueType)
        || typeInfo.IsGenericType
        && typeInfo.GetGenericTypeDefinition() = typedefof<Option<_>>

    let private isOptionOrNullableType (t:Type) =
        (t |> isNullableType) || (t |> isOptionType)

    let private unwrapNullableType (t:Type) =
        match t |> isNullableType with    
        | true -> t |> Nullable.GetUnderlyingType
        | false -> t

    let private unwrapOptionType (t:Type) =
        match t |> isOptionType with    
        | true -> t.GenericTypeArguments.[0]
        | false -> t    

    // TODO
    // _literalFuncs

    let Lambda (properties: IReadOnlyList<string>) =
        let builder = StringBuilder()
        builder.Append("(fun x -> ") |> ignore

        match properties.Count with
        | 1 -> builder.Append("x.").Append(properties.[0]) |> ignore
        | _ -> builder.Append("(").Append(String.Join(", ", (properties |> Seq.map(fun p -> "x." + p)))).Append(" :> obj)") |> ignore

        builder.Append(")") |> string

    let rec Reference (t: Type) =

        match _builtInTypes.TryGetValue t with
        | true, value -> value
        | _ ->
            if t |> isNullableType then sprintf "Nullable<%s>" (t |> unwrapNullableType |> Reference)
            elif t |> isOptionType then sprintf "%s option" (t |> unwrapOptionType |> Reference)
            else
                let builder = StringBuilder()

                if t.IsArray then
                    builder.Append(Reference(t.GetElementType())).Append("[") |> ignore
                    (',', t.GetArrayRank()) |> String |> builder.Append |> ignore    
                    builder.Append("]") |> ignore
                    
                elif t.IsNested then
                    builder.Append(Reference(t.DeclaringType)).Append(".") |> ignore

                builder.Append(t.ShortDisplayName()) |> string



        