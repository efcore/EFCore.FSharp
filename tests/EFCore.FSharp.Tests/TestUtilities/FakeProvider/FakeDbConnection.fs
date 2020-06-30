namespace EntityFrameworkCore.FSharp.Test.TestUtilities.FakeProvider

open System
open System.Collections.Generic
open System.Data
open System.Data.Common
open System.Threading
open System.Threading.Tasks
open Microsoft.EntityFrameworkCore.Update
open System.Diagnostics
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Storage
open EntityFrameworkCore.FSharp.Test.TestUtilities
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Diagnostics

type TestRelationalLoggingDefinitions() =
    inherit RelationalLoggingDefinitions()

type FakeDbParameter() =
    inherit DbParameter()

    override val ParameterName = null with get,set
    override val Value = null with get,set

    override val Direction = ParameterDirection.Input with get,set

    static member DefaultIsNullable = false
    override val IsNullable = FakeDbParameter.DefaultIsNullable with get,set

    static member DefaultDbType = DbType.AnsiString
    override val DbType = FakeDbParameter.DefaultDbType with get,set

    override val Size = 0 with get,set

    override this.SourceColumn
        with get() = NotImplementedException() |> raise
        and set value = NotImplementedException() |> raise

    override this.SourceColumnNullMapping
        with get() = NotImplementedException() |> raise
        and set value = NotImplementedException() |> raise

    override this.ResetDbType () = NotImplementedException() |> raise

type FakeDbParameterCollection() =
    inherit DbParameterCollection()

    let parameters = ResizeArray<obj>()

    override this.Count = parameters.Count

    override this.Add value =
        parameters.Add value
        parameters.Count - 1

    

    override this.GetEnumerator() = parameters.GetEnumerator() :> _

    override this.SyncRoot = NotImplementedException() |> raise

    override this.AddRange values = NotImplementedException() |> raise

    override this.Clear () = ()

    override this.Contains (value : obj) : bool = NotImplementedException() |> raise
    override this.Contains (value : string) : bool = NotImplementedException() |> raise

    override this.CopyTo (array, index) : unit = NotImplementedException() |> raise

    override this.IndexOf (value : obj) : int = NotImplementedException() |> raise
    override this.IndexOf (value : string) : int = NotImplementedException() |> raise

    override this.Insert (index, value) : unit = NotImplementedException() |> raise
    override this.Remove value : unit = NotImplementedException() |> raise
    override this.RemoveAt (name : string) : unit= NotImplementedException() |> raise
    override this.RemoveAt (index : int) : unit = NotImplementedException() |> raise

    override this.GetParameter (index:int) : DbParameter = parameters.[index] :?> DbParameter
    override this.GetParameter (name:string) : DbParameter = NotImplementedException() |> raise
    override this.SetParameter (name : string, value: DbParameter) : unit = NotImplementedException() |> raise
    override this.SetParameter (index : int, value: DbParameter) : unit = NotImplementedException() |> raise

