using WorkHub.Models;
using WorkHub.ViewModels;

namespace WorkHub.Views;

public partial class JobDetailPage : ContentPage
{
    public JobDetailPage(JobDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
