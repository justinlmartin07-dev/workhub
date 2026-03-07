using Microsoft.Maui.Controls.Shapes;
using WorkHub.Models;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class EventDetailPage : ContentPage
{
    private readonly EventDetailViewModel _viewModel;
    private Entry? _searchEntry;

    public EventDetailPage(EventDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.AssignedUsers.CollectionChanged += (s, e) => RebuildChips();
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EventDetailViewModel.AssignedUsers))
            {
                _viewModel.AssignedUsers.CollectionChanged += (s2, e2) => RebuildChips();
                RebuildChips();
            }
        };

        RebuildChips();
    }

    private void RebuildChips()
    {
        var currentText = _searchEntry?.Text ?? string.Empty;
        ChipContainer.Children.Clear();

        var primaryColor = Application.Current!.Resources["Primary"] as Color ?? Colors.Blue;

        foreach (var user in _viewModel.AssignedUsers)
        {
            var chip = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                StrokeThickness = 0,
                BackgroundColor = primaryColor,
                Padding = new Thickness(10, 4, 6, 4),
                Margin = new Thickness(0, 2, 4, 2),
            };

            var chipContent = new HorizontalStackLayout { Spacing = 4 };
            chipContent.Children.Add(new Label
            {
                Text = user.Name,
                FontSize = 13,
                TextColor = Colors.White,
                VerticalTextAlignment = TextAlignment.Center,
            });

            var removeBtn = new Label
            {
                Text = "\u2715",
                FontSize = 12,
                TextColor = Colors.White,
                VerticalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(2, 0),
            };
            var removeTap = new TapGestureRecognizer();
            var capturedUser = user;
            removeTap.Tapped += (s, e) => _viewModel.RemoveAssignmentCommand.Execute(capturedUser);
            removeBtn.GestureRecognizers.Add(removeTap);
            chipContent.Children.Add(removeBtn);

            chip.Content = chipContent;
            ChipContainer.Children.Add(chip);
        }

        _searchEntry = new Entry
        {
            Placeholder = "Type a name...",
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            MinimumWidthRequest = 120,
            Text = currentText,
        };
        _searchEntry.SetBinding(Entry.TextProperty, new Binding(nameof(EventDetailViewModel.UserSearchText),
            source: _viewModel, mode: BindingMode.TwoWay));
        FlexLayout.SetGrow(_searchEntry, 1);
        ChipContainer.Children.Add(_searchEntry);
    }
}
