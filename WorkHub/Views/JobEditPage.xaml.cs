using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class JobEditPage : ContentPage
{
    private readonly JobEditViewModel _viewModel;

    public JobEditPage(JobEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(JobEditViewModel.IsCustomerPickerOpen) && _viewModel.IsCustomerPickerOpen)
            {
                // Small delay to let the UI render before focusing
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                {
                    CustomerSearchBar.Focus();
                });
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.IsNew && _viewModel.AllCustomers.Count == 0)
        {
            _viewModel.LoadDataCommand.Execute(null);
        }
    }
}
