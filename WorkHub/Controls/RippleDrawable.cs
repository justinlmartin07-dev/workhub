namespace WorkHub.Controls;

// Draws the two layers of the ripple system onto a GraphicsView overlay:
//   1. A subtle full-fill shown while the pointer hovers (desktop).
//   2. An expanding circle that starts at the tap point and fades out.
// Both layers clip to the host element's corner radius so the ripple never
// spills outside rounded card or tab corners.
public class RippleDrawable : IDrawable
{
    public float TapX { get; set; }
    public float TapY { get; set; }
    public float RippleRadius { get; set; }
    public float RippleAlpha { get; set; }
    public float HoverAlpha { get; set; }
    public float ViewCornerRadius { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (RippleAlpha <= 0 && HoverAlpha <= 0) return;

        canvas.SaveState();

        if (ViewCornerRadius > 0)
        {
            var clip = new PathF();
            clip.AppendRoundedRectangle(0, 0, dirtyRect.Width, dirtyRect.Height, ViewCornerRadius);
            canvas.ClipPath(clip);
        }

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        if (HoverAlpha > 0)
        {
            canvas.Alpha = HoverAlpha;
            canvas.FillColor = isDark ? Colors.White : Colors.Black;
            canvas.FillRectangle(0, 0, dirtyRect.Width, dirtyRect.Height);
        }

        if (RippleRadius > 0 && RippleAlpha > 0)
        {
            canvas.Alpha = RippleAlpha;
            canvas.FillColor = isDark ? Colors.White : Color.FromArgb("#808080");
            canvas.FillCircle(TapX, TapY, RippleRadius);
        }

        canvas.RestoreState();
    }
}
