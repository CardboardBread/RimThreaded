using System;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RimThreaded.Utilities;

namespace RimThreaded;

/// <summary>
/// Class for handling/modifying mutable options of RimThreaded's operation.
/// </summary>
public class RimThreadedSettings : ModSettings
{
    public const string TweakValueCategory = nameof(RimThreadedSettings);

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

    [TweakValue(TweakValueCategory)] private static float _labelHeight = 25f;
    [TweakValue(TweakValueCategory)] private static float _intEntryHeight = 37f;
    [TweakValue(TweakValueCategory)] private static float _textFieldNumericHeight = 30f;
    [TweakValue(TweakValueCategory)] private static float _checkboxLabeledHeight = 27f;
    [TweakValue(TweakValueCategory)] private static float _patchConflictsHeight = 300f;
    [TweakValue(TweakValueCategory)] private static float _viewRectHeight = 1200f;
    [TweakValue(TweakValueCategory)] private static float _inRectBevel = 16f;
    [TweakValue(TweakValueCategory)] private static int _timeoutMillisecondsMultiplier = 100;

    public static RimThreadedSettings Instance => RimThreadedMod.Instance.Settings;

    private int _maxThreads;
    private string _maxThreadsEditBuffer;
    private int _timeoutMilliseconds;
    private string _timeoutMillisecondsEditBuffer;
    private float _timeSpeedNormal;
    private string _timeSpeedNormalEditBuffer;
    private float _timeSpeedFast;
    private string _timeSpeedFastEditBuffer;
    private float _timeSpeedSuperfast;
    private string _timeSpeedSuperfastEditBuffer;
    private float _timeSpeedUltrafast;
    private string _timeSpeedUltrafastEditBuffer;
    private bool _disableSomeAlerts;
    private bool _disableForcedSlowdowns; // TODO: this already exists in vanilla debug mode under `DebugViewSettings.neverForceNormalSpeed`
    private bool _exportTranspiledMethods;

    private Vector2 _mainScrollPos = Vector2.zero;
    private Vector2 _conflictsScrollPos = Vector2.zero;

    public int MaxThreads
    {
        get => Clamp(_maxThreads, MinThreadCount, MaxThreadCount);
        private set => _maxThreads = value;
    }
    public int TimeoutMilliseconds
    {
        get => Clamp(_timeoutMilliseconds, MinTimeoutMilliseconds, MaxTimeoutMilliseconds);
        private set => _timeoutMilliseconds = value;
    }
    public TimeSpan HalfTimeoutMilliseconds => new(0, 0, 0, 0, TimeoutMilliseconds / 2);
    public float TimeSpeedNormal { get => _timeSpeedNormal; private set => _timeSpeedNormal = value; }
    public float TimeSpeedFast { get => _timeSpeedFast; private set => _timeSpeedFast = value; }
    public float TimeSpeedSuperfast { get => _timeSpeedSuperfast; private set => _timeSpeedSuperfast = value; }
    public float TimeSpeedUltrafast { get => _timeSpeedUltrafast; private set => _timeSpeedUltrafast = value; }
    public bool DisableSomeAlerts { get => _disableSomeAlerts; private set => _disableSomeAlerts = value; }
    public bool DisableForcedSlowdowns { get => _disableForcedSlowdowns; private set => _disableForcedSlowdowns = value; }
    public bool ExportTranspiledMethods { get => _exportTranspiledMethods; private set => _exportTranspiledMethods = value; }

    public RimThreadedSettings() : base()
    {
    }

