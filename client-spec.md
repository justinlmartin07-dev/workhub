# WorkHub Client — Detailed Specification

## Technology

**.NET MAUI** — single codebase targeting Android and Windows.

**No paid UI component library.** WorkHub is built entirely on stock MAUI controls, free NuGet packages, and the MAUI Community Toolkit. This keeps the app fully free to deploy to any customer regardless of their size or revenue — no licensing thresholds, no annual subscriptions, no commercial conversations.

**Libraries:**

| Package | Purpose | License |
|---|---|---|
| `CommunityToolkit.Maui` | `SwipeView`, `Popup`, `MediaPicker`, behaviors, converters | MIT |
| `CommunityToolkit.Mvvm` | `ObservableObject`, `RelayCommand`, source generators | MIT |
| `Plugin.Maui.Calendar` | Calendar screen — month and day views with event display | MIT |
| `Plugin.LocalNotification` | Calendar event reminders — local push notifications | MIT |
| `SkiaSharp.Views.Maui.Controls` | Client-side photo compression | MIT |
| `Microsoft.Extensions.Http.Polly` | Retry/backoff on transient HTTP failures | MIT |

---

## Profile Button & Logout

A profile button sits in the top-right corner of every main screen — visible regardless of which tab is active. It shows the user's avatar (profile photo if set, initials if not) as a small circular button.

**Placement:**
- On Android phones: top-right of the app header area, inline with the screen title
- On Windows and tablets: top-right of the left rail, pinned to the bottom of the rail above a separator

Tapping opens a **Profile Sheet** (Community Toolkit `Popup` or bottom sheet) containing:
- Profile photo (tappable to change — opens `MediaPicker`)
- Display name (tappable to edit inline)
- **Change Password** button (opens a simple form with current password, new password, confirm new password)
- App version number (read-only, useful for support)
- **Log Out** button at the bottom

**Logout flow:**
1. User taps Log Out
2. Confirmation dialog: "Are you sure you want to log out?"
3. On confirm: call `POST /v1/auth/logout` with the stored refresh token to revoke it server-side
4. Clear `SecureStorage` — remove `access_token`, `refresh_token`, `token_expiry`
5. Navigate to `LoginPage`
6. On next login the user lands directly on the home screen as normal

**Password change flow:**
1. User taps Change Password in the profile sheet
2. Modal form appears with three fields: Current Password, New Password, Confirm New Password
3. Client validates that New Password matches Confirm and is at least 8 characters
4. Calls `PUT /v1/me/password` with `currentPassword` and `newPassword`
5. On success: clear tokens, navigate to LoginPage (the API revokes all sessions on password change)
6. On failure (wrong current password): show error inline, don't navigate

**Avatar rendering:**
```csharp
// If profile photo exists, show image
// Otherwise generate initials from name
public string Initials => string.Join("", Name.Split(' ')
    .Where(w => w.Length > 0)
    .Take(2)
    .Select(w => w[0].ToString().ToUpper()));
```

**Profile photo upload** follows the same compress-then-POST flow as job and customer photos — SkiaSharp compression before upload, stored in R2 under `profiles/{userId}/{guid}.jpg`.

---

## First-Run & Version Check Flow

**On every cold start**, WorkHub runs through this sequence before showing any content:

1. **Version check** — call `GET /v1/version` and compare `minimumAppVersion` against the current app version. If WorkHub is outdated, show a blocking "Update Required" screen with a message and no way to proceed. **If the API is unreachable** (network error, timeout, server down), **skip the version check and proceed to login.** The version check protects against outdated clients, not server outages — blocking the app when the server is temporarily down would make a bad situation worse.

2. **Auth check** — check `SecureStorage` for a stored access token. If none exists, this is either a first run or a logged-out state.

3. **First run** — if no token has ever been stored (use a simple `SecureStorage` flag like `has_launched_before`), show the **Welcome Screen**. On subsequent logouts, skip the welcome screen and go directly to the login page.

**Welcome Screen (first run only):**
- Full-screen layout with WorkHub logo centered
- Short one or two line description of WorkHub below the logo
- A prominent "Get Started" button at the bottom that navigates to the login page
- No back navigation — this is a one-way flow

```
App Launch
    ↓
Version Check
    ├── Outdated → "Update Required" screen (dead end)
    ├── API unreachable → skip check, continue ↓
    └── OK → continue ↓
Has access token?
    ├── Yes → validate token → Home
    └── No
         ├── First run? → Welcome Screen → Login Page → Home
         └── Returning?  → Login Page → Home
```

**Version check implementation:**
```csharp
try
{
    var version = await _apiService.GetVersionAsync();
    if (AppInfo.Current.VersionString.CompareTo(version.MinimumAppVersion) < 0)
    {
        await Shell.Current.GoToAsync("//UpdateRequiredPage");
        return;
    }
}
catch (HttpRequestException)
{
    // API unreachable — skip version check and proceed
    // The user will see a connection error on login if the server is truly down
}
```