type FakeDbCommand(?connection: FakeDbConnection, ?commandExecutor : FakeCommandExecutor) =
    inherit DbCommand()

    let mutable conn = match connection with | Some c -> c | None -> new FakeDbConnection(null)
    let mutable tran = new FakeDbTransaction(conn)

    let mutable disposeCount = 0
    let mutable commandText = ""
    let mutable commandType = CommandType.Text
    let mutable updateRowSource = UpdateRowSource.None

    let exec = match commandExecutor with | Some e -> e | None -> FakeCommandExecutor()

    // TODO: implement
    member private this.AssertTransaction() =
        if (isNull base.Transaction) then
            Debug.Assert(conn.ActiveTransaction = tran)
        else
            let transaction = base.Transaction :?> FakeDbTransaction
            Debug.Assert(transaction.Connection = (conn :> DbConnection));
            Debug.Assert(transaction.DisposeCount = 0);
        ()

    override this.DbConnection
        with get () = conn :> DbConnection
        and set value = conn <- value :?> FakeDbConnection

    override this.DbTransaction 
        with get () = tran :> DbTransaction
        and set value = tran <- value :?> FakeDbTransaction

    override this.Cancel () = NotImplementedException() |> raise

    override this.CommandText
        with get () = commandText
        and  set value = commandText <- value

    static member DefaultCommandTimeout  = 30

    override val CommandTimeout = FakeDbCommand.DefaultCommandTimeout with get,set

    override this.CommandType
        with get () = commandType
        and  set value = commandType <- value

    override this.UpdatedRowSource
        with get () = updateRowSource
        and  set value = updateRowSource <- value

    override this.CreateDbParameter() =
        FakeDbParameter() :> _

    override this.DbParameterCollection =
        FakeDbParameterCollection() :> _

    override this.Prepare () = NotImplementedException() |> raise

    override this.ExecuteNonQuery() =
        this.AssertTransaction()
        exec.ExecuteNonQuery(this)

    override this.ExecuteScalar() =
        this.AssertTransaction()
        exec.ExecuteScalar(this)

    override this.ExecuteDbDataReader behavior =
        this.AssertTransaction()
        exec.ExecuteDbDataReader this behavior

    override this.ExecuteNonQueryAsync cancellationToken =
        this.AssertTransaction()
        exec.ExecuteNonQueryAsync this cancellationToken

    override this.ExecuteScalarAsync cancellationToken =
        this.AssertTransaction()
        exec.ExecuteScalarAsync this cancellationToken

    override this.ExecuteDbDataReaderAsync (behavior, cancellationToken) =
        this.AssertTransaction()
        exec.ExecuteDbDataReaderAsync this behavior cancellationToken

    override this.DesignTimeVisible
        with get () = NotImplementedException() |> raise
        and  set value = NotImplementedException() |> raise

    member this.DisposeCount = disposeCount
    override this.Dispose disposing = 
        if disposing then
            disposeCount <- disposeCount + 1

        base.Dispose disposing
    

