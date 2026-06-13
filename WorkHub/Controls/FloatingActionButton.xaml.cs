using System.Windows.Input;

namespace WorkHub.Controls;

public partial class FloatingActionButton : ContentView
{
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(FloatingActionButton));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public FloatingActionButton()
    {
        InitializeComponent();
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        // Tap gestures have no press/release phases — pulse on tap instead,
        // matching the press feedback every Button gets globally.
        _ = PulseAsync();

        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }

    private async Task PulseAsync()
    {
        await Fab.ScaleTo(0.9, 70, Easing.CubicOut);
        await Fab.ScaleTo(1.0, 180, Easing.SpringOut);
    }
}
