namespace KaziWatts;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = KaziWatts.Services.Session.IsDarkPreferred ? AppTheme.Dark : AppTheme.Light;   // jamais le thème de Windows
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Fenêtre au format téléphone pour simuler le cadre 360 x 800 des maquettes
        AppShell shell;
        try { shell = new AppShell(); }
        catch (Exception ex) { KaziWatts.Services.CrashLog.Write("AppShell", ex); throw; }
        return new Window(shell)
        {
            Title = "KaziWatts",
            Width = 420,
            Height = 860,
            MinimumWidth = 360,
            MinimumHeight = 640
        };
    }
}
