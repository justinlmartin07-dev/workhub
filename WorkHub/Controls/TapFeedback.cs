namespace WorkHub.Controls;

// Gives tappable non-Button elements (icon Borders, menu rows, back arrows) the
// same feedback real Buttons get: a hover highlight and a tap scale pulse.
//
//   <Border controls:TapFeedback.Enable="True" ...>
//
// The default hover is the themed light gray used by borderless buttons; filled
// elements supply HoverLight/HoverDark to shift their own fill instead.
public static class TapFeedback
{
    public static readonly BindableProperty EnableProperty = BindableProperty.CreateAttached(
        "Enable", typeof(bool), typeof(TapFeedback), false, propertyChanged: OnEnableChanged);

    public static readonly BindableProperty HoverLightProperty = BindableProperty.CreateAttached(
        "HoverLight", typeof(Color), typeof(TapFeedback), null);

    public static readonly BindableProperty HoverDarkProperty = BindableProperty.CreateAttached(
        "HoverDark", typeof(Color), typeof(TapFeedback), null);

    public static bool GetEnable(BindableObject view) => (bool)view.GetValue(EnableProperty);
    public static void SetEnable(BindableObject view, bool value) => view.SetValue(EnableProperty, value);
    public static Color? GetHoverLight(BindableObject view) => (Color?)view.GetValue(HoverLightProperty);
    public static void SetHoverLight(BindableObject view, Color? value) => view.SetValue(HoverLightProperty, value);
    public static Color? GetHoverDark(BindableObject view) => (Color?)view.GetValue(HoverDarkProperty);
    public static void SetHoverDark(BindableObject view, Color? value) => view.SetValue(HoverDarkProperty, value);

    private static void OnEnableChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view || newValue is not true) return;

        // Hover (desktop): swap the background while the pointer is over the
        // element, restoring whatever it was on exit.
        Color? restingBackground = null;
        var hovering = false;

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (s, e) =>
        {
            if (hovering) return;
            hovering = true;
            restingBackground = view.BackgroundColor;
            view.BackgroundColor = HoverColorFor(view);
        };
        pointer.PointerExited += (s, e) =>
        {
            if (!hovering) return;
            hovering = false;
            view.BackgroundColor = restingBackground;
        };
        view.GestureRecognizers.Add(pointer);

        // Tap pulse: same scale feedback every Button gets globally. Runs
        // alongside the element's own command-bearing TapGestureRecognizer.
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            await view.ScaleTo(0.92, 60, Easing.CubicOut);
            await view.ScaleTo(1.0, 180, Easing.SpringOut);
            // Clear hover — PointerExited won't fire when the view is replaced
            // (e.g. detail panel swaps to edit form after clicking Edit/Delete).
            if (hovering)
            {
                hovering = false;
                view.BackgroundColor = restingBackground;
            }
        };
        view.GestureRecognizers.Add(tap);
    }

    private static Color HoverColorFor(View view)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var custom = isDark ? GetHoverDark(view) : GetHoverLight(view);
        if (custom != null) return custom;
        // Defaults match the GhostButton hover: Gray100 light / Gray700 dark
        return isDark ? Color.FromArgb("#334155") : Color.FromArgb("#F1F5F9");
    }
}
