using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

/// <summary>Streams a SQLite query's first result set to an ASP.NET Core response as UTF-8 compact CSV.</summary>
public sealed class DbCsvResult : ActionResult, IResult
{
	private readonly string _query;
	private readonly SqliteParameter[] _parameters;

	/// <summary>Creates a streaming result without parameters.</summary>
	public DbCsvResult(string query) : this(query, Data.NoParams) { }

	/// <summary>Creates a streaming result with named parameters.</summary>
	public DbCsvResult(string query, params (string name, object value)[] parameters)
		: this(query, Data.SqlParams(parameters)) { }

	/// <summary>Creates a streaming result with SQLite parameters.</summary>
	public DbCsvResult(string query, params SqliteParameter[] parameters)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);
		_query = query;
		_parameters = parameters;
	}

	/// <inheritdoc />
	public override Task ExecuteResultAsync(ActionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return ExecuteAsync(context.HttpContext);
	}

	/// <inheritdoc />
	public async Task ExecuteAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.RequestAborted.ThrowIfCancellationRequested();

		using var connection = new SqliteConnection(Db.ConnectionString);
		using var command = new SqliteCommand(_query, connection);
		Data.AttachParams(command, _parameters);
		connection.Open();
		context.RequestAborted.ThrowIfCancellationRequested();

		using var reader = command.ExecuteReader();
		await WriteResponseAsync(context, reader);
	}

	internal static async Task WriteResponseAsync(HttpContext context, DbDataReader reader)
	{
		var response = context.Response;
		response.ContentType = "text/csv; charset=utf-8";
		response.ContentLength = null;
		var cancellationToken = context.RequestAborted;
		cancellationToken.ThrowIfCancellationRequested();
		if (!reader.HasRows)
			return;

		using var writer = new Utf8CsvStreamWriter(response.Body, cancellationToken);
		foreach (string part in CsvWriter.ReadCsvParts(reader, cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			await writer.WriteStringAsync(part);
		}
		await writer.FinishAsync();
	}
}
