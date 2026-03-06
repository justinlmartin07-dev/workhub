using WorkHub.Services;

namespace WorkHub;

public partial class App : Application
{
	private readonly AuthService _authService;

	public App(AuthService authService)
	{
		InitializeComponent();
		_authService = authService;
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
			await Task.Delay(200);

			var version = await _authService.CheckVersionAsync();
			if (version != null)
			{
				var currentVersion = AppInfo.VersionString;
				if (Version.TryParse(currentVersion, out var cur) &&
				    Version.TryParse(version.MinimumAppVersion, out var min) &&
				    cur < min)
				{
					await Shell.Current.GoToAsync("update");
					return;
				}
			}

			var hasSession = await _authService.TryRestoreSessionAsync();
			if (hasSession)
			{
				await Shell.Current.GoToAsync("main");
			}
			else
			{
				await Shell.Current.GoToAsync("login");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Startup error: {ex}");
			try
			{
				await Shell.Current.GoToAsync("login");
			}
			catch (Exception ex2)
			{
				System.Diagnostics.Debug.WriteLine($"Navigation error: {ex2}");
			}
		}
	}
}
