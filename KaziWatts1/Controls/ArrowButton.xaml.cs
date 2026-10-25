namespace KaziWatts.Controls;

public partial class ArrowButton : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(ArrowButton), "",
        propertyChanged: (b, _, n) => ((ArrowButton)b).TextLabel.Text = (string)n);

    /// <summary>"arrow" (flèche), "check" (coche), "plus" ou "none" (sans icône).</summary>
    public static readonly BindableProperty IconNameProperty = BindableProperty.Create(
        nameof(IconName), typeof(string), typeof(ArrowButton), "arrow",
        propertyChanged: (b, _, n) => ((ArrowButton)b).ApplyIcon((string)n));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string IconName { get => (string)GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }

    public event EventHandler? Clicked;

    public ArrowButton() => InitializeComponent();

    private void ApplyIcon(string name)
    {
        var icon = name switch { "arrow" => "arrow-right", "none" => "", _ => name };
        Glyph.IsVisible = icon.Length > 0;
        Glyph.Name = icon;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled) && Root != null) Root.Opacity = IsEnabled ? 1 : 0.5;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (IsEnabled) Clicked?.Invoke(this, EventArgs.Empty);
    }
}
