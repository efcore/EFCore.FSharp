namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open Microsoft.FSharp.Linq.NullableOperators
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations

module FSharpMigrationOperationGenerator =

    type OperationWriter =        

        static member private writeParameter name value sb =
            sb
                |> appendLine ","
                |> append name |> append " = " |> appendLine (value |> FSharpHelper.Literal)

        static member private writeParameterIfTrue trueOrFalse name value sb =
            match trueOrFalse with
            | true -> sb |> OperationWriter.writeParameter name value
            | false -> sb

        static member private writeOptionalParameter (name:string) value (sb:IndentedStringBuilder) =
            sb |> OperationWriter.writeParameterIfTrue (isNull value |> not) name value

        static member private writeNullable name (nullableParamter: Nullable<_>) sb =
            sb |> OperationWriter.writeParameterIfTrue nullableParamter.HasValue name nullableParamter.Value

        static member private Annotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
            annotations
                |> Seq.iter(fun a ->
                    sb
                    |> appendLine ""
                    |> append ".Annotation("
                    |> append (FSharpHelper.LiteralWriter.Literal a.Name)
                    |> append ", "
                    |> append (FSharpHelper.LiteralWriter.UnknownLiteral a.Value)
                    |> append ")"
                    |> ignore
                )
            sb

        static member private OldAnnotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
            annotations
                |> Seq.iter(fun a ->
                    sb
                    |> appendLine ""
                    |> append ".OldAnnotation("
                    |> append (FSharpHelper.LiteralWriter.Literal a.Name)
                    |> append ", "
                    |> append (FSharpHelper.LiteralWriter.UnknownLiteral a.Value)
                    |> append ")"
                    |> ignore
                )
            sb

        static member Generate (op:MigrationOperation, sb:IndentedStringBuilder) :IndentedStringBuilder =
            invalidOp ((op.GetType()) |> DesignStrings.UnknownOperation)

        static member Generate (op:AddColumnOperation, sb:IndentedStringBuilder) =

            sb
                |> append ".AddColumn<"
                |> append (op.ClrType |> FSharpHelper.Reference)
                |> appendLine ">("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeOptionalParameter "type" op.ColumnType
                |> OperationWriter.writeParameterIfTrue (op.IsUnicode ?= false) "unicode" "false"
                |> OperationWriter.writeNullable "maxLength" op.MaxLength
                |> OperationWriter.writeParameterIfTrue op.IsRowVersion "rowVersion" true
                |> OperationWriter.writeParameter "nullable" op.IsNullable
                |>
                    if not(isNull op.DefaultValueSql) then
                        OperationWriter.writeParameter "defaultValueSql" op.DefaultValueSql
                    elif not(isNull op.ComputedColumnSql) then
                        OperationWriter.writeParameter "computedColumnSql" op.ComputedColumnSql
                    elif not(isNull op.DefaultValue) then
                        OperationWriter.writeParameter "defaultValue" op.DefaultValue
                    else
                        append ""
                |> append ")"                

                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:AddForeignKeyOperation, sb:IndentedStringBuilder) =

            sb
                |> appendLine ".AddForeignKey("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
                |> OperationWriter.writeOptionalParameter "principalSchema" op.PrincipalSchema
                |> OperationWriter.writeParameter "principalTable" op.PrincipalTable
                |> OperationWriter.writeParameterIfTrue (op.PrincipalColumns.Length = 1) "principalColumn" op.PrincipalColumns.[0]
                |> OperationWriter.writeParameterIfTrue (op.PrincipalColumns.Length <> 1) "principalColumns" op.PrincipalColumns
                |> OperationWriter.writeParameterIfTrue (op.OnUpdate <> ReferentialAction.NoAction) "onUpdate" op.OnUpdate
                |> OperationWriter.writeParameterIfTrue (op.OnDelete <> ReferentialAction.NoAction) "onDelete" op.OnDelete
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:AddPrimaryKeyOperation, sb:IndentedStringBuilder) =
            sb
                |> appendLine ".AddPrimaryKey("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:AddUniqueConstraintOperation, sb:IndentedStringBuilder) =
            sb
                |> appendLine ".AddUniqueConstraint("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:AlterColumnOperation, sb:IndentedStringBuilder) =
            sb
                |> append ".AlterColumn<"
                |> append (op.ClrType |> FSharpHelper.Reference)
                |> appendLine ">("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeOptionalParameter "type" op.ColumnType
                |> OperationWriter.writeParameterIfTrue (op.IsUnicode ?= false) "unicode" "false"
                |> OperationWriter.writeNullable "maxLength" op.MaxLength
                |> OperationWriter.writeParameterIfTrue op.IsRowVersion "rowVersion" true
                |> OperationWriter.writeParameter "nullable" op.IsNullable
                |>
                    if not(isNull op.DefaultValueSql) then
                        OperationWriter.writeParameter "defaultValueSql" op.DefaultValueSql
                    elif not(isNull op.ComputedColumnSql) then
                        OperationWriter.writeParameter "computedColumnSql" op.ComputedColumnSql
                    elif not(isNull op.DefaultValue) then
                        OperationWriter.writeParameter "defaultValue" op.DefaultValue
                    else
                        append ""
                |> OperationWriter.writeParameterIfTrue (op.OldColumn.ClrType |> isNull |> not) "oldType" (sprintf "typedefof<%s>" (op.OldColumn.ClrType |> FSharpHelper.Reference))
                |> OperationWriter.writeOptionalParameter "oldType" op.OldColumn.ColumnType
                |> OperationWriter.writeParameterIfTrue (op.OldColumn.IsUnicode ?= false) "oldUnicode" "false"
                |> OperationWriter.writeNullable "oldMaxLength" op.OldColumn.MaxLength
                |> OperationWriter.writeParameterIfTrue op.OldColumn.IsRowVersion "oldRowVersion" true
                |> OperationWriter.writeParameter "oldNullable" op.OldColumn.IsNullable
                |>
                    if not(isNull op.OldColumn.DefaultValueSql) then
                        OperationWriter.writeParameter "oldDefaultValueSql" op.OldColumn.DefaultValueSql
                    elif not(isNull op.OldColumn.ComputedColumnSql) then
                        OperationWriter.writeParameter "oldComputedColumnSql" op.OldColumn.ComputedColumnSql
                    elif not(isNull op.OldColumn.DefaultValue) then
                        OperationWriter.writeParameter "oldDefaultValue" op.OldColumn.DefaultValue
                    else
                        append ""
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> OperationWriter.OldAnnotations (op.OldColumn.GetAnnotations())
                |> unindent


        static member Generate (op:AlterDatabaseOperation , sb:IndentedStringBuilder) =            
            sb
                |> appendLine ".AlterDatabase()"
                |> indent
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> OperationWriter.OldAnnotations (op.OldDatabase.GetAnnotations())
                |> unindent

        static member Generate (op:AlterSequenceOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".AlterSequence("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
                |> OperationWriter.writeNullable "minValue " op.MinValue
                |> OperationWriter.writeNullable "maxValue " op.MaxValue
                |> OperationWriter.writeParameterIfTrue op.IsCyclic "cyclic" "true"
                |> OperationWriter.writeParameterIfTrue (op.OldSequence.IncrementBy <> 1) "oldIncrementBy" op.OldSequence.IncrementBy
                |> OperationWriter.writeNullable "oldMinValue " op.OldSequence.MinValue
                |> OperationWriter.writeNullable "oldMaxValue " op.OldSequence.MaxValue
                |> OperationWriter.writeParameterIfTrue op.OldSequence.IsCyclic "oldCyclic" "true"
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> OperationWriter.OldAnnotations (op.OldSequence.GetAnnotations())
                |> unindent

        static member Generate (op:AlterTableOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".AlterTable("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> OperationWriter.OldAnnotations (op.OldTable.GetAnnotations())
                |> unindent

        static member Generate (op:CreateIndexOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".CreateIndex("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                |> OperationWriter.writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
                |> OperationWriter.writeParameterIfTrue op.IsUnique "unique" "true"
                |> OperationWriter.writeOptionalParameter "filter" op.Filter
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:EnsureSchemaOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".EnsureSchema("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:CreateSequenceOperation , sb:IndentedStringBuilder) =
            sb
                |> append ".CreateSequence"
                |>
                    if op.ClrType <> typedefof<Int64> then
                        append (sprintf "<%s>" (op.ClrType |> FSharpHelper.Reference))
                    else
                        append ""
                |> appendLine "("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameterIfTrue (op.StartValue <> 1L) "startValue" op.StartValue
                |> OperationWriter.writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
                |> OperationWriter.writeNullable "minValue " op.MinValue
                |> OperationWriter.writeNullable "maxValue " op.MaxValue
                |> OperationWriter.writeParameterIfTrue op.IsCyclic "cyclic" "true"
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent


        static member Generate (op:CreateTableOperation , sb:IndentedStringBuilder) =
            //TODO: implement
            sb

        static member Generate (op:DropColumnOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropColumn("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropForeignKeyOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropForeignKey("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropIndexOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropIndex("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropPrimaryKeyOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropPrimaryKey("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropSchemaOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropSchema("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropSequenceOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropSequence("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropTableOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropTable("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:DropUniqueConstraintOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".DropUniqueConstraint("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:RenameColumnOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".RenameColumn("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeParameter "newName" op.NewName
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:RenameIndexOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".RenameIndex("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "table" op.Table
                |> OperationWriter.writeParameter "newName" op.NewName
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:RenameSequenceOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".RenameSequence("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "newName" op.NewName
                |> OperationWriter.writeParameter "newSchema" op.NewSchema
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:RenameTableOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".RenameTable("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "newName" op.NewName
                |> OperationWriter.writeParameter "newSchema" op.NewSchema
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:RestartSequenceOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine ".RestartSequence("
                |> indent
                |> OperationWriter.writeParameter "name" op.Name
                |> OperationWriter.writeOptionalParameter "schema" op.Schema
                |> OperationWriter.writeParameter "startValue" op.StartValue
                |> append ")"
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:SqlOperation , sb:IndentedStringBuilder) =
            sb
                |> appendLine (sprintf ".Sql(%s)" (op.Sql |> FSharpHelper.Literal))
                |> indent
                |> OperationWriter.Annotations (op.GetAnnotations())
                |> unindent

        static member Generate (op:InsertDataOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            sb

        static member Generate (op:DeleteDataOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            sb

        static member Generate (op:UpdateDataOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            sb

    let private generateOperation (op:MigrationOperation) =
        let curry f x y = f (x, y)

        match op with
        | :? AddColumnOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AddForeignKeyOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AddPrimaryKeyOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AddUniqueConstraintOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AlterColumnOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AlterDatabaseOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AlterSequenceOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? AlterTableOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? CreateIndexOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? EnsureSchemaOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? CreateSequenceOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? CreateTableOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropColumnOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropForeignKeyOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropIndexOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropPrimaryKeyOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropSchemaOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropSequenceOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropTableOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DropUniqueConstraintOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? RenameColumnOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? RenameIndexOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? RenameSequenceOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? RenameTableOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? RestartSequenceOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? SqlOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? InsertDataOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? DeleteDataOperation as op' -> op' |> (curry OperationWriter.Generate)
        | :? UpdateDataOperation as op' -> op' |> (curry OperationWriter.Generate)
        | _ -> op |> (curry OperationWriter.Generate) // The failure case

    let Generate (builderName:string) (operations: IReadOnlyList<MigrationOperation>) (sb:IndentedStringBuilder) =
        operations
            |> Seq.iter(fun op -> 
                sb
                    |> appendLine builderName
                    |> generateOperation op
                    |> appendLine ""
                    |> appendLine ""
                    |> ignore
            )
        sb
