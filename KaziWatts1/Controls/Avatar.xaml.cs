namespace KaziWatts.Controls;

public partial class Avatar : ContentView
{
    public static readonly BindableProperty InitialsProperty = BindableProperty.Create(
        nameof(Initials), typeof(string), typeof(Avatar), "",
        propertyChanged: (b, _, n) => ((Avatar)b).InitialsLabel.Text = (string)n);

    public static readonly BindableProperty DiameterProperty = BindableProperty.Create(
        nameof(Diameter), typeof(double), typeof(Avatar), 42.0,
        propertyChanged: (b, _, n) =>
        {
            var a = (Avatar)b; var d = (double)n;
            a.Disc.WidthRequest = d; a.Disc.HeightRequest = d;
            a.InitialsLabel.FontSize = Math.Round(d * 0.34, 1);
        });

    public string Initials { get => (string)GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    public Avatar() => InitializeComponent();
}
