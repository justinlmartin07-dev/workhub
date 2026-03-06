using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class MainLayout : ContentPage
{
    private readonly MainLayoutViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private int _lastTabIndex = -1;
    private bool _isWide;

    public static MainLayout? Current { get; private set; }

    public MainLayout(MainLayoutViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        BindingContext = viewModel;

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
        }
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
            ListPanel.Margin = new Thickness(72, 0, 0, 0);

            ContentGrid.ColumnDefinitions.Clear();
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));

            Grid.SetColumn(ListPanel, 0);
            Grid.SetColumn(DetailPanel, 1);
            Grid.SetColumn(NavRail, 0);
        }
        else
        {
            // Narrow: list only, bottom tabs
            NavRail.IsVisible = false;
            DetailPanel.IsVisible = false;
            BottomTabs.IsVisible = true;
            ListPanel.Margin = new Thickness(0);

            ContentGrid.ColumnDefinitions.Clear();
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            Grid.SetColumn(ListPanel, 0);
        }
    }

    public bool IsWideLayout => _isWide;

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
        _lastTabIndex = tabIndex;

        ResetDetailPanel();

        View listContent = tabIndex switch
        {
            0 => _serviceProvider.GetRequiredService<CustomersListPage>(),
            1 => _serviceProvider.GetRequiredService<JobsListPage>(),
            2 => _serviceProvider.GetRequiredService<InventoryPage>(),
            3 => _serviceProvider.GetRequiredService<CalendarPage>(),
            _ => new Label { Text = "Unknown tab" }
        };

        ListPanel.Content = listContent;
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

    private async void HandleDetailRequest(DetailRequest request)
    {
        if (request.SwitchTabIndex.HasValue)
        {
            _viewModel.SelectedTabIndex = request.SwitchTabIndex.Value;
            _lastTabIndex = -1; // Force reload
            LoadTabContent(request.SwitchTabIndex.Value);
        }

        if (_isWide)
        {
            View? detailView = request.Route switch
            {
                "customerDetail" => CreateDetailView<CustomerDetailPage, CustomerDetailViewModel>(request),
                "jobDetail" => CreateDetailView<JobDetailPage, JobDetailViewModel>(request),
                "inventoryDetail" => CreateDetailView<InventoryItemDetailPage, InventoryItemDetailViewModel>(request),
                "eventDetail" => CreateDetailView<EventDetailPage, EventDetailViewModel>(request),
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

    private static async Task NavigateViaShell(DetailRequest request)
    {
        var query = string.Join("&", request.QueryParams.Select(p => $"{p.Key}={p.Value}"));
        var route = string.IsNullOrEmpty(query) ? request.Route : $"{request.Route}?{query}";
        await Shell.Current.GoToAsync(route);
    }
}
