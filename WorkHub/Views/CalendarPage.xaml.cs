using Microsoft.Maui.Controls.Shapes;
using WorkHub.Models;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CalendarPage : ContentView
{
    private const int MaxLanesPerWeek = 3;

    private readonly CalendarViewModel _viewModel;
    private Color _primaryColor = Colors.Teal;
    private Color _infoColor = Colors.SteelBlue;
    private Color _warningColor = Colors.Orange;
    private Color _textColor = Colors.Black;
    private Color _mutedTextColor = Colors.Gray;
    private Color _separatorColor = Colors.LightGray;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _primaryColor = ResolveColor("Primary", Colors.Teal);
        _infoColor = ResolveColor("Info", Colors.SteelBlue);
        _warningColor = ResolveColor("Warning", Colors.Orange);

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        _textColor = isDark
            ? ResolveColor("Gray100", Colors.White)
            : ResolveColor("Gray900", Colors.Black);
        _mutedTextColor = isDark
            ? ResolveColor("Gray500", Colors.Gray)
            : ResolveColor("Gray400", Colors.Gray);
        _separatorColor = isDark
            ? ResolveColor("SurfaceBorderDark", Colors.DimGray)
            : ResolveColor("SurfaceBorderLight", Colors.LightGray);

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CalendarViewModel.Weeks))
                BuildMonthGrid();
        };
    }

    private static Color ResolveColor(string key, Color fallback)
        => Application.Current?.Resources[key] as Color ?? fallback;

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
        MonthGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        MonthGrid.RowSpacing = 0;

        int rowIndex = 0;
        foreach (var week in _viewModel.Weeks)
        {
            MonthGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            // Top separator line for every week except the first
            if (rowIndex > 0)
            {
                var separator = new BoxView
                {
                    HeightRequest = 0.5,
                    Color = _separatorColor,
                    VerticalOptions = LayoutOptions.Start,
                };
                Grid.SetRow(separator, rowIndex);
                Grid.SetColumn(separator, 0);
                MonthGrid.Children.Add(separator);
            }

            var weekView = BuildWeekRow(week);
            Grid.SetRow(weekView, rowIndex);
            Grid.SetColumn(weekView, 0);
            MonthGrid.Children.Add(weekView);
            rowIndex++;
        }
    }

    private View BuildWeekRow(CalendarWeek week)
    {
        var grid = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 1,
            Padding = new Thickness(0, 2, 0, 0),
        };
        for (int c = 0; c < 7; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        // Row 0: day numbers; Rows 1..N: event lanes
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int l = 0; l < MaxLanesPerWeek; l++)
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(18)));

        // Day-number headers
        var days = week.AllDays;
        for (int i = 0; i < 7; i++)
        {
            var header = BuildDayHeader(days[i]);
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, i);
            grid.Children.Add(header);
        }

        // Tap targets per column (covers the area below the day number)
        for (int i = 0; i < 7; i++)
        {
            var day = days[i];
            if (!day.IsCurrentMonth) continue;
            var tapTarget = new BoxView
            {
                BackgroundColor = Colors.Transparent,
            };
            tapTarget.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _viewModel.SelectDayCommand,
                CommandParameter = day,
            });
            Grid.SetRow(tapTarget, 1);
            Grid.SetRowSpan(tapTarget, MaxLanesPerWeek);
            Grid.SetColumn(tapTarget, i);
            grid.Children.Add(tapTarget);
        }

        // Event bars
        int hiddenCount = 0;
        var hiddenByColumn = new int[7];
        foreach (var bar in week.EventBars)
        {
            if (bar.Lane >= MaxLanesPerWeek)
            {
                for (int c = bar.StartColumn; c < bar.StartColumn + bar.ColumnSpan; c++)
                    hiddenByColumn[c]++;
                hiddenCount++;
                continue;
            }
            var barView = BuildEventBar(bar, days[bar.StartColumn]);
            Grid.SetRow(barView, bar.Lane + 1);
            Grid.SetColumn(barView, bar.StartColumn);
            Grid.SetColumnSpan(barView, bar.ColumnSpan);
            grid.Children.Add(barView);
        }

        // "+N more" overflow indicators per column (placed in last lane)
        if (hiddenCount > 0)
        {
            for (int c = 0; c < 7; c++)
            {
                if (hiddenByColumn[c] == 0) continue;
                var more = new Label
                {
                    Text = $"+{hiddenByColumn[c]} more",
                    FontSize = 9,
                    TextColor = _mutedTextColor,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    InputTransparent = true,
                };
                Grid.SetRow(more, MaxLanesPerWeek);
                Grid.SetColumn(more, c);
                grid.Children.Add(more);
            }
        }

        return grid;
    }

    private View BuildDayHeader(CalendarDay day)
    {
        var color = day.IsCurrentMonth ? _textColor : _mutedTextColor;
        var dayLabel = new Label
        {
            Text = day.DayNumber,
            FontSize = 12,
            FontAttributes = day.IsToday ? FontAttributes.Bold : FontAttributes.None,
            TextColor = day.IsToday ? Colors.White : color,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };

        View content;
        if (day.IsToday)
        {
            content = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 11 },
                StrokeThickness = 0,
                BackgroundColor = _primaryColor,
                WidthRequest = 22,
                HeightRequest = 22,
                HorizontalOptions = LayoutOptions.Center,
                Content = dayLabel,
            };
        }
        else
        {
            content = dayLabel;
        }

        var container = new ContentView
        {
            Padding = new Thickness(0, 2, 0, 2),
            Content = content,
        };

        if (day.IsCurrentMonth)
        {
            container.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _viewModel.SelectDayCommand,
                CommandParameter = day,
            });
        }

        return container;
    }

    private View BuildEventBar(WeekEventBar bar, CalendarDay day)
    {
        var color = GetEventColor(bar.Event);
        // Round only the leading/trailing edges if the event continues into adjacent weeks
        var topLeft = bar.ContinuesLeft ? 0 : 3;
        var bottomLeft = bar.ContinuesLeft ? 0 : 3;
        var topRight = bar.ContinuesRight ? 0 : 3;
        var bottomRight = bar.ContinuesRight ? 0 : 3;

        var border = new Border
        {
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(topLeft, topRight, bottomLeft, bottomRight)
            },
            StrokeThickness = 0,
            BackgroundColor = color,
            Padding = new Thickness(2.5, 0, 0, 0),
            Margin = new Thickness(bar.ContinuesLeft ? 0 : 0.5, 0, 0, 0),
            HeightRequest = 16,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = bar.Event.Title,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                LineBreakMode = LineBreakMode.NoWrap,
                VerticalTextAlignment = TextAlignment.Center,
            },
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = _viewModel.SelectDayCommand,
            CommandParameter = day,
        });
        return border;
    }

    private Color GetEventColor(CalendarEventResponse evt)
    {
        if (evt.JobId.HasValue) return _primaryColor;
        if (evt.CustomerId.HasValue) return _infoColor;
        return _warningColor;
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
