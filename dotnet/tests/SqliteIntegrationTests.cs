using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nuel.Sqlite;
using SqliteDb = nuel.Sqlite.Db;

namespace Db.Tests;

[TestClass]
[DoNotParallelize]
public class SqliteIntegrationTests
{
	[TestMethod]
	public void Save_UsesIntIdsAndRollsBackAnOverflowingInsert()
	{
		using var database = new SqliteTestDatabase();
		SqliteDb.Execute("""
			CREATE TABLE "Entry Log" ("Record ID" INTEGER PRIMARY KEY, "label.with.dot" TEXT);
			""");

		var row = new JsonObject { ["label.with.dot"] = "O'Neil" };
		Assert.AreEqual(1, SqliteDb.Save(row, "main.Entry Log", "Record ID"));
		row["Record ID"] = 1;
		row["label.with.dot"] = "updated";
		Assert.AreEqual(1, SqliteDb.Save(row, "main.Entry Log", "Record ID"));
		Assert.AreEqual("updated", SqliteDb.Str("""SELECT "label.with.dot" FROM "Entry Log" """));
		Assert.IsTrue(SqliteDb.Delete(1, "main.Entry Log", "Record ID"));

		SqliteDb.Insert(new JsonObject { ["Record ID"] = int.MaxValue }, "Entry Log");
		row.Remove("Record ID");
		Assert.ThrowsExactly<OverflowException>(() => SqliteDb.Save(row, "Entry Log", "Record ID"));
		Assert.AreEqual(1, SqliteDb.Val<int>("""SELECT COUNT(*) FROM "Entry Log" """));
	}

	[TestMethod]
	public void Persistence_BindsValuesAndSupportsTextKeys()
	{
		using var database = new SqliteTestDatabase();
		SqliteDb.Execute("""CREATE TABLE Items ("key.id" TEXT PRIMARY KEY, "odd""column" ANY, Payload TEXT) STRICT""");
		const string key = "it's a key";
		var row = new JsonObject
		{
			["key.id"] = key,
			["odd\"column"] = 2.5,
			["Payload"] = new JsonArray(1, "test")
		};

		Assert.AreEqual(1, SqliteDb.Insert(row, "Items"));
		Assert.AreEqual(2.5, SqliteDb.Val<double>("""SELECT "odd""column" FROM Items"""));
		Assert.AreEqual("real", SqliteDb.Str("""SELECT typeof("odd""column") FROM Items"""));
		Assert.AreEqual("[1,\"test\"]", SqliteDb.Str("SELECT Payload FROM Items WHERE \"key.id\" = @key", ("@key", key)));
		row["odd\"column"] = 3;
		Assert.AreEqual(1, SqliteDb.Update(row, "Items", "key.id"));
		Assert.AreEqual(3L, SqliteDb.Val<long>("""SELECT "odd""column" FROM Items"""));
		Assert.IsTrue(SqliteDb.Delete(key, "Items", "key.id"));
	}

	[TestMethod]
	public void Transaction_RollsBackWhenABatchedStatementFailsAfterAResult()
	{
		using var database = new SqliteTestDatabase();
		SqliteDb.Execute("CREATE TABLE Items (Id INTEGER PRIMARY KEY)");
		Assert.ThrowsExactly<SqliteException>(() => SqliteDb.Transaction("""
			INSERT INTO Items VALUES (1);
			SELECT Id FROM Items;
			INSERT INTO Items VALUES (1);
			"""));
		Assert.AreEqual(0, SqliteDb.Val<int>("SELECT COUNT(*) FROM Items"));

		using var insert = new SqliteCommand("INSERT INTO Items VALUES (@id)");
		insert.Parameters.AddWithValue("@id", 5);
		CollectionAssert.AreEqual(new[] { 1 }, SqliteDb.Transaction(insert));
		Assert.IsNull(insert.Connection);
		Assert.AreEqual(5L, SqliteDb.Val<long>("SELECT Id FROM Items"));
	}

