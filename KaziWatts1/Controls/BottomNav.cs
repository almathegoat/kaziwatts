namespace KaziWatts.Controls;

/// <summary>Barre du bas des maquettes : Accueil · Paiements · Colocs · Profil.</summary>
public class BottomNav : ContentView
{
    private static readonly Color Blue = Color.FromArgb("#245CFF");
    private static readonly Color BlueDark = Color.FromArgb("#83A7FF");
    private static readonly Color MutedLight = Color.FromArgb("#6C7993");
    private static readonly Color MutedDark = Color.FromArgb("#9BAAC5");
    private static readonly Color CardLight = Colors.White;
    private static readonly Color CardDark = Color.FromArgb("#121E36");
    private static readonly Color LineLight = Color.FromArgb("#E2E9F5");
    private static readonly Color LineDark = Color.FromArgb("#263550");

    private static readonly (string Route, string Icon, string Label)[] Tabs =
    {
        ("home", "home", "Accueil"),
        ("payments", "wallet", "Paiements"),
        ("roommates", "users", "Colocs"),
        ("profile", "user", "Profil"),
    };

    /// <summary>Onglet actif : home, payments, roommates ou profile.</summary>
    public static readonly BindableProperty ActiveProperty = BindableProperty.Create(
        nameof(Active), typeof(string), typeof(BottomNav), "home",
        propertyChanged: (b, _, _) => ((BottomNav)b).Build());

    public string Active { get => (string)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    public BottomNav() => Build();

    private void Build()
    {
        var row = new Grid { Padding = new Thickness(10, 12, 10, 12), ColumnSpacing = 0 };
        for (var i = 0; i < Tabs.Length; i++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (route, iconName, text) = Tabs[i];
            var active = route == Active;

            var icon = new Icon { Name = iconName, Size = 22, Thickness = 1.5, HorizontalOptions = LayoutOptions.Center };
            var label = new Label
            {
                Text = text, FontSize = 10.5, HorizontalOptions = LayoutOptions.Center,
                FontFamily = active ? "InterSemiBold" : "InterRegular"
            };
            if (active)
            {
                icon.SetAppThemeColor(Icon.ColorProperty, Blue, BlueDark);
                label.SetAppThemeColor(Label.TextColorProperty, Blue, BlueDark);
            }
            else
            {
                icon.SetAppThemeColor(Icon.ColorProperty, MutedLight, MutedDark);
                label.SetAppThemeColor(Label.TextColorProperty, MutedLight, MutedDark);
            }

            var item = new VerticalStackLayout { Spacing = 6, Children = { icon, label }, BackgroundColor = Colors.Transparent };
            var tap = new TapGestureRecognizer();
            var target = route;
            tap.Tapped += async (_, _) => await Shell.Current.GoToAsync("//" + target);
            item.GestureRecognizers.Add(tap);
            Grid.SetColumn(item, i);
            row.Children.Add(item);
        }

        var line = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.Start };
        line.SetAppThemeColor(BoxView.ColorProperty, LineLight, LineDark);

        var root = new Grid { Children = { row, line } };
        root.SetAppThemeColor(VisualElement.BackgroundColorProperty, CardLight, CardDark);
        Content = root;
    }
}
