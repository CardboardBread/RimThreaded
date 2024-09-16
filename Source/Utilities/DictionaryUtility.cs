using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities;

public static class DictionaryUtility
{
    public static TValue ComputeValueIfAbsent<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TValue> absentFunc)
    {
        dict.TryComputeValueIfAbsent(key, absentFunc, out var value);
        return value;
    }

    public static bool TryComputeValueIfAbsent<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TValue> absentFunc, out TValue value)
    {
        if (dict.TryGetValue(key, out var getValue))
        {
            value = getValue;
            return true;
        }
        else
        {
            value = absentFunc();
            dict[key] = value;
            return false;
        }
    }

    public static Dictionary<TKey, IEnumerable<TValue>> ToMultiDictionary<TKey, TValue>(this IEnumerable<IGrouping<TKey, TValue>> grouping)
    {
        var dict = new Dictionary<TKey, IEnumerable<TValue>>();
        foreach (var group in grouping)
        {
            dict[group.Key] = group;
        }
        return dict;
    }

    // For multi-dictionaries, creates a multi-value store if it's not present.
    public static TValue NewValueIfAbsent<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        dict.TryComputeValueIfAbsent(key, () => new(), out var value);
        return value;
    }
}