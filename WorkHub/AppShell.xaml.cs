using WorkHub.Views;

namespace WorkHub;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("main", typeof(MainLayout));
        Routing.RegisterRoute("update", typeof(UpdateRequiredPage));
        Routing.RegisterRoute("customerDetail", typeof(CustomerDetailPage));
        Routing.RegisterRoute("customerEdit", typeof(CustomerEditPage));
        Routing.RegisterRoute("jobDetail", typeof(JobDetailPage));
        Routing.RegisterRoute("jobEdit", typeof(JobEditPage));
        Routing.RegisterRoute("inventoryDetail", typeof(InventoryItemDetailPage));
        Routing.RegisterRoute("eventDetail", typeof(EventDetailPage));
        Routing.RegisterRoute("photoViewer", typeof(PhotoViewerPage));
        Routing.RegisterRoute("locationPhotos", typeof(LocationPhotosPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
        Routing.RegisterRoute("changePassword", typeof(ChangePasswordPage));
    }
}
