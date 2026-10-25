using Microsoft.Maui.Graphics;

namespace KaziWatts.Controls;

/// <summary>Jauge circulaire de l'accueil : piste + arc cyan de 282° (ouverture en bas), épaisseur 10.</summary>
public class RingDrawable : IDrawable
{
    public Color Track { get; set; } = Color.FromArgb("#3052B4");
    public Color Arc { get; set; } = Color.FromArgb("#62DDFF");
    public float Thickness { get; set; } = 10f;
    public float StartDeg { get; set; } = 129f;   // bas-gauche (0° = 3 h, sens horaire)
    public float SweepDeg { get; set; } = 282f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var cx = dirtyRect.Width / 2f;
        var cy = dirtyRect.Height / 2f;
        var r = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - Thickness / 2f;

        canvas.StrokeSize = Thickness;
        canvas.StrokeLineCap = LineCap.Round;

        canvas.StrokeColor = Track;
        canvas.DrawCircle(cx, cy, r);

        var path = new PathF();
        for (var d = 0f; d <= SweepDeg; d += 2f)
        {
            var a = (StartDeg + d) * MathF.PI / 180f;
            var x = cx + r * MathF.Cos(a);
            var y = cy + r * MathF.Sin(a);
            if (d == 0f) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        canvas.StrokeColor = Arc;
        canvas.DrawPath(path);
    }
}
