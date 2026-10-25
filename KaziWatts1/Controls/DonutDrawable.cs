using Microsoft.Maui.Graphics;

namespace KaziWatts.Controls;

/// <summary>Anneau de répartition (écran « Répartition facture ») : segments séparés par un petit espace.</summary>
public class DonutDrawable : IDrawable
{
    public (float Fraction, Color Color)[] Segments { get; set; } = Array.Empty<(float, Color)>();
    public float Thickness { get; set; } = 14f;
    public float GapDeg { get; set; } = 2.6f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var cx = dirtyRect.Width / 2f;
        var cy = dirtyRect.Height / 2f;
        var r = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - Thickness / 2f;

        canvas.StrokeSize = Thickness;
        canvas.StrokeLineCap = LineCap.Butt;

        var start = -90f; // 12 h, sens horaire
        foreach (var (fraction, color) in Segments)
        {
            var sweep = fraction * 360f;
            var a0 = start + GapDeg / 2f;
            var a1 = start + sweep - GapDeg / 2f;
            var path = new PathF();
            var first = true;
            for (var a = a0; a <= a1; a += 1f)
            {
                var rad = a * MathF.PI / 180f;
                var x = cx + r * MathF.Cos(rad);
                var y = cy + r * MathF.Sin(rad);
                if (first) { path.MoveTo(x, y); first = false; } else path.LineTo(x, y);
            }
            canvas.StrokeColor = color;
            canvas.DrawPath(path);
            start += sweep;
        }
    }
}
