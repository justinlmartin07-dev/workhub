using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;

namespace WorkHub.Controls;

// Injects a ripple overlay into an existing Border without changing its XAML
// structure — safe for CollectionView DataTemplates where the Border's
// VisualStateManager must stay on the DataTemplate root.
//
// Usage:
//   <Border ... controls:RippleEffect.Enable="True">
//
// The overlay Grid is injected once when the element attaches to the visual tree.
// Hover highlight and ripple animation are identical to RippleView.
public static class RippleEffect
{
    public static readonly BindableProperty EnableProperty = BindableProperty.CreateAttached(
        "Enable", typeof(bool), typeof(RippleEffect), false, propertyChanged: OnEnableChanged);

    public static bool GetEnable(BindableObject v) => (bool)v.GetValue(EnableProperty);
    public static void SetEnable(BindableObject v, bool value) => v.SetValue(EnableProperty, value);

    // Track injected borders so HandlerChanged re-fires don't double-inject.
    private static readonly ConditionalWeakTable<Border, GraphicsView> _overlays = new();

    private static void OnEnableChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Border border || newValue is not true) return;

        // Hover (desktop).
        var pointer = new PointerGestureRecognizer();
        border.GestureRecognizers.Add(pointer);

        // Ripple — extra recognizer fires alongside any existing command recognizer.
        var tap = new TapGestureRecognizer();
        border.GestureRecognizers.Add(tap);

        border.HandlerChanged += (_, _) => EnsureOverlay(border, pointer, tap);
    }

    private static void EnsureOverlay(Border border, PointerGestureRecognizer pointer, TapGestureRecognizer tap)
    {
        if (border.Handler == null || border.Content == null) return;
        if (_overlays.TryGetValue(border, out _)) return; // already injected

        // Detect corner radius from StrokeShape so callers don't need to duplicate it.
        var cornerRadius = border.StrokeShape is RoundRectangle rr
            ? (float)rr.CornerRadius.TopLeft : 0f;

        var drawable = new RippleDrawable { ViewCornerRadius = cornerRadius };
        var overlay = new GraphicsView
        {
            Drawable = drawable,
            InputTransparent = true,
            BackgroundColor = Colors.Transparent,
        };
        _overlays.Add(border, overlay);

        // Wrap existing content in a Grid so we can stack the overlay on top.
        var existingContent = border.Content;
        border.Content = null;
        var wrapper = new Grid();
        wrapper.Add(existingContent);
        wrapper.Add(overlay); // on top
        border.Content = wrapper;

        // Hover
        pointer.PointerEntered += (_, _) =>
        {
            border.AbortAnimation("hoverOut");
            border.Animate("hoverIn", v =>
            {
                drawable.HoverAlpha = (float)(v * 0.08);
                overlay.Invalidate();
            }, 0, 1, length: 120, easing: Easing.CubicOut);
        };
        pointer.PointerExited += (_, _) =>
        {
            var from = drawable.HoverAlpha;
            border.AbortAnimation("hoverIn");
            border.Animate("hoverOut", v =>
            {
                drawable.HoverAlpha = (float)(from * (1 - v));
                overlay.Invalidate();
            }, 0, 1, length: 200, easing: Easing.CubicOut);
        };

        // Ripple
        tap.Tapped += (_, e) =>
        {
            var pos = e.GetPosition(border) ?? new Point(border.Width / 2, border.Height / 2);
            drawable.TapX = (float)pos.X;
            drawable.TapY = (float)pos.Y;

            var maxRadius = (float)Math.Sqrt(
                Math.Pow(Math.Max(pos.X, border.Width - pos.X), 2) +
                Math.Pow(Math.Max(pos.Y, border.Height - pos.Y), 2));

            border.AbortAnimation("ripple");
            border.Animate("ripple",
                new Animation(v =>
                {
                    drawable.RippleRadius = (float)(maxRadius * v);
                    drawable.RippleAlpha = (float)(0.35 * (1.0 - v));
                    overlay.Invalidate();
                }, 0, 1, Easing.CubicOut),
                length: 450,
                finished: (_, _) =>
                {
                    drawable.RippleRadius = 0;
                    drawable.RippleAlpha = 0;
                    overlay.Invalidate();
                });
        };
    }
}