    private int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _maxThreads, nameof(MaxThreads), DefaultMaxThreads);
        Scribe_Values.Look(ref _timeoutMilliseconds, nameof(TimeoutMilliseconds), DefaultTimeoutMilliseconds);
        Scribe_Values.Look(ref _timeSpeedNormal, nameof(TimeSpeedNormal), DefaultTimeSpeedNormal);
        Scribe_Values.Look(ref _timeSpeedFast, nameof(TimeSpeedFast), DefaultTimeSpeedFast);
        Scribe_Values.Look(ref _timeSpeedSuperfast, nameof(TimeSpeedSuperfast), DefaultTimeSpeedSuperfast);
        Scribe_Values.Look(ref _timeSpeedUltrafast, nameof(TimeSpeedUltrafast), DefaultTimeSpeedUltrafast);
        Scribe_Values.Look(ref _disableSomeAlerts, nameof(DisableSomeAlerts), DefaultDisableSomeAlerts);
        Scribe_Values.Look(ref _disableForcedSlowdowns, nameof(DisableForcedSlowdowns), DefaultDisableForcedSlowdowns);
        Scribe_Values.Look(ref _exportTranspiledMethods, nameof(ExportTranspiledMethods), DefaultExportTranspiledMethods);
    }

    public void DoWindowContents(Rect inRect)
    {
        var viewRect = new Rect(x: 0f, y: 0f, width: inRect.width - _inRectBevel, height: _viewRectHeight);
        Widgets.BeginScrollView(inRect, ref _mainScrollPos, viewRect);
        var listingStd = new Listing_Standard();
        listingStd.Begin(viewRect);

        Widgets.Label(listingStd.GetRect(_labelHeight), "Total worker threads (recommendation 1-2 per CPU core):");
        Widgets.IntEntry(listingStd.GetRect(_intEntryHeight), ref _maxThreads, ref _maxThreadsEditBuffer);

        Widgets.Label(listingStd.GetRect(_labelHeight), $"Timeout (in milliseconds) waiting for threads (default: {DefaultTimeoutMilliseconds}):");
        Widgets.IntEntry(listingStd.GetRect(_intEntryHeight), ref _timeoutMilliseconds, ref _timeoutMillisecondsEditBuffer, _timeoutMillisecondsMultiplier);

        Widgets.Label(listingStd.GetRect(_labelHeight), "Timespeed Normal (multiply by 60 for Max TPS):");
        Widgets.TextFieldNumeric(listingStd.GetRect(_textFieldNumericHeight), ref _timeSpeedNormal, ref _timeSpeedNormalEditBuffer);

        Widgets.Label(listingStd.GetRect(_labelHeight), "Timespeed Fast (multiply by 60 for Max TPS):");
        Widgets.TextFieldNumeric(listingStd.GetRect(_textFieldNumericHeight), ref _timeSpeedFast, ref _timeSpeedFastEditBuffer);

        Widgets.Label(listingStd.GetRect(_labelHeight), "Timespeed Superfast (multiply by 60 for Max TPS):");
        Widgets.TextFieldNumeric(listingStd.GetRect(_textFieldNumericHeight), ref _timeSpeedSuperfast, ref _timeSpeedSuperfastEditBuffer);

        Widgets.Label(listingStd.GetRect(_labelHeight), "Timespeed Ultrafast (multiply by 60 for Max TPS):");
        Widgets.TextFieldNumeric(listingStd.GetRect(_textFieldNumericHeight), ref _timeSpeedUltrafast, ref _timeSpeedUltrafastEditBuffer);

        Widgets.CheckboxLabeled(listingStd.GetRect(_checkboxLabeledHeight), "Disable alert updates at 4x speed:", ref _disableSomeAlerts);

        Widgets.CheckboxLabeled(listingStd.GetRect(_checkboxLabeledHeight), "Disable forced slowdowns on events like combat:", ref DebugViewSettings.neverForceNormalSpeed);
        //disableForcedSlowdowns;

        if (Prefs.LogVerbose)
        {
            Widgets.CheckboxLabeled(listingStd.GetRect(_checkboxLabeledHeight), "Export transpiled methods on startup:", ref _exportTranspiledMethods);
            Widgets.Label(listingStd.GetRect(_labelHeight), $"Transpiled methods will be exported to: {RimThreadedMod.PatchExportsFolderPath}");
        }

        Widgets.TextAreaScrollable(listingStd.GetRect(_patchConflictsHeight), PatchConflictUtility.PatchConflictsText ?? "", ref _conflictsScrollPos);

        listingStd.End();
        Widgets.EndScrollView();
    }
}