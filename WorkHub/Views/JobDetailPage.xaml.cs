using WorkHub.Models;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class JobDetailPage : ContentPage
{
    public bool IsNarrowLayout { get; }

    public JobDetailPage(JobDetailViewModel viewModel)
    {
        IsNarrowLayout = !(MainLayout.Current?.IsWideLayout ?? false);
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.NoteEditRequested += OnNoteEditRequested;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        // Keep the slide-out parts panel from overflowing narrow phones: cap at 360,
        // but leave a 32px gutter so the scrim is always tappable to dismiss.
        if (width > 0)
            PartsPanel.WidthRequest = Math.Min(360, width - 32);
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is JobDetailViewModel vm && vm.IsPartsPanelOpen)
        {
            vm.ClosePartsPanelCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }

    private void OnNoteEditRequested(object? sender, EventArgs e)
    {
        NoteEditor.Focus();
        NoteEditor.CursorPosition = NoteEditor.Text?.Length ?? 0;
    }

    private void OnQuantityEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not JobItemResponse item) return;
        if (BindingContext is not JobDetailViewModel vm) return;

        if (int.TryParse(entry.Text, out var qty) && qty >= 1 && qty != item.Quantity)
        {
            vm.UpdateQuantityCommand.Execute(new QuantityUpdateRequest(item, qty));
        }
        else
        {
            // Reset to current value
            entry.Text = item.Quantity.ToString();
        }
    }

    private void OnIncrementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Parent is not Grid grid) return;
        if (grid.BindingContext is not JobItemResponse item) return;
        if (BindingContext is not JobDetailViewModel vm) return;

        var entry = grid.Children.OfType<Entry>().FirstOrDefault();
        if (entry == null) return;

        var newQty = item.Quantity + 1;
        item.Quantity = newQty;
        entry.Text = newQty.ToString();
        vm.SaveQuantityInBackground(item, newQty);
    }

    private void OnDecrementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Parent is not Grid grid) return;
        if (grid.BindingContext is not JobItemResponse item) return;
        if (BindingContext is not JobDetailViewModel vm) return;
        if (item.Quantity <= 1) return;

        var entry = grid.Children.OfType<Entry>().FirstOrDefault();
        if (entry == null) return;

        var newQty = item.Quantity - 1;
        item.Quantity = newQty;
        entry.Text = newQty.ToString();
        vm.SaveQuantityInBackground(item, newQty);
    }
}
