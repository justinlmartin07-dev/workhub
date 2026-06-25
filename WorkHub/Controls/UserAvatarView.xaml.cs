using System.Windows.Input;

namespace WorkHub.Controls;

public partial class UserAvatarView : ContentView
{
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(UserAvatarView));

    public static readonly BindableProperty UserNameProperty = BindableProperty.Create(
        nameof(UserName), typeof(string), typeof(UserAvatarView), string.Empty,
        propertyChanged: (b, _, _) => ((UserAvatarView)b).UpdateInitials());

    public static readonly BindableProperty PhotoUrlProperty = BindableProperty.Create(
        nameof(PhotoUrl), typeof(string), typeof(UserAvatarView), null,
        propertyChanged: (b, _, _) => ((UserAvatarView)b).UpdatePhoto());

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    public string? PhotoUrl
    {
        get => (string?)GetValue(PhotoUrlProperty);
        set => SetValue(PhotoUrlProperty, value);
    }

    public UserAvatarView() => InitializeComponent();

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }

    private void UpdateInitials()
    {
        var parts = (UserName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        InitialsLabel.Text = parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpper(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpper()
        };
    }

    private void UpdatePhoto()
    {
        var hasPhoto = !string.IsNullOrEmpty(PhotoUrl);
        ProfileImage.IsVisible = hasPhoto;
        if (hasPhoto) ProfileImage.Source = PhotoUrl;
    }
}
