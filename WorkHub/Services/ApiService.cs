using System.Net.Http.Json;
using WorkHub.Models;

namespace WorkHub.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
    }

    // Customers
    public async Task<PagedResponse<CustomerResponse>> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 25)
    {
        var url = $"v1/customers?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&q={Uri.EscapeDataString(search)}";
        return await _httpClient.GetFromJsonAsync<PagedResponse<CustomerResponse>>(url) ?? new();
    }

    public async Task<CustomerResponse?> GetCustomerAsync(Guid id)
        => await _httpClient.GetFromJsonAsync<CustomerResponse>($"v1/customers/{id}");

    public async Task<CustomerResponse?> CreateCustomerAsync(CreateCustomerRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/customers", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerResponse>();
    }

    public async Task<CustomerResponse?> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/customers/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerResponse>();
    }

    public async Task DeleteCustomerAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"v1/customers/{id}");
        response.EnsureSuccessStatusCode();
    }

    // Jobs
    public async Task<PagedResponse<JobListItemResponse>> GetJobsAsync(string? search = null, string? status = null, string? priority = null, Guid? customerId = null, int page = 1, int pageSize = 25)
    {
        var url = $"v1/jobs?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&q={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrEmpty(priority)) url += $"&priority={Uri.EscapeDataString(priority)}";
        if (customerId.HasValue) url += $"&customerId={customerId.Value}";
        return await _httpClient.GetFromJsonAsync<PagedResponse<JobListItemResponse>>(url) ?? new();
    }

    public async Task<JobResponse?> GetJobAsync(Guid id)
        => await _httpClient.GetFromJsonAsync<JobResponse>($"v1/jobs/{id}");

    public async Task<JobResponse?> CreateJobAsync(CreateJobRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/jobs", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobResponse>();
    }

    public async Task<JobResponse?> UpdateJobAsync(Guid id, UpdateJobRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/jobs/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobResponse>();
    }

    public async Task DeleteJobAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"v1/jobs/{id}");
        response.EnsureSuccessStatusCode();
    }

    // Job Notes
    public async Task<List<JobNoteResponse>> GetJobNotesAsync(Guid jobId)
        => await _httpClient.GetFromJsonAsync<List<JobNoteResponse>>($"v1/jobs/{jobId}/notes") ?? new();

    public async Task<JobNoteResponse?> CreateJobNoteAsync(Guid jobId, CreateJobNoteRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"v1/jobs/{jobId}/notes", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobNoteResponse>();
    }

    public async Task<JobNoteResponse?> UpdateJobNoteAsync(Guid jobId, Guid noteId, UpdateJobNoteRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/jobs/{jobId}/notes/{noteId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobNoteResponse>();
    }

    public async Task DeleteJobNoteAsync(Guid jobId, Guid noteId)
    {
        var response = await _httpClient.DeleteAsync($"v1/jobs/{jobId}/notes/{noteId}");
        response.EnsureSuccessStatusCode();
    }

    // Job Items (inventory-based)
    public async Task<List<JobItemResponse>> GetJobItemsAsync(Guid jobId, string? listType = null)
    {
        var url = $"v1/jobs/{jobId}/items";
        if (!string.IsNullOrEmpty(listType)) url += $"?list_type={Uri.EscapeDataString(listType)}";
        return await _httpClient.GetFromJsonAsync<List<JobItemResponse>>(url) ?? new();
    }

    public async Task<JobItemResponse?> CreateJobItemAsync(Guid jobId, CreateJobInventoryRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"v1/jobs/{jobId}/items", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobItemResponse>();
    }

    public async Task<JobItemResponse?> UpdateJobItemAsync(Guid jobId, Guid itemId, UpdateJobInventoryRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/jobs/{jobId}/items/{itemId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobItemResponse>();
    }

    public async Task DeleteJobItemAsync(Guid jobId, Guid itemId)
    {
        var response = await _httpClient.DeleteAsync($"v1/jobs/{jobId}/items/{itemId}");
        response.EnsureSuccessStatusCode();
    }

    // Job Adhoc Items
    public async Task<List<JobItemResponse>> GetJobAdhocItemsAsync(Guid jobId, string? listType = null)
    {
        var url = $"v1/jobs/{jobId}/adhoc-items";
        if (!string.IsNullOrEmpty(listType)) url += $"?list_type={Uri.EscapeDataString(listType)}";
        return await _httpClient.GetFromJsonAsync<List<JobItemResponse>>(url) ?? new();
    }

    public async Task<JobItemResponse?> CreateJobAdhocItemAsync(Guid jobId, CreateJobAdhocItemRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"v1/jobs/{jobId}/adhoc-items", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobItemResponse>();
    }

    public async Task<JobItemResponse?> UpdateJobAdhocItemAsync(Guid jobId, Guid itemId, UpdateJobAdhocItemRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/jobs/{jobId}/adhoc-items/{itemId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobItemResponse>();
    }

    public async Task DeleteJobAdhocItemAsync(Guid jobId, Guid itemId)
    {
        var response = await _httpClient.DeleteAsync($"v1/jobs/{jobId}/adhoc-items/{itemId}");
        response.EnsureSuccessStatusCode();
    }

    // Inventory
    public async Task<PagedResponse<InventoryItemResponse>> GetInventoryAsync(string? search = null, int page = 1, int pageSize = 25)
    {
        var url = $"v1/inventory?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&q={Uri.EscapeDataString(search)}";
        return await _httpClient.GetFromJsonAsync<PagedResponse<InventoryItemResponse>>(url) ?? new();
    }

    public async Task<InventoryItemResponse?> GetInventoryItemAsync(Guid id)
        => await _httpClient.GetFromJsonAsync<InventoryItemResponse>($"v1/inventory/{id}");

    public async Task<InventoryItemResponse?> CreateInventoryItemAsync(CreateInventoryItemRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/inventory", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemResponse>();
    }

    public async Task<InventoryItemResponse?> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/inventory/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemResponse>();
    }

    public async Task DeleteInventoryItemAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"v1/inventory/{id}");
        response.EnsureSuccessStatusCode();
    }

    // Calendar Events
    public async Task<List<CalendarEventResponse>> GetEventsAsync(DateTime from, DateTime to, Guid? userId = null)
    {
        var url = $"v1/events?from={from:O}&to={to:O}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        return await _httpClient.GetFromJsonAsync<List<CalendarEventResponse>>(url) ?? new();
    }

    public async Task<CalendarEventResponse?> GetEventAsync(Guid id)
        => await _httpClient.GetFromJsonAsync<CalendarEventResponse>($"v1/events/{id}");

    public async Task<CalendarEventResponse?> CreateEventAsync(CreateEventRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/events", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CalendarEventResponse>();
    }

    public async Task<CalendarEventResponse?> UpdateEventAsync(Guid id, UpdateEventRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"v1/events/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CalendarEventResponse>();
    }

    public async Task DeleteEventAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"v1/events/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignUserToEventAsync(Guid eventId, Guid userId)
    {
        var response = await _httpClient.PostAsJsonAsync($"v1/events/{eventId}/assignments", new AssignUserRequest { UserId = userId });
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveUserFromEventAsync(Guid eventId, Guid userId)
    {
        var response = await _httpClient.DeleteAsync($"v1/events/{eventId}/assignments/{userId}");
        response.EnsureSuccessStatusCode();
    }

    // Photos
    public async Task<PhotoResponse?> UploadCustomerPhotoAsync(Guid customerId, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);
        var response = await _httpClient.PostAsync($"v1/customers/{customerId}/photos", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PhotoResponse>();
    }

    public async Task<PhotoResponse?> UploadJobPhotoAsync(Guid jobId, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);
        var response = await _httpClient.PostAsync($"v1/jobs/{jobId}/photos", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PhotoResponse>();
    }

    public async Task DeletePhotoAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"v1/photos/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> GetLocationPhotoCountAsync(string address, Guid? excludeCustomerId = null, Guid? excludeJobId = null)
    {
        var url = $"v1/photos/by-address/count?address={Uri.EscapeDataString(address)}";
        if (excludeCustomerId.HasValue) url += $"&excludeCustomerId={excludeCustomerId.Value}";
        if (excludeJobId.HasValue) url += $"&excludeJobId={excludeJobId.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var count = await response.Content.ReadFromJsonAsync<int>();
        return count;
    }

    public async Task<List<LocationPhotoGroupResponse>> GetLocationPhotosAsync(string address, Guid? excludeCustomerId = null, Guid? excludeJobId = null)
    {
        var url = $"v1/photos/by-address?address={Uri.EscapeDataString(address)}";
        if (excludeCustomerId.HasValue) url += $"&excludeCustomerId={excludeCustomerId.Value}";
        if (excludeJobId.HasValue) url += $"&excludeJobId={excludeJobId.Value}";
        return await _httpClient.GetFromJsonAsync<List<LocationPhotoGroupResponse>>(url) ?? new();
    }

    // Users
    public async Task<List<UserBriefResponse>> GetUsersAsync()
        => await _httpClient.GetFromJsonAsync<List<UserBriefResponse>>("v1/users") ?? new();

    public async Task<UserProfileResponse?> GetProfileAsync()
        => await _httpClient.GetFromJsonAsync<UserProfileResponse>("v1/me");

    public async Task<UserProfileResponse?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("v1/me", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfileResponse>();
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("v1/me/password", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PhotoResponse?> UploadProfilePhotoAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);
        var response = await _httpClient.PostAsync("v1/me/photo", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PhotoResponse>();
    }

    public async Task DeleteProfilePhotoAsync()
    {
        var response = await _httpClient.DeleteAsync("v1/me/photo");
        response.EnsureSuccessStatusCode();
    }
}