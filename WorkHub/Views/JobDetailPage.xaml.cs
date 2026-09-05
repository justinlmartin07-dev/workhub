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
        viewModel.Reloading += OnViewModelReloading;
    }

    private void OnViewModelReloading(object? sender, EventArgs e) => CommitPendingQuantityEdit();

    protected override void OnDisappearing()
    {
        // Narrow layout: back-navigation away from the page doesn't unfocus the Entry either.
        CommitPendingQuantityEdit();
        base.OnDisappearing();
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

    // The TwoWay Text binding writes typed values into the model as the user types,
    // so the pre-edit value is snapshotted on focus to detect changes on unfocus.
    // The entry itself is tracked because an edit can end without an Unfocused
    // event (job switch on the reused view, back-navigation) and must be
    // committed from CommitPendingQuantityEdit instead.
    private Entry? _focusedQuantityEntry;
    private decimal _quantityBeforeEdit;

    private void OnQuantityEntryFocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is JobItemResponse item)
        {
            _focusedQuantityEntry = entry;
            _quantityBeforeEdit = item.Quantity;
        }
    }

    private void OnQuantityEntryUnfocused(object? sender, FocusEventArgs e)
    {
        _focusedQuantityEntry = null;
        if (sender is Entry entry)
            CommitQuantityEdit(entry);
    }

    private void CommitPendingQuantityEdit()
    {
        if (_focusedQuantityEntry == null) return;
        var entry = _focusedQuantityEntry;
        _focusedQuantityEntry = null;
        CommitQuantityEdit(entry);
    }

    private void CommitQuantityEdit(Entry entry)
    {
        if (entry.BindingContext is not JobItemResponse item) return;
        if (BindingContext is not JobDetailViewModel vm) return;

        // Fractional quantities (e.g. 1.56) are allowed; the DB stores 2 decimals.
        if (decimal.TryParse(entry.Text, out var qty) && qty > 0)
        {
            qty = Math.Round(qty, 2);
            if (qty == _quantityBeforeEdit) return;
            _quantityBeforeEdit = qty;
            item.Quantity = qty;
            vm.SaveQuantityInBackground(item, qty);
        }
        else
        {
            item.Quantity = _quantityBeforeEdit;
            // Invalid text never reached the model, so the setter above may no-op
            // without raising PropertyChanged; reset the text explicitly.
            entry.Text = _quantityBeforeEdit.ToString();
        }
    }

    private void OnIncrementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        if (button.BindingContext is not JobItemResponse item) return;
        if (BindingContext is not JobDetailViewModel vm) return;

        item.Quantity++;
        vm.SaveQuantityInBackground(item, item.Quantity);
    }

    private void OnDecrementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        if (button.BindingContext is not JobItemResponse item) return;
        if (BindingContext is not JobDetailViewModel vm) return;
        if (item.Quantity <= 1) return;

        // Step down a whole unit, but never below 1 (2.56 → 1.56 → 1).
        item.Quantity = Math.Max(1, item.Quantity - 1);
        vm.SaveQuantityInBackground(item, item.Quantity);
    }
}
