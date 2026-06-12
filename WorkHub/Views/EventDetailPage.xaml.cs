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
            else if (e.PropertyName == nameof(EventDetailViewModel.IsCustomerPickerOpen)
                     && _viewModel.IsCustomerPickerOpen)
            {
                // Wait for the picker to render before focusing: focusing the
                // not-yet-visible SearchBar fails on Windows, and WinUI then
                // focuses the first control on the page (the title) and scrolls
                // back up to it.
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () => CustomerSearchBar.Focus());
            }
        };

        RebuildChips();

#if WINDOWS
        // When the customer picker opens, WinUI grabs focus for the first control
        // (the title) before our delayed focus reaches the search box, flashing
        // the title and scrolling up to it. Disable focus-driven auto-scroll and
        // deflect that programmatic grab straight to the search box.
        PageScroll.HandlerChanged += (s, e) =>
        {
            if (PageScroll.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ScrollViewer sv)
                return;
            sv.BringIntoViewOnFocusChange = false;
            sv.GettingFocus += (sender, args) =>
            {
                // While the picker is open, no non-pointer focus may land on any
                // text box except the picker's search box: redirect when the search
                // box exists, cancel when it is still being created (first open) —
                // the delayed focus below lands on it once it's ready.
                if (!_viewModel.IsCustomerPickerOpen
                    || args.FocusState == Microsoft.UI.Xaml.FocusState.Pointer
                    || args.NewFocusedElement is not Microsoft.UI.Xaml.Controls.TextBox tb)
                    return;

                var search = CustomerSearchBar.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.AutoSuggestBox;
                if (search != null && IsWithin(tb, search))
                    return;
                if (search == null || !args.TrySetNewFocusedElement(search))
                    args.TryCancel();
            };
        };
#endif
    }

#if WINDOWS
    private static bool IsWithin(Microsoft.UI.Xaml.DependencyObject? element, Microsoft.UI.Xaml.DependencyObject root)
    {
        while (element != null)
        {
            if (element == root) return true;
            element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }
#endif

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
