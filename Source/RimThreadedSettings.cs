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
        public const int MinThreadCount = 1;
        public const int MaxThreadCount = 128;
        public const int MinTimeoutMilliseconds = 5000;
        public const int MaxTimeoutMilliseconds = 1000000;
        public const int InfiniteTimeoutMilliseconds = System.Threading.Timeout.Infinite;

        public const int DefaultTimeoutMilliseconds = 8000;
        public const float DefaultTimeSpeedNormal = 1f;
        public const float DefaultTimeSpeedFast = 3f;
        public const float DefaultTimeSpeedSuperfast = 6f;
        public const float DefaultTimeSpeedUltrafast = 15f;
        public const bool DefaultDisableSomeAlerts = false;
        public const bool DefaultDisableForcedSlowdowns = false;

        public static int DefaultMaxThreads => SystemInfo.processorCount;
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
        private bool disableForcedSlowdowns; // TODO: this already exists in vanilla debug mode under "View Settings"
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
        }

        public void DoWindowContents(Rect inRect)
        {
            var viewRect = new Rect(x: 0f, y: 0f, width: inRect.width - 16f, height: 1200f);
            Widgets.BeginScrollView(inRect, ref mainScrollPos, viewRect);
            var listingStd = new Listing_Standard();
            listingStd.Begin(viewRect);

            Widgets.Label(listingStd.GetRect(25f), "Total worker threads (recommendation 1-2 per CPU core):");
            Widgets.IntEntry(listingStd.GetRect(37f), ref maxThreads, ref maxThreadsEditBuffer);

            Widgets.Label(listingStd.GetRect(25f), "Timeout (in miliseconds) waiting for threads (default: 8000):");
            Widgets.IntEntry(listingStd.GetRect(37f), ref timeoutMilliseconds, ref timeoutMillisecondsEditBuffer, 100);

            Widgets.Label(listingStd.GetRect(25f), "Timespeed Normal (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(30f), ref timeSpeedNormal, ref timeSpeedNormalEditBuffer);

            Widgets.Label(listingStd.GetRect(25f), "Timespeed Fast (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(30f), ref timeSpeedFast, ref timeSpeedFastEditBuffer);

            Widgets.Label(listingStd.GetRect(25f), "Timespeed Superfast (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(30f), ref timeSpeedSuperfast, ref timeSpeedSuperfastEditBuffer);

            Widgets.Label(listingStd.GetRect(25f), "Timespeed Ultrafast (multiply by 60 for Max TPS):");
            Widgets.TextFieldNumeric(listingStd.GetRect(30f), ref timeSpeedUltrafast, ref timeSpeedUltrafastEditBuffer);

            Widgets.CheckboxLabeled(listingStd.GetRect(27f), "Disable alert updates at 4x speed:", ref disableSomeAlerts);

            Widgets.CheckboxLabeled(listingStd.GetRect(27f), "Disable forced slowdowns on events like combat:", ref DebugViewSettings.neverForceNormalSpeed);
            //disableForcedSlowdowns;

            if (Prefs.LogVerbose)
            {
                Widgets.CheckboxLabeled(listingStd.GetRect(27f), "Export transpiled methods on startup:", ref exportTranspiledMethods);
                Widgets.Label(listingStd.GetRect(25f), $"Transpiled methods will be exported to: {RimThreadedMod.PatchExportsFolderPath}");
            }

            Widgets.TextAreaScrollable(listingStd.GetRect(300f), RimThreadedHarmony.PatchConflictsText ?? "", ref conflictsScrollPos);

            listingStd.End();
            Widgets.EndScrollView();
        }
    }
}

