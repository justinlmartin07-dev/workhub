using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class MainLayout : ContentPage
{
    private const double SplitterWidth = 8.0;
    private const double MinListColumnWidth = 240.0;
    private const double MinDetailColumnWidth = 280.0;
    private const double DefaultListColumnWidth = 400.0;
    private const string ListColumnWidthKey = "MainLayout.ListColumnWidth";

    private readonly MainLayoutViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private int _lastTabIndex = -1;
    private bool _isWide;
    private double _listColumnWidth;
    private double _splitterDragStartWidth;

    // Tab content is created once and reused so each page keeps its state
    // (list data, search text, selection, scroll) across tab switches.
    private readonly Dictionary<int, View> _tabPages = new();
    // Each tab's detail pane is parked here on switch and restored on return,
    // so in-progress work (including unsaved edits) survives tab changes.
    private readonly Dictionary<int, View?> _tabDetails = new();
    // The heavy detail views (job/customer) are built once and reused — XAML
    // inflation is the dominant cost of opening a detail once data is cached.
    // Their VMs reset themselves when the entity id changes (IReusableDetail).
    private readonly Dictionary<string, View> _detailViewCache = new();

    public static MainLayout? Current { get; private set; }

    public MainLayout(MainLayoutViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        BindingContext = viewModel;

        _listColumnWidth = Preferences.Get(ListColumnWidthKey, DefaultListColumnWidth);

#if WINDOWS
        Splitter.HandlerChanged += OnSplitterHandlerChanged;
#endif

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        WeakReferenceMessenger.Default.Register<ShowDetailMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() => HandleDetailRequest(m.Value));
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Current = this;
        if (_lastTabIndex == -1)
        {
            _viewModel.SelectedTabIndex = 0;
            LoadTabContent(0);
            PrewarmDetailViews();
        }
    }

    private bool _prewarmStarted;

    // Inflate the heavy detail views once, shortly after launch, so the first
    // click on a job/customer doesn't pay the XAML-construction cost either.
    private void PrewarmDetailViews()
    {
        if (_prewarmStarted) return;
        _prewarmStarted = true;

        Dispatcher.Dispatch(async () =>
        {
            // Let the first list render before taking UI-thread time.
            await Task.Delay(750);
            if (!_isWide) return; // narrow mode pushes detail pages via Shell

            if (!_detailViewCache.ContainsKey("jobDetail"))
                GetOrCreateCachedDetailView<JobDetailPage, JobDetailViewModel>(
                    "jobDetail", new DetailRequest { Route = "jobDetail" });

            await Task.Delay(250);

            if (!_detailViewCache.ContainsKey("customerDetail"))
                GetOrCreateCachedDetailView<CustomerDetailPage, CustomerDetailViewModel>(
                    "customerDetail", new DetailRequest { Route = "customerDetail" });
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Current == this) Current = null;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        var wasWide = _isWide;
        _isWide = width >= 720;

        if (_isWide != wasWide)
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_isWide)
        {
            // Wide: nav rail (72) + list (1*) + detail (2*)
            NavRail.IsVisible = true;
            DetailPanel.IsVisible = true;
            BottomTabs.IsVisible = false;
            ListPanel.Margin = new Thickness(80, 0, 0, 0);

            UpdateColumnProportions();
            Grid.SetColumn(NavRail, 0);
        }
        else
        {
            // Narrow: list only, bottom tabs
            NavRail.IsVisible = false;
            DetailPanel.IsVisible = false;
            Splitter.IsVisible = false;
            BottomTabs.IsVisible = true;
            ListPanel.Margin = new Thickness(0);

            ContentGrid.ColumnDefinitions.Clear();
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            Grid.SetColumn(ListPanel, 0);
        }
    }

    private void UpdateColumnProportions()
    {
        bool isCalendar = _viewModel.SelectedTabIndex == 3;
        ContentGrid.ColumnDefinitions.Clear();

        if (isCalendar)
        {
            // Calendar: fixed 2:1 ratio, no splitter (zero-width middle column keeps DetailPanel at col 2)
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0, GridUnitType.Absolute)));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            Splitter.IsVisible = false;
        }
        else
        {
            // Customers / Jobs / Inventory: resizable list column, draggable splitter, star detail
            var clampedWidth = ClampListColumnWidth(_listColumnWidth);
            _listColumnWidth = clampedWidth;
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(clampedWidth, GridUnitType.Absolute)));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(SplitterWidth, GridUnitType.Absolute)));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            Splitter.IsVisible = true;
        }

        Grid.SetColumn(ListPanel, 0);
        Grid.SetColumn(Splitter, 1);
        Grid.SetColumn(DetailPanel, 2);
    }

    private double ClampListColumnWidth(double width)
    {
        var available = ContentGrid.Width;
        var maxList = double.IsNaN(available) || available <= 0
            ? double.PositiveInfinity
            : Math.Max(MinListColumnWidth, available - MinDetailColumnWidth - SplitterWidth);
        return Math.Clamp(width, MinListColumnWidth, maxList);
    }

    private void OnSplitterPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
