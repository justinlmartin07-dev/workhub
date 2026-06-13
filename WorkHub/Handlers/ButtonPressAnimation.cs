using System.Runtime.CompilerServices;

namespace WorkHub.Handlers;

// Global press feedback for every Button: a quick scale-down on press and a
// springy return on release — the "ripple" half of the button feedback, with
// the color half handled by the visual states in Styles.xaml. Attached once
// per button via the handler mapper in MauiProgram, so no XAML changes are
// needed anywhere.
public static class ButtonPressAnimation
{
    // Handler mappings re-run when a cached page reattaches — track buttons
    // we've already wired so events aren't subscribed twice.
    private static readonly ConditionalWeakTable<Button, object> Attached = new();

    public static void Attach(Button button)
    {
        if (Attached.TryGetValue(button, out _)) return;
        Attached.Add(button, new object());

        button.Pressed += static (s, e) =>
        {
            if (s is Button b)
                _ = b.ScaleTo(0.94, 70, Easing.CubicOut);
        };
        button.Released += static (s, e) =>
        {
            if (s is Button b)
                _ = b.ScaleTo(1.0, 180, Easing.SpringOut);
        };
    }
}
