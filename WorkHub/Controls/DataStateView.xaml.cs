namespace WorkHub.Controls;

public partial class DataStateView : ContentView
{
    public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
        nameof(IsLoading), typeof(bool), typeof(DataStateView), false, propertyChanged: OnStateChanged);

    public static readonly BindableProperty HasErrorProperty = BindableProperty.Create(
        nameof(HasError), typeof(bool), typeof(DataStateView), false, propertyChanged: OnStateChanged);

    public static readonly BindableProperty IsEmptyProperty = BindableProperty.Create(
        nameof(IsEmpty), typeof(bool), typeof(DataStateView), false, propertyChanged: OnStateChanged);

    public static readonly BindableProperty HasContentProperty = BindableProperty.Create(
        nameof(HasContent), typeof(bool), typeof(DataStateView), false, propertyChanged: OnStateChanged);

    public static readonly BindableProperty ErrorMessageProperty = BindableProperty.Create(
        nameof(ErrorMessage), typeof(string), typeof(DataStateView), string.Empty,
        propertyChanged: (b, o, n) => ((DataStateView)b).ErrorLabel.Text = n?.ToString());

    public static readonly BindableProperty EmptyMessageProperty = BindableProperty.Create(
        nameof(EmptyMessage), typeof(string), typeof(DataStateView), "No items found",
        propertyChanged: (b, o, n) => ((DataStateView)b).EmptyLabel.Text = n?.ToString());

    public static readonly BindableProperty RetryCommandProperty = BindableProperty.Create(
        nameof(RetryCommand), typeof(System.Windows.Input.ICommand), typeof(DataStateView),
        propertyChanged: (b, o, n) => ((DataStateView)b).RetryButton.Command = n as System.Windows.Input.ICommand);

    public static readonly BindableProperty BodyProperty = BindableProperty.Create(
        nameof(Body), typeof(View), typeof(DataStateView),
        propertyChanged: (b, o, n) => ((DataStateView)b).ContentArea.Content = n as View);

    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public bool HasError { get => (bool)GetValue(HasErrorProperty); set => SetValue(HasErrorProperty, value); }
    public bool IsEmpty { get => (bool)GetValue(IsEmptyProperty); set => SetValue(IsEmptyProperty, value); }
    public bool HasContent { get => (bool)GetValue(HasContentProperty); set => SetValue(HasContentProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public string EmptyMessage { get => (string)GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }
    public System.Windows.Input.ICommand RetryCommand { get => (System.Windows.Input.ICommand)GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
    public View Body { get => (View)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    public DataStateView()
    {
        InitializeComponent();
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (DataStateView)bindable;
        view.UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        LoadingView.IsVisible = IsLoading;
        ErrorView.IsVisible = !IsLoading && HasError;
        EmptyView.IsVisible = !IsLoading && !HasError && IsEmpty;
        ContentArea.IsVisible = !IsLoading && !HasError && HasContent;
    }
}
