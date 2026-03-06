using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using WorkHub.Handlers;
using WorkHub.Services;
using WorkHub.ViewModels;
using WorkHub.Views;

namespace WorkHub;

public static class MauiProgram
{
	private const string ApiBaseUrl = "http://localhost:5180/";

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Auth handler
		builder.Services.AddTransient<AuthDelegatingHandler>();

		// HttpClients
		builder.Services.AddHttpClient("AuthClient", client =>
		{
			client.BaseAddress = new Uri(ApiBaseUrl);
			client.DefaultRequestHeaders.Add("Accept", "application/json");
		});

		builder.Services.AddHttpClient("ApiClient", client =>
		{
			client.BaseAddress = new Uri(ApiBaseUrl);
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.Timeout = TimeSpan.FromSeconds(30);
		}).AddHttpMessageHandler<AuthDelegatingHandler>();

		// Services
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<ApiService>();
		builder.Services.AddSingleton<PhotoService>();

		// ViewModels
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<MainLayoutViewModel>();
		builder.Services.AddTransient<CustomersListViewModel>();
		builder.Services.AddTransient<CustomerDetailViewModel>();
		builder.Services.AddTransient<CustomerEditViewModel>();
		builder.Services.AddTransient<JobsListViewModel>();
		builder.Services.AddTransient<JobDetailViewModel>();
		builder.Services.AddTransient<JobEditViewModel>();
		builder.Services.AddTransient<InventoryViewModel>();
		builder.Services.AddTransient<InventoryItemDetailViewModel>();
		builder.Services.AddTransient<CalendarViewModel>();
		builder.Services.AddTransient<EventDetailViewModel>();
		builder.Services.AddTransient<PhotoViewerViewModel>();
		builder.Services.AddTransient<LocationPhotosViewModel>();
		builder.Services.AddTransient<ProfileViewModel>();
		builder.Services.AddTransient<ChangePasswordViewModel>();

		// Pages
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<WelcomePage>();
		builder.Services.AddTransient<UpdateRequiredPage>();
		builder.Services.AddTransient<MainLayout>();
		builder.Services.AddTransient<CustomersListPage>();
		builder.Services.AddTransient<CustomerDetailPage>();
		builder.Services.AddTransient<CustomerEditPage>();
		builder.Services.AddTransient<JobsListPage>();
		builder.Services.AddTransient<JobDetailPage>();
		builder.Services.AddTransient<JobEditPage>();
		builder.Services.AddTransient<InventoryPage>();
		builder.Services.AddTransient<InventoryItemDetailPage>();
		builder.Services.AddTransient<CalendarPage>();
		builder.Services.AddTransient<EventDetailPage>();
		builder.Services.AddTransient<PhotoViewerPage>();
		builder.Services.AddTransient<LocationPhotosPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<ChangePasswordPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
