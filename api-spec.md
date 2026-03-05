# WorkHub.Api — Detailed Specification

## Technology

**ASP.NET Core Web API** (.NET 8+) hosted on Railway.

Authentication is **self-hosted** — the API validates user credentials against BCrypt password hashes in the database and issues its own JWTs. No external auth provider is used. This keeps the system fully self-contained with no third-party dependencies that could change policies or require a browser.

Photo storage is handled by **Cloudflare R2** — S3-compatible object storage with a **private bucket**. The database stores only the R2 object key for each photo. When returning photo data, the API generates presigned URLs with a 1-hour expiry so the client can fetch images directly from R2 without the bytes passing through the API server.

---

## Project Structure

```
/WorkHub.Api
  /Controllers
    AuthController.cs
    CustomersController.cs
    JobsController.cs
    PhotosController.cs
    InventoryController.cs
    CalendarController.cs
    UsersController.cs
    MeController.cs
  /Services
    AuthService.cs
    PhotoService.cs
    TokenCleanupService.cs
  /Models
  /Data
    DbContext.cs
    /Migrations
  /DTOs
    /Requests
    /Responses
  Program.cs
  appsettings.json
```

Keep it flat and simple. No clean architecture or CQRS — this is a small internal tool.

---

## Authentication & Authorization

### Overview

The API handles authentication entirely on its own:

1. User submits email + password to `POST /v1/auth/login`
2. API checks if the account is locked — if `locked_until` is in the future, reject immediately with a message indicating how long until unlock
3. API verifies the password against the BCrypt hash in the `users` table
4. On success: reset `failed_login_attempts` to 0, return a short-lived **access token** (JWT, 30-minute expiry) and a long-lived **refresh token** (opaque string, 30-day expiry)
5. On failure: increment `failed_login_attempts`. If it reaches 5, set `locked_until` to `now + 15 minutes`
6. Client stores both tokens in `SecureStorage` and attaches the access token to every API request
7. When the access token expires, the client sends the refresh token to `POST /v1/auth/refresh` to get a new pair
8. All endpoints except `/v1/auth/*` and `/v1/version` require a valid access token (`[Authorize]`)

### JWT Configuration

The API signs its own JWTs using a symmetric key stored as an environment variable. No external issuer or authority.

```csharp
// Program.cs
var jwtKey = builder.Configuration["JWT_SECRET_KEY"];
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "workhub-api",
            ValidateAudience = true,
            ValidAudience = "workhub-app",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
```

### Request Body Size Limit

Enforce a 50MB maximum request body size to match the client-side photo size check and prevent abuse:

```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});
```

This applies globally. All photo upload endpoints accept `multipart/form-data` within this limit. Requests exceeding 50MB receive a `413 Payload Too Large` response.

**NuGet packages:**
```
dotnet add package BCrypt.Net-Next
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

### AuthService

```csharp
// Services/AuthService.cs
public class AuthService
{
    private readonly WorkHubDbContext _db;
    private readonly IConfiguration _config;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(WorkHubDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResult> AttemptLoginAsync(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
            return LoginResult.Failed("Invalid email or password");

        // Check lockout
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTimeOffset.UtcNow)
        {
            var remaining = (int)(user.LockedUntil.Value - DateTimeOffset.UtcNow).TotalMinutes + 1;
            return LoginResult.Failed($"Account locked. Try again in {remaining} minute{(remaining == 1 ? "" : "s")}.");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0; // Reset counter; the lock is now active
            }
            await _db.SaveChangesAsync();
            return LoginResult.Failed("Invalid email or password");
        }

        // Success — reset lockout state
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();

        var accessToken = GenerateAccessToken(user);
        var (refreshToken, expiresAt) = await GenerateRefreshTokenAsync(user.Id);

