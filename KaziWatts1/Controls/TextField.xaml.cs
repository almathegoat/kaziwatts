namespace KaziWatts.Controls;

public partial class TextField : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TextField), "", BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var f = (TextField)b;
            if (f.Input.Text != (string?)n) f.Input.Text = (string?)n;
        });

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(TextField), "",
        propertyChanged: (b, _, n) => ((TextField)b).Input.Placeholder = (string)n);

    /// <summary>Si vrai, le texte est masqué et l'icône sert à l'afficher / le cacher.</summary>
    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
        nameof(IsPassword), typeof(bool), typeof(TextField), false,
        propertyChanged: (b, _, n) => { var f = (TextField)b; f.Input.IsPassword = (bool)n; f.UpdateIcon(); });

    /// <summary>Icône affichée quand le texte est masqué (ou icône fixe si le champ n'est pas un mot de passe).</summary>
    public static readonly BindableProperty IconNameProperty = BindableProperty.Create(
        nameof(IconName), typeof(string), typeof(TextField), "",
        propertyChanged: (b, _, _) => ((TextField)b).UpdateIcon());

    /// <summary>Icône affichée quand le mot de passe est visible.</summary>
    public static readonly BindableProperty RevealedIconNameProperty = BindableProperty.Create(
        nameof(RevealedIconName), typeof(string), typeof(TextField), "eye-off",
        propertyChanged: (b, _, _) => ((TextField)b).UpdateIcon());

    public static readonly BindableProperty KeyboardTypeProperty = BindableProperty.Create(
        nameof(KeyboardType), typeof(Keyboard), typeof(TextField), Keyboard.Default,
        propertyChanged: (b, _, n) => ((TextField)b).Input.Keyboard = (Keyboard)n);

    public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(
        nameof(IsReadOnly), typeof(bool), typeof(TextField), false,
        propertyChanged: (b, _, n) => ((TextField)b).Input.IsReadOnly = (bool)n);

    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>Déclenché quand on touche l'icône d'un champ qui n'est pas un mot de passe (ex. copier).</summary>
    public event EventHandler? IconTapped;

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool IsPassword { get => (bool)GetValue(IsPasswordProperty); set => SetValue(IsPasswordProperty, value); }
    public string IconName { get => (string)GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }
    public string RevealedIconName { get => (string)GetValue(RevealedIconNameProperty); set => SetValue(RevealedIconNameProperty, value); }
    public Keyboard KeyboardType { get => (Keyboard)GetValue(KeyboardTypeProperty); set => SetValue(KeyboardTypeProperty, value); }

    public event EventHandler<TextChangedEventArgs>? TextChanged;

    public TextField() => InitializeComponent();

    private bool _revealed;

    private void UpdateIcon()
    {
        Trailing.Name = _revealed ? RevealedIconName : IconName;
        Trailing.IsVisible = !string.IsNullOrEmpty(Trailing.Name);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Text != e.NewTextValue) Text = e.NewTextValue ?? "";
        TextChanged?.Invoke(this, e);
    }

    private void OnIconTapped(object? sender, TappedEventArgs e)
    {
        if (!IsPassword) { IconTapped?.Invoke(this, EventArgs.Empty); return; }
        _revealed = !_revealed;
        Input.IsPassword = !_revealed;
        UpdateIcon();
    }
}
