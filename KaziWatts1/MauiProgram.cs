namespace KaziWatts;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Windows : retire le cadre, le fond et les marges natives des champs de saisie
        // pour que seul le cadre arrondi de TextField (conforme aux maquettes) soit visible.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("KaziNoChrome", (handler, view) =>
        {
            try
            {
            var tb = handler.PlatformView;
            var clear = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            tb.Background = clear;
            tb.Padding = new Microsoft.UI.Xaml.Thickness(0);
            tb.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
            foreach (var key in new[] { "TextControlBackgroundFocused", "TextControlBackgroundPointerOver",
                                        "TextControlBorderBrush", "TextControlBorderBrushFocused",
                                        "TextControlBorderBrushPointerOver" })
                tb.Resources[key] = clear;
            tb.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
            }
            catch (Exception ex) { KaziWatts.Services.CrashLog.Write("EntryChrome", ex); }
        });

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
            });
        return builder.Build();
    }
}
