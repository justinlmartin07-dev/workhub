using System.Windows.Input;

namespace WorkHub.Controls;

// Grid-based wrapper for tappable surfaces (nav rail, bottom tabs) that adds:
//   - Hover highlight: subtle fill fades in/out as the pointer enters/exits (desktop).
//   - Ripple animation: a circle expands from the exact tap point to the farthest
//     corner, then fades, giving material-style tap feedback.
//
// Usage — replace an existing tappable Border with RippleView and move the command:
//
//   <controls:RippleView Command="{Binding SelectTabCommand}" CommandParameter="0"
//                        CornerRadius="14" HorizontalOptions="Fill">
//       <Border ...>  <!-- DataTriggers/VisualStates on Border still work -->
//           ...
//       </Border>
//   </controls:RippleView>
public class RippleView : Grid
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(RippleView));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(RippleView));

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(RippleView), 0f,
            propertyChanged: (b, _, n) => ((RippleView)b)._drawable.ViewCornerRadius = (float)n);

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private readonly RippleDrawable _drawable = new();
    private readonly GraphicsView _overlay;

    public RippleView()
    {
        _overlay = new GraphicsView
        {
            Drawable = _drawable,
            InputTransparent = true,
            BackgroundColor = Colors.Transparent,
        };
        base.Add(_overlay);

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += OnPointerEntered;
        pointer.PointerExited += OnPointerExited;
        GestureRecognizers.Add(pointer);
    }

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);
        // XAML adds content before we can control order — keep overlay on top.
        if (child != _overlay)
        {
            Children.Remove(_overlay);
            Children.Add(_overlay);
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        this.AbortAnimation("hoverOut");
        this.Animate("hoverIn", v =>
        {
            _drawable.HoverAlpha = (float)(v * 0.08);
            _overlay.Invalidate();
        }, 0, 1, length: 120, easing: Easing.CubicOut);
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        var from = _drawable.HoverAlpha;
        this.AbortAnimation("hoverIn");
        this.Animate("hoverOut", v =>
        {
            _drawable.HoverAlpha = (float)(from * (1 - v));
            _overlay.Invalidate();
        }, 0, 1, length: 200, easing: Easing.CubicOut);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(this) ?? new Point(Width / 2, Height / 2);
        StartRipple(pos);
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    internal void StartRipple(Point pos)
    {
        _drawable.TapX = (float)pos.X;
        _drawable.TapY = (float)pos.Y;

        // Expand to the farthest corner so the ripple always fills the element.
        var maxRadius = (float)Math.Sqrt(
            Math.Pow(Math.Max(pos.X, Width - pos.X), 2) +
            Math.Pow(Math.Max(pos.Y, Height - pos.Y), 2));

        this.AbortAnimation("ripple");
        this.Animate("ripple",
            new Animation(v =>
            {
                _drawable.RippleRadius = (float)(maxRadius * v);
                _drawable.RippleAlpha = (float)(0.35 * (1.0 - v));
                _overlay.Invalidate();
            }, 0, 1, Easing.CubicOut),
            length: 450,
            finished: (_, _) =>
            {
                _drawable.RippleRadius = 0;
                _drawable.RippleAlpha = 0;
                _overlay.Invalidate();
            });
    }
}
