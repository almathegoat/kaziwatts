namespace KaziWatts.Controls;

public partial class SoftButton : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(SoftButton), "",
        propertyChanged: (b, _, n) => ((SoftButton)b).TextLabel.Text = (string)n);

    public static readonly BindableProperty IconNameProperty = BindableProperty.Create(
        nameof(IconName), typeof(string), typeof(SoftButton), "",
        propertyChanged: (b, _, n) =>
        {
            var s = (SoftButton)b;
            s.Glyph.Name = (string)n;
            s.Glyph.IsVisible = !string.IsNullOrEmpty((string)n);
        });

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string IconName { get => (string)GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }

    public event EventHandler? Clicked;

    public SoftButton() => InitializeComponent();

    private void OnTapped(object? sender, TappedEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
}
