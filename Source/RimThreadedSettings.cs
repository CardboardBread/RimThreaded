using System;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RimThreaded.Utilities;

namespace RimThreaded
{
    // Class for handling/modifying mutable options of RimThreaded's operation.
    public class RimThreadedSettings : ModSettings
    {
        internal const string TweakValueCategory = nameof(RimThreadedSettings);

        public const int MinThreadCount = 1;
        public const int MaxThreadCount = 128;
        public const int MinTimeoutMilliseconds = 5000;
        public const int MaxTimeoutMilliseconds = 1000000;
        public const int InfiniteTimeoutMilliseconds = System.Threading.Timeout.Infinite;

        public static int DefaultMaxThreads => SystemInfo.processorCount;
        public const int DefaultTimeoutMilliseconds = 8000;
        public const float DefaultTimeSpeedNormal = 1f;
        public const float DefaultTimeSpeedFast = 3f;
        public const float DefaultTimeSpeedSuperfast = 6f;
        public const float DefaultTimeSpeedUltrafast = 15f;
        public const bool DefaultDisableSomeAlerts = false;
        public const bool DefaultDisableForcedSlowdowns = false;
        public const bool DefaultExportTranspiledMethods = false;

        [TweakValue(TweakValueCategory)] private static float LabelHeight = 25f;
        [TweakValue(TweakValueCategory)] private static float IntEntryHeight = 37f;
        [TweakValue(TweakValueCategory)] private static float TextFieldNumericHeight = 30f;
        [TweakValue(TweakValueCategory)] private static float CheckboxLabeledHeight = 27f;
        [TweakValue(TweakValueCategory)] private static float PatchConflictsHeight = 300f;
        [TweakValue(TweakValueCategory)] private static float ViewRectHeight = 1200f;
        [TweakValue(TweakValueCategory)] private static float InRectBevel = 16f;
        [TweakValue(TweakValueCategory)] private static int TimeoutMillisecondsMultiplier = 100;

        public static RimThreadedSettings Instance => RimThreadedMod.Instance.Settings;

        private int maxThreads;
        private string maxThreadsEditBuffer;
        private int timeoutMilliseconds;
        private string timeoutMillisecondsEditBuffer;
        private float timeSpeedNormal;
        private string timeSpeedNormalEditBuffer;
        private float timeSpeedFast;
        private string timeSpeedFastEditBuffer;
        private float timeSpeedSuperfast;
        private string timeSpeedSuperfastEditBuffer;
        private float timeSpeedUltrafast;
        private string timeSpeedUltrafastEditBuffer;
        private bool disableSomeAlerts;
        private bool disableForcedSlowdowns; // TODO: this already exists in vanilla debug mode under `DebugViewSettings.neverForceNormalSpeed`
        private bool exportTranspiledMethods;

        private Vector2 mainScrollPos = Vector2.zero;
        private Vector2 conflictsScrollPos = Vector2.zero;

        public int MaxThreads
        {
            get => Math.Min(Math.Max(maxThreads, MinThreadCount), MaxThreadCount);
            private set => maxThreads = value;
        }
        public int TimeoutMilliseconds
        {
            get => Math.Min(Math.Max(timeoutMilliseconds, MinTimeoutMilliseconds), MaxTimeoutMilliseconds);
            private set => timeoutMilliseconds = value;
        }
        public TimeSpan HalfTimeoutMilliseconds => new(0, 0, 0, 0, TimeoutMilliseconds / 2);
        public float TimeSpeedNormal { get => timeSpeedNormal; private set => timeSpeedNormal = value; }
        public float TimeSpeedFast { get => timeSpeedFast; private set => timeSpeedFast = value; }
        public float TimeSpeedSuperfast { get => timeSpeedSuperfast; private set => timeSpeedSuperfast = value; }
        public float TimeSpeedUltrafast { get => timeSpeedUltrafast; private set => timeSpeedUltrafast = value; }
        public bool DisableSomeAlerts { get => disableSomeAlerts; private set => disableSomeAlerts = value; }
        public bool DisableForcedSlowdowns { get => disableForcedSlowdowns; private set => disableForcedSlowdowns = value; }
        public bool ExportTranspiledMethods { get => exportTranspiledMethods; private set => exportTranspiledMethods = value; }

