using System.Windows;
using CajaLocal.Services;

namespace CajaLocal;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        Services = new AppServices();
        Services.Initialize();
        base.OnStartup(e);
    }
}
