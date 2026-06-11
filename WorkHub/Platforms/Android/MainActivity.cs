using Android.App;
using Android.Content.PM;
using Android.OS;

namespace WorkHub;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // MauiAppCompatActivity swaps the theme during base.OnCreate; layer the
        // override on top of whatever it installed.
        Theme?.ApplyStyle(Resource.Style.WorkHubThemeOverlay, force: true);
    }
}