	[TestMethod]
	public void TableAndTypedHelpersUseSqliteValues()
	{
		using var database = new SqliteTestDatabase();
		using var table = SqliteDb.Table("SELECT NULL AS V UNION ALL SELECT 1 UNION ALL SELECT 1.5 UNION ALL SELECT 'x'");
		Assert.AreEqual(typeof(object), table.Columns[0].DataType);
		CollectionAssert.AreEqual(new object[] { DBNull.Value, 1L, 1.5, "x" },
			table.Rows.Cast<System.Data.DataRow>().Select(row => row[0]).ToArray());

		var id = Guid.NewGuid();
		var time = new DateTimeOffset(2026, 4, 1, 12, 45, 36, TimeSpan.FromHours(3.5));
		var item = SqliteDb.Object<TypedItem>("SELECT @id AS Id, @time AS Time, 1 AS Enabled, 42 AS Amount",
			new SqliteParameter("@id", id), new SqliteParameter("@time", time));
		Assert.AreEqual(id, item.Id);
		Assert.AreEqual(time, item.Time);
		Assert.IsTrue(item.Enabled);
		Assert.AreEqual(42, item.Amount);
		Assert.AreEqual(id, SqliteDb.Val<Guid>("SELECT @id", ("@id", id)));
		Assert.AreEqual(42, SqliteDb.Object<TypedStruct>("SELECT 42 AS Amount").Amount);
	}

	[TestMethod]
	public async Task JsonNamedResultsPreserveStorageTypesAndMatchStreaming()
	{
		using var database = new SqliteTestDatabase();
		const string sql = """
			SELECT NULL AS V UNION ALL SELECT 1 UNION ALL SELECT 1.5 UNION ALL SELECT 'x' UNION ALL SELECT X'FF';
			SELECT 1 WHERE 0;
			SELECT @label;
			SELECT 1 AS Id;
			""";
		var results = new (string, JsonValueType)[]
		{
			("rows", JsonValueType.Array), ("empty", JsonValueType.Object),
			("label", JsonValueType.Value), ("compact", JsonValueType.Csv), ("missing", JsonValueType.Array)
		};
		string json = SqliteDb.Json(sql, results, ("@label", "sample"));
		using var parsed = JsonDocument.Parse(json);
		var root = parsed.RootElement;
		Assert.AreEqual(JsonValueKind.Null, root.GetProperty("rows")[0].GetProperty("V").ValueKind);
		Assert.AreEqual(1L, root.GetProperty("rows")[1].GetProperty("V").GetInt64());
		Assert.AreEqual(1.5, root.GetProperty("rows")[2].GetProperty("V").GetDouble());
		Assert.AreEqual("x", root.GetProperty("rows")[3].GetProperty("V").GetString());
		Assert.AreEqual("/w==", root.GetProperty("rows")[4].GetProperty("V").GetString());
		Assert.AreEqual(JsonValueKind.Null, root.GetProperty("empty").ValueKind);
		Assert.AreEqual("sample", root.GetProperty("label").GetString());
		Assert.AreEqual("!Id|1", root.GetProperty("compact").GetString());
		Assert.AreEqual(JsonValueKind.Null, root.GetProperty("missing").ValueKind);

		using var body = new MemoryStream();
		var context = new DefaultHttpContext();
		context.Response.Body = body;
		await new DbJsonResult(sql, results, ("@label", "sample")).ExecuteAsync(context);
		Assert.AreEqual(json, Encoding.UTF8.GetString(body.ToArray()));
	}

	public sealed class TypedItem
	{
		public Guid Id { get; set; }
		public DateTimeOffset Time { get; set; }
		public bool Enabled { get; set; }
		public int? Amount { get; set; }
	}

	public struct TypedStruct
	{
		public int Amount { get; set; }
	}
}
