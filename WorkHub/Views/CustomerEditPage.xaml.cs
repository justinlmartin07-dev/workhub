using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class CustomerEditPage : ContentPage
{
    private readonly CustomerEditViewModel _viewModel;

    public CustomerEditPage(CustomerEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.HasUnsavedChanges)
        {
            Dispatcher.Dispatch(async () =>
            {
                var discard = await DisplayAlert(
                    "Unsaved Changes", "You have unsaved changes. Discard them?", "Discard", "Stay");
                if (discard)
                    await Shell.Current.GoToAsync("..");
            });
            return true; // Prevent default back
        }
        return base.OnBackButtonPressed();
    }
}