and FakeDbDataReader (?columnNames:string[], ?results: ResizeArray<obj[]>) =
    inherit DbDataReader()

    let _columnNames = match columnNames with | Some e -> e | None -> [||]
    let _results = match results with | Some e -> e | None -> ResizeArray<obj[]>()

    let mutable _currentRow : obj[] = null
    let mutable _rowIndex : int = 0

    let mutable _readAsyncCount : int = 0
    let mutable _closeCount : int = 0
    let mutable _disposeCount : int = 0

    override this.Read () =
        _currentRow <-
            if _rowIndex < _results.Count then
                _rowIndex <- _rowIndex + 1
                _results.[_rowIndex]
            else null

        not (isNull _currentRow)

    member this.ReadAsyncCount = _readAsyncCount

    override this.ReadAsync cancellationToken =
        _readAsyncCount <- _readAsyncCount + 1

        _currentRow <-
            if _rowIndex < _results.Count then
                _rowIndex <- _rowIndex + 1
                _results.[_rowIndex]
            else null

        not (isNull _currentRow) |> Task.FromResult

    member this.CloseCount = _closeCount

    override this.Close () =
        _closeCount <- _closeCount + 1

    member this.DisposeCount = _disposeCount

    override this.Dispose disposing =
        if disposing then
            _disposeCount <- _disposeCount + 1
            base.Dispose()

    override this.FieldCount = _columnNames.Length

    override this.GetName ordinal = _columnNames.[ordinal]

    override this.IsDBNull ordinal = _currentRow.[ordinal] = (DBNull.Value :> obj)

    override this.GetValue ordinal = _currentRow.[ordinal]

    override this.GetInt32 ordinal = _currentRow.[ordinal] :?> _
    
    override this.Depth = NotImplementedException() |> raise

    override this.HasRows = NotImplementedException() |> raise

    override this.IsClosed = NotImplementedException() |> raise

    override this.Item with get(i:int) : obj = NotImplementedException() |> raise
    override this.Item with get(name: string) : obj = NotImplementedException() |> raise

    override this.RecordsAffected = 0

    override this.GetBoolean(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetByte(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length) = NotImplementedException() |> raise

    override this.GetChar(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetChars(ordinal, dataOffset, buffer, bufferOffset, length) = NotImplementedException() |> raise

    override this.GetDataTypeName(ordinal : int) = NotImplementedException() |> raise

    override this.GetDateTime(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetDecimal(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetDouble(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetEnumerator() = NotImplementedException() |> raise

    override this.GetFieldType(ordinal : int) = NotImplementedException() |> raise

    override this.GetFloat(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetGuid(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetInt16(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetInt64(ordinal : int) = _currentRow.[ordinal] :?> _

    override this.GetOrdinal(name: string) = NotImplementedException() |> raise

    override this.GetString(ordinal: int) = _currentRow.[ordinal] :?> _

    override this.GetValues(values) = NotImplementedException() |> raise

    override this.NextResult() = NotImplementedException() |> raise

    


and FakeCommandExecutor
    (
        ?executeNonQuery : FakeDbCommand -> int,
        ?executeScalar : FakeDbCommand -> obj,
        ?executeReader : FakeDbCommand -> CommandBehavior -> DbDataReader,
        ?executeNonQueryAsync : FakeDbCommand -> CancellationToken -> Task<int>,
        ?executeScalarAsync : FakeDbCommand -> CancellationToken -> Task<obj>,
        ?executeReaderAsync : FakeDbCommand -> CommandBehavior -> CancellationToken -> Task<DbDataReader>
    ) =

        let _executeNonQuery = match executeNonQuery with | Some e -> e | None -> (fun c -> -1)
        let _executeScalar = match executeScalar with | Some e -> e | None -> (fun c -> null)
        let _executeReader = match executeReader with | Some e -> e | None -> (fun c b -> new FakeDbDataReader() :> DbDataReader)
        let _executeNonQueryAsync = match executeNonQueryAsync with | Some e -> e | None -> (fun c ct -> Task.FromResult(-1))
        let _executeScalarAsync = match executeScalarAsync with | Some e -> e | None -> (fun c ct -> Task.FromResult<obj>(null))
        let _executeReaderAsync = match executeReaderAsync with | Some e -> e | None -> (fun c ct b -> Task.FromResult<DbDataReader>(new FakeDbDataReader()))

        member this.ExecuteNonQuery command = _executeNonQuery command
        member this.ExecuteScalar command = _executeScalar command
        member this.ExecuteDbDataReader command behaviour = _executeReader command behaviour

        member this.ExecuteNonQueryAsync command cancellationToken = _executeNonQueryAsync command cancellationToken
        member this.ExecuteScalarAsync command cancellationToken = _executeScalarAsync command cancellationToken
        member this.ExecuteDbDataReaderAsync command behaviour cancellationToken = _executeReaderAsync command behaviour cancellationToken

and FakeSqlGenerator(dependencies) =
    inherit UpdateSqlGenerator(dependencies)

    let mutable _appendBatchHeaderCalls = 0
    let mutable _appendInsertOperationCalls  = 0
    let mutable _appendUpdateOperationCalls  = 0
    let mutable _appendDeleteOperationCalls  = 0

    member this.AppendBatchHeaderCalls = _appendBatchHeaderCalls
    member this.AppendInsertOperationCalls  = _appendInsertOperationCalls
    member this.AppendUpdateOperationCalls  = _appendUpdateOperationCalls
    member this.AppendDeleteOperationCalls  = _appendDeleteOperationCalls

    override this.AppendBatchHeader commandStringBuilder =
        _appendBatchHeaderCalls <- _appendBatchHeaderCalls + 1
        base.AppendBatchHeader(commandStringBuilder)

    override this.AppendIdentityWhereCondition (commandStringBuilder, columnModification) =
        commandStringBuilder
            .Append(base.SqlGenerationHelper.DelimitIdentifier(columnModification.ColumnName))
            .Append(" = ")
            .Append("provider_specific_identity()")
            |> ignore

    override this.AppendSelectAffectedCountCommand (commandStringBuilder, name, schema, commandPosition) =
        commandStringBuilder
            .Append("SELECT provider_specific_rowcount();")
            .Append(Environment.NewLine)
            .Append(Environment.NewLine)
            |> ignore

        ResultSetMapping.LastInResultSet

    override this.AppendRowsAffectedWhereCondition (commandStringBuilder, expectedRowsAffected) =
        commandStringBuilder
            .Append("provider_specific_rowcount() = ")
            .Append(expectedRowsAffected)
            |> ignore

and [<AllowNullLiteral>] FakeDbTransaction(connection:FakeDbConnection, ?isolationLevel : IsolationLevel) =
    inherit DbTransaction()

    let mutable commitCount = 0
    let mutable rollbackCount = 0
    let mutable disposeCount = 0

    override this.DbConnection = connection :> DbConnection
    override this.IsolationLevel = match isolationLevel with | Some i -> i | None -> IsolationLevel.Unspecified

    member this.CommitCount = commitCount
    member this.RollbackCount = rollbackCount
    member this.DisposeCount = disposeCount

    override this.Commit() =
        commitCount <- commitCount + 1

    override this.Rollback() =
        rollbackCount <- rollbackCount + 1

    override this.Dispose disposing =
        if disposing then        
            disposeCount <- disposeCount + 1
            (this.DbConnection :?> FakeDbConnection).ActiveTransaction <- null;
        
        base.Dispose(disposing)

and [<AllowNullLiteral>] FakeDbConnection (connectionString: string, ?commandExecutor : FakeCommandExecutor, ?state : ConnectionState) as this =
    inherit DbConnection()

    let mutable connectionState : ConnectionState = match state with | Some s -> s | None -> ConnectionState.Closed
    let commandExecutor = match commandExecutor with | Some ce -> ce | None -> FakeCommandExecutor()       

    let dbCommands = ResizeArray<FakeDbCommand>()
    let dbTransactions = ResizeArray<FakeDbTransaction>()

    let mutable activeTransaction = new FakeDbTransaction(this)
    
    let mutable openCount = 0
    let mutable openCountAsync = 0
    let mutable closeCount = 0
    let mutable disposeCount = 0

    member this.SetState state = connectionState <- state
    override this.State = connectionState

    member this.DbCommands : IReadOnlyList<FakeDbCommand> = dbCommands :> _
    member this.DbTransactions : IReadOnlyList<FakeDbTransaction> = dbTransactions :> _

    member this.OpenCount = openCount
    member this.OpenCountAsync = openCountAsync
    member this.CloseCount = closeCount
    member this.DisposeCount = disposeCount

    member this.ActiveTransaction
        with get () = activeTransaction
        and  set value = activeTransaction <- value

    override val ConnectionString = null with get,set

    override this.Database = "Fake Database"
    override this.DataSource = "Fake DataSource"

    override this.ServerVersion =
        NotImplementedException() |> raise

    override this.ChangeDatabase databaseName =
        NotImplementedException() |> raise

    override this.Open () =
        openCount <- openCount + 1
        connectionState <- ConnectionState.Open

    override this.OpenAsync cancellationToken =
        openCountAsync <- openCountAsync + 1
        base.OpenAsync cancellationToken

    override this.Close () =
        closeCount <- closeCount + 1
        connectionState <- ConnectionState.Closed

    override this.CreateDbCommand () =
        let command =  new FakeDbCommand(this, commandExecutor)
        dbCommands.Add(command)
        command :> _

    override this.BeginDbTransaction isolationLevel =
        activeTransaction <- new FakeDbTransaction(this, isolationLevel)
        dbTransactions.Add(activeTransaction);
        this.ActiveTransaction :> _

    override this.Dispose disposing =
        if disposing then
            disposeCount <- disposeCount + 1

        base.Dispose disposing

and FakeRelationalConnection (options) =
    inherit RelationalConnection(
        options)

    let mutable _connection : DbConnection = base.DbConnection
    let _dbConnections = ResizeArray<FakeDbConnection>()

    member this.UseConnection connection =
        _connection <- connection

    override this.DbConnection = _connection

    member this.DbConnections : IReadOnlyList<FakeDbConnection> = _dbConnections :> _

    override this.CreateDbConnection () =
        let connection = new FakeDbConnection(base.ConnectionString)
        _dbConnections.Add(connection)
        connection :> _

and FakeRelationalDatabaseCreator () =

    member this.CanConnect() = NotImplementedException() |> raise
    member this.CanConnectAsync cancellationToken = NotImplementedException() |> raise

    interface IRelationalDatabaseCreator with
        member this.CanConnect(): bool = 
            raise (System.NotImplementedException())
        member this.CanConnectAsync(cancellationToken: CancellationToken): Task<bool> = 
            raise (System.NotImplementedException())
        member this.HasTables(): bool = 
            raise (System.NotImplementedException())
        member this.HasTablesAsync(cancellationToken: CancellationToken): Task<bool> = 
            raise (System.NotImplementedException())
    
        member this.EnsureDeleted() = NotImplementedException() |> raise
        member this.EnsureDeletedAsync cancellationToken = NotImplementedException() |> raise
        member this.EnsureCreated() = NotImplementedException() |> raise
        member this.EnsureCreatedAsync cancellationToken = NotImplementedException() |> raise        
        member this.Exists() = NotImplementedException() |> raise
        member this.ExistsAsync cancellationToken = NotImplementedException() |> raise
        member this.Create() = NotImplementedException() |> raise
        member this.CreateAsync cancellationToken = NotImplementedException() |> raise
        member this.Delete() = NotImplementedException() |> raise
        member this.DeleteAsync cancellationToken = NotImplementedException() |> raise
        member this.CreateTables() = NotImplementedException() |> raise
        member this.CreateTablesAsync cancellationToken = NotImplementedException() |> raise
        member this.GenerateCreateScript() = NotImplementedException() |> raise

and [<AllowNullLiteral>] FakeRelationalOptionsExtension =
    inherit RelationalOptionsExtension

    new () = { inherit RelationalOptionsExtension() }
    new (copyOptions: FakeRelationalOptionsExtension) = { inherit RelationalOptionsExtension(copyOptions) }

    static member AddEntityFrameworkRelationalDatabase serviceCollection =
        
        let serviceMap (map : ServiceCollectionMap) =
            map.TryAdd(typeof<ProviderCodeGenerator>, typeof<TestProviderCodeGenerator>, ServiceLifetime.Singleton) |> ignore

        
        let builder =
            EntityFrameworkRelationalServicesBuilder(serviceCollection)
                .TryAdd<IDatabaseProvider, DatabaseProvider<FakeRelationalOptionsExtension>>()
                .TryAdd<ISqlGenerationHelper, RelationalSqlGenerationHelper>()
                .TryAdd<IRelationalTypeMappingSource, TestRelationalTypeMappingSource>()
                .TryAdd<IMigrationsSqlGenerator, TestRelationalMigrationSqlGenerator>()                
                .TryAdd<IRelationalConnection, FakeRelationalConnection>()
                .TryAdd<IHistoryRepository>(fun _ -> null)
                .TryAdd<IUpdateSqlGenerator, FakeSqlGenerator>()
                .TryAdd<IModificationCommandBatchFactory, TestModificationCommandBatchFactory>()
                .TryAdd<IRelationalDatabaseCreator, FakeRelationalDatabaseCreator>()
                .TryAdd<LoggingDefinitions, TestRelationalLoggingDefinitions>()
                .TryAddProviderSpecificServices(Action<ServiceCollectionMap>(serviceMap))


        builder.TryAddCoreServices() |> ignore

        serviceCollection

    override this.Info 
        with get() = 
            System.NotImplementedException() |> raise


    override this.Clone () =
        FakeRelationalOptionsExtension(this) :> _

    override this.ApplyServices services =

        services |> FakeRelationalOptionsExtension.AddEntityFrameworkRelationalDatabase |> ignore
        ()

