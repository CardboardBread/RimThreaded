using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    public static class DictionaryUtility
    {
        public static V ComputeValueIfAbsent<K, V>(this Dictionary<K, V> dict, K key, Func<V> absentFunc)
        {
            dict.TryComputeValueIfAbsent(key, absentFunc, out V value);
            return value;
        }

        public static bool TryComputeValueIfAbsent<K, V>(this Dictionary<K, V> dict, K key, Func<V> absentFunc, out V value)
        {
            if (dict.ContainsKey(key))
            {
                value = dict[key];
                return true;
            }
            else
            {
                value = absentFunc();
                dict[key] = value;
                return false;
            }
        }

        public static Dictionary<K, IEnumerable<V>> ToMultiDictionary<K, V>(this IEnumerable<IGrouping<K, V>> grouping)
        {
            var dict = new Dictionary<K, IEnumerable<V>>();
            foreach (var group in grouping)
            {
                dict[group.Key] = group;
            }
            return dict;
        }

        // For multi-dictionaries, creates a multi-value store if it's not present.
        public static V NewValueIfAbsent<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            dict.TryComputeValueIfAbsent(key, () => new(), out V value);
            return value;
        }
    }
}
