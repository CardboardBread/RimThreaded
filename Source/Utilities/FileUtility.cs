using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    public static class FileUtility
    {
        public static bool IsNormalFile(string path, out FileAttributes issue)
        {
            issue = default;
            var attributes = File.GetAttributes(path);

            if (!NormalFileAttributes().Any(fa => attributes.HasFlag(fa)))
            {
                var merge = NormalFileAttributes().Aggregate((l, r) => l | r);
                issue = merge;
            }

            foreach (var negative in NotNormalFileAttributes())
            {
                if (attributes.HasFlag(negative))
                {
                    issue = negative;
                    return false;
                }
            }

            return true;
        }

        internal static IEnumerable<FileAttributes> NormalFileAttributes()
        {
            yield return FileAttributes.Normal;
            yield return FileAttributes.Temporary;
            yield return FileAttributes.SparseFile;
        }

        internal static IEnumerable<FileAttributes> NotNormalFileAttributes()
        {
            yield return FileAttributes.ReadOnly;
            yield return FileAttributes.Hidden;
            yield return FileAttributes.System;
            yield return FileAttributes.Directory;
            yield return FileAttributes.Archive;
            yield return FileAttributes.Device;
            yield return FileAttributes.ReparsePoint;
            yield return FileAttributes.Compressed;
            yield return FileAttributes.Offline;
            yield return FileAttributes.Encrypted;
        }
    }
}
