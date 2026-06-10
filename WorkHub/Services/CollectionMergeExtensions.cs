using System.Collections.ObjectModel;

namespace WorkHub.Services;

public static class CollectionMergeExtensions
{
    /// <summary>
    /// Syncs <paramref name="target"/> to <paramref name="fresh"/> in place: removes missing
    /// items, inserts new ones, replaces changed ones, and reorders to match the fresh order.
    /// Unchanged items keep their object identity, so the UI only re-renders rows that
    /// actually changed and selection on an unchanged item survives the refresh.
    /// </summary>
    /// <param name="keyOf">Stable identity of an item (e.g. its Id).</param>
    /// <param name="unchanged">True when two items with the same key are equal for display purposes.</param>
    /// <param name="tryUpdateInPlace">
    /// Optional: given (existing, fresh) items that differ, apply the change to the existing
    /// instance and return true to avoid replacing it (for items with observable properties).
    /// </param>
    public static void MergeInto<T, TKey>(
        this ObservableCollection<T> target,
        IReadOnlyList<T> fresh,
        Func<T, TKey> keyOf,
        Func<T, T, bool> unchanged,
        Func<T, T, bool>? tryUpdateInPlace = null)
        where TKey : notnull
    {
        var freshKeys = new HashSet<TKey>(fresh.Select(keyOf));
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!freshKeys.Contains(keyOf(target[i])))
                target.RemoveAt(i);
        }

        var comparer = EqualityComparer<TKey>.Default;
        for (int i = 0; i < fresh.Count; i++)
        {
            var item = fresh[i];
            var key = keyOf(item);

            int existingIndex = -1;
            for (int j = i; j < target.Count; j++)
            {
                if (comparer.Equals(keyOf(target[j]), key))
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                target.Insert(i, item);
                continue;
            }

            if (existingIndex != i)
                target.Move(existingIndex, i);

            if (unchanged(target[i], item)) continue;
            if (tryUpdateInPlace != null && tryUpdateInPlace(target[i], item)) continue;
            target[i] = item;
        }
    }
}
