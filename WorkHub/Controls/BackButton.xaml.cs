using System.Windows.Input;

namespace WorkHub.Controls;

public partial class BackButton : ContentView
{
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(BackButton));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public BackButton()
    {
        InitializeComponent();
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
