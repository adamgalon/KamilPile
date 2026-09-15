using MetrykiPali.Presentation;
using MetrykiPali.Services;
using MetrykiPali.Views;

namespace MetrykiPali;

/// <summary>
/// The composition root: the one place that decides which real implementations
/// the presenter gets. Swapping any of them - a different reader, a different
/// store - is a change here and nowhere else, and it is exactly what the tests
/// do when they substitute fakes.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var view = new MainForm();
        var presenter = new MainPresenter(
            view,
            new PileTableReader(),
            new MetrykaWriter(),
            new JsonProjectRepository());

        presenter.Start();
        Application.Run(view);
    }
}