        return LoginResult.Succeeded(accessToken, refreshToken, expiresAt, user);
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["JWT_SECRET_KEY"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name)
        };

        var token = new JwtSecurityToken(
            issuer: "workhub-api",
            audience: "workhub-app",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string token, DateTimeOffset expiresAt)> GenerateRefreshTokenAsync(Guid userId)
    {
        // Generate a cryptographically random refresh token
        var tokenBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);

        // Store the hash, not the plaintext
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToBase64String(hash);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();
        return (token, expiresAt);
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToBase64String(hash);

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.ExpiresAt > DateTimeOffset.UtcNow);

        return stored;
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored != null)
        {
            _db.RefreshTokens.Remove(stored);
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        _db.RefreshTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync();
    }
}
```

### Refresh Token Cleanup

A background service runs once daily to purge expired refresh tokens from the database. Without this, the `refresh_tokens` table accumulates dead rows over time.

```csharp
// Services/TokenCleanupService.cs
public class TokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkHubDbContext>();

                var deleted = await db.RefreshTokens
                    .Where(rt => rt.ExpiresAt <= DateTimeOffset.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    _logger.LogInformation("Cleaned up {Count} expired refresh tokens", deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during refresh token cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddHostedService<TokenCleanupService>();
```

### Getting the Current User in Controllers

All authenticated controllers read the user ID from the JWT claims. A helper extension method keeps this clean:

```csharp
// Extensions/ClaimsPrincipalExtensions.cs
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(claim!);
    }
}

// Usage in any controller
var userId = User.GetUserId();
```

---

## API Versioning

All routes are prefixed with `/v1/` (e.g. `/v1/customers`, `/v1/jobs`). The version is part of the URL rather than a header — simpler to work with in MAUI's `HttpClient`.

A dedicated health/version endpoint allows the client to check compatibility on launch:

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/v1/version` | None | Returns current API version and minimum supported app version |

**Response:**
```json
{
  "apiVersion": "1.0.0",
  "minimumAppVersion": "1.0.0"
}
```

The client compares its own version against `minimumAppVersion` on every cold start. If WorkHub version is lower than the minimum, WorkHub shows a blocking "Please update WorkHub" screen and prevents further use. This allows breaking API changes to be deployed safely — bump `minimumAppVersion` in the API config when old clients must be forced to update.

If the version check call fails (API unreachable), the client should **skip the check and proceed to login** rather than blocking. The version check protects against outdated clients, not against server outages. See the client spec for the full cold start flow.

`minimumAppVersion` is stored as an environment variable in Railway so it can be changed without a redeployment.

---

## Endpoints

