namespace KaziWatts.Controls;

public partial class AppCheckBox : ContentView
{
    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
        nameof(IsChecked), typeof(bool), typeof(AppCheckBox), false,
        propertyChanged: (b, _, n) => ((AppCheckBox)b).Tick.IsVisible = (bool)n);

    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    public event EventHandler? Toggled;

    public AppCheckBox()
    {
        InitializeComponent();
        Tick.IsVisible = IsChecked;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        IsChecked = !IsChecked;
        Toggled?.Invoke(this, EventArgs.Empty);
    }
}
