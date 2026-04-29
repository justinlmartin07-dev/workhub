using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class ProfilePage : ContentPage
{
    private const double WideThreshold = 720.0;

    private readonly ProfileViewModel _viewModel;
    private bool? _lastIsWide;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadProfileCommand.Execute(null);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        var isWide = width >= WideThreshold;
        if (_lastIsWide == isWide) return;
        _lastIsWide = isWide;
        ApplyResponsiveLayout(isWide);
    }

    private void ApplyResponsiveLayout(bool isWide)
    {
        FieldsGrid.ColumnDefinitions.Clear();
        FieldsGrid.RowDefinitions.Clear();
        ActionsGrid.ColumnDefinitions.Clear();
        ActionsGrid.RowDefinitions.Clear();

        if (isWide)
        {
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            FieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(NameField, 0); Grid.SetColumn(NameField, 0);
            Grid.SetRow(EmailField, 0); Grid.SetColumn(EmailField, 1);
            Grid.SetRow(MemberSinceField, 1); Grid.SetColumn(MemberSinceField, 0);

            ActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            ActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            ActionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(ChangePasswordButton, 0); Grid.SetColumn(ChangePasswordButton, 0);
            Grid.SetRow(LogoutButton, 0); Grid.SetColumn(LogoutButton, 1);
        }
        else
        {
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            FieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(NameField, 0); Grid.SetColumn(NameField, 0);
            Grid.SetRow(EmailField, 1); Grid.SetColumn(EmailField, 0);
            Grid.SetRow(MemberSinceField, 2); Grid.SetColumn(MemberSinceField, 0);

            ActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            ActionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ActionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(ChangePasswordButton, 0); Grid.SetColumn(ChangePasswordButton, 0);
            Grid.SetRow(LogoutButton, 1); Grid.SetColumn(LogoutButton, 0);
        }
    }
}
