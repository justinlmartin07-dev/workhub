namespace WorkHub;

// API base URL is selected at compile time by build configuration:
//   Debug   → local API (dotnet run in WorkHub.Api, http launch profile)
//   QA      → Railway staging (deploys from the qa branch)
//   Release → Railway production
// Build with `dotnet build -c QA` etc. — no files to edit or revert.
public static class AppConfig
{
#if QA
	public const string ApiBaseUrl = "https://workhub-api-staging.up.railway.app/";
#elif DEBUG
#if ANDROID
	// Android emulator reaches the host machine via 10.0.2.2, not localhost.
	// For a physical device, replace with the dev machine's LAN IP.
	public const string ApiBaseUrl = "http://10.0.2.2:5180/";
#else
	public const string ApiBaseUrl = "http://localhost:5180/";
#endif
#else
	public const string ApiBaseUrl = "https://workhub-api-production-1baa.up.railway.app/";
#endif
}
