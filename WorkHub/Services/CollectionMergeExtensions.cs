using System.Collections.ObjectModel;

namespace WorkHub.Services;

public static class CollectionMergeExtensions
{
    /// <summary>
    /// Syncs <paramref name="target"/> to <paramref name="fresh"/> in place: removes missing
    /// items, inserts new ones, replaces changed ones, and reorders to match the fresh order.
    /// Unchanged items keep their object identity, so the UI only re-renders rows that
    /// actually changed and selection on an unchanged item survives the refresh.
    ///
    /// Reordering issues the minimum number of Move notifications (rows outside the longest
    /// increasing subsequence of the new order). Every collection change makes the native
    /// list do UI-thread layout work, so a naive "move each displaced row" reorder freezes
    /// the UI for seconds on large lists when one row travels far (e.g. a status resort).
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
        var freshIndexByKey = new Dictionary<TKey, int>(fresh.Count);
        for (int i = 0; i < fresh.Count; i++)
            freshIndexByKey[keyOf(fresh[i])] = i;

        // 1. Remove rows that are gone.
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!freshIndexByKey.ContainsKey(keyOf(target[i])))
                target.RemoveAt(i);
        }

        // 2. Update changed rows in place (position-independent).
        for (int i = 0; i < target.Count; i++)
        {
            var freshItem = fresh[freshIndexByKey[keyOf(target[i])]];
            if (unchanged(target[i], freshItem)) continue;
            if (tryUpdateInPlace != null && tryUpdateInPlace(target[i], freshItem)) continue;
            target[i] = freshItem;
        }

        // 3. Insert new rows and reorder survivors with minimal moves: surviving
        // rows on the longest increasing subsequence of fresh positions are
        // already relatively ordered and never move; every other row gets exactly
        // one Move, and each new row one Insert. Rows are processed back-to-front
        // and each is placed immediately before its fresh-order successor
        // (relative placement — the successor chain is already internally
        // ordered, so absolute indices don't need to be final yet).
        var sequence = new int[target.Count];
        for (int i = 0; i < target.Count; i++)
            sequence[i] = freshIndexByKey[keyOf(target[i])];
        var stable = LongestIncreasingSubsequenceValues(sequence);

        int IndexOfKey(TKey key)
        {
            for (int j = 0; j < target.Count; j++)
                if (EqualityComparer<TKey>.Default.Equals(keyOf(target[j]), key)) return j;
            return -1;
        }

        for (int i = fresh.Count - 1; i >= 0; i--)
        {
            int current = IndexOfKey(keyOf(fresh[i]));
            if (current >= 0 && stable.Contains(i)) continue;

            int anchor = i == fresh.Count - 1 ? target.Count : IndexOfKey(keyOf(fresh[i + 1]));
            if (current < 0)
            {
                target.Insert(anchor, fresh[i]);
            }
            else
            {
                int destination = current < anchor ? anchor - 1 : anchor;
                if (current != destination)
                    target.Move(current, destination);
            }
        }
    }

    // Returns the VALUES (fresh indices) forming a longest strictly increasing
    // subsequence of the input, via patience sorting with predecessor links.
    private static HashSet<int> LongestIncreasingSubsequenceValues(int[] sequence)
    {
        var result = new HashSet<int>();
        if (sequence.Length == 0) return result;

        var tailPositions = new List<int>();       // position in sequence of the smallest tail per LIS length
        var previous = new int[sequence.Length];   // predecessor position links

        for (int i = 0; i < sequence.Length; i++)
        {
            int lo = 0, hi = tailPositions.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sequence[tailPositions[mid]] < sequence[i]) lo = mid + 1;
                else hi = mid;
            }
            previous[i] = lo > 0 ? tailPositions[lo - 1] : -1;
            if (lo == tailPositions.Count) tailPositions.Add(i);
            else tailPositions[lo] = i;
        }

        for (int pos = tailPositions[^1]; pos >= 0; pos = previous[pos])
            result.Add(sequence[pos]);
        return result;
    }
}