Once logged in, the Welcome Screen is never shown again.

---

## App Structure

```
/WorkHub
  /Views
    WelcomePage
    LoginPage
    UpdateRequiredPage
    CustomersListPage
    CustomerDetailPage
    CustomerEditPage
    JobsListPage
    JobDetailPage
    JobEditPage
    InventoryPage
    InventoryItemDetailPage
    CalendarPage
    EventDetailPage
    LocationPhotosPage
    PhotoViewerPage
    ChangePasswordPage
  /ViewModels
    LoginViewModel
    CustomersListViewModel
    CustomerDetailViewModel
    CustomerEditViewModel
    JobsListViewModel
    JobDetailViewModel
    JobEditViewModel
    InventoryViewModel
    InventoryItemDetailViewModel
    CalendarViewModel
    EventDetailViewModel
    LocationPhotosViewModel
    PhotoViewerViewModel
    ProfileViewModel
    ChangePasswordViewModel
  /Models
  /Services
    ApiService.cs           ← HttpClient wrapper for all API calls
    AuthService.cs          ← Token storage, refresh, login/logout
    NavigationService.cs
    PhotoService.cs         ← Shared compression and upload logic
    NotificationService.cs  ← Local notification scheduling for reminders
  /Handlers
    AuthDelegatingHandler.cs ← Attaches JWT to every request, triggers refresh
  /Controls
    DataStateView.xaml       ← Reusable loading/error/content wrapper
  /Resources
    /Styles
    /Images
  AppShell.xaml
  MauiProgram.cs
```

---

## Navigation

Use **Shell navigation** — it's MAUI's built-in nav system and handles the tab bar, flyout menu, and deep linking cleanly.

Suggested shell structure:

```
Shell
  ├── Tab: Customers
  │     └── CustomersListPage → CustomerDetailPage → JobDetailPage
  ├── Tab: Jobs (all active jobs, filterable by status/priority; search via SearchBar)
  │     └── JobsListPage → JobDetailPage
  ├── Tab: Inventory
  │     └── InventoryPage → InventoryItemDetailPage
  └── Tab: Calendar
        └── CalendarPage → EventDetailPage
```

---

## Screens

### Login

A native MAUI page with two input fields and a button. No WebView, no browser, no redirect — just a simple form that posts credentials directly to the API.

```xml
<!-- LoginPage.xaml -->
<VerticalStackLayout Padding="24" VerticalOptions="Center">
    <Image Source="workhub_logo.png"
           HeightRequest="80"
           HorizontalOptions="Center"
           Margin="0,0,0,32" />

    <Entry Placeholder="Email"
           Text="{Binding Email}"
           Keyboard="Email"
           ReturnType="Next" />

    <Entry Placeholder="Password"
           Text="{Binding Password}"
           IsPassword="True"
           ReturnType="Done"
           ReturnCommand="{Binding LoginCommand}" />

    <Label Text="{Binding ErrorMessage}"
           TextColor="Red"
           IsVisible="{Binding HasError}"
           HorizontalOptions="Center"
           Margin="0,8,0,0" />

    <Button Text="Log In"
            Command="{Binding LoginCommand}"
            IsEnabled="{Binding IsNotBusy}"
            Margin="0,16,0,0" />

    <ActivityIndicator IsRunning="{Binding IsBusy}"
                       IsVisible="{Binding IsBusy}"
                       HorizontalOptions="Center"
                       Margin="0,8,0,0" />
</VerticalStackLayout>
```

