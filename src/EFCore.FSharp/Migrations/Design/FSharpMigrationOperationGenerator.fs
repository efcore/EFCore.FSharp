namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open EntityFrameworkCore.FSharp

type FSharpMigrationOperationGenerator(code: ICSharpHelper) =

    let toOnedimensionalArray firstDimension (a: obj [,]) =
        Array.init
            a.Length
            (fun i ->
                if firstDimension then
                    a.[i, 0]
                else
                    a.[0, i])

    let sanitiseName name =
        if FSharpUtilities.isKeyword name then
            sprintf "``%s``" name
        else
            name

    let writeName nameValue =
        $"name = %s{code.UnknownLiteral nameValue}"

    let writeSchema schemaValue =
        schemaValue
        |> Option.ofObj
        |> Option.map (fun s -> $",schema = %s{code.UnknownLiteral s}")

    let writeParameter name value =
        $",%s{sanitiseName name} = %s{code.UnknownLiteral value}"

    let writeParameterIfTrue trueOrFalse name value =
        if trueOrFalse then
            writeParameter name value |> Some
        else
            None

    let writeOptionalParameter (name: string) (value: #obj) =
        writeParameterIfTrue (notNull value) name (value |> code.UnknownLiteral |> sanitiseName)

    let writeNullableParameterIfValue name (nullableParameter: Nullable<_>) =

        if nullableParameter.HasValue then
            let value = nullableParameter |> code.UnknownLiteral

            $",%s{sanitiseName name} = Nullable(%s{value})"
            |> Some
        else
            None

    let annotations includeIgnore (annotations: Annotation seq) =

        let lines =
            annotations
            |> Seq.map (fun a -> $".Annotation(%s{code.Literal a.Name}, %s{code.UnknownLiteral a.Value})")

        if lines |> Seq.isEmpty then
            if includeIgnore then
                ") |> ignore"
            else
                ")"

        elif lines |> Seq.length = 1 then
            let line = lines |> Seq.head

            if includeIgnore then
                $"){line} |> ignore"
            else
                $"){line}"

        else
            let last = lines |> Seq.last

            let tail =
                lines
                |> Seq.tail
                |> Seq.map
                    (fun l ->
                        if includeIgnore && l = last then
                            l + " |> ignore"
                        else
                            l)

            stringBuilder {
                ")" + (lines |> Seq.head)
                indent { tail }
            }

    let oldAnnotations (annotations: Annotation seq) =
        let lines =
            annotations
            |> Seq.map (fun a -> $".OldAnnotation(%s{code.Literal a.Name}, %s{code.UnknownLiteral a.Value})")

        if lines |> Seq.isEmpty then
            " |> ignore"

        elif lines |> Seq.length = 1 then
            let line = lines |> Seq.head
            $"{line} |> ignore"

        else
            let last = lines |> Seq.last

            let tail =
                lines
                |> Seq.tail
                |> Seq.map (fun l -> if l = last then l + " |> ignore" else l)

            stringBuilder {
                lines |> Seq.head
                indent { tail }
            }


    let generateAddColumnOperation (op: AddColumnOperation) =

        stringBuilder {
            $".AddColumn<{op.ClrType |> unwrapOptionType |> code.Reference}>("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameterIfTrue (op.ColumnType |> notNull) "type" op.ColumnType
                writeNullableParameterIfValue "unicode" op.IsUnicode
                writeNullableParameterIfValue "maxLength" op.MaxLength
                writeNullableParameterIfValue "fixedLength" op.IsFixedLength
                writeParameterIfTrue op.IsRowVersion "rowVersion" true
                writeParameter "nullable" op.IsNullable
                writeOptionalParameter "defaultValueSql" op.DefaultValueSql
                writeOptionalParameter "computedColumnSql" op.ComputedColumnSql
                writeOptionalParameter "defaultValue" op.DefaultValue

                if isOptionType op.ClrType then
                    $").SetValueConverter(OptionConverter<%s{op.ClrType |> unwrapOptionType |> code.Reference}> ()"

                annotations true (op.GetAnnotations())
            }
        }

    let generateAddForeignKeyOperation (op: AddForeignKeyOperation) =

        stringBuilder {
            ".AddForeignKey("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
                writeOptionalParameter "principalSchema" op.PrincipalSchema
                writeParameter "principalTable" op.PrincipalTable
                writeParameterIfTrue (op.PrincipalColumns.Length = 1) "principalColumn" op.PrincipalColumns.[0]
                writeParameterIfTrue (op.PrincipalColumns.Length <> 1) "principalColumns" op.PrincipalColumns
                writeParameterIfTrue (op.OnUpdate <> ReferentialAction.NoAction) "onUpdate" op.OnUpdate
                writeParameterIfTrue (op.OnDelete <> ReferentialAction.NoAction) "onDelete" op.OnDelete
            }

            annotations true (op.GetAnnotations())
        }

    let generateAddPrimaryKeyOperation (op: AddPrimaryKeyOperation) =
        stringBuilder {
            ".AddPrimaryKey("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            }

            annotations true (op.GetAnnotations())
        }

    let generateAddUniqueConstraintOperation (op: AddUniqueConstraintOperation) =
        stringBuilder {
            ".AddUniqueConstraint("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            }

            annotations true (op.GetAnnotations())
        }

    let generateAlterColumnOperation (op: AlterColumnOperation) =
        stringBuilder {
            $".AlterColumn<{code.Reference op.ClrType}>("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameterIfTrue (op.ColumnType |> notNull) "type" op.ColumnType
                writeNullableParameterIfValue "unicode" op.IsUnicode
                writeNullableParameterIfValue "maxLength" op.MaxLength
                writeParameterIfTrue op.IsRowVersion "rowVersion" true
                writeParameter "nullable" op.IsNullable
                writeOptionalParameter "defaultValueSql" op.DefaultValueSql
                writeOptionalParameter "computedColumnSql" op.ComputedColumnSql
                writeOptionalParameter "defaultValue" op.DefaultValue

                if notNull op.OldColumn.ClrType then
                    $",oldClrType = typedefof<%s{code.Reference op.OldColumn.ClrType}>"

                writeParameterIfTrue (op.OldColumn.ColumnType |> notNull) "oldType" op.OldColumn.ColumnType
                writeNullableParameterIfValue "oldUnicode" op.OldColumn.IsUnicode
                writeNullableParameterIfValue "oldMaxLength" op.OldColumn.MaxLength
                writeParameterIfTrue op.OldColumn.IsRowVersion "oldRowVersion" true
                writeParameter "oldNullable" op.OldColumn.IsNullable
                writeOptionalParameter "oldDefaultValueSql" op.OldColumn.DefaultValueSql
                writeOptionalParameter "oldComputedColumnSql" op.OldColumn.ComputedColumnSql
                writeOptionalParameter "oldDefaultValue" op.OldColumn.DefaultValue

                if isOptionType op.ClrType then
                    $").SetValueConverter(OptionConverter<%s{op.ClrType |> unwrapOptionType |> code.Reference}> ()"

                let hasNoOldAnnotations =
                    op.OldColumn.GetAnnotations() |> Seq.isEmpty

                annotations hasNoOldAnnotations (op.GetAnnotations())

                if not hasNoOldAnnotations then
                    oldAnnotations (op.OldColumn.GetAnnotations())
            }
        }


    let generateAlterDatabaseOperation (op: AlterDatabaseOperation) =
        stringBuilder {
            ".AlterDatabase()"

            indent {

                let hasNoOldAnnotations =
                    op.OldDatabase.GetAnnotations() |> Seq.isEmpty

                annotations hasNoOldAnnotations (op.GetAnnotations())

                if not hasNoOldAnnotations then
                    oldAnnotations (op.OldDatabase.GetAnnotations())
            }
        }

    let generateAlterSequenceOperation (op: AlterSequenceOperation) =
        stringBuilder {
            ".AlterSequence("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
                writeNullableParameterIfValue "minValue " op.MinValue
                writeNullableParameterIfValue "maxValue " op.MaxValue
                writeParameterIfTrue op.IsCyclic "cyclic" "true"
                writeParameterIfTrue (op.OldSequence.IncrementBy <> 1) "oldIncrementBy" op.OldSequence.IncrementBy
                writeNullableParameterIfValue "oldMinValue " op.OldSequence.MinValue
                writeNullableParameterIfValue "oldMaxValue " op.OldSequence.MaxValue
                writeParameterIfTrue op.OldSequence.IsCyclic "oldCyclic" "true"

                let hasNoOldAnnotations =
                    op.OldSequence.GetAnnotations() |> Seq.isEmpty

                annotations hasNoOldAnnotations (op.GetAnnotations())

                if not hasNoOldAnnotations then
                    oldAnnotations (op.OldSequence.GetAnnotations())
            }
        }

    let generateAlterTableOperation (op: AlterTableOperation) =
        stringBuilder {
            ".AlterTable("

            indent {
                writeName op.Name
                writeSchema op.Schema

                let hasNoOldAnnotations =
                    op.OldTable.GetAnnotations() |> Seq.isEmpty

                annotations hasNoOldAnnotations (op.GetAnnotations())

                if not hasNoOldAnnotations then
                    oldAnnotations (op.OldTable.GetAnnotations())
            }
        }

    let generateCreateIndexOperation (op: CreateIndexOperation) =
        stringBuilder {
            ".CreateIndex("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
                writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
                writeParameterIfTrue op.IsUnique "unique" true
                writeOptionalParameter "filter" op.Filter
                annotations true (op.GetAnnotations())
            }
        }

    let generateEnsureSchemaOperation (op: EnsureSchemaOperation) =
        stringBuilder {
            ".EnsureSchema("

            indent {
                writeName op.Name
                annotations true (op.GetAnnotations())
            }
        }

    let generateCreateSequenceOperation (op: CreateSequenceOperation) =

        let typedef =
            if op.ClrType <> typedefof<Int64> then
                $"<%s{code.Reference op.ClrType}>"
            else
                ""

        stringBuilder {
            $".CreateSequence{typedef}("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameterIfTrue (op.StartValue <> 1L) "startValue" op.StartValue
                writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
                writeNullableParameterIfValue "minValue " op.MinValue
                writeNullableParameterIfValue "maxValue " op.MaxValue
                writeParameterIfTrue op.IsCyclic "cyclic" "true"
                annotations true (op.GetAnnotations())
            }
        }

    let generateCreateTableOperation (op: CreateTableOperation) =

        let map = Dictionary<string, string>()

        let writeColumn (c: AddColumnOperation) =
            let propertyName = code.Identifier c.Name
            map.Add(c.Name, propertyName)

            stringBuilder {
                $"%s{propertyName} ="

                indent {
                    $"table.Column<{code.Reference c.ClrType}>("

                    indent {
                        $"nullable = %s{code.Literal c.IsNullable}"
                        writeParameterIfTrue (c.Name <> propertyName) "name" c.Name
                        writeParameterIfTrue (c.ColumnType |> notNull) "type" c.ColumnType
                        writeNullableParameterIfValue "unicode" c.IsUnicode
                        writeNullableParameterIfValue "maxLength" c.MaxLength
                        writeParameterIfTrue (c.IsRowVersion) "rowVersion" c.IsRowVersion

                        if c.DefaultValueSql |> notNull then
                            sprintf ", defaultValueSql = %s" (code.Literal c.DefaultValueSql)
                        elif c.ComputedColumnSql |> notNull then
                            sprintf ", computedColumnSql = %s" (code.Literal c.ComputedColumnSql)
                        elif c.DefaultValue |> notNull then
                            sprintf ", defaultValue = %s" (code.UnknownLiteral c.DefaultValue)
                    }
                }

                if isOptionType c.ClrType then
                    sprintf
                        ").SetValueConverter(OptionConverter<%s> ()"
                        (c.ClrType |> unwrapOptionType |> code.Reference)

                indent { annotations false (c.GetAnnotations()) }

            }

        let writeColumns =
            stringBuilder {
                ",columns = (fun table -> "
                "{|"

                indent {
                    op.Columns
                    |> Seq.filter notNull
                    |> Seq.map writeColumn
                }

                "|})"
            }

        let writeUniqueConstraint (uc: AddUniqueConstraintOperation) =

            let constraints =
                uc.Columns
                |> Seq.map (fun c -> map.[c])
                |> Seq.toList
                |> code.Lambda

            stringBuilder {
                $"table.UniqueConstraint({code.Literal uc.Name}, {constraints}"
                indent { annotations true (op.PrimaryKey.GetAnnotations()) }
                ""
            }

        let writeCheckConstraint (cc: AddCheckConstraintOperation) =
            stringBuilder {
                $"table.CheckConstraint({code.Literal cc.Name}, {code.Literal cc.Sql}"
                indent { annotations true (op.PrimaryKey.GetAnnotations()) }
                ""
            }

        let writeForeignKeyConstraint (fk: AddForeignKeyOperation) =

            stringBuilder {
                "table.ForeignKey("

                let constraints =
                    fk.Columns
                    |> Seq.map (fun c -> map.[c])
                    |> Seq.toList
                    |> code.Lambda

                indent {
                    writeName fk.Name

                    if fk.Columns.Length = 1
                       || isNull fk.PrincipalColumns then
                        $",column = {constraints}"
                    else
                        $",columns = {constraints}"

                    writeParameterIfTrue (fk.PrincipalSchema |> notNull) "principalSchema" fk.PrincipalSchema
                    writeParameter "principalTable" fk.PrincipalTable
                    writeParameterIfTrue (fk.PrincipalColumns.Length = 1) "principalColumn" fk.PrincipalColumns.[0]
                    writeParameterIfTrue (fk.PrincipalColumns.Length <> 1) "principalColumns" fk.PrincipalColumns
                    writeParameterIfTrue (fk.OnUpdate <> ReferentialAction.NoAction) "onUpdate" fk.OnUpdate
                    writeParameterIfTrue (fk.OnDelete <> ReferentialAction.NoAction) "onDelete" fk.OnDelete
                    annotations true (fk.GetAnnotations())
                }

                ""
            }

        let writeConstraints =

            let hasConstraints =
                notNull op.PrimaryKey
                || (op.UniqueConstraints |> Seq.isEmpty |> not)
                || (op.ForeignKeys |> Seq.isEmpty |> not)

            if hasConstraints then

                stringBuilder {
                    ", constraints ="

                    indent {
                        "(fun table -> "

                        indent {
                            if notNull op.PrimaryKey then

                                let pkName = op.PrimaryKey.Name |> code.Literal

                                let pkColumns =
                                    op.PrimaryKey.Columns
                                    |> Seq.map (fun c -> map.[c])
                                    |> Seq.toList
                                    |> code.Lambda

                                $"table.PrimaryKey(%s{pkName}, %s{pkColumns}"
                                annotations true (op.PrimaryKey.GetAnnotations())

                            op.UniqueConstraints
                            |> Seq.map writeUniqueConstraint

                            op.CheckConstraints
                            |> Seq.map writeCheckConstraint

                            op.ForeignKeys
                            |> Seq.map writeForeignKeyConstraint
                        }

                        ")"
                    }
                }
                |> Some
            else
                None

        stringBuilder {
            ".CreateTable("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeColumns
                writeConstraints
            }

            annotations true (op.GetAnnotations())

        }

    let generateDropColumnOperation (op: DropColumnOperation) =
        stringBuilder {
            ".DropColumn("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropForeignKeyOperation (op: DropForeignKeyOperation) =
        stringBuilder {
            ".DropForeignKey("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropIndexOperation (op: DropIndexOperation) =
        stringBuilder {
            ".DropIndex("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropPrimaryKeyOperation (op: DropPrimaryKeyOperation) =
        stringBuilder {
            ".DropPrimaryKey("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropSchemaOperation (op: DropSchemaOperation) =
        stringBuilder {
            ".DropSchema("

            indent {
                writeName op.Name
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropSequenceOperation (op: DropSequenceOperation) =
        stringBuilder {
            ".DropSequence("

            indent {
                writeName op.Name
                writeSchema op.Schema
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropTableOperation (op: DropTableOperation) =
        stringBuilder {
            ".DropTable("

            indent {
                writeName op.Name
                writeSchema op.Schema
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropUniqueConstraintOperation (op: DropUniqueConstraintOperation) =
        stringBuilder {
            ".DropUniqueConstraint("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                annotations true (op.GetAnnotations())
            }
        }

    let generateRenameColumnOperation (op: RenameColumnOperation) =
        stringBuilder {
            ".RenameColumn("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameter "newName" op.NewName
                annotations true (op.GetAnnotations())
            }
        }

    let generateRenameIndexOperation (op: RenameIndexOperation) =
        stringBuilder {
            ".RenameIndex("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameter "newName" op.NewName
                annotations true (op.GetAnnotations())
            }
        }

    let generateRenameSequenceOperation (op: RenameSequenceOperation) =
        stringBuilder {
            ".RenameSequence("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "newName" op.NewName
                writeParameter "newSchema" op.NewSchema
                annotations true (op.GetAnnotations())
            }
        }

    let generateRenameTableOperation (op: RenameTableOperation) =
        stringBuilder {
            ".RenameTable("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "newName" op.NewName
                writeParameter "newSchema" op.NewSchema
                annotations true (op.GetAnnotations())
            }
        }

    let generateRestartSequenceOperation (op: RestartSequenceOperation) =
        stringBuilder {
            ".RestartSequence("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "startValue" op.StartValue
                annotations true (op.GetAnnotations())
            }
        }

    let generateSqlOperation (op: SqlOperation) =

        let sqlAnnotations =
            op.GetAnnotations()
            |> Seq.map (fun a -> $".Annotation(%s{code.Literal a.Name}, %s{code.UnknownLiteral a.Value})")
            |> join ""

        $".Sql(%s{code.Literal op.Sql})%s{sqlAnnotations} |> ignore"

    let generateInsertDataOperation (op: InsertDataOperation) =

        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s," (op.Schema |> code.Literal)

                yield sprintf "table = %s," (op.Table |> code.Literal)

                if op.Columns.Length = 1 then
                    yield sprintf "column = %s," (op.Columns.[0] |> code.Literal)
                else
                    yield sprintf "columns = %s," (op.Columns |> code.Literal)

                let length0 = op.Values.GetLength(0)
                let length1 = op.Values.GetLength(1)

                let valuesArray =
                    if length0 = 1 && length1 = 1 then
                        sprintf "value = %s :> obj" (op.Values.[0, 0] |> code.UnknownLiteral)
                    elif length0 = 1 then
                        sprintf
                            "values = %s"
                            (op.Values
                             |> toOnedimensionalArray false
                             |> code.Literal)
                    elif length1 = 1 then
                        let arr = op.Values |> toOnedimensionalArray true
                        let lines = code.Literal(arr, true)
                        sprintf "values = %s" lines
                    else
                        sprintf "values = %s" (op.Values |> code.Literal)

                yield valuesArray
            }

        stringBuilder {
            ".InsertData("
            indent { parameters }
            ") |> ignore"
        }

    let generateDeleteDataOperation (op: DeleteDataOperation) =
        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s, " (op.Schema |> code.Literal)

                yield sprintf "table = %s, " (op.Table |> code.Literal)

                if op.KeyColumns.Length = 1 then
                    yield sprintf "keyColumn = %s, " (op.KeyColumns.[0] |> code.Literal)
                else
                    yield sprintf "keyColumns = %s, " (op.KeyColumns |> code.Literal)

                let length0 = op.KeyValues.GetLength(0)
                let length1 = op.KeyValues.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "keyValue = %s" (op.KeyValues.[0, 0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield
                        sprintf
                            "keyValues = %s"
                            (op.KeyValues
                             |> toOnedimensionalArray false
                             |> code.Literal)
                elif length1 = 1 then
                    let arr =
                        op.KeyValues |> toOnedimensionalArray true

                    let lines = code.Literal(arr, true)
                    yield sprintf "keyValues = %s" lines
                else
                    yield sprintf "keyValues = %s" (op.KeyValues |> code.Literal)
            }

        stringBuilder {
            ".DeleteData("
            indent { parameters }
            ") |> ignore"
        }

    let generateUpdateDataOperation (op: UpdateDataOperation) =
        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s, " (op.Schema |> code.Literal)

                yield sprintf "table = %s, " (op.Table |> code.Literal)

                if op.KeyColumns.Length = 1 then
                    yield sprintf "keyColumn = %s, " (op.KeyColumns.[0] |> code.Literal)
                else
                    yield sprintf "keyColumns = %s, " (op.KeyColumns |> code.Literal)

                let length0 = op.KeyValues.GetLength(0)
                let length1 = op.KeyValues.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "keyValue = %s" (op.KeyValues.[0, 0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield
                        sprintf
                            "keyValues = %s"
                            (op.KeyValues
                             |> toOnedimensionalArray false
                             |> code.Literal)
                elif length1 = 1 then
                    let arr =
                        op.KeyValues |> toOnedimensionalArray true

                    let lines = code.Literal(arr, true)
                    yield sprintf "keyValues = %s" lines
                else
                    yield sprintf "keyValues = %s" (op.KeyValues |> code.Literal)

                if op.Columns.Length = 1 then
                    yield sprintf "column = %s, " (op.Columns.[0] |> code.Literal)
                else
                    yield sprintf "columns = %s, " (op.Columns |> code.Literal)

                let length0 = op.Values.GetLength(0)
                let length1 = op.Values.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "value = %s" (op.Values.[0, 0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield
                        sprintf
                            "values = %s"
                            (op.Values
                             |> toOnedimensionalArray false
                             |> code.Literal)
                elif length1 = 1 then
                    let arr = op.Values |> toOnedimensionalArray true
                    let lines = code.Literal(arr, true)
                    yield sprintf "values = %s" lines
                else
                    yield sprintf "values = %s" (op.Values |> code.Literal)
            }

        stringBuilder {
            ".UpdateData("
            indent { parameters }
            ") |> ignore"
        }

    let generateAddCheckConstraintOperation (op: AddCheckConstraintOperation) =
        stringBuilder {
            ".AddCheckConstraint("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                writeParameter "sql" op.Sql
                annotations true (op.GetAnnotations())
            }
        }

    let generateDropCheckConstraintOperation (op: DropCheckConstraintOperation) =
        stringBuilder {
            ".DropCheckConstraint("

            indent {
                writeName op.Name
                writeSchema op.Schema
                writeParameter "table" op.Table
                annotations true (op.GetAnnotations())
            }
        }

    let generateOperation builderName (op: MigrationOperation) =
        let result =
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
            | :? AddCheckConstraintOperation as op' -> op' |> generateAddCheckConstraintOperation
            | :? DropCheckConstraintOperation as op' -> op' |> generateDropCheckConstraintOperation
            | _ ->
                op
                |> invalidOp ((op.GetType()) |> DesignStrings.UnknownOperation) // The failure case

        builderName + result

    let generate (builderName: string) (operations: MigrationOperation seq) (sb: IndentedStringBuilder) =

        if operations |> Seq.isEmpty then
            sb.AppendLine "()" |> ignore
        else

            let genOp = generateOperation builderName

            let generatedOperations =
                stringBuilder {
                    for o in operations do
                        genOp o
                        ""
                }

            sb.AppendLines generatedOperations |> ignore

    interface ICSharpMigrationOperationGenerator with
        member this.Generate(builderName, operations, builder) = generate builderName operations builder
