using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqliteDb = nuel.Sqlite.Db;

namespace Db.Tests;

[TestClass]
[DoNotParallelize]
public class JsonObjectTests
{
	[TestMethod]
	public void JsonObject_UsesSqliteStorageTypes()
	{
		using var database = new SqliteTestDatabase();
		var json = SqliteDb.JsonObject("""
			SELECT 100 AS Id, 2.5 AS Ratio, 'hello' AS Greeting, TRUE AS Enabled,
				'2026-06-01T12:00:00.125+03:30' AS DateText, X'010203' AS Bytes, NULL AS Missing
			""");

		Assert.AreEqual(100L, json["Id"]!.GetValue<long>());
		Assert.AreEqual(2.5, json["Ratio"]!.GetValue<double>());
		Assert.AreEqual("hello", json["Greeting"]!.GetValue<string>());
		Assert.AreEqual(1L, json["Enabled"]!.GetValue<long>());
		Assert.AreEqual("2026-06-01T12:00:00.125+03:30", json["DateText"]!.GetValue<string>());
		Assert.AreEqual("AQID", json["Bytes"]!.GetValue<string>());
		Assert.IsTrue(json.ContainsKey("Missing"));
		Assert.IsNull(json["Missing"]);
	}

	[TestMethod]
	public void JsonObject_NullWhitespaceOrEmptyQueryResult()
	{
		using var database = new SqliteTestDatabase();
		Assert.ThrowsExactly<ArgumentNullException>(() => SqliteDb.JsonObject(null!));
		Assert.ThrowsExactly<ArgumentException>(() => SqliteDb.JsonObject("   "));
		Assert.IsNull(SqliteDb.JsonObject("SELECT 1 AS Id WHERE 0"));
	}
}
