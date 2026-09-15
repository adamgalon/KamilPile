using System.Text.Json;
using System.Text.Json.Serialization;

using MetrykiPali.Model;

namespace MetrykiPali.Services;

/// <summary>
/// Saves and restores the working state, so piles can be logged over several
/// days and across restarts before the metryki are generated in one go.
/// </summary>
public sealed class JsonProjectRepository : IProjectRepository
{
    public const string FileExtension = ".mpali";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>The project reopened automatically on every start.</summary>
    public string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MetrykiPali",
        "projekt" + FileExtension);

    public void Save(string path, ProjectState state)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // Write to a temporary file first so an interrupted save cannot destroy
        // a journal that may represent weeks of site records.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
        File.Move(temp, path, overwrite: true);
    }

    public ProjectState? Load(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<ProjectState>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Keeps one dated backup per day, so a bad edit is recoverable.</summary>
    public void BackupOnce(string path)
    {
        if (!File.Exists(path)) return;

        var backup = Path.Combine(
            Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Today:yyyyMMdd}{FileExtension}.bak");

        if (!File.Exists(backup)) File.Copy(path, backup);
    }
}