### Auth

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/v1/auth/login` | None | Authenticate with email + password, receive tokens |
| POST | `/v1/auth/refresh` | None | Exchange a refresh token for a new token pair |
| POST | `/v1/auth/logout` | Bearer | Revoke the current refresh token |

**`POST /v1/auth/login` request:**
```json
{
  "email": "mike@example.com",
  "password": "my-password"
}
```

**Success response (200):**
```json
{
  "accessToken": "eyJhbG...",
  "refreshToken": "a1b2c3d4e5...",
  "expiresAt": "2026-03-02T16:00:00Z",
  "user": {
    "id": "user-uuid",
    "name": "Mike",
    "email": "mike@example.com",
    "profilePhotoUrl": "https://presigned-r2-url..."
  }
}
```

**Failure responses:**

`401` — invalid credentials:
```json
{
  "error": "Invalid email or password"
}
```
The error message is intentionally vague — it does not indicate whether the email exists or the password was wrong. This prevents account enumeration.

`423` — account locked:
```json
{
  "error": "Account locked. Try again in 12 minutes."
}
```
Returned when `locked_until` is in the future. The message includes an approximate time remaining.

**`POST /v1/auth/refresh` request:**
```json
{
  "refreshToken": "a1b2c3d4e5..."
}
```

**Success response (200):**
```json
{
  "accessToken": "eyJhbG...",
  "refreshToken": "f6g7h8i9j0...",
  "expiresAt": "2026-03-02T16:30:00Z"
}
```

On every successful refresh, the old refresh token is deleted and a new one is issued (**refresh token rotation**). This limits the damage if a refresh token is leaked — it can only be used once. If the client sends a refresh token that has already been used (meaning it was potentially stolen), revoke all tokens for that user as a precaution.

**Failure response (401):**
```json
{
  "error": "Invalid or expired refresh token"
}
```

**`POST /v1/auth/logout` request:**
```json
{
  "refreshToken": "a1b2c3d4e5..."
}
```
Deletes the refresh token record from the database. The access token will naturally expire within 30 minutes. Returns `204 No Content`.

### Customers

| Method | Route | Description |
|---|---|---|
| GET | `/v1/customers` | List all customers (search/filter via query params) |
| GET | `/v1/customers/{id}` | Get single customer with linked jobs |
| POST | `/v1/customers` | Create customer |
| PUT | `/v1/customers/{id}` | Update customer |
| DELETE | `/v1/customers/{id}` | Soft-delete customer (blocked if non-Complete jobs exist) |

**Query parameters for `GET /v1/customers`:**

| Param | Type | Description |
|---|---|---|
| `q` | string | Search filter — matches against `name` via `ILIKE` |
| `page` | int | Page number (default 1) |
| `pageSize` | int | Results per page (default 25, max 100) |

**Request body for `POST /v1/customers`:**
```json
{
  "name": "Martin Residence",
  "phone": "555-123-4567",
  "email": "martin@example.com",
  "address": "48 Elm St, Minneapolis, MN 55401",
  "notes": "Gate code: 4421"
}
```
Required: `name`. All other fields optional.

**Request body for `PUT /v1/customers/{id}`:**
Same shape as POST. All fields optional — only provided fields are updated. `normalized_address` is recomputed automatically if `address` is provided.

**Response for `GET /v1/customers/{id}`:**
```json
{
  "id": "abc-123",
  "name": "Martin Residence",
  "phone": "555-123-4567",
  "email": "martin@example.com",
  "address": "48 Elm St, Minneapolis, MN 55401",
  "notes": "Gate code: 4421",
  "createdBy": "user-uuid",
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-02-01T14:00:00Z",
  "photos": [
    { "id": "photo-uuid", "url": "https://presigned-r2-url...", "uploadedAt": "2025-01-20T09:00:00Z" }
  ],
  "jobs": [
    { "id": "job-uuid", "title": "Panel Upgrade", "status": "In Progress", "priority": "High" }
  ]
}
```

Photo `url` fields are presigned R2 URLs valid for 1 hour. The client should treat them as ephemeral and re-fetch if needed.

**Soft-delete behavior for `DELETE /v1/customers/{id}`:**
- If the customer has any jobs where `status != 'Complete'`, return `409 Conflict`:
```json
{
  "error": "Cannot delete customer with active jobs",
  "blockingJobs": [
    { "id": "job-uuid", "title": "Panel Upgrade", "status": "In Progress" }
  ]
}
```
- If all jobs are `Complete` or there are no jobs, set `deleted_at` on the customer and cascade soft-delete to all linked Complete jobs.

### Jobs

| Method | Route | Description |
|---|---|---|
| GET | `/v1/jobs` | List all jobs (filter by status, priority, customer; search via `?q=`) |
| GET | `/v1/jobs/{id}` | Get single job with photos, notes, and items |
| POST | `/v1/jobs` | Create job |
| PUT | `/v1/jobs/{id}` | Update job |
| DELETE | `/v1/jobs/{id}` | Soft-delete job |

**Query parameters for `GET /v1/jobs`:**

| Param | Type | Description |
|---|---|---|
| `q` | string | Search filter — matches against `title` and `scope_notes` via `ILIKE` |
| `status` | string | Filter by status: `Pending`, `In Progress`, `Complete` |
| `priority` | string | Filter by priority: `Low`, `Normal`, `High` |
| `customerId` | uuid | Filter jobs for a specific customer |
| `page` | int | Page number (default 1) |
| `pageSize` | int | Results per page (default 25, max 100) |

**Request body for `POST /v1/jobs`:**
```json
{
  "customerId": "customer-uuid",
  "title": "Panel Upgrade",
  "status": "Pending",
  "priority": "Normal",
  "scopeNotes": "Replace main panel and add 20A circuits for new equipment"
}
```
Required: `customerId`, `title`. `status` defaults to `Pending`, `priority` defaults to `Normal` if omitted.

**Request body for `PUT /v1/jobs/{id}`:**
Same shape as POST minus `customerId` (a job's customer cannot be reassigned). All fields optional — only provided fields are updated.

**Response for `GET /v1/jobs/{id}`:**
```json
{
  "id": "job-uuid",
  "customerId": "customer-uuid",
  "customerName": "Martin Residence",
  "customerAddress": "48 Elm St, Minneapolis, MN 55401",
  "title": "Panel Upgrade",
  "status": "In Progress",
  "priority": "High",
  "scopeNotes": "Replace main panel...",
  "createdBy": "user-uuid",
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-02-01T14:00:00Z",
  "photos": [
    { "id": "photo-uuid", "url": "https://presigned-r2-url...", "uploadedAt": "..." }
  ],
  "notes": [
    { "id": "note-uuid", "content": "Started demolition", "createdBy": "user-uuid", "createdByName": "Mike", "createdAt": "2025-01-16T08:00:00Z", "updatedAt": null },
    { "id": "note-uuid", "content": "Waiting on permit approval", "createdBy": "user-uuid", "createdByName": "Sarah", "createdAt": "2025-01-20T14:00:00Z", "updatedAt": null }
  ],
  "usedItems": [
    { "id": "ji-uuid", "inventoryItemId": "inv-uuid", "name": "20A Breaker", "partNumber": "BR-20A", "quantity": 4, "source": "library" }
  ],
  "toOrderItems": [
    { "id": "ji-uuid", "inventoryItemId": null, "name": "Custom bracket", "description": "For the new panel mount", "quantity": 2, "source": "adhoc" }
  ]
}
```

**Sort orders within the job detail response:**
- `photos`: ordered by `uploaded_at DESC` (newest first)
- `notes`: ordered by `created_at ASC` (oldest first — reads as a chronological log)
- `usedItems` and `toOrderItems`: ordered by `created_at ASC`

The `usedItems` and `toOrderItems` arrays combine both library-linked and ad-hoc items. The `source` field (`"library"` or `"adhoc"`) tells the client which update/delete endpoint to use.

### Job Notes

| Method | Route | Description |
|---|---|---|
| GET | `/v1/jobs/{id}/notes` | List all notes for a job (ordered by `created_at ASC`) |
| POST | `/v1/jobs/{id}/notes` | Add a note to a job |
| PUT | `/v1/jobs/{id}/notes/{noteId}` | Update a note's content |
| DELETE | `/v1/jobs/{id}/notes/{noteId}` | Delete a note (hard delete) |

**Request body for `POST /v1/jobs/{id}/notes`:**
```json
{
  "content": "Waiting on permit approval from the city"
}
```
`created_by` is set automatically from the JWT. Required: `content`.

**Request body for `PUT /v1/jobs/{id}/notes/{noteId}`:**
```json
{
  "content": "Permit approved — work can begin Monday"
}
```
Sets `updated_at` automatically.

**Response for note objects:**
```json
{
  "id": "note-uuid",
  "content": "Waiting on permit approval",
  "createdBy": "user-uuid",
  "createdByName": "Mike",
  "createdAt": "2025-02-01T14:00:00Z",
  "updatedAt": null
}
```

Notes are always returned in chronological order (`created_at ASC`) — oldest first, newest at the bottom. This makes the notes section read like a running log of activity on the job. New notes are appended at the end.

### Photos

| Method | Route | Description |
|---|---|---|
| POST | `/v1/customers/{id}/photos` | Upload a customer photo (multipart/form-data) |
| POST | `/v1/jobs/{id}/photos` | Upload a job photo (multipart/form-data) |
| DELETE | `/v1/photos/{id}` | Delete a photo (removes DB record and R2 object) |
| GET | `/v1/photos/by-address/count` | Count of photos at a normalized address (for button label) |
| GET | `/v1/photos/by-address` | All photos at an address grouped by job/customer |

All photo lists are returned ordered by `uploaded_at DESC` (newest first).

**Location photo lookup — count endpoint:**

`GET /v1/photos/by-address/count?address=48+Elm+St+Minneapolis&excludeCustomerId={id}&excludeJobId={id}`

Returns a plain integer. The API normalizes the query address server-side and matches against `address_tag` on both `customer_photos` and `job_photos`. The `excludeCustomerId` and `excludeJobId` parameters ensure the current context's own photos are excluded so the button only shows *other* photos at that location. If count is zero, the client hides the button entirely.

**Response:** `4`

**Location photo lookup — full endpoint:**

`GET /v1/photos/by-address?address=48+Elm+St+Minneapolis&excludeCustomerId={id}&excludeJobId={id}`

Returns all photos at the address grouped by customer and job, excluding the current context. Groups are ordered by most recent `uploaded_at` descending.

**Response:**
```json
[
  {
    "groupLabel": "HVAC Unit Replacement — Martin Residence",
    "jobId": "abc-123",
    "customerId": "def-456",
    "customerName": "Martin Residence",
    "jobTitle": "HVAC Unit Replacement",
    "photos": [
      { "id": "photo-uuid", "url": "https://presigned-r2-url...", "uploadedAt": "2025-02-15T09:00:00Z" }
    ]
  }
]
```

**Upload flow:**
1. Client captures or picks a photo, compresses it client-side, and POSTs it to the API as `multipart/form-data`
2. API uploads the bytes to R2 and gets back the object key
3. API normalizes the customer's address and stores it as `address_tag` on the photo record
4. API writes the DB record with the object key and address tag in the same operation
5. API generates a presigned URL for the new photo and returns it to the client

This is a single atomic operation — if the R2 upload fails, nothing is written to the DB and an error is returned to the client.

**Upload response:**
```json
{
  "id": "photo-uuid",
  "url": "https://presigned-r2-url...",
  "uploadedAt": "2025-02-15T09:00:00Z"
}
```

**Address normalization (server-side):**
```csharp
public static string NormalizeAddress(string address)
{
    if (string.IsNullOrWhiteSpace(address)) return string.Empty;
    // Lowercase, remove punctuation, collapse whitespace
    var normalized = address.ToLowerInvariant();
    normalized = Regex.Replace(normalized, @"[^\w\s]", " ");
    normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
    // Normalize common abbreviations
    normalized = normalized
        .Replace(" street", " st")
        .Replace(" avenue", " ave")
        .Replace(" boulevard", " blvd")
        .Replace(" drive", " dr")
        .Replace(" road", " rd")
        .Replace(" lane", " ln")
        .Replace(" court", " ct")
        .Replace(" place", " pl")
        .Replace(" apartment", " apt")
        .Replace(" suite", " ste")
        .Replace(" north", " n")
        .Replace(" south", " s")
        .Replace(" east", " e")
        .Replace(" west", " w");
    return normalized;
}
```

**Address normalization — known limitations:**

This is a simple string normalization, not a geocoding service. It handles the most common variations (casing, punctuation, standard abbreviations) but will **not** match addresses that are phrased differently. For example, "48 Elm St Apt 2" and "48 Elm St #2" will match (punctuation is stripped), but genuinely different phrasings like omitting the city or zip code from one entry but not another will cause a mismatch.

For a 3-person team entering addresses manually, this is acceptable — the team can standardize their own entry habits. If address matching becomes unreliable, a future improvement would be to integrate a geocoding API and match on lat/long instead.

### Inventory Library

| Method | Route | Description |
|---|---|---|
| GET | `/v1/inventory` | List all library items (search via `?q=`) |
| GET | `/v1/inventory/{id}` | Get single inventory item |
| POST | `/v1/inventory` | Create library item |
| PUT | `/v1/inventory/{id}` | Update library item |
| DELETE | `/v1/inventory/{id}` | Delete library item (blocked if referenced by jobs) |

**Query parameters for `GET /v1/inventory`:**

| Param | Type | Description |
|---|---|---|
| `q` | string | Search filter — matches against `name` and `part_number` via `ILIKE` |
| `page` | int | Page number (default 1) |
| `pageSize` | int | Results per page (default 25, max 100) |

**Request body for `POST /v1/inventory`:**
```json
{
  "name": "20A Breaker",
  "description": "Standard 20-amp single-pole breaker",
  "partNumber": "BR-20A"
}
```
Required: `name`. All other fields optional.

**Request body for `PUT /v1/inventory/{id}`:**
Same shape as POST. All fields optional — only provided fields are updated.

**Delete protection for `DELETE /v1/inventory/{id}`:**
Before deleting, the API checks for any `job_inventory` rows referencing this item. If found, returns `409 Conflict`:
```json
{
  "error": "Cannot delete inventory item — it is referenced by 3 job(s)",
  "referencingJobs": [
    { "id": "job-uuid", "title": "Panel Upgrade" },
    { "id": "job-uuid", "title": "Annual Service" }
  ]
}
```

### Job Items (Used & To Be Ordered)

Both lists are served from the same endpoints, filtered by `list_type` (`used` or `to_order`).

**Library-linked items:**

| Method | Route | Description |
|---|---|---|
| GET | `/v1/jobs/{id}/items?list_type=used` | Get used inventory items for a job |
| GET | `/v1/jobs/{id}/items?list_type=to_order` | Get to-be-ordered items for a job |
| POST | `/v1/jobs/{id}/items` | Add a library item to a job |
| PUT | `/v1/jobs/{id}/items/{itemId}` | Update quantity or list_type |
| DELETE | `/v1/jobs/{id}/items/{itemId}` | Remove item from job |

**Request body for `POST /v1/jobs/{id}/items`:**
```json
{
  "inventoryItemId": "inv-uuid",
  "quantity": 4,
  "listType": "used"
}
```
Required: `inventoryItemId`, `listType`. `quantity` defaults to 1 if omitted.

**Request body for `PUT /v1/jobs/{id}/items/{itemId}`:**
```json
{
  "quantity": 6,
  "listType": "to_order"
}
```
Both fields optional — only provided fields are updated. Sets `updated_at` automatically.

**Ad-hoc items:**

| Method | Route | Description |
|---|---|---|
| GET | `/v1/jobs/{id}/adhoc-items?list_type=used` | Get ad-hoc used items for a job |
| GET | `/v1/jobs/{id}/adhoc-items?list_type=to_order` | Get ad-hoc to-order items for a job |
| POST | `/v1/jobs/{id}/adhoc-items` | Add ad-hoc item |
| PUT | `/v1/jobs/{id}/adhoc-items/{itemId}` | Update ad-hoc item |
| DELETE | `/v1/jobs/{id}/adhoc-items/{itemId}` | Remove ad-hoc item |

**Request body for `POST /v1/jobs/{id}/adhoc-items`:**
```json
{
  "name": "Custom bracket",
  "description": "For the new panel mount",
  "quantity": 2,
  "listType": "used"
}
```
Required: `name`, `listType`. `quantity` defaults to 1, `description` optional.

**Request body for `PUT /v1/jobs/{id}/adhoc-items/{itemId}`:**
```json
{
  "name": "Custom bracket (revised)",
  "description": "Updated dimensions",
  "quantity": 3,
  "listType": "to_order"
}
```
All fields optional — only provided fields are updated. Sets `updated_at` automatically.

### User Profile

| Method | Route | Description |
|---|---|---|
| GET | `/v1/me` | Get current user's profile |
| PUT | `/v1/me` | Update display name |
| PUT | `/v1/me/password` | Change password |
| POST | `/v1/me/photo` | Upload profile photo (multipart/form-data) |
| DELETE | `/v1/me/photo` | Remove profile photo |

The `GET /v1/me` endpoint reads the user ID from the JWT claims — no user ID needed in the URL.

**Request body for `PUT /v1/me`:**
```json
{
  "name": "Mike Johnson"
}
```

**Request body for `PUT /v1/me/password`:**
```json
{
  "currentPassword": "old-password",
  "newPassword": "new-password"
}
```
The API verifies `currentPassword` against the stored hash before updating. Returns `400` if the current password is wrong. `newPassword` must be at least 8 characters.

On successful password change, all existing refresh tokens for the user are revoked — this forces re-login on all other devices for security.

**Response for `GET /v1/me`:**
```json
{
  "id": "user-uuid",
  "name": "Mike Johnson",
  "email": "mike@example.com",
  "profilePhotoUrl": "https://presigned-r2-url...",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

### Users

| Method | Route | Description |
|---|---|---|
| GET | `/v1/users` | List all users (for assignment pickers) |

This endpoint returns all users in the system. Used by the client to populate the user picker when assigning team members to calendar events.

**Response:**
```json
[
  {
    "id": "user-uuid",
    "name": "Mike Johnson",
    "profilePhotoUrl": "https://presigned-r2-url..."
  },
  {
    "id": "user-uuid",
    "name": "Sarah Chen",
    "profilePhotoUrl": null
  }
]
```

No pagination needed — this will only ever be a handful of users.

### Calendar Events

| Method | Route | Description |
|---|---|---|
| GET | `/v1/events` | List events (filter by date range, assigned user) |
| GET | `/v1/events/{id}` | Get single event with assignments |
| POST | `/v1/events` | Create event |
| PUT | `/v1/events/{id}` | Update event |
| DELETE | `/v1/events/{id}` | Delete event (hard delete) |
| POST | `/v1/events/{id}/assignments` | Assign a user to an event |
| DELETE | `/v1/events/{id}/assignments/{userId}` | Remove a user assignment |

**Query parameters for `GET /v1/events`:**

| Param | Type | Description |
|---|---|---|
| `from` | datetime | Start of date range (inclusive) |
| `to` | datetime | End of date range (inclusive) |
| `userId` | uuid | Filter events assigned to a specific user |

**Request body for `POST /v1/events`:**
```json
{
  "title": "Site inspection",
  "description": "Check panel installation progress",
  "startTime": "2025-03-15T09:00:00Z",
  "endTime": "2025-03-15T10:00:00Z",
  "reminderMinutes": 60,
  "customerId": "customer-uuid",
  "jobId": "job-uuid",
  "assignedUserIds": ["user-uuid-1", "user-uuid-2"]
}
```
Required: `title`, `startTime`. All other fields optional. `endTime` is nullable for deadlines. `reminderMinutes` accepts `15`, `30`, `60`, or `1440` (1 day). `assignedUserIds` creates the assignment records in the same operation — if omitted, no assignments are created.

**Request body for `PUT /v1/events/{id}`:**
Same shape as POST. All fields optional — only provided fields are updated. `assignedUserIds` is **not** included in PUT — use the assignment endpoints to manage assignments after creation.

**Request body for `POST /v1/events/{id}/assignments`:**
```json
{
  "userId": "user-uuid"
}
```

**Response for `GET /v1/events/{id}`:**
```json
{
  "id": "event-uuid",
  "title": "Site inspection",
  "description": "Check panel installation progress",
  "startTime": "2025-03-15T09:00:00Z",
  "endTime": "2025-03-15T10:00:00Z",
  "reminderMinutes": 60,
  "customerId": "customer-uuid",
  "customerName": "Martin Residence",
  "jobId": "job-uuid",
  "jobTitle": "Panel Upgrade",
  "createdBy": "user-uuid",
  "createdAt": "2025-03-01T12:00:00Z",
  "assignments": [
    { "userId": "user-uuid", "name": "Mike Johnson" },
    { "userId": "user-uuid", "name": "Sarah Chen" }
  ]
}
```

---

## Pagination

All list endpoints support pagination via `?page=` and `?pageSize=`. The response wraps results in a standard envelope:

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 142,
  "totalPages": 6
}
```

Default `pageSize` is 25. Maximum is 100. The client uses `totalCount` and `totalPages` to determine whether to show a "Load More" button or implement infinite scroll.

Endpoints that return small datasets (users, event assignments, items for a single job) return flat arrays without pagination.

---

## Key Design Decisions

- **Self-hosted JWT auth** — no external auth provider. The API validates credentials against BCrypt hashes and signs its own JWTs. This eliminates third-party dependencies and works on devices with no browser. User accounts are seeded directly in the database. Refresh tokens are stored as SHA-256 hashes and rotated on every use
- **Login lockout** — after 5 consecutive failed login attempts, the account is locked for 15 minutes. The counter resets on successful login. This prevents brute-force attacks on the public login endpoint without requiring a CAPTCHA or external rate-limiting service
- **30-minute access tokens, 30-day refresh tokens** — short-lived access tokens limit exposure if one is intercepted. Long-lived refresh tokens mean users rarely need to re-enter credentials. Refresh token rotation ensures each token can only be used once
- **Expired token cleanup** — a background `IHostedService` runs once daily and deletes expired refresh tokens. This prevents unbounded growth of the `refresh_tokens` table
- **50MB request body limit** — enforced at the Kestrel level to match the client-side photo size check and prevent abuse of upload endpoints
- **Soft deletes on customers and jobs** — all list endpoints filter `WHERE deleted_at IS NULL`. A `DELETE` request sets `deleted_at` rather than removing the row. Customer deletion is blocked if any non-Complete jobs exist — see the database spec for full cascade rules
- **Search** on customers, jobs, and inventory via a `?q=` query parameter, handled server-side with `ILIKE` in PostgreSQL
- **Sort order conventions** — photos are returned newest-first (`uploaded_at DESC`); notes are returned oldest-first (`created_at ASC`) to read as a chronological log. See the database spec for the full table of sort orders
- **Location photo lookup** — `GET /v1/photos/by-address` normalizes the address query and matches against `address_tag` on both photo tables. Returns photos grouped by customer and job, excluding the requesting context via `excludeCustomerId` and `excludeJobId` parameters. Address normalization (lowercase, strip punctuation, abbreviation expansion) happens server-side on both insert and query so matching is consistent. See "Address normalization — known limitations" above
- **Inventory delete protection** — `DELETE /inventory/{id}` first checks for any `job_inventory` rows referencing the item. If found, returns `409 Conflict` with a message indicating which jobs reference it
- **Address tag set at upload time** — the normalized address is snapshotted when the photo is uploaded. If the customer's address is later corrected, existing photo tags are not changed — the tag reflects where the photo was physically taken
- **Photo uploads proxied through the API** — client POSTs `multipart/form-data` to the API, which uploads to R2 and writes the DB record atomically. The API then returns a presigned URL
- **Presigned URLs for photo access** — R2 bucket is private. The API generates presigned URLs (1-hour expiry) when returning photo data. Photo URLs are ephemeral — the client should not cache them long-term
- **Pagination** on list endpoints via `?page=` and `?pageSize=` with a standard envelope response
- **Last-write-wins on concurrent edits** — no optimistic concurrency checks. For a 3-person internal tool the collision risk is negligible
- **No real-time/SignalR** — for 3 users, polling on app resume/refresh is sufficient
- **Job notes are separate records** — unlike customer notes (a single text field), job notes are individual timestamped records with author attribution
- **Calendar reminders stored as `reminder_minutes`** — the API stores the preference; the client schedules the local notification
- **Password change revokes all sessions** — when a user changes their password, all existing refresh tokens for that user are deleted, forcing re-login on all devices
- **Version check is non-blocking on failure** — if the API is unreachable during the cold start version check, the client skips the check and proceeds to login. The version check prevents outdated clients, not server outages

---

## Environment Variables

| Variable | Description |
|---|---|
| `DATABASE_URL` | PostgreSQL connection string (set automatically by Railway) |
| `JWT_SECRET_KEY` | Symmetric key for signing JWTs — minimum 32 characters, generated randomly. Use `openssl rand -base64 64` |
| `R2_ACCOUNT_ID` | Cloudflare account ID |
| `R2_ACCESS_KEY_ID` | R2 API token access key |
| `R2_SECRET_ACCESS_KEY` | R2 API token secret key |
| `R2_BUCKET_NAME` | R2 bucket name (`workhub-photos`) |
| `MINIMUM_APP_VERSION` | Minimum client version allowed to connect (e.g. `1.0.0`) |

**Generating a JWT secret key:**
```bash
openssl rand -base64 64
```
This produces a 64-byte random key encoded as base64. Store it in Railway's environment variables. Do not commit it to source control.

---

## Cloudflare R2 — Photo Storage

R2 is S3-compatible object storage. Photos are uploaded by the API server and stored in a **private R2 bucket** — the database holds only the resulting object key string. Photo access is controlled via presigned URLs generated by the API.

**Pricing:**
- Free tier: 10GB storage, 1M writes, 10M reads per month
- Beyond free tier: $0.015/GB/month storage, $4.50/million writes, $0.36/million reads
- Egress: always $0

For a 3-person CRM the free tier will last a long time.

**Setup:**
- Create an R2 bucket in the Cloudflare dashboard — **do not enable public access**
- Generate an API token with R2 read/write permissions
- Add credentials to Railway environment variables
- Install `AWSSDK.S3` NuGet package — R2 is fully S3-compatible

**Server-side upload:**
```csharp
var s3Client = new AmazonS3Client(
    accessKey, secretKey,
    new AmazonS3Config {
        ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true
    });

