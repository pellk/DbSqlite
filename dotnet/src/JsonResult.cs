using System.Buffers;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

/// <summary>Streams SQLite query results to an ASP.NET Core response as UTF-8 JSON.</summary>
public sealed class DbJsonResult : ActionResult, IResult
{
	private readonly string _query;
	private readonly JsonValueType _result;
	private readonly (string Name, JsonValueType ResultType)[] _results;
	private readonly SqliteParameter[] _parameters;

	/// <summary>Creates a streaming JSON result for the first result set.</summary>
	public DbJsonResult(string query, JsonValueType result = JsonValueType.Object)
		: this(query, result, Data.NoParams) { }

	/// <summary>Creates a streaming JSON result with named parameters.</summary>
	public DbJsonResult(string query, params (string name, object value)[] parameters)
		: this(query, JsonValueType.Object, Data.SqlParams(parameters)) { }

	/// <summary>Creates a streaming JSON result with SQLite parameters.</summary>
	public DbJsonResult(string query, params SqliteParameter[] parameters)
		: this(query, JsonValueType.Object, parameters) { }

	/// <summary>Creates a streaming JSON result with a result shape and named parameters.</summary>
	public DbJsonResult(string query, JsonValueType result, params (string name, object value)[] parameters)
		: this(query, result, Data.SqlParams(parameters)) { }

	/// <summary>Creates a streaming JSON result with a result shape and SQLite parameters.</summary>
	public DbJsonResult(string query, JsonValueType result, params SqliteParameter[] parameters)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);
		_query = query;
		_result = result;
		_parameters = parameters;
	}

	/// <summary>Creates a streaming JSON result for named result sets.</summary>
	public DbJsonResult(string query, (string Name, JsonValueType ResultType)[] result)
		: this(query, result, Data.NoParams) { }

	/// <summary>Creates a streaming JSON result for named result sets using named parameters.</summary>
	public DbJsonResult(string query, (string Name, JsonValueType ResultType)[] result,
		params (string name, object value)[] parameters)
		: this(query, result, Data.SqlParams(parameters)) { }

	/// <summary>Creates a streaming JSON result for named result sets using SQLite parameters.</summary>
	public DbJsonResult(string query, (string Name, JsonValueType ResultType)[] result,
		params SqliteParameter[] parameters) : this(query, JsonValueType.Object, parameters)
	{
		ArgumentNullException.ThrowIfNull(result);
		_results = result;
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
		if (_results is null)
			await WriteResponseAsync(context, reader, _result);
		else
			await WriteResponseAsync(context, reader, _results);
	}

	internal static Task WriteResponseAsync(HttpContext context, DbDataReader reader, JsonValueType result = JsonValueType.Object)
		=> WriteResponseAsync(context, (writer, token) => Db.WriteJsonRows(reader, result, writer, token));

	internal static Task WriteResponseAsync(HttpContext context, DbDataReader reader, (string Name, JsonValueType ResultType)[] result)
		=> WriteResponseAsync(context, (writer, token) => Db.WriteJsonResults(reader, result, writer, token));

	private static async Task WriteResponseAsync(HttpContext context,
		Func<Utf8JsonWriter, CancellationToken, IEnumerable<int>> write)
	{
		const int chunkSize = 16 * 1024;
		var response = context.Response;
		response.ContentType = "application/json; charset=utf-8";
		response.ContentLength = null;
		var cancellationToken = context.RequestAborted;
		cancellationToken.ThrowIfCancellationRequested();

		var buffer = new ArrayBufferWriter<byte>(chunkSize);
		using var writer = new Utf8JsonWriter(buffer, Data.JsonWriterOptions);
		foreach (int pendingBytes in write(writer, cancellationToken))
		{
			if (pendingBytes + buffer.WrittenCount >= chunkSize)
				await WriteChunkAsync();
		}
		await WriteChunkAsync();

		async ValueTask WriteChunkAsync()
		{
			writer.Flush();
			if (buffer.WrittenCount > 0)
			{
				await response.Body.WriteAsync(buffer.WrittenMemory, cancellationToken);
				buffer.ResetWrittenCount();
			}
		}
	}
}
