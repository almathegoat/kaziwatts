namespace KaziWatts.Controls;

public partial class PageHeader : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageHeader), "",
        propertyChanged: (b, _, n) => ((PageHeader)b).TitleLabel.Text = (string)n);

    /// <summary>Route de retour : ".." (écran précédent) ou une route absolue comme "//splash".</summary>
    public static readonly BindableProperty BackRouteProperty = BindableProperty.Create(
        nameof(BackRoute), typeof(string), typeof(PageHeader), "..");

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(PageHeader), "",
        propertyChanged: (b, _, n) =>
        {
            var h = (PageHeader)b;
            h.SubtitleLabel.Text = (string)n;
            h.SubtitleLabel.IsVisible = !string.IsNullOrEmpty((string)n);
        });

    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string BackRoute { get => (string)GetValue(BackRouteProperty); set => SetValue(BackRouteProperty, value); }

    public PageHeader() => InitializeComponent();

    private async void OnBack(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(BackRoute);
}
