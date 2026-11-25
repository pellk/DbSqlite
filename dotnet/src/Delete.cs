namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Deletes a record from the specified database table.</summary>
	/// <typeparam name="TKey">The type of the primary key.</typeparam>
	/// <param name="id">The ID of the record to be deleted.</param>
	/// <param name="table">The name of the table where the record is stored.</param>
	/// <param name="primaryKey">The primary key column of the table. Defaults to "Id" if not specified.</param>
	/// <returns>True if the record was deleted successfully; otherwise, false.</returns>
	public static bool Delete<TKey>(TKey id, string table, string primaryKey = "Id")
	{
		ArgumentNullException.ThrowIfNull(id);

		string safeTable = Data.EscapeTable(table);
		string safePk = Data.EscapeIdentifier(primaryKey);
		string query = $"DELETE FROM {safeTable} AS target WHERE target.{safePk} = @id";

		return Execute(query, ("@id", (object)id)) > 0;
	}

	/// <summary>Deletes a record from the specified database table.</summary>
	/// <param name="id">The ID of the record to be deleted.</param>
	/// <param name="table">The name of the table where the record is stored.</param>
	/// <param name="primaryKey">The primary key column of the table. Defaults to "Id" if not specified.</param>
	/// <returns>True if the record was deleted successfully; otherwise, false.</returns>
	public static bool Delete(int id, string table, string primaryKey = "Id")
		=> Delete<int>(id, table, primaryKey);
}
