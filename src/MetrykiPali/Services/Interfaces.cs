using MetrykiPali.Model;

namespace MetrykiPali.Services;

/// <summary>Reads the source table ("tabelka z palami") from a file.</summary>
public interface IScheduleReader
{
    IReadOnlyList<PileRange> Read(string path);
}

/// <summary>Writes the paginated "METRYKA PALI" workbook.</summary>
public interface IMetrykaWriter
{
    void Write(string path, IReadOnlyList<WorkDay> days, MetrykaSettings settings);
}

/// <summary>
/// Stores the working state between runs. The repository is what lets the
/// journal outlive the process - everything else can be rebuilt from the inputs.
/// </summary>
public interface IProjectRepository
{
    /// <summary>The project reopened automatically on every start.</summary>
    string DefaultPath { get; }

    ProjectState? Load(string path);
    void Save(string path, ProjectState state);

    /// <summary>Keeps one dated copy per day, so a bad edit stays recoverable.</summary>
    void BackupOnce(string path);
}
