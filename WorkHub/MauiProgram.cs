using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Platform;
using SkiaSharp.Views.Maui.Controls.Hosting;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif
using WorkHub.Handlers;
using WorkHub.Services;
using WorkHub.ViewModels;
using WorkHub.Views;

namespace WorkHub;

public static class MauiProgram
{
	public static IServiceProvider Services { get; private set; } = null!;

	// Reads the API base URL from the bundled Resources/Raw/appsettings.json.
	// Edit that file (and rebuild) to point the app at a different API.
	private static string LoadApiBaseUrl()
	{
		using var stream = Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync("appsettings.json")
			.GetAwaiter().GetResult();
		using var reader = new StreamReader(stream);
		using var doc = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd());
		return doc.RootElement.GetProperty("ApiBaseUrl").GetString()
			?? throw new InvalidOperationException("ApiBaseUrl missing from appsettings.json");
	}

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		var apiBaseUrl = LoadApiBaseUrl();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
#if WINDOWS
			// Don't draw under the Windows title bar — keeps page content (and popups)
			// from overlapping the system caption area.
			.ConfigureLifecycleEvents(events =>
			{
				events.AddWindows(windows => windows.OnWindowCreated(window =>
				{
					window.ExtendsContentIntoTitleBar = false;
				}));
			})