var objectKey = $"jobs/{jobId}/{Guid.NewGuid()}.jpg";

await s3Client.PutObjectAsync(new PutObjectRequest {
    BucketName = bucketName,
    Key = objectKey,
    InputStream = photoStream,
    ContentType = "image/jpeg"
});
```

**Generating presigned URLs:**
```csharp
public string GeneratePresignedUrl(string objectKey)
{
    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucketName,
        Key = objectKey,
        Expires = DateTime.UtcNow.AddHours(1)
    };
    return _s3Client.GetPreSignedURL(request);
}
```

**Object key convention:** `{entity}/{entityId}/{guid}.jpg`
Examples: `customers/abc-123/550e8400.jpg`, `jobs/def-456/f47ac10b.jpg`, `profiles/user-123/a1b2c3d4.jpg`

---

## Error Response Format

All error responses use a consistent JSON shape:

```json
{
  "error": "Human-readable error message",
  "details": { }
}
```

The `details` object is optional and varies by error type. HTTP status codes:

| Status | Usage |
|---|---|
| 400 | Validation errors (missing required fields, invalid values, bad password) |
| 401 | Missing/invalid JWT or wrong credentials on login |
| 404 | Record not found (or soft-deleted) |
| 409 | Conflict (delete blocked by dependencies) |
| 413 | Request body exceeds 50MB limit |
| 423 | Account locked due to too many failed login attempts |
| 500 | Unexpected server error |

---

## Railway Deployment

- Dockerfile or Railway's native .NET detection (auto-detects `*.csproj`)
- Environment variables set in Railway dashboard (see Environment Variables section above)
- **Use Hobby plan ($5/month)** — prevents container sleep on free tier
- PostgreSQL provisioned as a Railway service, connection string injected as `DATABASE_URL`