#if WINDOWS
        // Windows uses native PointerMoved events for smoother drag — bypass MAUI gesture pipeline
        return;
#else
        if (!_isWide || ContentGrid.ColumnDefinitions.Count < 3) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _splitterDragStartWidth = _listColumnWidth;
                break;
            case GestureStatus.Running:
                var newWidth = ClampListColumnWidth(_splitterDragStartWidth + e.TotalX);
                _listColumnWidth = newWidth;
                ContentGrid.ColumnDefinitions[0].Width = new GridLength(newWidth, GridUnitType.Absolute);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                Preferences.Set(ListColumnWidthKey, _listColumnWidth);
                break;
        }
#endif
    }

#if WINDOWS
    private bool _isDraggingSplitter;
    private double _splitterPointerStartX;
    private Microsoft.UI.Xaml.FrameworkElement? _splitterNative;

    private void OnSplitterHandlerChanged(object? sender, EventArgs e)
    {
        if (_splitterNative is not null)
        {
            _splitterNative.PointerPressed -= OnSplitterPointerPressed;
            _splitterNative.PointerMoved -= OnSplitterPointerMoved;
            _splitterNative.PointerReleased -= OnSplitterPointerReleased;
            _splitterNative.PointerCaptureLost -= OnSplitterPointerCaptureLost;
            _splitterNative = null;
        }
        if (Splitter.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
        {
            _splitterNative = fe;
            fe.PointerPressed += OnSplitterPointerPressed;
            fe.PointerMoved += OnSplitterPointerMoved;
            fe.PointerReleased += OnSplitterPointerReleased;
            fe.PointerCaptureLost += OnSplitterPointerCaptureLost;
        }
    }

    private void OnSplitterPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.UIElement ue
            && ContentGrid.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement gridUe)
        {
            ue.CapturePointer(e.Pointer);
            _isDraggingSplitter = true;
            _splitterDragStartWidth = _listColumnWidth;
            _splitterPointerStartX = e.GetCurrentPoint(gridUe).Position.X;
            e.Handled = true;
        }
    }

    private void OnSplitterPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter) return;
        if (ContentGrid.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement gridUe)
        {
            var currentX = e.GetCurrentPoint(gridUe).Position.X;
            var deltaX = currentX - _splitterPointerStartX;
            var newWidth = ClampListColumnWidth(_splitterDragStartWidth + deltaX);
            if (Math.Abs(newWidth - _listColumnWidth) >= 0.5
                && _isWide
                && ContentGrid.ColumnDefinitions.Count >= 3)
            {
                _listColumnWidth = newWidth;
                ContentGrid.ColumnDefinitions[0].Width = new GridLength(newWidth, GridUnitType.Absolute);
            }
            e.Handled = true;
        }
    }

    private void OnSplitterPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter) return;
        _isDraggingSplitter = false;
        if (sender is Microsoft.UI.Xaml.UIElement ue)
        {
            ue.ReleasePointerCapture(e.Pointer);
        }
        Preferences.Set(ListColumnWidthKey, _listColumnWidth);
        e.Handled = true;
    }

    private void OnSplitterPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDraggingSplitter)
        {
            _isDraggingSplitter = false;
            Preferences.Set(ListColumnWidthKey, _listColumnWidth);
        }
    }
