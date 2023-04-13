using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patching
{
    // internally group the results of a fresh `HarmonyTargetMethods` by assembly, and store each group as separate files
    // TODO: consider a meta-patch that adds this functionality to existing harmony patch classes that use `HarmonyTargetMethods`
    public class HarmonyTargetCache : IDisposable
    {
        [Serializable]
        public class HarmonyTargetGroup
        {
            public Guid GroupKey;
            public List<MethodBase> MethodBases;
        }

        public const string CacheFileExtension = "cache.json";

        public static class AssemblyIds
        {
            public static HashSet<Guid> Running;

            static AssemblyIds() // Lazy instantiation
            {
                Running = LoadedModManager.RunningMods
                    .SelectMany(modContentPack => modContentPack.assemblies.loadedAssemblies)
                    .Select(assembly => assembly.ManifestModule.ModuleVersionId)
                    .ToHashSet();
            }
        }

        protected const string AcceleratorName = $"{nameof(HarmonyTargetCache)}_{nameof(MemoryCache)}";
        protected static readonly MemoryCache Accelerator = new(AcceleratorName);

        private static string GetMethodSignature(MethodBase methodBase)
        {
            var parameters = methodBase.GetParameters().Select(p => p.ParameterType.FullName).Join();
            var enclosing = methodBase.DeclaringType.FullName;
            var name = methodBase.Name;
            return $"{enclosing}.{name}({parameters})";
        }

        private string directory;
        private bool isDisposed;

        public HarmonyTargetCache(string directory)
        {
            this.Directory = directory;

            if (Prefs.LogVerbose &&
                System.IO.Directory.Exists(FullDirectoryPath) &&
                System.IO.Directory.GetFiles(FullDirectoryPath) is string[] dirList &&
                dirList.Length > 0)
            {
                Log.Message($"Existing caches found for {directory}");
            }
        }

        public string Directory
        {
            get => directory;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException($"'{nameof(Directory)}' cannot be null or empty.", nameof(Directory));
                }

                directory = value;
            }
        }

        public string FullDirectoryPath => Path.Combine(RimThreadedMod.ExtrasFolderPath, directory);

        public void Initialize()
        {

        }

        protected string GroupFilePath(Assembly assembly, out string filename)
        {
            filename = GroupFileName(assembly);
            return Path.Combine(FullDirectoryPath, filename);
        }

        protected string GroupFilePath(Type type, out string filename) => GroupFilePath(type.Assembly, out filename);

        protected string GroupFilePath(MethodBase methodBase, out string filename) => GroupFilePath(methodBase.DeclaringType, out filename);

        protected string GroupFileName(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            var version = assembly.ManifestModule.ModuleVersionId;
            return $"{name}-{version}.{CacheFileExtension}";
        }

        protected string GroupFileName(Type type) => GroupFileName(type.Assembly);

        protected string GroupFileName(MethodBase methodBase) => GroupFileName(methodBase.DeclaringType);

        protected string GetItemKey(MethodBase methodBase)
        {
            return GetMethodSignature(methodBase);
        }

        protected Guid GetGroupKey(Assembly assembly)
        {
            return assembly.ManifestModule.ModuleVersionId;
        }

        protected Guid GetGroupKey(Type type) => GetGroupKey(type.Assembly);

        protected Guid GetGroupKey(MethodBase methodBase) => GetGroupKey(methodBase.DeclaringType);

        protected bool IsGroupExists(Assembly assembly, out string path, out string filename)
        {
            path = GroupFilePath(assembly, out filename);
            return File.Exists(path);
        }

        protected bool IsGroupExists(Type type, out string path, out string filename) => IsGroupExists(type.Assembly, out path, out filename);

        protected bool IsGroupExists(MethodBase methodBase, out string path, out string filename) => IsGroupExists(methodBase.DeclaringType, out path, out filename);

        protected HarmonyTargetGroup LoadGroupFile(Assembly assembly, out string groupFilePath, out string groupFilename)
        {
            if (!IsGroupExists(assembly, out groupFilePath, out groupFilename))
            {
                throw new FileNotFoundException();
            }

            // TODO: consider streaming the file, as it may be large and inefficient to load all at once
            // TODO: cache loaded group files
            var rawText = File.ReadAllText(groupFilePath);
            return JsonConvert.DeserializeObject<HarmonyTargetGroup>(rawText);
        }

        protected HarmonyTargetGroup LoadGroupFile(Type type, out string groupFilePath) => LoadGroupFile(type, out groupFilePath);

        protected HarmonyTargetGroup LoadGroupFile(MethodBase methodBase, out string groupFilePath) => LoadGroupFile(methodBase, out groupFilePath);

        protected void SaveGroupFile(Assembly assembly, IEnumerable<MethodBase> methodBases, out string groupFilePath)
        {
            var groupKey = GetGroupKey(assembly);
            groupFilePath = GroupFilePath(assembly, out var groupFilename);
            var filtered = methodBases.Where(methodBase => GetGroupKey(methodBase) == groupKey).ToList();
            
            var excluded = methodBases.Where(methodBase => GetGroupKey(methodBase) != groupKey).ToList();
            if (excluded.Any())
            {
                Log.Warning($"Attempted to cache foreign methods with group: {assembly}");
            }

            var groupStruct = new HarmonyTargetGroup()
            {
                GroupKey = groupKey,
                MethodBases = filtered
            };
            var groupText = JsonConvert.SerializeObject(groupStruct, Formatting.Indented);
            File.WriteAllText(groupFilePath, groupText);
        }

        protected string GetAcceleratorGroupKey(Assembly assembly)
        {
            return GetGroupKey(assembly).ToString();
        }

        protected bool IsGroupAccelerated(Assembly assembly)
        {
            var key = GetAcceleratorGroupKey(assembly);
            return Accelerator.Contains(key);
        }

        protected IEnumerable<string> GetAcceleratedItemKeysForGroup(Assembly assembly)
        {
            var key = GetAcceleratorGroupKey(assembly);
            return Accelerator.Get(key) as List<string>;
        }

        protected IEnumerable<string> BuildItemKeys(IEnumerable<MethodBase> methodBases)
        {
            return from methodBase in methodBases
                   where methodBase is not null
                   select GetItemKey(methodBase);
        }

        protected IEnumerable<(string key, MethodBase value)> BuildItemPairs(IEnumerable<MethodBase> methodBases)
        {
            return from methodBase in methodBases
                   where methodBase is not null
                   select BuildItemPair(methodBase);
        }

        protected (string key, MethodBase value) BuildItemPair(MethodBase methodBase)
        {
            var itemKey = GetItemKey(methodBase);
            return (itemKey, methodBase);
        }

        protected MethodBase GetAcceleratedItem(string itemKey)
        {
            return Accelerator.Get(itemKey) as MethodBase;
        }

        protected IEnumerable<MethodBase> GetAcceleratedGroup(Assembly assembly)
        {
            return from itemKey in GetAcceleratedItemKeysForGroup(assembly)
                   where itemKey is not null
                   select GetAcceleratedItem(itemKey) into methodBase
                   where methodBase is not null
                   select methodBase;
        }

        protected void SetAcceleratedGroup(Assembly assembly, IEnumerable<MethodBase> methodBases)
        {
            var key = GetAcceleratorGroupKey(assembly);
            var itemKeys = BuildItemKeys(methodBases);
            var itemPairs = BuildItemPairs(methodBases);

            Accelerator.Set(key, itemKeys, null);
            foreach (var (itemKey, value) in itemPairs)
            {
                Accelerator.Set(itemKey, value, null);
            }
        }

        public IEnumerable<MethodBase> GetCachedMethods(Assembly assembly)
        {
            if (IsGroupAccelerated(assembly))
            {
                return GetAcceleratedGroup(assembly);
            }

            var group = LoadGroupFile(assembly, out var groupFilePath, out var groupFilename);
            return group?.MethodBases;
        }

        public IEnumerable<MethodBase> GetCachedMethodsOrFallback(Assembly assembly, Func<IEnumerable<MethodBase>> fallback)
        {
            if (GetCachedMethods(assembly) is not IEnumerable<MethodBase> cachedMethods)
            {
                cachedMethods = fallback();
                SetCachedMethods(assembly, cachedMethods);
            }

            return cachedMethods;
        }

        public void SetCachedMethods(Assembly assembly, IEnumerable<MethodBase> methodBases)
        {
            SetAcceleratedGroup(assembly, methodBases);
            SaveGroupFile(assembly, methodBases, out _);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                isDisposed = true;
            }
        }

        ~HarmonyTargetCache()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
