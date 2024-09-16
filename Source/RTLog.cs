using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace RimThreaded;

public static class RTLog
{
    internal static string PrependModIdentifier(string text) => $"[{RimThreadedMod.Instance.SettingsCategory()}] : {text}";

    public static void Message(string text) => Log.Message(PrependModIdentifier(text));

    public static void Warning(string text) => Log.Warning(PrependModIdentifier(text));

    public static void WarningOnce(string text, int key) => Log.WarningOnce(PrependModIdentifier(text), key);

    public static void Error(string text) => Log.Error(PrependModIdentifier(text));

    public static void ErrorOnce(string text, int key) => Log.ErrorOnce(PrependModIdentifier(text), key);

    public static void HarmonyDebugMessage(string message) => SysDebug.WriteLineIf(Harmony.DEBUG, PrependModIdentifier(message));
}