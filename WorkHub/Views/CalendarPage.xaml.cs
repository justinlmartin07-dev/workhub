using Microsoft.Maui.Controls.Shapes;
using WorkHub.Models;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CalendarPage : ContentView
{
    private readonly CalendarViewModel _viewModel;
    private Border? _selectedBorder;
    private Color _primaryColor = Colors.Blue;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _primaryColor = Application.Current!.Resources["Primary"] as Color ?? Colors.Blue;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CalendarViewModel.Weeks))
                BuildMonthGrid();
        };
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null && _viewModel.Events.Count == 0)
        {
            _viewModel.LoadEventsCommand.Execute(null);
        }

#if WINDOWS
        AttachScrollHandler();
#endif
    }

#if WINDOWS
    private void AttachScrollHandler()
    {
        if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement nativeView)
        {
            nativeView.PointerWheelChanged += OnPointerWheelChanged;
        }
    }

    private bool _scrollCooldown;

    private async void OnPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        var isShift = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        var delta = point.Properties.MouseWheelDelta;

        // Only respond to horizontal scroll (shift+wheel or horizontal wheel)
        if (!isShift && !point.Properties.IsHorizontalMouseWheel)
            return;

        if (_scrollCooldown) return;
        _scrollCooldown = true;

        if (delta > 0)
            _viewModel.NextMonthCommand.Execute(null);
        else if (delta < 0)
            _viewModel.PreviousMonthCommand.Execute(null);

        e.Handled = true;

        await Task.Delay(300);
        _scrollCooldown = false;
    }
#endif

    private void BuildMonthGrid()
    {
        MonthGrid.Children.Clear();
        MonthGrid.RowDefinitions.Clear();
        MonthGrid.ColumnDefinitions.Clear();

        for (int c = 0; c < 7; c++)
            MonthGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        int rowIndex = 0;
        foreach (var week in _viewModel.Weeks)
        {
            MonthGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            var days = week.AllDays;
            for (int i = 0; i < 7; i++)
            {
                var cell = BuildDayCell(days[i]);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, i);
                MonthGrid.Children.Add(cell);
            }
            rowIndex++;
        }
    }

    private View BuildDayCell(CalendarDay day)
    {
        var container = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            StrokeThickness = day.IsSelected ? 2 : 0.5,
            Stroke = day.IsSelected ? _primaryColor : Colors.LightGray,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(2),
            Margin = new Thickness(1),
        };

        if (day.IsSelected)
            _selectedBorder = container;

        var stack = new VerticalStackLayout { Spacing = 1 };

        if (day.IsCurrentMonth)
        {
            // Day number - circle behind it for today
            var dayLabel = new Label
            {
                Text = day.DayNumber,
                FontSize = 12,
                FontAttributes = day.IsToday ? FontAttributes.Bold : FontAttributes.None,
                TextColor = day.IsToday ? Colors.White : Colors.Black,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
            };

            if (day.IsToday)
            {
                var circle = new Border
                {
                    StrokeShape = new Ellipse(),
                    StrokeThickness = 0,
                    BackgroundColor = _primaryColor,
                    WidthRequest = 24,
                    HeightRequest = 24,
                    HorizontalOptions = LayoutOptions.Center,
                    Content = dayLabel,
                };
                stack.Children.Add(circle);
            }
            else
            {
                stack.Children.Add(dayLabel);
            }

            // Event tiles
            foreach (var evt in day.Events)
            {
                var tile = new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 2 },
                    StrokeThickness = 0,
                    BackgroundColor = _primaryColor,
                    Padding = new Thickness(3, 1),
                    Margin = new Thickness(0, 0, 0, 1),
                };
                tile.Content = new Label
                {
                    Text = evt.Title,
                    FontSize = 9,
                    TextColor = Colors.White,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1,
                };
                tile.InputTransparent = true;
                stack.Children.Add(tile);
            }

            // Tap day to select — update border immediately, then notify VM
            var borderRef = container;
            container.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    // Deselect old
                    if (_selectedBorder != null)
                    {
                        _selectedBorder.StrokeThickness = 0.5;
                        _selectedBorder.Stroke = Colors.LightGray;
                    }
                    // Select new
                    borderRef.StrokeThickness = 2;
                    borderRef.Stroke = _primaryColor;
                    _selectedBorder = borderRef;

                    // Then let the VM do its work
                    _viewModel.SelectDayCommand.Execute(day);
                }),
            });
        }

        container.Content = stack;
        return container;
    }

    private void OnSwipedLeft(object? sender, SwipedEventArgs e)
    {
        _viewModel.NextMonthCommand.Execute(null);
    }

    private void OnSwipedRight(object? sender, SwipedEventArgs e)
    {
        _viewModel.PreviousMonthCommand.Execute(null);
    }
}
