using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrderDetailViewModel _viewModel;

    public bool IsNarrowLayout { get; }

    public OrderDetailPage(OrderDetailViewModel viewModel)
    {
        IsNarrowLayout = !(MainLayout.Current?.IsWideLayout ?? false);
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // CheckBox binds IsChecked OneWay, so this fires on user taps and on data
    // load; SetOrderedAsync no-ops when the value already matches.
    private void OnOrderedChanged(object? sender, CheckedChangedEventArgs e)
    {
        _ = _viewModel.SetOrderedAsync(e.Value);
    }
}