#endif

    public bool IsWideLayout => _isWide;

    public void ClearDetail()
    {
        DetailPanel.Content = null;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainLayoutViewModel.SelectedTabIndex))
        {
            LoadTabContent(_viewModel.SelectedTabIndex);
        }
    }

    private void LoadTabContent(int tabIndex)
    {
        if (tabIndex == _lastTabIndex) return;

        // Park the outgoing tab's detail pane so whatever was open (including an
        // unsaved edit) is restored when the user comes back to that tab.
        if (_lastTabIndex >= 0)
            _tabDetails[_lastTabIndex] = DetailPanel.Content;

        _lastTabIndex = tabIndex;

        if (_tabDetails.TryGetValue(tabIndex, out var detail) && detail != null)
            DetailPanel.Content = detail;
        else
            ResetDetailPanel();

        if (!_tabPages.TryGetValue(tabIndex, out var listContent))
        {
            listContent = tabIndex switch
            {
                0 => _serviceProvider.GetRequiredService<CustomersListPage>(),
                1 => _serviceProvider.GetRequiredService<JobsListPage>(),
                2 => _serviceProvider.GetRequiredService<InventoryPage>(),
                3 => _serviceProvider.GetRequiredService<CalendarPage>(),
                4 => _serviceProvider.GetRequiredService<OrdersPage>(),
                _ => new Label { Text = "Unknown tab" }
            };
            _tabPages[tabIndex] = listContent;
        }

        ListPanel.Content = listContent;
        if (_isWide) UpdateColumnProportions();
    }

    private void ResetDetailPanel()
    {
        DetailPanel.Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Select an item to view details",
                    TextColor = Colors.Gray,
                    FontSize = 16,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
    }

    private async Task<bool> CheckUnsavedChangesAsync()
    {
        if (DetailPanel.Content?.BindingContext is IHasUnsavedChanges { HasUnsavedChanges: true } vm)
        {
            var stay = !await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Unsaved Changes", "You have unsaved changes. Discard them?", "Discard", "Stay");
            if (stay)
            {
                // Revert list selection back to the item being edited
                var editId = vm switch
                {
                    CustomerEditViewModel c => c.CustomerId,
                    JobEditViewModel j => j.JobId,
                    _ => null
                };
                WeakReferenceMessenger.Default.Send(new SelectListItemMessage(
                    new SelectListItemRequest { ItemId = editId ?? "", TabIndex = _viewModel.SelectedTabIndex }));
            }
            return stay;
        }
        return false;
    }

    private async void HandleDetailRequest(DetailRequest request)
    {
        var switchingTab = request.SwitchTabIndex.HasValue && _isWide
            && request.SwitchTabIndex.Value != _viewModel.SelectedTabIndex;

        // A same-tab request replaces the visible detail — guard unsaved edits.
        // A tab-switch request parks the current detail instead (nothing is lost),
        // so the guard runs after the switch against the target tab's parked detail.
        if (_isWide && !switchingTab && await CheckUnsavedChangesAsync())
            return;

        // Only switch tabs in wide mode — in narrow mode the detail page is pushed
        // via Shell, and switching tabs would leave MainLayout on the wrong tab
        // when the user navigates back.
        if (request.SwitchTabIndex.HasValue && _isWide)
        {
            if (switchingTab)
            {
                _viewModel.SelectedTabIndex = request.SwitchTabIndex.Value;
                LoadTabContent(request.SwitchTabIndex.Value);

                if (await CheckUnsavedChangesAsync())
                    return;
            }

            // Tell the list to select/scroll to the item
            if (request.QueryParams.TryGetValue("id", out var id))
                WeakReferenceMessenger.Default.Send(new SelectListItemMessage(
                    new SelectListItemRequest { ItemId = id, TabIndex = request.SwitchTabIndex.Value }));
        }

        if (_isWide)
        {
            View? detailView = request.Route switch
            {
                "customerDetail" => GetOrCreateCachedDetailView<CustomerDetailPage, CustomerDetailViewModel>("customerDetail", request),
                "customerEdit" => CreateDetailView<CustomerEditPage, CustomerEditViewModel>(request),
                "jobDetail" => GetOrCreateCachedDetailView<JobDetailPage, JobDetailViewModel>("jobDetail", request),
                "jobEdit" => CreateDetailView<JobEditPage, JobEditViewModel>(request),
                "inventoryDetail" => CreateDetailView<InventoryItemDetailPage, InventoryItemDetailViewModel>(request),
                "orderDetail" => CreateDetailView<OrderDetailPage, OrderDetailViewModel>(request),
                "eventDetail" => CreateDetailView<EventDetailPage, EventDetailViewModel>(request),
                "daySummary" => CreateDetailView<CalendarDaySummaryPage, CalendarDaySummaryViewModel>(request),
                _ => null
            };

            if (detailView != null)
            {
                DetailPanel.Content = detailView;
            }
            else
            {
                await NavigateViaShell(request);
            }
        }
        else
        {
            await NavigateViaShell(request);
        }
    }

    // Reuse the cached view for this route: update the VM's properties (the
    // entity-id setter resets and reloads), or ask it to refresh when the same
    // item is shown again. Falls back to creating (and caching) the view.
    private View? GetOrCreateCachedDetailView<TPage, TViewModel>(string route, DetailRequest request)
        where TPage : ContentPage
        where TViewModel : class
    {
        if (_detailViewCache.TryGetValue(route, out var view))
        {
            if (view.BindingContext is TViewModel vm)
            {
                var changed = false;
                foreach (var param in request.Properties)
                {
                    var prop = vm.GetType().GetProperty(param.Key);
                    if (prop == null) continue;
                    if (!Equals(prop.GetValue(vm), param.Value))
                    {
                        prop.SetValue(vm, param.Value);
                        changed = true;
                    }
                }
                if (!changed && vm is ViewModels.IReusableDetail reusable)
                    reusable.RefreshOnReuse();
            }
            return view;
        }

        var created = CreateDetailView<TPage, TViewModel>(request);
        if (created != null)
            _detailViewCache[route] = created;
        return created;
    }

    private View? CreateDetailView<TPage, TViewModel>(DetailRequest request)
        where TPage : ContentPage
        where TViewModel : class
    {
        var page = _serviceProvider.GetRequiredService<TPage>();

        if (page.BindingContext is TViewModel vm)
        {
            foreach (var param in request.Properties)
            {
                var prop = vm.GetType().GetProperty(param.Key);
                prop?.SetValue(vm, param.Value);
            }
        }

        var content = page.Content;
        if (content != null)
        {
            content.BindingContext = page.BindingContext;
            page.Content = null;
            return content;
        }
        return null;
    }

    private static bool _shellNavInFlight;
    private static string? _lastShellRoute;
    private static DateTime _lastShellNavAt;

    private static async Task NavigateViaShell(DetailRequest request)
    {
        var query = string.Join("&", request.QueryParams.Select(p => $"{p.Key}={p.Value}"));
        var route = string.IsNullOrEmpty(query) ? request.Route : $"{request.Route}?{query}";

        // Duplicate pushes stack an identical page under the visible one, which
        // then takes two back presses to escape. Drop a repeat of the same route
        // while one navigation is animating or within a short window after it.
        if (_shellNavInFlight) return;
        if (route == _lastShellRoute && (DateTime.UtcNow - _lastShellNavAt) < TimeSpan.FromMilliseconds(1500))
            return;

        _shellNavInFlight = true;
        try
        {
            _lastShellRoute = route;
            _lastShellNavAt = DateTime.UtcNow;
            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            _shellNavInFlight = false;
        }
    }
}