```csharp
// LoginViewModel.cs
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty] string email = string.Empty;
    [ObservableProperty] string password = string.Empty;
    [ObservableProperty] string errorMessage = string.Empty;
    [ObservableProperty] bool hasError;
    [ObservableProperty] bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [RelayCommand]
    async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your email and password";
            HasError = true;
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            var result = await _authService.LoginAsync(Email.Trim(), Password);

            if (result.Success)
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                // result.ErrorMessage comes from the API — either
                // "Invalid email or password" (401) or
                // "Account locked. Try again in X minutes." (423)
                ErrorMessage = result.ErrorMessage;
                HasError = true;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to connect to server. Check your internet connection.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

The login page is intentionally simple — no "Forgot Password" link (passwords are reset by the admin directly in the database or via a future admin endpoint), no registration, no social login. If a user enters their password wrong 5 times, the API locks their account for 15 minutes and returns a message with the time remaining — the login page displays this inline in the same error label.

### Customers List
- `SearchBar` at top bound to a ViewModel filter property
- `CollectionView` with item template showing name, phone, and most recent job status badge
- Tap to open Customer Detail (navigate or load in right panel on wide screens)
- FAB (floating action button) to add new customer
- Pull-to-refresh
- Pagination: loads first page on open, "Load More" button or infinite scroll for subsequent pages

### Customer Detail
- Name, phone (tap to call), email, address (tap to open Maps)
- Customer photo with camera capture / gallery pick
- Photos section — if other photos exist at the same address, show "See N other photos at this location" button below
- **Add Job** button — creates a new job pre-linked to this customer
- List of linked jobs (tap to open)
- Notes section (single text field, editable inline)
- Edit button (opens CustomerEditPage)
- Delete button with confirmation dialog — if blocked by active jobs, show error with the blocking job names

**Tap-to-call:** `PhoneDialer.Open(phoneNumber)` via `CommunityToolkit.Maui`

**Tap-to-navigate (Google Maps):**
```csharp
var uri = $"geo:0,0?q={Uri.EscapeDataString(address)}";
await Launcher.OpenAsync(new Uri(uri));
```

**Tap-to-navigate (Google Earth):**
```csharp
var uri = $"https://earth.google.com/web/search/{Uri.EscapeDataString(address)}";
await Browser.OpenAsync(uri);
```

**Handling 409 on customer delete:**
```csharp
var response = await _apiService.DeleteCustomerAsync(customerId);
if (response.StatusCode == HttpStatusCode.Conflict)
{
    var error = await response.Content.ReadFromJsonAsync<DeleteConflictResponse>();
    var jobNames = string.Join(", ", error.BlockingJobs.Select(j => j.Title));
    await Shell.Current.DisplayAlert("Cannot Delete",
        $"This customer has active jobs: {jobNames}. Complete or delete these jobs first.", "OK");
    return;
}
```

### Jobs List
- `SearchBar` at top bound to a ViewModel filter property (searches `title` and `scope_notes`)
- Status filter chips/picker: All, Pending, In Progress, Complete
- Priority filter: All, Low, Normal, High
- `CollectionView` showing job title, customer name, status badge, priority badge
- Tap to open Job Detail
- FAB to create new job (shows customer picker first)
- Pull-to-refresh
- Pagination: same pattern as customers list

### Job Detail
- Title, status (picker), priority (picker)
- Scope notes (multiline text)
- Linked customer shown at top (tappable to navigate to customer detail)
- Photo gallery (newest first) with camera capture — if other photos exist at the customer's address, show "See N other photos at this location" button below the gallery
- Notes list (oldest first — reads as a chronological log, new notes appear at the bottom; each note shows author name and timestamp)
  - Add note: text input + "Add" button
  - Edit note: tap to edit inline (only if current user is the author, or allow all — your call)
  - Delete note: swipe-to-delete with confirmation
- Used items list (library + ad-hoc combined, showing `source` badge)
- To-be-ordered items list (library + ad-hoc combined)
- Edit button (opens JobEditPage for title/status/priority/scope)

### Inventory
- Searchable list of inventory items (searches `name` and `part_number`)
- Tap to view/edit item detail
- Add custom items via FAB
- Pull-to-refresh
- Pagination

**Handling 409 on inventory delete:**
```csharp
var response = await _apiService.DeleteInventoryItemAsync(itemId);
if (response.StatusCode == HttpStatusCode.Conflict)
{
    var error = await response.Content.ReadFromJsonAsync<DeleteConflictResponse>();
    var jobNames = string.Join(", ", error.ReferencingJobs.Select(j => j.Title));
    await Shell.Current.DisplayAlert("Cannot Delete",
        $"This item is used in jobs: {jobNames}. Remove it from those jobs first.", "OK");
    return;
}
```

### Location Photo History

A "See X other photos at this location" button appears on both the **Customer Detail** and **Job Detail** screens whenever other photos exist at the same address. If there are no other photos at that address, the button is hidden entirely.

**Button behavior:**
- On screen load, the client calls `GET /v1/photos/by-address/count?address=&excludeCustomerId=&excludeJobId=` with the customer's address and the current context IDs
- If count > 0, the button renders: `See 4 other photos at this location`
- Tapping opens a **Location Photos screen** which calls `GET /v1/photos/by-address?address=&excludeCustomerId=&excludeJobId=` to load the full grouped data
- If count == 0, no button is rendered — no empty state needed

This two-call approach avoids loading photo URLs until the user actually taps the button.

**Button placement:**
- On Customer Detail: below the customer photos strip, above the jobs list
- On Job Detail: below the job photos strip

```xml
<!-- Shown only when LocationPhotoCount > 0 -->
<Button Text="{Binding LocationPhotoCountLabel}"
        IsVisible="{Binding HasLocationPhotos}"
        Command="{Binding ViewLocationPhotosCommand}"
        Style="{StaticResource SecondaryButton}" />
```

```csharp
// In ViewModel — called on page load
var count = await _apiService.GetPhotoCountAtAddressAsync(
    address: customer.Address,
    excludeCustomerId: customer.Id,
    excludeJobId: null);

LocationPhotoCount = count;

public string LocationPhotoCountLabel =>
    $"See {LocationPhotoCount} other photo{(LocationPhotoCount == 1 ? "" : "s")} at this location";

