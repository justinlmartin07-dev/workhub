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
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _cost = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _markup = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _price = string.Empty;

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
    private string _originalCost = string.Empty;
    private string _originalMarkup = string.Empty;
    private string _originalPrice = string.Empty;

    public bool HasChanges =>
        !string.Equals(Name ?? string.Empty, _originalName, StringComparison.Ordinal)
        || !string.Equals(Description ?? string.Empty, _originalDescription, StringComparison.Ordinal)
        || !string.Equals(PartNumber ?? string.Empty, _originalPartNumber, StringComparison.Ordinal)
        || !string.Equals(Sku ?? string.Empty, _originalSku, StringComparison.Ordinal)
        || !string.Equals(CategoryValue, _originalCategory, StringComparison.Ordinal)
        || !string.Equals(Cost ?? string.Empty, _originalCost, StringComparison.Ordinal)
        || !string.Equals(Markup ?? string.Empty, _originalMarkup, StringComparison.Ordinal)
        || !string.Equals(Price ?? string.Empty, _originalPrice, StringComparison.Ordinal);

    public InventoryItemDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _ = LoadCategoryOptionsAsync();
        _ = LoadMarkupOptionsAsync();
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

    // Previously used markup percentages (sorted), shown by the markup dropdown button.
    private readonly List<decimal> _markupOptions = new();

    // Guards against Cost/Markup/Price recalculating each other in a loop.
    private bool _syncingPricing;

    private async Task LoadMarkupOptionsAsync()
    {
        try
        {
            var markups = await _apiService.GetInventoryMarkupsAsync();
            foreach (var markup in markups)
                EnsureMarkupOption(markup);
        }
        catch
        {
            // Dropdown stays empty; typing a markup still works.
        }
    }

    private void EnsureMarkupOption(decimal markup)
    {
        if (_markupOptions.Contains(markup)) return;
        int insertAt = 0;
        while (insertAt < _markupOptions.Count && _markupOptions[insertAt] < markup)
            insertAt++;
        _markupOptions.Insert(insertAt, markup);
    }

    public const string NoMarkupsPlaceholder = "No saved markups yet";

    // Inline dropdown under the markup entry: typing filters previously used
    // percentages by prefix; the ▾ button shows the full list.
    public ObservableCollection<string> MarkupSuggestions { get; } = new();

    [ObservableProperty]
    private bool _isMarkupSuggestionsVisible;

    private bool _suppressMarkupSuggestions;
    private DateTime _markupDropdownOpenedAt;

    private void UpdateMarkupSuggestions(string? text)
    {
        var typed = text?.Trim().TrimEnd('%');
        MarkupSuggestions.Clear();
        if (!string.IsNullOrEmpty(typed))
        {
            foreach (var option in _markupOptions)
            {
                var formatted = FormatMarkup(option);
                if (formatted.StartsWith(typed, StringComparison.Ordinal) && formatted != typed)
                    MarkupSuggestions.Add($"{formatted}%");
            }
        }
        IsMarkupSuggestionsVisible = MarkupSuggestions.Count > 0;
    }

    [RelayCommand]
    private void ToggleMarkupSuggestions()
    {
        if (IsMarkupSuggestionsVisible)
        {
            IsMarkupSuggestionsVisible = false;
            return;
        }
        MarkupSuggestions.Clear();
        foreach (var option in _markupOptions)
            MarkupSuggestions.Add($"{FormatMarkup(option)}%");
        if (MarkupSuggestions.Count == 0)
            MarkupSuggestions.Add(NoMarkupsPlaceholder);
        _markupDropdownOpenedAt = DateTime.UtcNow;
        IsMarkupSuggestionsVisible = true;
    }

    [RelayCommand]
    private void SelectMarkup(string suggestion)
    {
        if (suggestion != NoMarkupsPlaceholder)
        {
            _suppressMarkupSuggestions = true;
            Markup = suggestion.TrimEnd('%');
            _suppressMarkupSuggestions = false;
        }
        IsMarkupSuggestionsVisible = false;
    }

    /// <summary>
    /// Called when the markup entry loses focus. Skips the hide right after the
    /// ▾ button opened the list — pressing the button unfocuses the entry, and
    /// hiding then would close the dropdown the moment it opens.
    /// </summary>
    public void HideMarkupSuggestionsDeferred()
    {
        if (DateTime.UtcNow - _markupDropdownOpenedAt > TimeSpan.FromMilliseconds(400))
            IsMarkupSuggestionsVisible = false;
    }

    partial void OnCostChanged(string value) => RecalculatePrice();

    partial void OnMarkupChanged(string value)
    {
        RecalculatePrice();
        if (!_syncingPricing && !_suppressMarkupSuggestions)
            UpdateMarkupSuggestions(value);
    }

    partial void OnPriceChanged(string value) => RecalculateMarkup();

    private void RecalculatePrice()
    {
        if (_syncingPricing) return;
        if (!TryParsePricing(Cost, out var cost) || !TryParsePricing(Markup, out var markup)) return;
        _syncingPricing = true;
        Price = FormatMoney(Math.Round(cost * (1 + markup / 100m), 2));
        _syncingPricing = false;
    }

    private void RecalculateMarkup()
    {
        if (_syncingPricing) return;
        if (!TryParsePricing(Price, out var price) || !TryParsePricing(Cost, out var cost) || cost <= 0) return;
        _syncingPricing = true;
        Markup = FormatMarkup(Math.Round((price / cost - 1m) * 100m, 2));
        _syncingPricing = false;
    }

    private static bool TryParsePricing(string? text, out decimal value)
    {
        value = 0;
        var trimmed = text?.Trim().TrimStart('$').TrimEnd('%').Trim();
        return !string.IsNullOrEmpty(trimmed) && decimal.TryParse(trimmed, out value);
    }

    private static decimal? ParsePricingOrNull(string? text)
        => TryParsePricing(text, out var value) ? value : null;

    private static string FormatMoney(decimal value) => value.ToString("0.00");
    private static string FormatMarkup(decimal value) => value.ToString("0.##");

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
                // Assign loaded values without triggering price/markup recalculation.
                _syncingPricing = true;
                Cost = item.Cost.HasValue ? FormatMoney(item.Cost.Value) : string.Empty;
                Markup = item.MarkupPercent.HasValue ? FormatMarkup(item.MarkupPercent.Value) : string.Empty;
                Price = item.Price.HasValue ? FormatMoney(item.Price.Value) : string.Empty;
                _syncingPricing = false;
                if (item.MarkupPercent.HasValue)
                    EnsureMarkupOption(item.MarkupPercent.Value);
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
        _originalCost = Cost ?? string.Empty;
        _originalMarkup = Markup ?? string.Empty;
        _originalPrice = Price ?? string.Empty;
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
                    Category = CategoryValue.Length == 0 ? null : CategoryValue,
                    Cost = ParsePricingOrNull(Cost),
                    MarkupPercent = ParsePricingOrNull(Markup),
                    Price = ParsePricingOrNull(Price)
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
                    Category = CategoryValue,
                    Cost = ParsePricingOrNull(Cost),
                    MarkupPercent = ParsePricingOrNull(Markup),
                    Price = ParsePricingOrNull(Price)
                };
                await _apiService.UpdateInventoryItemAsync(Guid.Parse(ItemId!), request);
                if (request.MarkupPercent.HasValue)
                    EnsureMarkupOption(request.MarkupPercent.Value);
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
