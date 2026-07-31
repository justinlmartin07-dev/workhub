using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WorkHub.Messages;
using WorkHub.Models;
using WorkHub.Services;

namespace WorkHub.ViewModels;

[QueryProperty(nameof(ItemId), "id")]
public partial class InventoryItemDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string? _itemId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _partNumber = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _sku = string.Empty;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _pageTitle = "New Item";

    public const string NoCategoryOption = "No Category";
    public const string AddNewCategoryOption = "+ New Category…";

    public ObservableCollection<string> CategoryOptions { get; } = new() { NoCategoryOption, AddNewCategoryOption };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string? _selectedCategory = NoCategoryOption;

    // The real category value behind the picker sentinels ("" = none).
    private string CategoryValue =>
        SelectedCategory is null || SelectedCategory == NoCategoryOption || SelectedCategory == AddNewCategoryOption
            ? string.Empty
            : SelectedCategory;

    private string _originalName = string.Empty;
    private string _originalDescription = string.Empty;
    private string _originalPartNumber = string.Empty;
    private string _originalSku = string.Empty;
    private string _originalCategory = string.Empty;

    public bool HasChanges =>
        !string.Equals(Name ?? string.Empty, _originalName, StringComparison.Ordinal)
        || !string.Equals(Description ?? string.Empty, _originalDescription, StringComparison.Ordinal)
        || !string.Equals(PartNumber ?? string.Empty, _originalPartNumber, StringComparison.Ordinal)
        || !string.Equals(Sku ?? string.Empty, _originalSku, StringComparison.Ordinal)
        || !string.Equals(CategoryValue, _originalCategory, StringComparison.Ordinal);

    public InventoryItemDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _ = LoadCategoryOptionsAsync();
    }

    private async Task LoadCategoryOptionsAsync()
    {
        try
        {
            var categories = await _apiService.GetInventoryCategoriesAsync();
            foreach (var category in categories)
                EnsureCategoryOption(category);
        }
        catch
        {
            // Options stay minimal; the picker still works for the current value.
        }
    }

    /// <summary>Inserts a category into the picker options (sorted, before the "+ New" entry) if missing.</summary>
    private string EnsureCategoryOption(string category)
    {
        var existing = CategoryOptions.FirstOrDefault(o =>
            o != NoCategoryOption && o != AddNewCategoryOption
            && string.Equals(o, category, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        int insertAt = 1;
        while (insertAt < CategoryOptions.Count - 1
               && string.Compare(CategoryOptions[insertAt], category, StringComparison.OrdinalIgnoreCase) < 0)
            insertAt++;
        CategoryOptions.Insert(insertAt, category);
        return category;
    }

    partial void OnSelectedCategoryChanged(string? oldValue, string? newValue)
    {
        if (newValue == AddNewCategoryOption)
            _ = PromptNewCategoryAsync(oldValue ?? NoCategoryOption);
    }

    private async Task PromptNewCategoryAsync(string previous)
    {
        var name = await Shell.Current.DisplayPromptAsync("New Category", "Category name:", maxLength: 100);
        name = name?.Trim();
        SelectedCategory = string.IsNullOrEmpty(name) ? previous : EnsureCategoryOption(name);
    }

    partial void OnItemIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _))
        {
            IsNew = false;
            PageTitle = "Item Details";
            LoadItemCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadItemAsync()
    {
        if (!Guid.TryParse(ItemId, out var id)) return;
        await LoadAsync(async () =>
        {
            var item = await _apiService.GetInventoryItemAsync(id);
            if (item != null)
            {
                Name = item.Name;
                Description = item.Description ?? string.Empty;
                PartNumber = item.PartNumber ?? string.Empty;
                Sku = item.Sku ?? string.Empty;
                SelectedCategory = string.IsNullOrEmpty(item.Category)
                    ? NoCategoryOption
                    : EnsureCategoryOption(item.Category);
                SnapshotOriginal();
            }
        });
    }

    private void SnapshotOriginal()
    {
        _originalName = Name ?? string.Empty;
        _originalDescription = Description ?? string.Empty;
        _originalPartNumber = PartNumber ?? string.Empty;
        _originalSku = Sku ?? string.Empty;
        _originalCategory = CategoryValue;
        OnPropertyChanged(nameof(HasChanges));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required";
            HasError = true;
            return;
        }

        await LoadAsync(async () =>
        {
            if (IsNew)
            {
                var request = new CreateInventoryItemRequest
                {
                    Name = Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    PartNumber = string.IsNullOrWhiteSpace(PartNumber) ? null : PartNumber.Trim(),
                    Sku = string.IsNullOrWhiteSpace(Sku) ? null : Sku.Trim(),
                    Category = CategoryValue.Length == 0 ? null : CategoryValue
                };
                await _apiService.CreateInventoryItemAsync(request);
                WeakReferenceMessenger.Default.Send(new DataChangedMessage("inventory"));
                NavigateBack();
            }
            else
            {
                // For optional fields, empty string clears the value server-side;
                // null would mean "unchanged" and a cleared field would survive the save.
                var request = new UpdateInventoryItemRequest
                {
                    Name = Name.Trim(),
                    Description = Description.Trim(),
                    PartNumber = PartNumber.Trim(),
                    Sku = Sku.Trim(),
                    Category = CategoryValue
                };
                await _apiService.UpdateInventoryItemAsync(Guid.Parse(ItemId!), request);
                WeakReferenceMessenger.Default.Send(new DataChangedMessage("inventory"));
                SnapshotOriginal();
            }
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Item", $"Delete {Name}?", "Delete", "Cancel");
        if (!confirm) return;
        try
        {
            var response = await _apiService.DeleteInventoryItemAsync(Guid.Parse(ItemId!));
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonObject>();
                var jobs = body?["details"]?["referencingJobs"]?.AsArray();
                var jobNames = jobs != null
                    ? string.Join(", ", jobs.Select(j => j?["title"]?.GetValue<string>() ?? "Unknown"))
                    : "unknown jobs";
                await Shell.Current.DisplayAlert("Cannot Delete", $"This item is referenced by: {jobNames}", "OK");
                return;
            }
            response.EnsureSuccessStatusCode();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage("inventory"));
            NavigateBack();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private void Cancel() => NavigateBack();

    private async void NavigateBack()
    {
        if (Views.MainLayout.Current?.IsWideLayout == true)
            Views.MainLayout.Current.ClearDetail();
        else
            await Shell.Current.GoToAsync("..");
    }
}
