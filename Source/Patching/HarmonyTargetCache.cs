using Newtonsoft.Json;
using RimThreaded.Utilities;
using System.IO;
using System.Linq;
using System.Runtime.Caching;

namespace RimThreaded.Patching
{
    // Cache the results of HarmonyTargetMethods for each assembly, since they are versioned.
    // Cached results will be obnly returned for identical assembly versions; if a mod updates then the results should be regenerated.
    // Caching for a patch should be versioned by RimThreaded as well, such that if the technique to find results changes.
    // For example, RimThreaded version 1.2.3 will cache the results of Patch_EncapsulateField on Assembly-CSharp version 1.2.4 and if either changes, a new set of results must be cached.
    public static class HarmonyTargetCache
    {
        // Place reflective objects like MethodBase in here to speed up caching between expensive patches
        internal static MemoryCache inMemoryCache = new(nameof(HarmonyTargetCache));

        public static IEnumerable<MethodBase> GetCachedResultsOrFallback(string category, Assembly assembly, Func<Assembly, IEnumerable<MethodBase>> fallback)
        {
            if (!TryGetCachedResults(category, assembly, out var results))
            {
                results = fallback(assembly);
                SetCachedResults(category, assembly, results);
            }

            return results;
        }

        public static bool HasCachedResults(string category, Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException($"'{nameof(category)}' cannot be null or whitespace.", nameof(category));
            }
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var filename = GetCacheFileName(assembly);
            var filepath = GetCacheFilePath(category, filename);
            return File.Exists(filepath);
        }

        public static string GetCacheFilePath(string category, string filename)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException($"'{nameof(category)}' cannot be null or whitespace.", nameof(category));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException($"'{nameof(filename)}' cannot be null or whitespace.", nameof(filename));
            }

            return Path.Combine(RimThreadedMod.ExtrasFolderPath, category, filename);
        }

        public static string GetCacheFileName(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var name = assembly.GetName().Name;
            var version = assembly.ManifestModule.ModuleVersionId;
            return $"{name}-{version}.cache.json";
        }

        public static IEnumerable<MethodBase> GetCachedResults(string category, Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException($"'{nameof(category)}' cannot be null or whitespace.", nameof(category));
            }
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var filename = GetCacheFileName(assembly);
            var filepath = GetCacheFilePath(category, filename);
            if (!File.Exists(filepath))
            {
                return null;
            }

            var filetext = File.ReadAllText(filepath);
            if (string.IsNullOrWhiteSpace(filetext))
            {
                return null;
            }

            var fileResults = JsonConvert.DeserializeObject<IEnumerable<MemberNotation>>(filetext);
            if (fileResults is null)
            {
                return null;
            }
            if (fileResults.Count() == 0)
            {
                return Enumerable.Empty<MethodBase>();
            }

            var results = fileResults.Select(MemberNotation.ToAnyMethod);
            return results;
        }

        public static bool TryGetCachedResults(string category, Assembly assembly, out IEnumerable<MethodBase> results)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException($"'{nameof(category)}' cannot be null or whitespace.", nameof(category));
            }
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            results = GetCachedResults(category, assembly);
            return results is not null && results.Count() > 0;
        }

        public static void SetCachedResults(string category, Assembly assembly, IEnumerable<MethodBase> results)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException($"'{nameof(category)}' cannot be null or whitespace.", nameof(category));
            }
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var serializedResults = results.Select(MemberNotation.FromAnyMethod);

            var filename = GetCacheFileName(assembly);
            var filepath = GetCacheFilePath(category, filename);

            var filetext = JsonConvert.SerializeObject(serializedResults, Formatting.Indented);
            File.WriteAllText(filepath, filetext);
        }
    }
}
