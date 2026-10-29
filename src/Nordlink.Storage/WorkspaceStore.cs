using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nordlink.Contracts;

namespace Nordlink.Storage;

public sealed class WorkspaceStore
{
    public string Root { get; }
    public EnvironmentKind Environment { get; }
    private readonly string connectionString;

    public WorkspaceStore(string basePath, EnvironmentKind environment)
    {
        Environment = environment;
        Root = Path.GetFullPath(Path.Combine(basePath, environment.ToString().ToLowerInvariant()));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, "artifacts"));
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(Root, "nordlink.db") }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS documents(kind TEXT NOT NULL, id TEXT NOT NULL, json TEXT NOT NULL, PRIMARY KEY(kind,id));
            CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY, timestamp TEXT NOT NULL, environment TEXT NOT NULL, action TEXT NOT NULL, subject TEXT NOT NULL);
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void Save<T>(string kind, string id, T value)
    {
        var context = value switch
        {
            VehicleProfile x => x.Context, ModuleIdentity x => x.Context, DiagnosticReport x => x.Context,
            ParameterDefinition x => x.Context, Recording x => x.Context, SecurityCredential x => x.Context,
            ConfigurationBackup x => x.Context, FirmwarePackage x => x.Context, OperationJob x => x.Context,
            ArtifactMetadata x => x.Context, InterfaceVersionInfo x => x.Context, _ => null
        };
        if (context is not null && context.Environment != Environment) throw new InvalidOperationException("Evidence cannot cross environment stores.");
        if (value is VehicleProfile vehicle && vehicle.Modules.Any(m => m.Context.Environment != Environment))
            throw new InvalidOperationException("Module evidence cannot cross environment stores.");
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO documents(kind,id,json) VALUES($kind,$id,$json) ON CONFLICT(kind,id) DO UPDATE SET json=excluded.json";
        cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(value, WireJson.Options)); cmd.ExecuteNonQuery();
    }
    public T? Get<T>(string kind, string id)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT json FROM documents WHERE kind=$kind AND id=$id";
        cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is string json ? JsonSerializer.Deserialize<T>(json, WireJson.Options) : default;
    }
    public T[] List<T>(string kind)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT json FROM documents WHERE kind=$kind ORDER BY id"; cmd.Parameters.AddWithValue("$kind", kind);
        using var reader = cmd.ExecuteReader(); var result = new List<T>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), WireJson.Options)!);
        return result.ToArray();
    }
    public void Audit(string action, string subject)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO audit(timestamp,environment,action,subject) VALUES($at,$env,$action,$subject)";
        cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$env", Environment.ToString()); cmd.Parameters.AddWithValue("$action", action);
        cmd.Parameters.AddWithValue("$subject", subject); cmd.ExecuteNonQuery();
    }
    public int RecoverInterruptedJobs()
    {
        var interrupted = List<OperationJob>("job").Where(j => j.State is JobState.Running or JobState.Reviewed).ToArray();
        foreach (var j in interrupted)
        {
            Save("job", j.Id, j with { State = JobState.Interrupted, Failure = "Gateway restarted; explicit fresh review required. No operation was retried." });
            Audit("operation.interrupted", j.Id);
        }
        return interrupted.Length;
    }
    public void SaveRecording(Recording recording)
    {
        if (recording.Context.Environment != Environment) throw new InvalidOperationException("Recording environment mismatch.");
        if (recording.Samples.Any(s => !s.Gap && s.Value is null || s.Gap && s.Value is not null || s.Value is double v && !double.IsFinite(v)))
            throw new ArgumentException("Samples must carry a finite value or an explicit gap.");
        if (!recording.Samples.SequenceEqual(recording.Samples.OrderBy(s => s.Timestamp))) throw new ArgumentException("Sample timestamps must be ordered.");
        Save("recording", recording.Id, recording);
    }
    public static string RecordingCsv(Recording recording)
    {
        static string Quote(string s) => "\"" + (s.Length > 0 && "=+-@\t\r".Contains(s[0]) ? "'" : "") + s.Replace("\"", "\"\"") + "\"";
        var b = new StringBuilder("timestamp,parameter,unit,value,gap\r\n");
        foreach (var s in recording.Samples)
            b.Append(Quote(s.Timestamp.ToString("O"))).Append(',').Append(Quote(s.ParameterId)).Append(',').Append(Quote(s.Unit)).Append(',')
                .Append(s.Value?.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(s.Gap ? "true" : "false").Append("\r\n");
        return b.ToString();
    }
}
