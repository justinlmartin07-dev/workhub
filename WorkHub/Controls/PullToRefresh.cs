namespace WorkHub.Controls;

// Android-only pull-to-refresh. RefreshView must NOT be used on Windows —
// it breaks CollectionView scrolling (see WorkHub/CLAUDE.md) — so the wrap
// happens at runtime instead of in XAML, and compiles to a no-op elsewhere.
public static class PullToRefresh
{
    public static void Enable(DataStateView stateView)
    {
#if ANDROID
        if (stateView.Body is null or RefreshView) return;
        var refresh = new RefreshView { Content = stateView.Body };
        refresh.SetBinding(RefreshView.CommandProperty, "RefreshCommand");
        refresh.SetBinding(RefreshView.IsRefreshingProperty, "IsRefreshing"); // TwoWay by default
        stateView.Body = refresh;
#endif
    }
}