public bool HasLocationPhotos => LocationPhotoCount > 0;
```

**Location Photos screen:**

A modal or pushed page showing all photos at the address grouped by job/customer. Each group has a header showing the customer name and job title. Photos within each group are ordered newest first.

```
┌─────────────────────────────────────┐
│  ← Photos at 48 Elm St             │
├─────────────────────────────────────┤
│  📍 48 Elm St, Minneapolis          │
├─────────────────────────────────────┤
│  HVAC Unit Replacement              │
│  Martin Residence · Feb 2026        │
│  [photo] [photo] [photo]            │
├─────────────────────────────────────┤
│  Annual HVAC Service                │
│  Martin Residence · Nov 2025        │
│  [photo] [photo]                    │
└─────────────────────────────────────┘
```

Tapping a photo opens a full-screen viewer (PhotoViewerPage). The screen title shows the normalized address. Groups are ordered by most recent photo date descending so the newest work at that location is always at the top.

### Photo Viewer

A full-screen modal page for viewing a single photo. Minimal UI — just the image and a close button.

**Features:**
- Pinch-to-zoom via a `ScrollView` wrapping an `Image` (or a dedicated `ZoomableImage` from Community Toolkit if available)
- Swipe left/right to navigate between photos in the same group (pass the photo list and current index as navigation parameters)
- Tap to toggle UI overlay (close button visible/hidden)
- Close button (top-left X) or swipe-down to dismiss

```csharp
// Navigation to PhotoViewerPage
await Shell.Current.GoToAsync(nameof(PhotoViewerPage), new Dictionary<string, object>
{
    { "Photos", photoList },
    { "CurrentIndex", selectedIndex }
});
```

### Calendar
- Month and day views via `Plugin.Maui.Calendar` (MIT licensed, no thresholds)
- Tap date to see events for that day
- Add event: title, date/time, optional link to customer or job
- Assign one or more team members to an event via a user picker (populated from `GET /v1/users`)
- **Reminder picker** — dropdown with options: None, 15 minutes before, 30 minutes before, 1 hour before, 1 day before
- Creator recorded automatically from the logged-in user
- Pull-to-refresh on the events list for a selected day

**Reminder scheduling:**

When an event is created or updated with a `reminderMinutes` value, the client schedules a local notification using `Plugin.LocalNotification`:

```csharp
// NotificationService.cs
public async Task ScheduleReminderAsync(CalendarEvent evt)
{
    if (evt.ReminderMinutes == null) return;

    var notifyTime = evt.StartTime.AddMinutes(-evt.ReminderMinutes.Value);
    if (notifyTime <= DateTimeOffset.UtcNow) return; // Don't schedule past reminders

    var notification = new NotificationRequest
    {
        NotificationId = evt.Id.GetHashCode(), // Deterministic ID for updates/cancellation
        Title = "WorkHub Reminder",
        Description = evt.Title,
        Schedule = new NotificationRequestSchedule
        {
            NotifyTime = notifyTime.LocalDateTime
        }
    };

    await LocalNotificationCenter.Current.Show(notification);
}

public void CancelReminder(Guid eventId)
{
    LocalNotificationCenter.Current.Cancel(eventId.GetHashCode());
}
```

When an event is updated, cancel the old reminder and schedule a new one. When an event is deleted, cancel the reminder.

**On app launch**, after auth succeeds, fetch upcoming events for the next 7 days and reschedule any reminders — this handles the case where reminders were lost (app reinstall, cache cleared, etc.).

---

## Camera & Photos

Photo capture and selection differs by platform. All photos are compressed client-side before upload regardless of source.

**Install:** `dotnet add package SkiaSharp.Views.Maui.Controls`

### Android (phones and tablets)

Uses `MediaPicker` from `CommunityToolkit.Maui` for both camera capture and gallery selection:

```csharp
// Capture from camera
var photo = await MediaPicker.CapturePhotoAsync();

// Pick from gallery
var photo = await MediaPicker.PickPhotoAsync();
```

Both return a `FileResult` which feeds into the shared compression pipeline below.

Requires permissions declared in `AndroidManifest.xml`:
```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />
```

Runtime permission requests must be handled in code — use `CommunityToolkit.Maui`'s `Permissions` API and show a user-friendly explanation if denied.

### Windows (file picker only)

Windows uses `FilePicker` with an image type filter. Users can select any common image format — JPEG, PNG, BMP, TIFF, WEBP. The compression pipeline handles all of these via SkiaSharp's format-agnostic decoder.

```csharp
var result = await FilePicker.PickAsync(new PickOptions {
    Title = "Select a photo",
    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
        { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" } }
    })
});

if (result != null)
    await CompressAndUploadAsync(result);