#endif
			.ConfigureMauiHandlers(handlers =>
			{
				// ── All platforms: press feedback (scale pulse) on every Button ──
				Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("PressFeedback", (handler, view) =>
				{
					if (view is Button button)
						ButtonPressAnimation.Attach(button);
				});

#if WINDOWS
				// ── Entry: rounded, filled, borderless ──
				Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ThemedEntry", (handler, view) =>
				{
					var native = handler.PlatformView;
					var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

					native.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
					native.Padding = new Microsoft.UI.Xaml.Thickness(14, 10, 14, 10);

					// Rounded corners
					native.Resources["TextControlCornerRadius"] = new Microsoft.UI.Xaml.CornerRadius(10);

					// Fill colors
					var bgColor = isDark
						? Windows.UI.Color.FromArgb(255, 30, 41, 59)   // SurfaceCardDark
						: Windows.UI.Color.FromArgb(255, 241, 245, 249); // Gray100
					var bgBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
					native.Resources["TextControlBackground"] = bgBrush;
					native.Resources["TextControlBackgroundPointerOver"] = bgBrush;
					native.Resources["TextControlBackgroundFocused"] = bgBrush;

					// Border on focus
					var borderColor = isDark
						? Windows.UI.Color.FromArgb(255, 45, 212, 191)  // PrimaryDark
						: Windows.UI.Color.FromArgb(255, 20, 184, 166); // Primary
					var borderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
					native.Resources["TextControlBorderBrushFocused"] = borderBrush;
					native.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(2);

					// Remove bottom highlight bar
					var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
					native.Resources["TextControlBorderBrush"] = transparentBrush;
					native.Resources["TextControlBorderBrushPointerOver"] = transparentBrush;

					// Compact entries override
					if (view is Entry entry && entry.HeightRequest > 0 && entry.HeightRequest <= 32)
					{
						native.Padding = new Microsoft.UI.Xaml.Thickness(4, 0, 4, 0);
						native.MinHeight = 0;
					}
				});

				// ── Editor: rounded, filled ──
				Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("ThemedEditor", (handler, view) =>
				{
					var native = handler.PlatformView;
					var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

					native.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
					native.Padding = new Microsoft.UI.Xaml.Thickness(14, 10, 14, 10);

					native.Resources["TextControlCornerRadius"] = new Microsoft.UI.Xaml.CornerRadius(10);

					var bgColor = isDark
						? Windows.UI.Color.FromArgb(255, 30, 41, 59)
						: Windows.UI.Color.FromArgb(255, 241, 245, 249);
					var bgBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
					native.Resources["TextControlBackground"] = bgBrush;
					native.Resources["TextControlBackgroundPointerOver"] = bgBrush;
					native.Resources["TextControlBackgroundFocused"] = bgBrush;

					var borderColor = isDark
						? Windows.UI.Color.FromArgb(255, 45, 212, 191)
						: Windows.UI.Color.FromArgb(255, 20, 184, 166);
					native.Resources["TextControlBorderBrushFocused"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
					native.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(2);

					var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
					native.Resources["TextControlBorderBrush"] = transparentBrush;
					native.Resources["TextControlBorderBrushPointerOver"] = transparentBrush;
				});

				// ── Picker (ComboBox): rounded, filled ──
				Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("ThemedPicker", (handler, view) =>
				{
					var native = handler.PlatformView;
					var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

					native.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(6);
					native.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
					native.Padding = new Microsoft.UI.Xaml.Thickness(14, 8, 14, 8);

					var bgColor = isDark
						? Windows.UI.Color.FromArgb(255, 30, 41, 59)
						: Windows.UI.Color.FromArgb(255, 241, 245, 249);
					var bgBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
					native.Resources["ComboBoxBackground"] = bgBrush;
					native.Resources["ComboBoxBackgroundPointerOver"] = bgBrush;
					native.Resources["ComboBoxBackgroundPressed"] = bgBrush;
					native.Resources["ComboBoxBackgroundFocused"] = bgBrush;

					var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
					native.Resources["ComboBoxBorderBrush"] = transparentBrush;
					native.Resources["ComboBoxBorderBrushPointerOver"] = transparentBrush;
					native.Resources["ComboBoxBorderBrushPressed"] = transparentBrush;
				});

				// ── DatePicker: rounded, filled ──
				Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("ThemedDatePicker", (handler, view) =>
				{
					var native = handler.PlatformView;
					var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

					native.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(10);
					native.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);

					var bgColor = isDark
						? Windows.UI.Color.FromArgb(255, 30, 41, 59)
						: Windows.UI.Color.FromArgb(255, 241, 245, 249);
					var bgBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
					native.Background = bgBrush;
				});

				// ── TimePicker: rounded, filled ──
				Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("ThemedTimePicker", (handler, view) =>
				{
					var native = handler.PlatformView;
					var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

					native.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(10);
					native.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);

					var bgColor = isDark
						? Windows.UI.Color.FromArgb(255, 30, 41, 59)
						: Windows.UI.Color.FromArgb(255, 241, 245, 249);
					native.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
				});

				// ── Button: compact override ──
				Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("CompactButton", (handler, view) =>
				{
					if (view is Button button && button.HeightRequest > 0 && button.HeightRequest <= 32)
					{
						var native = handler.PlatformView;
						native.Padding = new Microsoft.UI.Xaml.Thickness(0);
						native.MinHeight = 0;
						native.MinWidth = 0;
						native.Height = button.HeightRequest;
						native.Width = button.WidthRequest;
						if (native.Content is Microsoft.UI.Xaml.FrameworkElement content)
						{
							content.Margin = new Microsoft.UI.Xaml.Thickness(0);
						}
						native.Resources["ButtonPadding"] = new Microsoft.UI.Xaml.Thickness(0);
					}
				});

				// ── Button: corner radius ──
				Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("CornerRadius6", (handler, view) =>
				{
					handler.PlatformView.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(6);
				});

				// ── SearchBar: rounded, filled ──
				Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("ThemedSearchBar", (handler, view) =>
				{
					var native = handler.PlatformView;
					var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

					native.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(2);

					var bgColor = isDark
						? Windows.UI.Color.FromArgb(255, 30, 41, 59)
						: Windows.UI.Color.FromArgb(255, 241, 245, 249);
					var bgBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
					native.Resources["AutoSuggestBoxBackground"] = bgBrush;

					var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
					native.Resources["AutoSuggestBoxBorderBrush"] = transparentBrush;
				});

				// ── MenuFlyoutItem: render icons in their real color ──
				// WinUI shows a MenuFlyoutItem's icon as monochrome, tinted to the menu
				// foreground (white on a light menu) — which makes our gray icons nearly
				// invisible. Turn monochrome off so the actual pixels show.
				Microsoft.Maui.Handlers.MenuFlyoutItemHandler.Mapper.AppendToMapping("ColorIcon", (handler, view) =>
				{
					var native = handler.PlatformView;

					static void Apply(Microsoft.UI.Xaml.Controls.MenuFlyoutItem item)
					{
						if (item.Icon is Microsoft.UI.Xaml.Controls.BitmapIcon bitmap)
							bitmap.ShowAsMonochrome = false;
					}

					Apply(native);
					// The icon is assigned asynchronously once the image source resolves,
					// so re-apply whenever the Icon property changes.
					native.RegisterPropertyChangedCallback(
						Microsoft.UI.Xaml.Controls.MenuFlyoutItem.IconProperty,
						(s, _) => { if (s is Microsoft.UI.Xaml.Controls.MenuFlyoutItem mfi) Apply(mfi); });
				});

				// ── Frame: increase corner radius globally ──
				Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping("ThemedFrame", (handler, view) =>
				{
					// Frame corner radius is handled in XAML, no native override needed
				});

				// ── Border: set horizontal-resize cursor when StyleId="ResizeHorizontal" ──
				Microsoft.Maui.Handlers.BorderHandler.Mapper.AppendToMapping("ResizeCursor", (handler, view) =>
				{
					if (view is Border b && b.StyleId == "ResizeHorizontal" && handler.PlatformView is not null)
					{
						var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
						var prop = typeof(Microsoft.UI.Xaml.UIElement).GetProperty(
							"ProtectedCursor",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						prop?.SetValue(handler.PlatformView, cursor);
					}
				});
#endif

#if ANDROID
				// ── Entry: remove underline, rounded fill ──
				// Appended to the Background key (not a custom key) so it re-applies
				// after MAUI's own background mapper, which otherwise wipes the shape
				// when the implicit style's BackgroundColor lands.
				Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Microsoft.Maui.IView.Background), (handler, view) =>
				{
					var native = handler.PlatformView;

					// Stepper quantity cells: flat fill in the standard input colors —
					// skip the rounded input shape (whose padding would crush the
					// tiny cell). Set natively; a XAML BackgroundColor does not
					// survive on the EditText here.
					if (view is Microsoft.Maui.Controls.Entry { StyleId: "stepper-qty" })
					{
						native.BackgroundTintList = null;
						native.SetBackgroundColor(ThemedColor("SurfaceInputLight", "SurfaceInputDark"));
						native.SetPadding(0, 0, 0, 0);
						return;
					}

					// Clear any tint — a transparent tint would render the custom
					// background drawable below fully invisible.
					native.BackgroundTintList = null;

					// Rounded filled background
					var bgColor = ThemedColor("SurfaceInputLight", "SurfaceInputDark");

					var shape = new Android.Graphics.Drawables.GradientDrawable();
					shape.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
					shape.SetCornerRadius(28f); // ~10dp
					shape.SetColor(bgColor);
					var strokeColor = ThemedColor("SurfaceBorderLight", "SurfaceBorderDark");
					shape.SetStroke(3, strokeColor); // ~1dp
					native.Background = shape;
					native.SetPadding(40, 24, 40, 24);
				});

				// ── Editor: remove underline, rounded fill ──
				Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(nameof(Microsoft.Maui.IView.Background), (handler, view) =>
				{
					var native = handler.PlatformView;

					native.BackgroundTintList = null;

					var bgColor = ThemedColor("SurfaceInputLight", "SurfaceInputDark");

					var shape = new Android.Graphics.Drawables.GradientDrawable();
					shape.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
					shape.SetCornerRadius(28f);
					shape.SetColor(bgColor);
					var strokeColor = ThemedColor("SurfaceBorderLight", "SurfaceBorderDark");
					shape.SetStroke(3, strokeColor); // ~1dp
					native.Background = shape;
					native.SetPadding(40, 24, 40, 24);
				});

				// ── Picker: remove underline, rounded fill ──
				Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping(nameof(Microsoft.Maui.IView.Background), (handler, view) =>
				{
					var native = handler.PlatformView;

					native.BackgroundTintList = null;

					var bgColor = ThemedColor("SurfaceInputLight", "SurfaceInputDark");

					var shape = new Android.Graphics.Drawables.GradientDrawable();
					shape.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
					shape.SetCornerRadius(28f);
					shape.SetColor(bgColor);
					var strokeColor = ThemedColor("SurfaceBorderLight", "SurfaceBorderDark");
					shape.SetStroke(3, strokeColor); // ~1dp
					native.Background = shape;
					native.SetPadding(40, 24, 40, 24);
				});

				// ── SearchBar: remove underline ──
				Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("ThemedSearchBar", (handler, view) =>
				{
					var native = handler.PlatformView;
					// Walk for the inner EditText — AppCompat resource IDs aren't exposed to consumers
					var searchEditText = FindFirstChildOfType<Android.Widget.EditText>(native);
					if (searchEditText != null)
					{
						searchEditText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
							Android.Graphics.Color.Transparent);
					}
				});
