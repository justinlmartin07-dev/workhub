using WorkHub.Models;

namespace WorkHub.Controls;

public class InventoryRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item is InventoryGroupHeader ? HeaderTemplate : ItemTemplate)!;
}
