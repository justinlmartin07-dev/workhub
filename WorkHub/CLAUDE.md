# WorkHub MAUI Client

## Known MAUI Windows Issues

- **RefreshView breaks CollectionView scrolling** — do NOT wrap CollectionView in RefreshView on Windows. Lists get stuck and snap back to top. Lists load all pages upfront instead.
- **RemainingItemsThreshold unreliable on Windows** — don't rely on it for pagination. VMs loop through all API pages on load instead.
- **CollectionView flicker on item replace** — updating an item in an ObservableCollection causes the whole row to re-render. For quantity +/- buttons, update Entry.Text directly in code-behind and fire-and-forget the API call.
- **Calendar grid rebuild is slow** — don't rebuild the entire grid on day selection. Update border strokes directly on the old/new cells in the tap handler.
- **CarouselView is unusable on Windows** — swiping is janky and programmatic `Position` changes don't scroll backwards. PhotoViewerPopup shows a plain Image + prev/next buttons on Windows and keeps CarouselView only on Android.
- **Grouped CollectionView crashes on Windows** — `IsGrouped="True"` throws WinUI's "Value does not fall within the expected range" when groups are inserted or mutated at runtime (e.g. clearing a search filter restores filtered-out groups). Don't use IsGrouped. The inventory list renders a flat collection mixing header + item rows via a DataTemplateSelector; expand/collapse is plain row insert/remove.
- **SecureStorage.SetAsync with an empty string throws on Windows** — WinRT `DataProtectionProvider` rejects zero-length buffers with the same "Value does not fall within the expected range" COMException. Never store `value ?? ""`; use `AuthService.SetOrRemoveAsync`, which removes the key for null/empty values instead.
- **CommunityToolkit Toast/Snackbar crash the app on Windows** — this app is unpackaged (`WindowsPackageType=None`); `Toast.Make(...).Show()` needs package identity for AppNotification and dies with a stowed exception (0xc000027b) in Microsoft.UI.Xaml.dll. Use in-app feedback instead (e.g. the transient `SkuCopied` "Copied" label on OrderDetailPage).
