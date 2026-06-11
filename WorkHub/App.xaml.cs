using WorkHub.Services;

namespace WorkHub;

public partial class App : Application
{
	private readonly AuthService _authService;

	public App(AuthService authService, PhotoCacheService photoCache)
	{
		InitializeComponent();
		_authService = authService;
		_ = photoCache.TrimAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = new AppShell();
		var window = new Window(shell);

		shell.Loaded += async (s, e) =>
		{
			await HandleStartupAsync();
		};

		return window;
	}

	private async Task HandleStartupAsync()
	{
		try
		{
			// Don't block startup on the network: restore the local session and
			// navigate immediately. The version check runs concurrently and
			// redirects to the update page when (and only when) it says so.
			var versionTask = _authService.CheckVersionAsync();

			var hasSession = await _authService.TryRestoreSessionAsync();
			if (hasSession)
			{
				await Shell.Current.GoToAsync("//main");
			}
			else
			{
				await Shell.Current.GoToAsync("//login");
			}

			var version = await versionTask;
			if (version != null)
			{
				var currentVersion = AppInfo.VersionString;
				if (Version.TryParse(currentVersion, out var cur) &&
				    Version.TryParse(version.MinimumAppVersion, out var min) &&
				    Normalize(cur) < Normalize(min))
				{
					await Shell.Current.GoToAsync("//update");
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Startup error: {ex}");
			try
			{
				await Shell.Current.GoToAsync("//login");
			}
			catch (Exception ex2)
			{
				System.Diagnostics.Debug.WriteLine($"Navigation error: {ex2}");
			}
		}
	}

	// Version("1.0") compares less than Version("1.0.0") because missing
	// components default to -1, so pad to four components before comparing.
	private static Version Normalize(Version v) =>
		new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
