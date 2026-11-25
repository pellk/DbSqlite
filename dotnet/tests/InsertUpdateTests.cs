using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nuel.Sqlite;

namespace Db.Tests;

[TestClass]
public class InsertUpdateTests
{
	[TestMethod]
	public void InsertQuery_JsonElement_GeneratesParameterizedQuery()
	{
		string jsonText = """
		{
			"Name": "Alice",
			"Age": 30,
			"Price": 19.95,
			"IsActive": true,
			"Notes": null
		}
		""";

		using var doc = JsonDocument.Parse(jsonText);
		var (query, sqlParams) = Data.InsertQuery(doc.RootElement, "Users");

		Assert.AreEqual("INSERT INTO \"Users\" (\"Name\",\"Age\",\"Price\",\"IsActive\",\"Notes\") VALUES (@p0,@p1,@p2,@p3,@p4)", query);
		Assert.AreEqual(5, sqlParams.Length);

		Assert.AreEqual("@p0", sqlParams[0].ParameterName);
		Assert.AreEqual("Alice", sqlParams[0].Value);

		Assert.AreEqual("@p1", sqlParams[1].ParameterName);
		Assert.AreEqual(30, sqlParams[1].Value);

		Assert.AreEqual("@p2", sqlParams[2].ParameterName);
		Assert.AreEqual(19.95, sqlParams[2].Value);

		Assert.AreEqual("@p3", sqlParams[3].ParameterName);
		Assert.AreEqual(true, sqlParams[3].Value);

		Assert.AreEqual("@p4", sqlParams[4].ParameterName);
		Assert.AreEqual(DBNull.Value, sqlParams[4].Value);
	}

	[TestMethod]
	public void InsertQuery_JsonObject_GeneratesParameterizedQuery()
	{
		var obj = new JsonObject
		{
			["Title"] = "Widget",
			["Qty"] = 5,
			["Price"] = 12.50,
			["InStock"] = false,
			["Extra"] = null
		};

		var (query, sqlParams) = Data.InsertQuery(obj, "Products");

		Assert.AreEqual("INSERT INTO \"Products\" (\"Title\",\"Qty\",\"Price\",\"InStock\",\"Extra\") VALUES (@p0,@p1,@p2,@p3,@p4)", query);
		Assert.AreEqual(5, sqlParams.Length);

		Assert.AreEqual("@p0", sqlParams[0].ParameterName);
		Assert.AreEqual("Widget", sqlParams[0].Value);

		Assert.AreEqual("@p1", sqlParams[1].ParameterName);
		Assert.AreEqual(5, sqlParams[1].Value);

		Assert.AreEqual("@p3", sqlParams[3].ParameterName);
		Assert.AreEqual(false, sqlParams[3].Value);

		Assert.AreEqual("@p4", sqlParams[4].ParameterName);
		Assert.AreEqual(DBNull.Value, sqlParams[4].Value);
	}

	[TestMethod]
	public void UpdateQuery_JsonElement_GeneratesParameterizedQuery()
	{
		string jsonText = """
		{
			"Id": 42,
			"Name": "Bob",
			"Age": 26
		}
		""";

		using var doc = JsonDocument.Parse(jsonText);
		var (query, sqlParams) = UpdateQuery.Create(doc.RootElement, "Users", "Id");

		Assert.AreEqual("UPDATE \"Users\" AS target SET \"Name\"=@p0,\"Age\"=@p1 WHERE target.\"Id\"=@id", query);
		Assert.AreEqual(3, sqlParams.Length);

		Assert.AreEqual("@p0", sqlParams[0].ParameterName);
		Assert.AreEqual("Bob", sqlParams[0].Value);

		Assert.AreEqual("@p1", sqlParams[1].ParameterName);
		Assert.AreEqual(26, sqlParams[1].Value);

		Assert.AreEqual("@id", sqlParams[2].ParameterName);
		Assert.AreEqual(42, sqlParams[2].Value);
	}

	[TestMethod]
	public void UpdateQuery_JsonElement_MissingPrimaryKey_ThrowsArgumentException()
	{
		string jsonText = """
		{
			"Name": "Bob",
			"Age": 26
		}
		""";

		using var doc = JsonDocument.Parse(jsonText);
		Assert.ThrowsExactly<ArgumentException>(() => UpdateQuery.Create(doc.RootElement, "Users", "Id"));
	}

	[TestMethod]
	public void UpdateQuery_JsonObject_GeneratesParameterizedQuery()
	{
		var obj = new JsonObject
		{
			["Id"] = 100,
			["Title"] = "Updated Widget",
			["Price"] = 15.00
		};

		var (query, sqlParams) = UpdateQuery.Create(obj, "Products", "Id");

		Assert.AreEqual("UPDATE \"Products\" AS target SET \"Title\"=@p0,\"Price\"=@p1 WHERE target.\"Id\"=@id", query);
		Assert.AreEqual(3, sqlParams.Length);

		Assert.AreEqual("@p0", sqlParams[0].ParameterName);
		Assert.AreEqual("Updated Widget", sqlParams[0].Value);

		Assert.AreEqual("@id", sqlParams[2].ParameterName);
		Assert.AreEqual(100, sqlParams[2].Value);
	}

	[TestMethod]
	public void UpdateQuery_JsonObject_MissingPrimaryKey_ThrowsArgumentException()
	{
		var obj = new JsonObject
		{
			["Title"] = "No ID Widget"
		};

		Assert.ThrowsExactly<ArgumentException>(() => UpdateQuery.Create(obj, "Products", "Id"));
	}
}

