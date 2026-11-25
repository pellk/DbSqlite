using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nuel.Sqlite;

namespace Db.Tests;

[TestClass]
public class ExecuteDeleteTests
{
	[TestMethod]
	public void EscapeIdentifier_SimpleName_WrapsInDoubleQuotes()
	{
		Assert.AreEqual("\"Users\"", Data.EscapeIdentifier("Users"));
	}

	[TestMethod]
	public void EscapeIdentifier_SchemaQualifiedName_WrapsEachPart()
	{
		Assert.AreEqual("\"dbo\".\"Users\"", Data.EscapeTable("dbo.Users"));
		Assert.AreEqual("\"Catalog\".\"dbo\".\"Users\"", Data.EscapeTable("Catalog.dbo.Users"));
	}

	[TestMethod]
	public void EscapeIdentifier_BracketedName_TreatsBracketsAsPartOfName()
	{
		Assert.AreEqual("\"[Users]\"", Data.EscapeIdentifier("[Users]"));
	}

	[TestMethod]
	public void EscapeIdentifier_ClosingBracket_StaysInName()
	{
		Assert.AreEqual("\"User]Name\"", Data.EscapeIdentifier("User]Name"));
	}

	[TestMethod]
	public void EscapeIdentifier_InjectionAttempt_NeutralizesWithinIdentifier()
	{
		Assert.AreEqual("\"Users; DROP TABLE Orders; --\"", Data.EscapeIdentifier("Users; DROP TABLE Orders; --"));
		Assert.AreEqual("\"Id] = 1 OR 1=1; --\"", Data.EscapeIdentifier("Id] = 1 OR 1=1; --"));
	}

	[TestMethod]
	public void EscapeIdentifier_Null_ThrowsArgumentNullException()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() => Data.EscapeIdentifier(null!));
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("   ")]
	public void EscapeIdentifier_Whitespace_ThrowsArgumentException(string invalidIdentifier)
	{
		Assert.ThrowsExactly<ArgumentException>(() => Data.EscapeIdentifier(invalidIdentifier));
	}

	[TestMethod]
	public void AttachParams_Tuples_AttachesCorrectly()
	{
		using var cmd = new SqliteCommand();
		(string name, object? value)[] parameters = [("@id", 123), ("@name", "Alice"), ("@nullVal", null)];

		Data.AttachParams(cmd, Data.SqlParams(parameters!));

		Assert.AreEqual(3, cmd.Parameters.Count);
		Assert.AreEqual("@id", cmd.Parameters[0].ParameterName);
		Assert.AreEqual(123, cmd.Parameters[0].Value);
		Assert.AreEqual("@name", cmd.Parameters[1].ParameterName);
		Assert.AreEqual("Alice", cmd.Parameters[1].Value);
		Assert.AreEqual("@nullVal", cmd.Parameters[2].ParameterName);
		Assert.AreEqual(DBNull.Value, cmd.Parameters[2].Value);
	}

	[TestMethod]
	public void AttachParams_SqliteParameter_CanBeReusedAcrossCommands()
	{
		using var cmd1 = new SqliteCommand();
		using var cmd2 = new SqliteCommand();

		var param = new SqliteParameter("@shared", 42);
		SqliteParameter?[] parameters = [param, null];

		// Attach to first command
		Data.AttachParams(cmd1, parameters!);
		Assert.AreEqual(1, cmd1.Parameters.Count);
		Assert.AreEqual(42, cmd1.Parameters[0].Value);

		// Attach to second command
		Data.AttachParams(cmd2, parameters!);
		Assert.AreEqual(1, cmd2.Parameters.Count);
		Assert.AreEqual(42, cmd2.Parameters[0].Value);
	}

	[TestMethod]
	public void Execute_NullQuery_ThrowsArgumentNullException()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() =>
			nuel.Sqlite.Db.Execute(null!));
	}

	[TestMethod]
	public void Execute_WhitespaceQuery_ThrowsArgumentException()
	{
		Assert.ThrowsExactly<ArgumentException>(() =>
			nuel.Sqlite.Db.Execute("   "));
	}

	[TestMethod]
	public void Delete_NullId_ThrowsArgumentNullException()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() =>
			nuel.Sqlite.Db.Delete<string>(null!, "Users"));
	}

	[TestMethod]
	public void Delete_InvalidTableOrPk_ThrowsArgumentException()
	{
		Assert.ThrowsExactly<ArgumentException>(() =>
			nuel.Sqlite.Db.Delete(1, "   "));
		Assert.ThrowsExactly<ArgumentException>(() =>
			nuel.Sqlite.Db.Delete(1, "Users", "   "));
	}
}
