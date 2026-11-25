using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

sealed class EnergyStore
{
    readonly string connectionString;
    public SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }

    public EnergyStore(string state, string assets)
    {
        connectionString = new SqliteConnectionStringBuilder {
            DataSource = Path.Combine(state, "energy.sqlite"), ForeignKeys = true
        }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = File.ReadAllText(Path.Combine(assets, "schema.sql"));
        cmd.ExecuteNonQuery();
        cmd.CommandText = "ATTACH DATABASE $seed AS sample";
        cmd.Parameters.AddWithValue("$seed", Path.Combine(assets, "sample.sqlite"));
        cmd.ExecuteNonQuery();
        using var tx = db.BeginTransaction();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM history; DELETE FROM meter; DELETE FROM metrics; DELETE FROM equipment;
            INSERT INTO meter SELECT * FROM sample.meter;
            INSERT INTO equipment SELECT * FROM sample.equipment;
            INSERT INTO history SELECT * FROM sample.history;
            INSERT INTO metrics SELECT * FROM sample.metrics;
            """;
        cmd.ExecuteNonQuery();
        // Older versions named each account hash file with the same email key.
        foreach (var file in Directory.EnumerateFiles(state, "*.hash"))
        {
            cmd.CommandText = "INSERT OR IGNORE INTO accounts VALUES($key,$hash)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$key", Path.GetFileNameWithoutExtension(file));
            cmd.Parameters.AddWithValue("$hash", File.ReadAllText(file));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public object? Scalar(string sql, params (string name, object value)[] args)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name,value) in args) cmd.Parameters.AddWithValue(name,value);
        return cmd.ExecuteScalar();
    }

    public List<Dictionary<string,object>> Rows(string sql)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string,object>>();
        while (reader.Read())
            rows.Add(Enumerable.Range(0,reader.FieldCount).ToDictionary(reader.GetName,reader.GetValue));
        return rows;
    }

    public bool Healthy()
    {
        try { return Scalar("SELECT id FROM metrics WHERE id=1") != null; }
        catch (SqliteException) { return false; }
    }

    public IResult MeterCsv()
    {
        var rows = Rows("SELECT timestamp,holiday,temperature_c,occupancy,security,office,machines,cooling,energy_kwh FROM meter ORDER BY timestamp");
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",",rows[0].Keys));
        foreach (var row in rows)
            csv.AppendLine(string.Join(",",row.Values.Select(v => Convert.ToString(v,CultureInfo.InvariantCulture))));
        return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "meter.csv");
    }
}
