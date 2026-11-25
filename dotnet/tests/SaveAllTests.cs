using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqliteDb = nuel.Sqlite.Db;

namespace Db.Tests;

[TestClass]
[DoNotParallelize]
public class SaveAllTests
{
	[TestMethod]
	public void SaveAll_DeletesThenUpdatesThenInsertsUsingFirstInsertColumns()
	{
		using var database = new SqliteTestDatabase();
		SqliteDb.Execute("""
			CREATE TABLE Items (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL,
				Value REAL DEFAULT 10, Extra TEXT DEFAULT 'default');
			INSERT INTO Items (Id, Name) VALUES (1, 'keep'), (2, 'delete');
			""");

		using var json = JsonDocument.Parse("""
			[
				{"Id":0,"Name":"O'Neil","Value":1.25},
				{"Id":"1","Name":"updated"},
				{"Name":"second","Extra":"ignored"}
			]
			""");

		var originalCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			Assert.AreEqual(4, SqliteDb.SaveAll(json.RootElement, "2", "Items"));
		}
		finally
		{
			CultureInfo.CurrentCulture = originalCulture;
		}

		using var rows = SqliteDb.Table("SELECT Id, Name, Value, Extra FROM Items ORDER BY Id");
		Assert.AreEqual(3, rows.Rows.Count);
		Assert.AreEqual("updated", rows.Rows[0]["Name"]);
		Assert.AreEqual(3L, rows.Rows[1]["Id"]);
		Assert.AreEqual("O'Neil", rows.Rows[1]["Name"]);
		Assert.AreEqual(1.25, rows.Rows[1]["Value"]);
		Assert.AreEqual(DBNull.Value, rows.Rows[2]["Value"]);
		Assert.AreEqual("default", rows.Rows[2]["Extra"]);
	}

	[TestMethod]
	public void SaveAll_RollsBackEveryStageOnFailure()
	{
		using var database = new SqliteTestDatabase();
		SqliteDb.Execute("""
			CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE);
			INSERT INTO Items VALUES (1, 'keep'), (2, 'delete');
			""");

		var rows = JsonNode.Parse("""
			[{"Id":1,"Name":"updated"},{"Name":"repeated"},{"Name":"repeated"}]
			""")!.AsArray();
		Assert.ThrowsExactly<SqliteException>(() => SqliteDb.SaveAll(rows, "2", "Items"));
		CollectionAssert.AreEqual(new[] { "keep", "delete" },
			SqliteDb.StrList("SELECT Name FROM Items ORDER BY Id"));
	}

	[TestMethod]
	public void SaveAll_HandlesLargeBatchesAndDeleteOnly()
	{
		using var database = new SqliteTestDatabase();
		SqliteDb.Execute("CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT);");
		var array = new JsonArray(Enumerable.Range(1, 1200)
			.Select(i => (JsonNode)new JsonObject { ["Name"] = $"Item {i}" }).ToArray());
		Assert.AreEqual(1200, SqliteDb.SaveAll(array, null, "Items"));
		Assert.AreEqual(1200, SqliteDb.SaveAll(new JsonArray(), string.Join(",", Enumerable.Range(1, 1200)), "Items"));
		Assert.AreEqual(0, SqliteDb.Val<int>("SELECT COUNT(*) FROM Items"));
	}

	[TestMethod]
	public void SaveAll_RejectsInvalidArrayAndDeleteIds()
	{
		using var json = JsonDocument.Parse("{}");
		Assert.ThrowsExactly<ArgumentException>(() => SqliteDb.SaveAll(json.RootElement, null, "Items"));
		Assert.ThrowsExactly<ArgumentException>(() => SqliteDb.SaveAll(new JsonObject(), null, "Items"));
		Assert.ThrowsExactly<InvalidDataException>(() => SqliteDb.SaveAll(new JsonArray(), "1,2,abc", "Items"));
	}
}
