namespace MetrykiPali.Tests;

/// <summary>
/// The real services, shared by the tests that exercise them directly.
/// They hold no state between calls, so one instance each is enough.
/// </summary>
internal static class TestServices
{
    internal static readonly IScheduleReader Reader = new PileTableReader();
    internal static readonly IMetrykaWriter Writer = new MetrykaWriter();
    internal static readonly IProjectRepository Repository = new JsonProjectRepository();
}
