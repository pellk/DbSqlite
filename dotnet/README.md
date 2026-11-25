# nuel_DbSqlite

SQLite query, persistence, and export helpers for .NET 10. Database operations use `Microsoft.Data.Sqlite` synchronously. The two ASP.NET Core streaming results write their HTTP responses asynchronously.

## Initialization

```sh
dotnet add package nuel_DbSqlite
```

```csharp
using Microsoft.Data.Sqlite;
using nuel.Sqlite;

Db.ConnectionString = "Data Source=app.db;Foreign Keys=True";

int count = Db.Val<int>("SELECT COUNT(*) FROM Users WHERE City = @city", ("@city", "London"));
string name = Db.Str("SELECT Name FROM Users WHERE Id = @id", new SqliteParameter("@id", 1));
Db.Execute("UPDATE Users SET Nickname = @name WHERE Id = @id",
    Db.NS("@name", "   "), new SqliteParameter("@id", 1));
```

Set `Db.ConnectionString` during application startup. Each operation opens and disposes its own connection. For an in-memory database shared across calls, use a named connection string such as `Data Source=app-memory;Mode=Memory;Cache=Shared` and keep a connection to it open for as long as the database is needed.

Pass named parameters as `(name, value)` tuples or `SqliteParameter` instances. Tuple values set to C# `null` bind as SQL `NULL`. `Db.NS` trims a string and binds blank input as SQL `NULL`. Query methods take SQLite SQL text. To migrate a stored-procedure call, write its SQLite statements explicitly.

## Query helpers

| Method | Result |
| --- | --- |
| `Val<T>` | First column of the first row, converted to `T`. Returns `default(T)` for a null or missing value. |
| `Str` | First column of the first row as a string, or `null`. |
| `List<T>` | First column of each row converted to `T`. SQL `NULL` becomes `default(T)`. |
| `StrList` | First column of each row as a string, with `null` entries for SQL `NULL`. |
| `Object<T>` | First row mapped to a new object by case-insensitive property name, or `default(T)`. |
| `ObjList<T>` | Rows mapped to new objects by case-insensitive property name. |
| `Dictionary<K,V>` | First two columns as keys and values. Skips null keys and retains the last value for a duplicate key. |
| `Table` | `DataTable` with `object` columns that preserve the CLR type of each cell. |
| `Values` | Flat array of raw values across all result sets, in row and column order. SQL `NULL` is `DBNull.Value`. |

List, dictionary, and table helpers return `null` for empty results. `Values` returns an empty array. SQLite query values are integers (`long`), real numbers (`double`), text (`string`), blobs (`byte[]`), or SQL `NULL`. Typed helpers convert values to the requested CLR type, such as an integer to `bool` or ISO date text to `DateTimeOffset`.

## CSV

```csharp
string csv = Db.Csv(
    "SELECT Id, Name, CAST(Balance AS REAL) AS Balance, Active, CreatedAt FROM Users");
```

The following output includes all three markers.

```text
!Id~$Name~%Balance~!Active~$CreatedAt|1~Alice~19.5~1~2026-06-01T12:30:00|2~Sam~5~0~Ø
```

| Marker | Value |
| --- | --- |
| `$` | SQLite text, or a blob as base64 |
| `!` | SQLite integer, including booleans stored as `0` or `1` |
| `%` | SQLite floating-point number |

The compact format uses `~` between columns, `|` between rows, and `Ø` for SQL `NULL`. The first non-null value sets the marker for each column. All-null columns use `$`. A value that needs a different marker raises an error. Use an explicit cast such as `CAST(Value AS REAL)` for an integer/real column, or use JSON to preserve each value's type.

The wire format has no escaping for `~`, `|`, or a text field equal to `Ø`. The npm parser can also discard a single-column empty row or trailing line breaks in a row. Use JSON for arbitrary text. JavaScript represents the two numeric markers as `number`. Cast 64-bit integers to `TEXT` when exact values above JavaScript's safe integer range are required.

