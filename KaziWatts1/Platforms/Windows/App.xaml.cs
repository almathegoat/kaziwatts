using KaziWatts.Services;
using Microsoft.UI.Xaml;

namespace KaziWatts.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        CrashLog.Hook();
        UnhandledException += (_, e) =>
        {
            CrashLog.Write("WinUI", e.Exception);
            e.Handled = true;   // l'application ne se ferme plus ; détail dans %TEMP%\kaziwatts_crash.log
            var msg = e.Exception?.Message ?? "Erreur inconnue";
            try
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var page = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
                    if (page != null) await page.DisplayAlert("Erreur", msg, "OK");
                });
            }
            catch { /* on ne plante jamais à cause du message d'erreur */ }
        };
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp()
    {
        try { return MauiProgram.CreateMauiApp(); }
        catch (Exception ex) { CrashLog.Write("CreateMauiApp", ex); throw; }
    }
}
