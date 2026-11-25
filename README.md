# nuel.DbSqlite

SQLite query and persistence helpers for .NET 10, with JSON and compact CSV export.

The C# library is packaged as `nuel_DbSqlite`. The JavaScript and TypeScript parser is distributed as `@nuell/db` and maintained in the `npm` directory of the `pellk/Db` repository. [npm package](https://www.npmjs.com/package/@nuell/db) · [Parser repository](https://github.com/pellk/Db)

The C# database methods run synchronously. `DbCsvResult` and `DbJsonResult` read SQLite synchronously and write the ASP.NET Core response asynchronously.

```csharp
using nuel.Sqlite;

Db.ConnectionString = "Data Source=app.db;Foreign Keys=True";

app.MapGet("/api/users", () => new DbCsvResult(
    "SELECT Id, Name, CAST(Balance AS REAL) AS Balance FROM Users"));
```

```typescript
import { parseCsv } from '@nuell/db';

type User = { Id: number; Name: string; Balance: number };

const response = await fetch('/api/users');
const users = parseCsv<User>(await response.text());
```

SQLite CSV uses the existing compact format and the same npm package. The three emitted column markers are `$` for strings, `!` for integers, and `%` for floating-point numbers. For arbitrary text or columns with mixed types, return JSON and read it with `response.json()`.

## Development

```sh
dotnet build dotnet/Db.slnx
dotnet test dotnet/Db.slnx
dotnet pack dotnet/src/Db.csproj -c Release
```

[nuel_DbSqlite library reference](dotnet/README.md) · [MIT license](LICENSE)
