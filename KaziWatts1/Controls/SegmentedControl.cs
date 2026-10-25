using Shapes = Microsoft.Maui.Controls.Shapes;

namespace KaziWatts.Controls;

/// <summary>Sélecteur segmenté des maquettes : conteneur doux, pastille bleue sur l'élément choisi.</summary>
public class SegmentedControl : ContentView
{
    private static readonly Color SoftLight = Color.FromArgb("#EAF0FF");
    private static readonly Color SoftDark = Color.FromArgb("#1B2D53");
    private static readonly Color MutedLight = Color.FromArgb("#6C7993");
    private static readonly Color MutedDark = Color.FromArgb("#9BAAC5");
    private static readonly Color Blue = Color.FromArgb("#245CFF");

    /// <summary>Libellés séparés par « | » (ex. « Clair|Sombre|Système »).</summary>
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(string), typeof(SegmentedControl), "",
        propertyChanged: (b, _, _) => ((SegmentedControl)b).Build());

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(SegmentedControl), 0, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((SegmentedControl)b).Refresh());

    public static readonly BindableProperty ControlHeightProperty = BindableProperty.Create(
        nameof(ControlHeight), typeof(double), typeof(SegmentedControl), 43.0,
        propertyChanged: (b, _, _) => ((SegmentedControl)b).Build());

    public string Items { get => (string)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double ControlHeight { get => (double)GetValue(ControlHeightProperty); set => SetValue(ControlHeightProperty, value); }

    public event EventHandler? SelectedChanged;

    private readonly List<(Border Pill, Label On, Label Off)> _cells = new();

    private void Build()
    {
        _cells.Clear();
        var labels = (Items ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0) { Content = null; return; }

        var grid = new Grid { ColumnSpacing = 0 };
        for (var i = 0; i < labels.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var index = i;

            var pill = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Blue,
                StrokeShape = new Shapes.RoundRectangle { CornerRadius = new CornerRadius(11) },
                IsVisible = false
            };
            var off = new Label
            {
                Text = labels[i], FontFamily = "InterSemiBold", FontSize = 12,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            };
            off.SetAppThemeColor(Label.TextColorProperty, MutedLight, MutedDark);
            var on = new Label
            {
                Text = labels[i], FontFamily = "InterSemiBold", FontSize = 12, TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, IsVisible = false
            };

            var cell = new Grid { Children = { pill, off, on } };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (SelectedIndex == index) return;
                SelectedIndex = index;
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            };
            cell.GestureRecognizers.Add(tap);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
            _cells.Add((pill, on, off));
        }

        var container = new Border
        {
            Padding = new Thickness(4),
            HeightRequest = ControlHeight,
            StrokeThickness = 0,
            StrokeShape = new Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
            Content = grid
        };
        container.SetAppThemeColor(VisualElement.BackgroundColorProperty, SoftLight, SoftDark);
        Content = container;
        Refresh();
    }

    private void Refresh()
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            var sel = i == SelectedIndex;
            _cells[i].Pill.IsVisible = sel;
            _cells[i].On.IsVisible = sel;
            _cells[i].Off.IsVisible = !sel;
        }
    }
}