        public RimThreadedSettings() : base()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxThreads, nameof(MaxThreads), DefaultMaxThreads);
            Scribe_Values.Look(ref timeoutMilliseconds, nameof(TimeoutMilliseconds), DefaultTimeoutMilliseconds);
            Scribe_Values.Look(ref timeSpeedNormal, nameof(TimeSpeedNormal), DefaultTimeSpeedNormal);
            Scribe_Values.Look(ref timeSpeedFast, nameof(TimeSpeedFast), DefaultTimeSpeedFast);
            Scribe_Values.Look(ref timeSpeedSuperfast, nameof(TimeSpeedSuperfast), DefaultTimeSpeedSuperfast);
            Scribe_Values.Look(ref timeSpeedUltrafast, nameof(TimeSpeedUltrafast), DefaultTimeSpeedUltrafast);
            Scribe_Values.Look(ref disableSomeAlerts, nameof(DisableSomeAlerts), DefaultDisableSomeAlerts);
            Scribe_Values.Look(ref disableForcedSlowdowns, nameof(DisableForcedSlowdowns), DefaultDisableForcedSlowdowns);
            Scribe_Values.Look(ref exportTranspiledMethods, nameof(ExportTranspiledMethods), DefaultExportTranspiledMethods);
        }

        public void DoWindowContents(Rect inRect)
        {
            var viewRect = new Rect(x: 0f, y: 0f, width: inRect.width - InRectBevel, height: ViewRectHeight);
            Widgets.BeginScrollView(inRect, ref mainScrollPos, viewRect);
            var listingStd = new Listing_Standard();
            listingStd.Begin(viewRect);

            Widgets.Label(listingStd.GetRect(LabelHeight), "Total worker threads (recommendation 1-2 per CPU core):");
            Widgets.IntEntry(listingStd.GetRect(IntEntryHeight), ref maxThreads, ref maxThreadsEditBuffer);

            Widgets.Label(listingStd.GetRect(LabelHeight), $"Timeout (in miliseconds) waiting for threads (default: {DefaultTimeoutMilliseconds}):");
            Widgets.IntEntry(listingStd.GetRect(IntEntryHeight), ref timeoutMilliseconds, ref timeoutMillisecondsEditBuffer, TimeoutMillisecondsMultiplier);

            Widgets.Label(listingStd.GetRect(LabelHeight), "Timespeed Normal (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(TextFieldNumericHeight), ref timeSpeedNormal, ref timeSpeedNormalEditBuffer);

            Widgets.Label(listingStd.GetRect(LabelHeight), "Timespeed Fast (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(TextFieldNumericHeight), ref timeSpeedFast, ref timeSpeedFastEditBuffer);

            Widgets.Label(listingStd.GetRect(LabelHeight), "Timespeed Superfast (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(TextFieldNumericHeight), ref timeSpeedSuperfast, ref timeSpeedSuperfastEditBuffer);

            Widgets.Label(listingStd.GetRect(LabelHeight), "Timespeed Ultrafast (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(TextFieldNumericHeight), ref timeSpeedUltrafast, ref timeSpeedUltrafastEditBuffer);

            Widgets.CheckboxLabeled(listingStd.GetRect(CheckboxLabeledHeight), "Disable alert updates at 4x speed:", ref disableSomeAlerts);

            Widgets.CheckboxLabeled(listingStd.GetRect(CheckboxLabeledHeight), "Disable forced slowdowns on events like combat:", ref DebugViewSettings.neverForceNormalSpeed);
            //disableForcedSlowdowns;

            if (Prefs.LogVerbose)
            {
                Widgets.CheckboxLabeled(listingStd.GetRect(CheckboxLabeledHeight), "Export transpiled methods on startup:", ref exportTranspiledMethods);
                Widgets.Label(listingStd.GetRect(LabelHeight), $"Transpiled methods will be exported to: {RimThreadedMod.PatchExportsFolderPath}");
            }

            Widgets.TextAreaScrollable(listingStd.GetRect(PatchConflictsHeight), RimThreadedHarmony.PatchConflictsText ?? "", ref conflictsScrollPos);

            listingStd.End();
            Widgets.EndScrollView();
        }
    }
}