```

Add an upfront file size check before compression to handle unusually large files gracefully:

```csharp
var fileInfo = new FileInfo(result.FullPath);
if (fileInfo.Length > 50 * 1024 * 1024) // 50MB limit
{
    await DisplayAlert("File too large",
        "Please select an image under 50MB.", "OK");
    return;
}
```

### Shared Compression Pipeline

Both platforms feed into the same compression method. SkiaSharp's `SKBitmap.Decode` handles any common image format as input and always outputs JPEG:

```csharp
// PhotoService.cs
public async Task<Stream> CompressPhotoAsync(FileResult photo)
{
    using var original = await photo.OpenReadAsync();
    using var bitmap = SKBitmap.Decode(original);

    if (bitmap == null)
        throw new InvalidOperationException("Could not decode image file.");

    // Resize if larger than 1920px on longest edge
    SKBitmap scaled = bitmap;
    try
    {
        if (bitmap.Width > 1920 || bitmap.Height > 1920)
        {
            var ratio = Math.Min(1920f / bitmap.Width, 1920f / bitmap.Height);
            var newWidth = (int)(bitmap.Width * ratio);
            var newHeight = (int)(bitmap.Height * ratio);
            scaled = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
        }

        var compressed = new MemoryStream();
        scaled.Encode(compressed, SKEncodedImageFormat.Jpeg, quality: 80);
        compressed.Position = 0;
        return compressed;
    }
    finally
    {
        // Dispose the resized bitmap if it's a different object than the original
        if (scaled != bitmap)
            scaled?.Dispose();
    }
}

public async Task CompressAndUploadAsync(FileResult photo, Guid entityId, string entityType)
{
    using var stream = await CompressPhotoAsync(photo);
    using var formContent = new MultipartFormDataContent();
    formContent.Add(new StreamContent(stream), "photo",
        Path.ChangeExtension(photo.FileName, ".jpg"));

    switch (entityType)
    {
        case "job":
            await _apiService.UploadJobPhotoAsync(entityId, formContent);
            break;
        case "customer":
            await _apiService.UploadCustomerPhotoAsync(entityId, formContent);
            break;
        case "profile":
            await _apiService.UploadProfilePhotoAsync(formContent);
            break;
    }
}
```

**Expected size reduction:**

| Input | Typical output | Reduction |
|---|---|---|
| 3.5MB phone JPEG | 300–600KB | ~85% |
| 10MB PNG screenshot | 400–800KB | ~92% |
| 20MB DSLR JPEG | 500KB–1MB | ~95% |

Quality at 80% JPEG with a 1920px cap is more than sufficient for job documentation.

---

## Authentication

Authentication is handled entirely through a native login form and direct API calls. No browser, no WebView, no OAuth redirects, no third-party auth provider.

### AuthService

```csharp
// Services/AuthService.cs
public class AuthService
{
    private readonly HttpClient _http; // A separate HttpClient NOT wrapped by AuthDelegatingHandler

    public AuthService(IHttpClientFactory httpClientFactory)
    {
        // Use a named client that does NOT have the AuthDelegatingHandler
        // to avoid circular dependency (auth handler calls auth service)
        _http = httpClientFactory.CreateClient("AuthClient");
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("auth/login", new { email, password });

        if (!response.IsSuccessStatusCode)
        {
            // Parse error message from API — covers both 401 (bad credentials)
            // and 423 (account locked with time remaining)
            var errorBody = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return new LoginResult
            {
                Success = false,
                ErrorMessage = errorBody?.Error ?? "Login failed"
            };
        }

        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();

        await SecureStorage.SetAsync("access_token", data.AccessToken);
        await SecureStorage.SetAsync("refresh_token", data.RefreshToken);
        await SecureStorage.SetAsync("token_expiry", data.ExpiresAt.ToString("o"));

        return new LoginResult { Success = true, User = data.User };
    }

    public async Task<string?> GetValidTokenAsync()
    {
        var token = await SecureStorage.GetAsync("access_token");
        var expiry = await SecureStorage.GetAsync("token_expiry");

        if (token == null) return null;

        // Refresh if within 60 seconds of expiry
        if (expiry != null && DateTimeOffset.UtcNow >= DateTimeOffset.Parse(expiry).AddSeconds(-60))
        {
            token = await TryRefreshAsync();
        }

        return token;
    }

