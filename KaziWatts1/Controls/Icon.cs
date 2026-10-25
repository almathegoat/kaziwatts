using Shapes = Microsoft.Maui.Controls.Shapes;

namespace KaziWatts.Controls;

/// <summary>
/// Icône vectorielle (tracés Lucide sur une grille 24 x 24, comme dans les maquettes).
/// L'icône occupe un carré de <see cref="Size"/> points ; le dessin est mis à l'échelle Size / 24.
/// </summary>
public class Icon : ContentView
{
    // Nom -> (tracé, xMin, yMin, xMax, yMax) sur la grille 24 x 24
    private static readonly Dictionary<string, (string Data, double X0, double Y0, double X1, double Y1)> Defs = new()
    {
        ["arrow-right"] = ("M5,12 L19,12 M12,5 L19,12 L12,19", 5, 5, 19, 19),
        ["arrow-left"]  = ("M19,12 L5,12 M12,19 L5,12 L12,5", 5, 5, 19, 19),
        ["check"]       = ("M20,6 L9,17 L4,12", 4, 6, 20, 17),
        ["plus"]        = ("M12,5 L12,19 M5,12 L19,12", 5, 5, 19, 19),
        ["users"]       = ("M16,21 L16,19 A4,4 0 0 0 12,15 L6,15 A4,4 0 0 0 2,19 L2,21 M13,7 A4,4 0 1 1 5,7 A4,4 0 1 1 13,7 Z M22,21 L22,19 A4,4 0 0 0 19,15.13 M16,3.13 A4,4 0 0 1 16,10.87", 2, 3, 22, 21),
        ["copy"]        = ("M10,8 L20,8 A2,2 0 0 1 22,10 L22,20 A2,2 0 0 1 20,22 L10,22 A2,2 0 0 1 8,20 L8,10 A2,2 0 0 1 10,8 Z M4,16 A2,2 0 0 1 2,14 L2,4 A2,2 0 0 1 4,2 L14,2 A2,2 0 0 1 16,4", 2, 2, 22, 22),
        ["chevron-right"] = ("M9,18 L15,12 L9,6", 9, 6, 15, 18),
        ["link"]        = ("M10,13 A5,5 0 0 0 17.54,13.54 L20.54,10.54 A5,5 0 0 0 13.47,3.47 L11.75,5.18 M14,11 A5,5 0 0 0 6.46,10.46 L3.46,13.46 A5,5 0 0 0 10.53,20.53 L12.24,18.82", 2.5, 2.5, 21.5, 21.5),
        ["share"]       = ("M18,2 A3,3 0 1 1 18,8 A3,3 0 1 1 18,2 Z M6,9 A3,3 0 1 1 6,15 A3,3 0 1 1 6,9 Z M18,16 A3,3 0 1 1 18,22 A3,3 0 1 1 18,16 Z M8.59,13.51 L15.42,17.49 M15.41,6.51 L8.59,10.49", 3, 2, 21, 22),
        ["scan-line"]   = ("M3,7 L3,5 A2,2 0 0 1 5,3 L7,3 M17,3 L19,3 A2,2 0 0 1 21,5 L21,7 M21,17 L21,19 A2,2 0 0 1 19,21 L17,21 M7,21 L5,21 A2,2 0 0 1 3,19 L3,17 M7,12 L17,12", 3, 3, 21, 21),
        ["pie"]         = ("M21.21,15.89 A10,10 0 1 1 8,2.83 M22,12 A10,10 0 0 0 12,2 L12,12 Z", 2, 2, 22, 22),
        ["bell"]        = ("M6,8 A6,6 0 0 1 18,8 C18,15 21,17 21,17 L3,17 C3,17 6,15 6,8 M10.3,21 A1.94,1.94 0 0 0 13.7,21", 3, 2, 21, 22),
        ["zap"]         = ("M13,2 L3,14 L12,14 L11,22 L21,10 L12,10 Z", 3, 2, 21, 22),
        ["home"]        = ("M3,10 L12,2 L21,10 L21,20 A2,2 0 0 1 19,22 L5,22 A2,2 0 0 1 3,20 Z M9,22 L9,13 L15,13 L15,22", 3, 2, 21, 22),
        ["wallet"]      = ("M5,6 L19,6 A2,2 0 0 1 21,8 L21,19 A2,2 0 0 1 19,21 L5,21 A2,2 0 0 1 3,19 L3,8 A2,2 0 0 1 5,6 Z M3,8 A2,2 0 0 1 5,4 L17,4 M21,12 L17,12 A2,2 0 0 0 17,16 L21,16", 3, 4, 21, 21),
        ["gauge"]       = ("M12,14 L16,10 M3.34,19 A10,10 0 1 1 20.66,19", 2, 2, 22, 19),
        ["trending-up"] = ("M22,7 L13.5,15.5 L8.5,10.5 L2,17 M16,7 L22,7 L22,13", 2, 7, 22, 17),
        ["shield-check"] = ("M20,13 C20,18 16.5,20.5 12.34,21.95 A1,1 0 0 1 11.66,21.95 C7.5,20.5 4,18 4,13 L4,6 A1,1 0 0 1 5,5 C7,5 9.5,3.8 11.24,2.28 A1.17,1.17 0 0 1 12.76,2.28 C14.51,3.81 17,5 19,5 A1,1 0 0 1 20,6 Z M9,12 L11,14 L15,10", 4, 2, 20, 22),
        ["smartphone"]  = ("M7,2 L17,2 A2,2 0 0 1 19,4 L19,20 A2,2 0 0 1 17,22 L7,22 A2,2 0 0 1 5,20 L5,4 A2,2 0 0 1 7,2 Z M12,18 L12,18.01", 5, 2, 19, 22),
        ["download"]    = ("M21,15 L21,19 A2,2 0 0 1 19,21 L5,21 A2,2 0 0 1 3,19 L3,15 M7,10 L12,15 L17,10 M12,15 L12,3", 3, 3, 21, 21),
        ["arrow-up-right"]   = ("M7,7 L17,7 L17,17 M7,17 L17,7", 7, 7, 17, 17),
        ["arrow-down-left"]  = ("M17,7 L7,17 M17,17 L7,17 L7,7", 7, 7, 17, 17),
        ["sliders"]     = ("M21,4 L14,4 M10,4 L3,4 M21,12 L12,12 M8,12 L3,12 M21,20 L16,20 M12,20 L3,20 M14,2 L14,6 M8,10 L8,14 M16,18 L16,22", 3, 2, 21, 22),
        ["scale"]       = ("M16,16 L19,8 L22,16 C21.13,16.65 20.08,17 19,17 C17.92,17 16.87,16.65 16,16 Z M2,16 L5,8 L8,16 C7.13,16.65 6.08,17 5,17 C3.92,17 2.87,16.65 2,16 Z M7,21 L17,21 M12,3 L12,21 M3,7 L5,7 C7,7 10,6 12,5 C14,6 17,7 19,7 L21,7", 2, 3, 22, 21),
        ["image-plus"]  = ("M16,5 L22,5 M19,2 L19,8 M21,11.5 L21,19 A2,2 0 0 1 19,21 L5,21 A2,2 0 0 1 3,19 L3,5 A2,2 0 0 1 5,3 L12.5,3 M21,15 L17.91,11.91 A2,2 0 0 0 15.09,11.91 L6,21 M11,9 A2,2 0 1 1 7,9 A2,2 0 1 1 11,9 Z", 3, 2, 22, 21),
        ["hash"]        = ("M4,9 L20,9 M4,15 L20,15 M10,3 L8,21 M16,3 L14,21", 4, 3, 20, 21),
        ["help"]        = ("M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 Z M9.09,9 A3,3 0 0 1 14.99,10 C14.99,12 11.99,13 11.99,13 M12,17 L12,17.01", 2, 2, 22, 22),
        ["info"]        = ("M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 Z M12,16 L12,12 M12,8 L12,8.01", 2, 2, 22, 22),
        ["log-out"]     = ("M9,21 L5,21 A2,2 0 0 1 3,19 L3,5 A2,2 0 0 1 5,3 L9,3 M16,17 L21,12 L16,7 M21,12 L9,12", 3, 3, 21, 21),
        ["sun"]         = ("M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12 Z M12,2 L12,4 M12,20 L12,22 M4.93,4.93 L6.34,6.34 M17.66,17.66 L19.07,19.07 M2,12 L4,12 M20,12 L22,12 M6.34,17.66 L4.93,19.07 M19.07,4.93 L17.66,6.34", 2, 2, 22, 22),
        ["moon"]        = ("M12,3 A6,6 0 0 0 21,12 A9,9 0 1 1 12,3 Z", 3, 3, 21, 21),
        ["user-plus"]   = ("M16,21 L16,19 A4,4 0 0 0 12,15 L6,15 A4,4 0 0 0 2,19 L2,21 M13,7 A4,4 0 1 1 5,7 A4,4 0 1 1 13,7 Z M19,8 L19,14 M22,11 L16,11", 2, 3, 22, 21),
        ["user"]        = ("M12,3 A5,5 0 1 1 12,13 A5,5 0 1 1 12,3 Z M20,21 A8,8 0 0 0 4,21", 4, 3, 20, 21),
        ["mail"]        = ("M4,4 L20,4 A2,2 0 0 1 22,6 L22,18 A2,2 0 0 1 20,20 L4,20 A2,2 0 0 1 2,18 L2,6 A2,2 0 0 1 4,4 Z M22,7 L12,13 L2,7", 2, 4, 22, 20),
        ["eye"]         = ("M2,12 C4,8 7.5,5 12,5 C16.5,5 20,8 22,12 C20,16 16.5,19 12,19 C7.5,19 4,16 2,12 Z M12,9 A3,3 0 1 1 12,15 A3,3 0 1 1 12,9 Z", 2, 5, 22, 19),
        ["eye-off"]     = ("M2,12 C4,8 7.5,5 12,5 C16.5,5 20,8 22,12 C20,16 16.5,19 12,19 C7.5,19 4,16 2,12 Z M12,9 A3,3 0 1 1 12,15 A3,3 0 1 1 12,9 Z M3,3 L21,21", 2, 3, 22, 21),
    };

    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        nameof(Name), typeof(string), typeof(Icon), "", propertyChanged: (b, _, _) => ((Icon)b).Refresh());
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(Icon), 20.0, propertyChanged: (b, _, _) => ((Icon)b).Refresh());
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(Icon), Colors.Gray, propertyChanged: (b, _, _) => ((Icon)b).Refresh());
    public static readonly BindableProperty ThicknessProperty = BindableProperty.Create(
        nameof(Thickness), typeof(double), typeof(Icon), 1.6, propertyChanged: (b, _, _) => ((Icon)b).Refresh());

    public string Name { get => (string)GetValue(NameProperty); set => SetValue(NameProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    public Icon()
    {
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        InputTransparent = false;
        Refresh();
    }

    private void Refresh()
    {
        WidthRequest = Size;
        HeightRequest = Size;
        if (!Defs.TryGetValue(Name ?? "", out var d)) { Content = null; return; }

        var k = Size / 24.0;
        var geometry = (Shapes.Geometry)new Shapes.PathGeometryConverter().ConvertFromInvariantString(d.Data)!;
        Content = new Shapes.Path
        {
            Data = geometry,
            Aspect = Stretch.Uniform,
            Stroke = new SolidColorBrush(Color),
            StrokeThickness = Thickness * k,
            StrokeLineCap = Shapes.PenLineCap.Round,
            StrokeLineJoin = Shapes.PenLineJoin.Round,
            WidthRequest = (d.X1 - d.X0) * k,
            HeightRequest = (d.Y1 - d.Y0) * k,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
    }
}
