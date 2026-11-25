using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nuel.Sqlite;
using SqliteDb = nuel.Sqlite.Db;

namespace Db.Tests;

[TestClass]
[DoNotParallelize]
public class CsvRowTests
{
	[TestMethod]
	public async Task Csv_UsesOnlyThreeMarkersAndInfersPastLeadingNulls()
	{
		using var database = new SqliteTestDatabase();
		const string query = """
			SELECT NULL AS Number, NULL AS Ratio, 'first' AS Label, NULL AS Payload, NULL AS Empty
			UNION ALL SELECT 42, 2.5, NULL, X'0102', NULL
			""";
		const string expected = "!Number~%Ratio~$Label~$Payload~$Empty|Ø~Ø~first~Ø~Ø|42~2.5~Ø~AQI=~Ø";

		Assert.AreEqual(expected, SqliteDb.Csv(query));
		using var body = new MemoryStream();
		var context = new DefaultHttpContext();
		context.Response.Body = body;
		await new DbCsvResult(query).ExecuteAsync(context);
		Assert.AreEqual(expected, System.Text.Encoding.UTF8.GetString(body.ToArray()));
	}

	[TestMethod]
	public async Task Csv_MixedNumericMarkersRequireAnExplicitCast()
	{
		using var database = new SqliteTestDatabase();
		const string query = "SELECT 2 AS Value UNION ALL SELECT 3.5";
		Assert.ThrowsExactly<InvalidOperationException>(() => SqliteDb.Csv(query));

		using var body = new MemoryStream();
		var context = new DefaultHttpContext();
		context.Response.Body = body;
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new DbCsvResult(query).ExecuteAsync(context));

		Assert.AreEqual("%Value|2|3.5", SqliteDb.Csv("SELECT CAST(Value AS REAL) AS Value FROM (" + query + ")"));
	}
}