#endif
			});

		// Auth handler
		builder.Services.AddTransient<AuthDelegatingHandler>();

		// HttpClients
		builder.Services.AddHttpClient("AuthClient", client =>
		{
			client.BaseAddress = new Uri(apiBaseUrl);
			client.DefaultRequestHeaders.Add("Accept", "application/json");
		});

		builder.Services.AddHttpClient("ApiClient", client =>
		{
			client.BaseAddress = new Uri(apiBaseUrl);
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.Timeout = TimeSpan.FromSeconds(30);
		}).AddHttpMessageHandler<AuthDelegatingHandler>();

		// Services
		builder.Services.AddSingleton<ListCacheService>();
		builder.Services.AddSingleton<PhotoCacheService>();
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<ApiService>();
		builder.Services.AddSingleton<PhotoService>();
		builder.Services.AddSingleton<LocationBiasService>();

		// ViewModels
		// List VMs are singletons: their data is preloaded at launch (MainLayout)
		// and survives page re-creation, so opening a tab shows content instantly.
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<MainLayoutViewModel>();
		builder.Services.AddSingleton<CustomersListViewModel>();
		builder.Services.AddTransient<CustomerDetailViewModel>();
		builder.Services.AddTransient<CustomerEditViewModel>();
		builder.Services.AddSingleton<JobsListViewModel>();
		builder.Services.AddTransient<JobDetailViewModel>();
		builder.Services.AddTransient<JobEditViewModel>();
		builder.Services.AddSingleton<InventoryViewModel>();
		builder.Services.AddTransient<InventoryItemDetailViewModel>();
		builder.Services.AddSingleton<CalendarViewModel>();
		builder.Services.AddSingleton<OrdersViewModel>();
		builder.Services.AddTransient<OrderDetailViewModel>();
		builder.Services.AddTransient<EventDetailViewModel>();
		builder.Services.AddTransient<CalendarDaySummaryViewModel>();
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
		builder.Services.AddTransient<OrdersPage>();
		builder.Services.AddTransient<OrderDetailPage>();
		builder.Services.AddTransient<EventDetailPage>();
		builder.Services.AddTransient<CalendarDaySummaryPage>();
		builder.Services.AddTransient<PhotoViewerPopup>();
		builder.Services.AddTransient<LocationPhotosPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<ChangePasswordPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		Services = app.Services;
		return app;
	}

#if ANDROID
	// Resolve a themed color from the app resource dictionary so native handler
	// styling follows Colors.xaml instead of duplicating values.
	private static Android.Graphics.Color ThemedColor(string lightKey, string darkKey)
	{
		var key = Application.Current?.RequestedTheme == AppTheme.Dark ? darkKey : lightKey;
		if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
			return color.ToPlatform();
		return Android.Graphics.Color.Transparent;
	}

	private static T? FindFirstChildOfType<T>(Android.Views.View view) where T : Android.Views.View
	{
		if (view is T match) return match;
		if (view is Android.Views.ViewGroup group)
		{
			for (int i = 0; i < group.ChildCount; i++)
			{
				var child = group.GetChildAt(i);
				if (child == null) continue;
				var found = FindFirstChildOfType<T>(child);
				if (found != null) return found;
			}
		}
		return null;
	}
#endif
}
