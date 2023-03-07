using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    public static class HarmonyExtensions
    {
    }
#pragma warning disable 649
    [Serializable]
    class Replacements
    {
        public List<ClassReplacement> ClassReplacements;
    }

    [Serializable]
    class ClassReplacement
    {
        public string ClassName;
        public bool IgnoreMissing;
        public List<ThreadStaticDetail> ThreadStatics;
    }
    [Serializable]
    class ThreadStaticDetail
    {
        public string FieldName;
        public string PatchedClassName;
        public bool SelfInitialized;
    }
#pragma warning restore 649
}