`Db.Csv(object[])` uses the same markers for objects with shared public properties. It formats CLR booleans as integers, dates as strings, and byte arrays as base64 strings. A query with no rows or an empty object array returns `null`.

## JSON

```csharp
string first = Db.Json("SELECT Id, Name FROM Users ORDER BY Id LIMIT 1");
string all = Db.Json("SELECT Id, Name FROM Users ORDER BY Id", JsonValueType.Array);
var firstNode = Db.JsonObject("SELECT Id, Name FROM Users ORDER BY Id LIMIT 1");
```

`Db.Json` returns `null` as JSON text for an empty object result and `[]` for an empty array. `Db.JsonObject` returns a `JsonObject` or C# `null`. JSON uses each cell's SQLite storage type. Date text is preserved, integer flags stay numeric, and blobs are base64 strings. Non-finite real values become strings because JSON numbers must be finite.

Pass a tuple array to name the result sets returned by a SQLite SQL batch.

```csharp
string json = Db.Json(
    """
    SELECT COUNT(*) FROM Users WHERE City = @city;
    SELECT Id, Name FROM Users WHERE City = @city ORDER BY Id;
    """,
    [("count", JsonValueType.Value), ("users", JsonValueType.Array)],
    ("@city", "London"));
```

Each named result can be a first-row `Object`, a row `Array`, a first-cell `Value`, or a compact `Csv` string. An empty or missing named result becomes JSON `null`. The named results correspond to row-producing statements in order.

## ASP.NET Core streaming

Both results can be returned from Minimal APIs and MVC controller actions. The package references the `Microsoft.AspNetCore.App` shared framework.

```csharp
app.MapGet("/users.csv", (string city) =>
    new DbCsvResult("SELECT Id, Name FROM Users WHERE City = @city", ("@city", city)));

app.MapGet("/users.json", () =>
    new DbJsonResult("SELECT Id, Name FROM Users", JsonValueType.Array));
```

The results query at execution time, use synchronous SQLite operations, and write asynchronously to the response. Cancellation is checked between database operations and during response writes. CSV may buffer leading rows to determine the column markers, including the full result when a column is all null. An empty CSV result has an empty response body.

Use [`@nuell/db`](https://www.npmjs.com/package/@nuell/db) to parse compact CSV. JSON can be consumed directly with standard JSON parsers.

## Writes and transactions

```csharp
using System.Text.Json.Nodes;

int id = Db.Save(new { Id = 0, Name = "Alice" }, "Users");
int count = Db.Update(new JsonObject { ["Id"] = id, ["Name"] = "Alicia" }, "Users", "Id");
bool deleted = Db.Delete(id, "Users");

int[] rows = Db.Transaction(
    ("DELETE FROM Orders WHERE CustomerId = @id", new[] { ("@id", (object)customerId) }),
    ("DELETE FROM Customers WHERE Id = @id", new[] { ("@id", (object)customerId) }));
```

- `Execute` runs SQLite SQL and returns the SQLite affected-row count. `Transaction` runs SQL batches or commands atomically and returns one count per command.
- `Insert` and `Update` take a JSON object and return affected-row counts. `Update` reads the key from that object. Nested JSON is bound as text, and non-integer JSON numbers bind as SQLite real values.
- `Delete` accepts a primary key value and returns whether a row was affected. `Update` and `Delete` can use non-integer keys.
- `Save` takes a C# object or a JSON object. A missing or zero ID requests an insert into a table with a generated `INTEGER PRIMARY KEY`. A nonzero ID requests an update. It returns the `int` ID, or zero if no row was updated. An inserted ID outside the `int` range rolls back the insert and raises `OverflowException`.
- `SaveAll` takes a JSON array and optional comma-separated delete IDs. It deletes, updates, and inserts in one transaction, returning the total affected rows. The first insert determines the insert columns. Later rows supply SQL `NULL` for missing columns and their extra fields are ignored.
- For generated writes, pass identifiers as names without SQL quoting. Use `database.table` to qualify a table with an attached database or `main`.
