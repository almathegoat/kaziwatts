namespace KaziWatts.Controls;

public partial class StepBar : ContentView
{
    /// <summary>Étape atteinte : 1 (Colocation), 2 (Compteur) ou 3 (Terminé).</summary>
    public static readonly BindableProperty StepProperty = BindableProperty.Create(
        nameof(Step), typeof(int), typeof(StepBar), 1,
        propertyChanged: (b, _, _) => ((StepBar)b).Update());

    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    public StepBar()
    {
        InitializeComponent();
        Update();
    }

    private void Update()
    {
        Seg1On.IsVisible = Step >= 1; Lab1On.IsVisible = Step >= 1; Lab1Off.IsVisible = Step < 1;
        Seg2On.IsVisible = Step >= 2; Lab2On.IsVisible = Step >= 2; Lab2Off.IsVisible = Step < 2;
        Seg3On.IsVisible = Step >= 3; Lab3On.IsVisible = Step >= 3; Lab3Off.IsVisible = Step < 3;
    }
}
