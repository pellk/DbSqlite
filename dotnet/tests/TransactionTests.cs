using System.Collections;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Db.Tests;

[TestClass]
[DoNotParallelize]
public class TransactionTests
{
	[TestMethod]
	public void Transaction_NullInputs_ReturnNull()
	{
		Assert.IsNull(nuel.Sqlite.Db.Transaction((string)null!));
		Assert.IsNull(nuel.Sqlite.Db.Transaction((string)null!, ("@p", 1)));
		Assert.IsNull(nuel.Sqlite.Db.Transaction((string)null!, new SqliteParameter("@p", 1)));
		Assert.IsNull(nuel.Sqlite.Db.Transaction((IEnumerable<string>)null!));
		Assert.IsNull(nuel.Sqlite.Db.Transaction((IEnumerable<(string, SqliteParameter[])>)null!));
		Assert.IsNull(nuel.Sqlite.Db.Transaction((IEnumerable<(string, (string, object)[])>)null!));
		Assert.IsNull(nuel.Sqlite.Db.Transaction((IEnumerable<SqliteCommand>)null!));
	}

	private class SingleEnumerationTracker<T> : IEnumerable<T>
	{
		private readonly IEnumerable<T> _source;
		public int EnumerationCount { get; private set; }

		public SingleEnumerationTracker(IEnumerable<T> source)
		{
			_source = source;
		}

		public IEnumerator<T> GetEnumerator()
		{
			EnumerationCount++;
			return _source.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[TestMethod]
	public void Transaction_IEnumerable_DoesNotPreEnumerateWithCount()
	{
		var tracker = new SingleEnumerationTracker<string>(new[] { "SELECT 1" });

		string original = nuel.Sqlite.Db.ConnectionString;
		try
		{
			nuel.Sqlite.Db.ConnectionString = "Data Source=missing-transaction-test.db;Mode=ReadOnly";
			nuel.Sqlite.Db.Transaction(tracker);
		}
		catch
		{
			// Expected failure opening connection
		}

		finally
		{
			nuel.Sqlite.Db.ConnectionString = original;
		}

		Assert.AreEqual(0, tracker.EnumerationCount, "IEnumerable was enumerated before connection open (e.g. via Count()).");
	}

	[TestMethod]
	public void Transaction_DataSqlParams_ConvertsCorrectly()
	{
		(string name, object value)[] tuples = [("@name", "GO;ok"), ("@num", 42), ("@nullVal", null!)];
		var sqlParams = nuel.Sqlite.Data.SqlParams(tuples);

		Assert.AreEqual(3, sqlParams.Length);
		Assert.AreEqual("@name", sqlParams[0].ParameterName);
		Assert.AreEqual("GO;ok", sqlParams[0].Value);
		Assert.AreEqual("@num", sqlParams[1].ParameterName);
		Assert.AreEqual(42, sqlParams[1].Value);
		Assert.AreEqual("@nullVal", sqlParams[2].ParameterName);
		Assert.AreEqual(DBNull.Value, sqlParams[2].Value);
	}
}

