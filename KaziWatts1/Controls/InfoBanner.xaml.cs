namespace KaziWatts.Controls;

public partial class InfoBanner : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(InfoBanner), "",
        propertyChanged: (b, _, n) => ((InfoBanner)b).TextLabel.Text = (string)n);

    public static readonly BindableProperty IconNameProperty = BindableProperty.Create(
        nameof(IconName), typeof(string), typeof(InfoBanner), "info",
        propertyChanged: (b, _, n) => ((InfoBanner)b).Glyph.Name = (string)n);

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string IconName { get => (string)GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }

    public InfoBanner() => InitializeComponent();
}