    private async Task<string?> TryRefreshAsync()
    {
        try
        {
            var refreshToken = await SecureStorage.GetAsync("refresh_token");
            if (refreshToken == null) return null;

            var response = await _http.PostAsJsonAsync("auth/refresh",
                new { refreshToken });

            if (!response.IsSuccessStatusCode)
            {
                await SignOutAndRedirectAsync();
                return null;
            }

            var data = await response.Content.ReadFromJsonAsync<RefreshResponse>();
            await SecureStorage.SetAsync("access_token", data.AccessToken);
            await SecureStorage.SetAsync("refresh_token", data.RefreshToken);
            await SecureStorage.SetAsync("token_expiry", data.ExpiresAt.ToString("o"));
            return data.AccessToken;
        }
        catch
        {
            await SignOutAndRedirectAsync();
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await SecureStorage.GetAsync("refresh_token");
        if (refreshToken != null)
        {
            try
            {
                var accessToken = await SecureStorage.GetAsync("access_token");
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                await _http.PostAsJsonAsync("auth/logout", new { refreshToken });
            }
            catch
            {
                // Best effort — even if the server call fails, clear local state
            }
        }

        SecureStorage.Remove("access_token");
        SecureStorage.Remove("refresh_token");
        SecureStorage.Remove("token_expiry");
        await Shell.Current.GoToAsync("//LoginPage");
    }

    public async Task SignOutAndRedirectAsync()
    {
        SecureStorage.Remove("access_token");
        SecureStorage.Remove("refresh_token");
        SecureStorage.Remove("token_expiry");
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
```

**Key points:**
- `AuthService` uses a **separate named `HttpClient`** that does NOT have the `AuthDelegatingHandler` attached. This avoids a circular dependency (the handler calls `GetValidTokenAsync()`, which would call the handler again).
- Login is a simple POST with email and password — no browser involved.
- Token refresh is transparent to the rest of the app.
- Logout revokes the refresh token server-side (best-effort) and clears local state.

---

## Token Attachment via DelegatingHandler

All authenticated API requests pass through a `DelegatingHandler` that calls `GetValidTokenAsync()` on every outgoing request. This ensures the token is always fresh at the moment the request fires.

```csharp
// Handlers/AuthDelegatingHandler.cs
public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly AuthService _authService;

    public AuthDelegatingHandler(AuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authService.GetValidTokenAsync();
        if (token != null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If we get a 401 despite having a token, force re-auth
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.SignOutAndRedirectAsync();
        }

        return response;
    }
}
```

**Registration in `MauiProgram.cs`:**
```csharp
var apiBaseUrl = "https://<railway-url>/v1/";

// Auth client — no auth handler (used by AuthService itself)
builder.Services.AddHttpClient("AuthClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddSingleton<AuthService>();
builder.Services.AddTransient<AuthDelegatingHandler>();

// Main API client — with auth handler and retry policy
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<AuthDelegatingHandler>()
.AddTransientHttpErrorPolicy(p =>
    p.WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

The Polly retry policy automatically retries on transient failures (408, 429, 5xx) with exponential backoff — two retries at 2s and 4s. This handles the spotty connectivity common at job sites without adding complexity to every API call.

---

## Loading & Error States

Every screen that fetches data follows the same three-state pattern: **Loading**, **Content**, and **Error**. Define this once in a shared `DataStateView` component and reuse it on every page.

**The three states:**

- **Loading** — shown immediately when a page loads or a refresh is triggered. A centered activity indicator with no other content visible.
- **Content** — the normal populated UI. Shown once data is returned successfully.
- **Error** — shown when an API call fails. A centered message ("Something went wrong") with a "Try Again" button that retries the last request. Does not show a stack trace or technical detail.

**Shared component pattern:**

```xml
<!-- DataStateView.xaml — reusable across all pages -->
<ContentView>
    <!-- Loading -->
    <ActivityIndicator IsRunning="{Binding IsLoading}"
                       IsVisible="{Binding IsLoading}"
                       HorizontalOptions="Center"
                       VerticalOptions="Center" />

    <!-- Error -->
    <VerticalStackLayout IsVisible="{Binding HasError}"
                         HorizontalOptions="Center"
                         VerticalOptions="Center">
        <Label Text="Something went wrong" />
        <Button Text="Try Again" Command="{Binding RetryCommand}" />
    </VerticalStackLayout>

    <!-- Content -->
    <ContentPresenter IsVisible="{Binding HasContent}" />
</ContentView>
```

**Base ViewModel pattern — apply to every ViewModel:**

```csharp
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] bool isLoading;
    [ObservableProperty] bool hasError;
    [ObservableProperty] bool hasContent;

    protected async Task LoadAsync(Func<Task> action)
    {
        IsLoading = true;
        HasError = false;
        HasContent = false;
        try
        {
            await action();
            HasContent = true;
        }
        catch
        {
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

**Additional UX rules:**
- Pull-to-refresh on all list pages triggers the same `LoadAsync` cycle
- Mutations (save, delete) show a brief inline activity indicator on the button, not a full-screen loader
- Destructive actions (delete customer, delete job) always show a confirmation dialog before proceeding
- If a delete returns `409 Conflict`, show a descriptive alert explaining what's blocking the deletion (see Customer Detail and Inventory sections above)
- If an API call returns `401`, the `AuthDelegatingHandler` handles the silent redirect — the error state is never shown for auth failures

---

## API Service

Centralise all HTTP calls in `ApiService.cs`. The base URL includes the `/v1/` prefix so individual method calls don't need to repeat it. Token attachment and retry are handled by the `AuthDelegatingHandler` and Polly policy — `ApiService` doesn't deal with auth at all.

```csharp
public class ApiService
{
    private readonly HttpClient _http;
    // BaseAddress set to https://<railway-url>/v1/

    // Version
    public Task<VersionResponse> GetVersionAsync() { ... }

    // Customers
    public Task<PagedResponse<CustomerSummary>> GetCustomersAsync(string? search = null, int page = 1) { ... }
    public Task<CustomerDetail> GetCustomerAsync(Guid id) { ... }
    public Task<CustomerDetail> CreateCustomerAsync(CreateCustomerRequest dto) { ... }
    public Task UpdateCustomerAsync(Guid id, UpdateCustomerRequest dto) { ... }
    public Task<HttpResponseMessage> DeleteCustomerAsync(Guid id) { ... } // Returns raw response for 409 handling

    // Jobs
    public Task<PagedResponse<JobSummary>> GetJobsAsync(string? search = null, string? status = null, string? priority = null, Guid? customerId = null, int page = 1) { ... }
    public Task<JobDetail> GetJobAsync(Guid id) { ... }
    public Task<JobDetail> CreateJobAsync(CreateJobRequest dto) { ... }
    public Task UpdateJobAsync(Guid id, UpdateJobRequest dto) { ... }
    public Task<HttpResponseMessage> DeleteJobAsync(Guid id) { ... }

    // Job Notes
    public Task<List<JobNote>> GetJobNotesAsync(Guid jobId) { ... }
    public Task<JobNote> CreateJobNoteAsync(Guid jobId, CreateJobNoteRequest dto) { ... }
    public Task UpdateJobNoteAsync(Guid jobId, Guid noteId, UpdateJobNoteRequest dto) { ... }
    public Task DeleteJobNoteAsync(Guid jobId, Guid noteId) { ... }

    // Photos
    public Task<PhotoResponse> UploadCustomerPhotoAsync(Guid customerId, MultipartFormDataContent photo) { ... }
    public Task<PhotoResponse> UploadJobPhotoAsync(Guid jobId, MultipartFormDataContent photo) { ... }
    public Task<PhotoResponse> UploadProfilePhotoAsync(MultipartFormDataContent photo) { ... }
    public Task DeletePhotoAsync(Guid photoId) { ... }
    public Task<int> GetPhotoCountAtAddressAsync(string address, Guid? excludeCustomerId = null, Guid? excludeJobId = null) { ... }
    public Task<List<PhotoGroup>> GetPhotosAtAddressAsync(string address, Guid? excludeCustomerId = null, Guid? excludeJobId = null) { ... }

    // Inventory
    public Task<PagedResponse<InventoryItem>> GetInventoryAsync(string? search = null, int page = 1) { ... }
    public Task<InventoryItem> GetInventoryItemAsync(Guid id) { ... }
    public Task<InventoryItem> CreateInventoryItemAsync(CreateInventoryItemRequest dto) { ... }
    public Task UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest dto) { ... }
    public Task<HttpResponseMessage> DeleteInventoryItemAsync(Guid id) { ... } // Returns raw response for 409 handling

    // Job Items
    public Task<List<JobItemResponse>> GetJobItemsAsync(Guid jobId, string listType) { ... }
    public Task AddJobItemAsync(Guid jobId, AddJobItemRequest dto) { ... }
    public Task UpdateJobItemAsync(Guid jobId, Guid itemId, UpdateJobItemRequest dto) { ... }
    public Task DeleteJobItemAsync(Guid jobId, Guid itemId) { ... }
    public Task<List<JobItemResponse>> GetJobAdhocItemsAsync(Guid jobId, string listType) { ... }
    public Task AddJobAdhocItemAsync(Guid jobId, AddJobAdhocItemRequest dto) { ... }
    public Task UpdateJobAdhocItemAsync(Guid jobId, Guid itemId, UpdateJobAdhocItemRequest dto) { ... }
    public Task DeleteJobAdhocItemAsync(Guid jobId, Guid itemId) { ... }

    // Users
    public Task<List<UserSummary>> GetUsersAsync() { ... }

    // Profile
    public Task<UserProfile> GetMeAsync() { ... }
    public Task UpdateMeAsync(UpdateProfileRequest dto) { ... }
    public Task ChangePasswordAsync(ChangePasswordRequest dto) { ... }
    public Task DeleteProfilePhotoAsync() { ... }

    // Calendar
    public Task<List<CalendarEventSummary>> GetEventsAsync(DateTimeOffset from, DateTimeOffset to, Guid? userId = null) { ... }
    public Task<CalendarEventDetail> GetEventAsync(Guid id) { ... }
    public Task<CalendarEventDetail> CreateEventAsync(CreateEventRequest dto) { ... }
    public Task UpdateEventAsync(Guid id, UpdateEventRequest dto) { ... }
    public Task DeleteEventAsync(Guid id) { ... }
    public Task AssignUserToEventAsync(Guid eventId, Guid userId) { ... }
    public Task RemoveUserFromEventAsync(Guid eventId, Guid userId) { ... }
}
```

Use `System.Text.Json` for serialization — it's built into .NET and performs well on Android.

**Presigned URL handling:** Photo URLs returned by the API are presigned and expire after 1 hour. The client should not cache these URLs persistently. When displaying photos, use the URLs directly in `Image.Source`. If a photo fails to load (expired URL), re-fetching the parent record will provide fresh URLs.

---

## Touch & UX Considerations

- **Minimum tap target:** 48×48dp — enforce on all buttons and list items
- **Large, readable fonts** — default to 16sp+ for body text
- **Swipe to delete** on list items via `SwipeView` from Community Toolkit — wraps any `CollectionView` item
- **Pull to refresh** on all list pages
- **Confirmation dialogs** before destructive actions via Community Toolkit `Popup`
- **Offline state** — show a clear banner if API is unreachable; no silent failures

---

## UI Layout

The app targets three distinct form factors with different layout behaviour for each.

### Form Factors & Breakpoints

| Form factor | Width | Layout | Nav |
|---|---|---|---|
| Android phone | < 720dp | Single-panel | Bottom tab bar |
| Android tablet portrait | ≥ 720dp | Two-panel | Left rail |
| Android tablet landscape | ≥ 720dp | Two-panel | Left rail |
| Windows portrait | < 720dp | Single-panel | Left rail |
| Windows landscape | ≥ 720dp | Two-panel | Left rail |

**Breakpoint: 720dp** — triggers two-panel layout and left navigation rail. A portrait Windows touchscreen PC will typically be narrower than 720dp and correctly falls back to single-panel.

### Navigation

**Android phones** use a bottom tab bar — the standard Android pattern and the most thumb-friendly on a small screen.

**Everything else** (tablets and Windows) uses a left-side navigation rail. MAUI's Shell doesn't natively support a left rail out of the box, so implement it as a custom `FlyoutHeader` with `FlyoutBehavior="Locked"` and a collapsed flyout width, or use a `Grid`-based shell replacement with a manual tab column on the left.

```xml
<!-- Conceptual structure for left rail on wide screens -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="72" />   <!-- Icon-only rail -->
        <ColumnDefinition Width="*" />    <!-- Content area -->
    </Grid.ColumnDefinitions>

    <!-- Left rail: tab icons -->
    <VerticalStackLayout Grid.Column="0" BackgroundColor="{StaticResource Primary}">
        <ImageButton Source="customers_icon.png" Command="{Binding GoToCustomersCommand}" />
        <ImageButton Source="jobs_icon.png" Command="{Binding GoToJobsCommand}" />
        <ImageButton Source="inventory_icon.png" Command="{Binding GoToInventoryCommand}" />
        <ImageButton Source="calendar_icon.png" Command="{Binding GoToCalendarCommand}" />
    </VerticalStackLayout>

    <!-- Content area -->
    <ContentView Grid.Column="1" x:Name="ContentArea" />
</Grid>
```

### Two-Panel Layout

Applied to Customers, Jobs, and Inventory screens on wide form factors. The list sits on the left, the detail view on the right. Tapping a list item loads detail into the right panel without navigating away from the page.

```xml
<Grid>
    <VisualStateManager.VisualStateGroups>
        <VisualStateGroup>
            <VisualState x:Name="Narrow">
                <VisualState.StateTriggers>
                    <AdaptiveTrigger MinWindowWidth="0" />
                </VisualState.StateTriggers>
                <VisualState.Setters>
                    <Setter TargetName="DetailPanel" Property="IsVisible" Value="False" />
                </VisualState.Setters>
            </VisualState>
            <VisualState x:Name="Wide">
                <VisualState.StateTriggers>
                    <AdaptiveTrigger MinWindowWidth="720" />
                </VisualState.StateTriggers>
                <VisualState.Setters>
                    <Setter TargetName="DetailPanel" Property="IsVisible" Value="True" />
                </VisualState.Setters>
            </VisualState>
        </VisualStateGroup>
    </VisualStateManager.VisualStateGroups>

    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="320" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <ContentView Grid.Column="0" x:Name="ListPanel" />
    <ContentView Grid.Column="1" x:Name="DetailPanel" />
</Grid>
```

On narrow screens, tapping a list item pushes a detail page onto the navigation stack as normal. On wide screens, tapping loads the detail into the right panel in place — no navigation push.

### Per Form Factor Summary

**Android phone (portrait, single-panel)**
- Bottom tab bar with icons and labels
- Full-width list pages
- Tap list item → navigate to detail page
- FABs in bottom-right corner

**Android tablet (portrait or landscape, two-panel)**
- Left navigation rail (icon only, 72dp wide)
- List panel 320dp wide, detail fills remainder
- Tap list item → loads detail in right panel
- No FAB — use an "Add" button in the list panel header instead, as FABs can conflict with the rail

**Windows landscape (two-panel)**
- Left navigation rail (icon only, 72dp wide)
- Same two-panel behaviour as tablet
- Mouse and touch both supported — ensure tap targets are still ≥ 48dp for touch mode
- Test window resizing — the layout should switch cleanly between single and two-panel as the window is dragged narrower or wider

**Windows portrait (single-panel)**
- Left navigation rail stays visible (it's narrow enough not to interfere)
- Single-panel content fills the remaining width
- Tap list item → navigate to detail page, same as phone
