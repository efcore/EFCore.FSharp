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

    let private writeParameter name value sb =
        sb
            |> appendLine ","
            |> append name |> append " = " |> appendLine (value |> FSharpHelper.Literal)

    let private writeParameterIfTrue trueOrFalse name value sb =
        match trueOrFalse with
        | true -> sb |> writeParameter name value
        | false -> sb

    let private writeOptionalParameter (name:string) value (sb:IndentedStringBuilder) =
        sb |> writeParameterIfTrue (isNull value |> not) name value

    let private writeNullable name (nullableParamter: Nullable<_>) sb =
        sb |> writeParameterIfTrue nullableParamter.HasValue name nullableParamter.Value

    let private annotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
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

    let private oldAnnotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
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

    let private generateMigrationOperation (op:MigrationOperation) (sb:IndentedStringBuilder) :IndentedStringBuilder =
        invalidOp ((op.GetType()) |> DesignStrings.UnknownOperation)

    let private generateAddColumnOperation (op:AddColumnOperation) (sb:IndentedStringBuilder) =

        sb
            |> append ".AddColumn<"
            |> append (op.ClrType |> FSharpHelper.Reference)
            |> appendLine ">("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeOptionalParameter "type" op.ColumnType
            |> writeParameterIfTrue (op.IsUnicode ?= false) "unicode" "false"
            |> writeNullable "maxLength" op.MaxLength
            |> writeParameterIfTrue op.IsRowVersion "rowVersion" true
            |> writeParameter "nullable" op.IsNullable
            |>
                if not(isNull op.DefaultValueSql) then
                    writeParameter "defaultValueSql" op.DefaultValueSql
                elif not(isNull op.ComputedColumnSql) then
                    writeParameter "computedColumnSql" op.ComputedColumnSql
                elif not(isNull op.DefaultValue) then
                    writeParameter "defaultValue" op.DefaultValue
                else
                    append ""
            |> append ")"                

            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAddForeignKeyOperation (op:AddForeignKeyOperation) (sb:IndentedStringBuilder) =

        sb
            |> appendLine ".AddForeignKey("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> writeOptionalParameter "principalSchema" op.PrincipalSchema
            |> writeParameter "principalTable" op.PrincipalTable
            |> writeParameterIfTrue (op.PrincipalColumns.Length = 1) "principalColumn" op.PrincipalColumns.[0]
            |> writeParameterIfTrue (op.PrincipalColumns.Length <> 1) "principalColumns" op.PrincipalColumns
            |> writeParameterIfTrue (op.OnUpdate <> ReferentialAction.NoAction) "onUpdate" op.OnUpdate
            |> writeParameterIfTrue (op.OnDelete <> ReferentialAction.NoAction) "onDelete" op.OnDelete
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAddPrimaryKeyOperation (op:AddPrimaryKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AddPrimaryKey("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAddUniqueConstraintOperation (op:AddUniqueConstraintOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AddUniqueConstraint("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAlterColumnOperation (op:AlterColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> append ".AlterColumn<"
            |> append (op.ClrType |> FSharpHelper.Reference)
            |> appendLine ">("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeOptionalParameter "type" op.ColumnType
            |> writeParameterIfTrue (op.IsUnicode ?= false) "unicode" "false"
            |> writeNullable "maxLength" op.MaxLength
            |> writeParameterIfTrue op.IsRowVersion "rowVersion" true
            |> writeParameter "nullable" op.IsNullable
            |>
                if not(isNull op.DefaultValueSql) then
                    writeParameter "defaultValueSql" op.DefaultValueSql
                elif not(isNull op.ComputedColumnSql) then
                    writeParameter "computedColumnSql" op.ComputedColumnSql
                elif not(isNull op.DefaultValue) then
                    writeParameter "defaultValue" op.DefaultValue
                else
                    append ""
            |> writeParameterIfTrue (op.OldColumn.ClrType |> isNull |> not) "oldType" (sprintf "typedefof<%s>" (op.OldColumn.ClrType |> FSharpHelper.Reference))
            |> writeOptionalParameter "oldType" op.OldColumn.ColumnType
            |> writeParameterIfTrue (op.OldColumn.IsUnicode ?= false) "oldUnicode" "false"
            |> writeNullable "oldMaxLength" op.OldColumn.MaxLength
            |> writeParameterIfTrue op.OldColumn.IsRowVersion "oldRowVersion" true
            |> writeParameter "oldNullable" op.OldColumn.IsNullable
            |>
                if not(isNull op.OldColumn.DefaultValueSql) then
                    writeParameter "oldDefaultValueSql" op.OldColumn.DefaultValueSql
                elif not(isNull op.OldColumn.ComputedColumnSql) then
                    writeParameter "oldComputedColumnSql" op.OldColumn.ComputedColumnSql
                elif not(isNull op.OldColumn.DefaultValue) then
                    writeParameter "oldDefaultValue" op.OldColumn.DefaultValue
                else
                    append ""
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldColumn.GetAnnotations())
            |> unindent


    let private generateAlterDatabaseOperation (op:AlterDatabaseOperation) (sb:IndentedStringBuilder) =            
        sb
            |> appendLine ".AlterDatabase()"
            |> indent
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldDatabase.GetAnnotations())
            |> unindent

    let private generateAlterSequenceOperation (op:AlterSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterSequence("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
            |> writeNullable "minValue " op.MinValue
            |> writeNullable "maxValue " op.MaxValue
            |> writeParameterIfTrue op.IsCyclic "cyclic" "true"
            |> writeParameterIfTrue (op.OldSequence.IncrementBy <> 1) "oldIncrementBy" op.OldSequence.IncrementBy
            |> writeNullable "oldMinValue " op.OldSequence.MinValue
            |> writeNullable "oldMaxValue " op.OldSequence.MaxValue
            |> writeParameterIfTrue op.OldSequence.IsCyclic "oldCyclic" "true"
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldSequence.GetAnnotations())
            |> unindent

    let private generateAlterTableOperation (op:AlterTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterTable("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldTable.GetAnnotations())
            |> unindent

    let private generateCreateIndexOperation (op:CreateIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".CreateIndex("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> writeParameterIfTrue op.IsUnique "unique" "true"
            |> writeOptionalParameter "filter" op.Filter
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateEnsureSchemaOperation (op:EnsureSchemaOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".EnsureSchema("
            |> indent
            |> writeParameter "name" op.Name
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateCreateSequenceOperation (op:CreateSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> append ".CreateSequence"
            |>
                if op.ClrType <> typedefof<Int64> then
                    append (sprintf "<%s>" (op.ClrType |> FSharpHelper.Reference))
                else
                    append ""
            |> appendLine "("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameterIfTrue (op.StartValue <> 1L) "startValue" op.StartValue
            |> writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
            |> writeNullable "minValue " op.MinValue
            |> writeNullable "maxValue " op.MaxValue
            |> writeParameterIfTrue op.IsCyclic "cyclic" "true"
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent


    let private generateCreateTableOperation (op:CreateTableOperation) (sb:IndentedStringBuilder) =
        //TODO: implement
        sb

    let private generateDropColumnOperation (op:DropColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropColumn("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropForeignKeyOperation (op:DropForeignKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropForeignKey("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropIndexOperation (op:DropIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropIndex("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropPrimaryKeyOperation (op:DropPrimaryKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropPrimaryKey("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropSchemaOperation (op:DropSchemaOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropSchema("
            |> indent
            |> writeParameter "name" op.Name
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropSequenceOperation (op:DropSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropSequence("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropTableOperation (op:DropTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropTable("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropUniqueConstraintOperation (op:DropUniqueConstraintOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropUniqueConstraint("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameColumnOperation (op:RenameColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameColumn("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameter "newName" op.NewName
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameIndexOperation (op:RenameIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameIndex("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameter "newName" op.NewName
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameSequenceOperation (op:RenameSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameSequence("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "newName" op.NewName
            |> writeParameter "newSchema" op.NewSchema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameTableOperation (op:RenameTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameTable("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "newName" op.NewName
            |> writeParameter "newSchema" op.NewSchema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRestartSequenceOperation (op:RestartSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RestartSequence("
            |> indent
            |> writeParameter "name" op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "startValue" op.StartValue
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateSqlOperation (op:SqlOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine (sprintf ".Sql(%s)" (op.Sql |> FSharpHelper.Literal))
            |> indent
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateInsertDataOperation (op:InsertDataOperation) (sb:IndentedStringBuilder) =
        // TODO: implement
        sb

    let private generateDeleteDataOperation (op:DeleteDataOperation) (sb:IndentedStringBuilder) =
        // TODO: implement
        sb

    let private generateUpdateDataOperation (op:UpdateDataOperation) (sb:IndentedStringBuilder) =
        // TODO: implement
        sb

    let private generateOperation (op:MigrationOperation) =
        match op with
        | :? AddColumnOperation as op' -> op' |> generateAddColumnOperation
        | :? AddForeignKeyOperation as op' -> op' |> generateAddForeignKeyOperation
        | :? AddPrimaryKeyOperation as op' -> op' |> generateAddPrimaryKeyOperation
        | :? AddUniqueConstraintOperation as op' -> op' |> generateAddUniqueConstraintOperation
        | :? AlterColumnOperation as op' -> op' |> generateAlterColumnOperation
        | :? AlterDatabaseOperation as op' -> op' |> generateAlterDatabaseOperation
        | :? AlterSequenceOperation as op' -> op' |> generateAlterSequenceOperation
        | :? AlterTableOperation as op' -> op' |> generateAlterTableOperation
        | :? CreateIndexOperation as op' -> op' |> generateCreateIndexOperation
        | :? EnsureSchemaOperation as op' -> op' |> generateEnsureSchemaOperation
        | :? CreateSequenceOperation as op' -> op' |> generateCreateSequenceOperation
        | :? CreateTableOperation as op' -> op' |> generateCreateTableOperation
        | :? DropColumnOperation as op' -> op' |> generateDropColumnOperation
        | :? DropForeignKeyOperation as op' -> op' |> generateDropForeignKeyOperation
        | :? DropIndexOperation as op' -> op' |> generateDropIndexOperation
        | :? DropPrimaryKeyOperation as op' -> op' |> generateDropPrimaryKeyOperation
        | :? DropSchemaOperation as op' -> op' |> generateDropSchemaOperation
        | :? DropSequenceOperation as op' -> op' |> generateDropSequenceOperation
        | :? DropTableOperation as op' -> op' |> generateDropTableOperation
        | :? DropUniqueConstraintOperation as op' -> op' |> generateDropUniqueConstraintOperation
        | :? RenameColumnOperation as op' -> op' |> generateRenameColumnOperation
        | :? RenameIndexOperation as op' -> op' |> generateRenameIndexOperation
        | :? RenameSequenceOperation as op' -> op' |> generateRenameSequenceOperation
        | :? RenameTableOperation as op' -> op' |> generateRenameTableOperation
        | :? RestartSequenceOperation as op' -> op' |> generateRestartSequenceOperation
        | :? SqlOperation as op' -> op' |> generateSqlOperation
        | :? InsertDataOperation as op' -> op' |> generateInsertDataOperation
        | :? DeleteDataOperation as op' -> op' |> generateDeleteDataOperation
        | :? UpdateDataOperation as op' -> op' |> generateUpdateDataOperation
        | _ -> op |> generateMigrationOperation // The failure case

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
