using KaziWatts.Services;
using Shapes = Microsoft.Maui.Controls.Shapes;

namespace KaziWatts.Controls;

/// <summary>Bouton rond soleil / lune : seul moyen (avec l'onglet Profil) de passer du clair au sombre.</summary>
public class ThemeToggle : ContentView
{
    public static readonly BindableProperty OnPhotoProperty = BindableProperty.Create(
        nameof(OnPhoto), typeof(bool), typeof(ThemeToggle), false,
        propertyChanged: (b, _, _) => ((ThemeToggle)b).Refresh());

    /// <summary>Vrai quand le bouton est posé sur la photo de l'écran d'accueil (icône blanche, fond translucide).</summary>
    public bool OnPhoto { get => (bool)GetValue(OnPhotoProperty); set => SetValue(OnPhotoProperty, value); }

    private readonly Icon _icon = new() { Size = 20, Thickness = 1.7 };
    private readonly Border _bubble;

    public ThemeToggle()
    {
        _bubble = new Border
        {
            WidthRequest = 40, HeightRequest = 40, Padding = 0, StrokeThickness = 1,
            StrokeShape = new Shapes.RoundRectangle { CornerRadius = new CornerRadius(20) },
            Content = _icon
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { Session.SetDark(!Session.IsDarkPreferred); Refresh(); };
        _bubble.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(this, "Changer de thème (clair / sombre)");

        Content = _bubble;
        Refresh();
        Loaded += (_, _) => { if (Application.Current != null) Application.Current.RequestedThemeChanged += OnThemeChanged; Refresh(); };
        Unloaded += (_, _) => { if (Application.Current != null) Application.Current.RequestedThemeChanged -= OnThemeChanged; };
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var dark = Session.IsDarkPreferred;
        _icon.Name = dark ? "sun" : "moon";          // l'icône montre le thème vers lequel on bascule
        if (OnPhoto)
        {
            _bubble.BackgroundColor = Color.FromArgb("#4D000000");
            _bubble.Stroke = new SolidColorBrush(Color.FromArgb("#80FFFFFF"));
            _icon.Color = Colors.White;
        }
        else
        {
            _bubble.BackgroundColor = dark ? Color.FromArgb("#121E36") : Colors.White;
            _bubble.Stroke = new SolidColorBrush(Color.FromArgb(dark ? "#263550" : "#E2E9F5"));
            _icon.Color = dark ? Color.FromArgb("#83A7FF") : Color.FromArgb("#245CFF");
        }
    }
}
