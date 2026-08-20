namespace WorkHub.Controls;

// Data-driven replacement for the VSM "Selected" state on list row cards.
// WinUI recycles CollectionView containers without reliably resetting visual
// state, so VSM rings stick to recycled containers and stale rings pile up as
// the list scrolls. IsSelected is bound per row to
// (row == CollectionView.SelectedItem), which re-evaluates on both container
// recycle (BindingContext change) and selection change, so exactly one row
// ever wears the ring.
public static class SelectionRing
{
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.CreateAttached(
        "IsSelected", typeof(bool), typeof(SelectionRing), false, propertyChanged: OnIsSelectedChanged);

    public static bool GetIsSelected(BindableObject view) => (bool)view.GetValue(IsSelectedProperty);
    public static void SetIsSelected(BindableObject view, bool value) => view.SetValue(IsSelectedProperty, value);

    private static void OnIsSelectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Border border) return;

        if ((bool)newValue)
        {
            var light = GetColor("Primary", Colors.Blue);
            var dark = GetColor("PrimaryDark", Colors.Blue);
            border.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(light), new SolidColorBrush(dark));
            border.StrokeThickness = 2;
            border.SetAppTheme(VisualElement.ShadowProperty, MakeShadow(light), MakeShadow(dark));
        }
        else
        {
            // Drop the theme bindings too, or a later theme switch would
            // re-assert the ring on this (now deselected) row.
            border.RemoveBinding(Border.StrokeProperty);
            border.RemoveBinding(VisualElement.ShadowProperty);
            border.Stroke = Brush.Transparent;
            border.StrokeThickness = 0;
            border.Shadow = null!;
        }
    }

    private static Shadow MakeShadow(Color color) => new()
    {
        Brush = new SolidColorBrush(color),
        Offset = new Point(0, 2),
        Radius = 12,
        Opacity = 0.25f,
    };

    private static Color GetColor(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c ? c : fallback;
}
