﻿using RimThreaded.Patches.RimWorldPatches;
using RimThreaded.Patches.RimWorldPlanetPatches;
using RimThreaded.Patches.VerseAIPatches;
using RimThreaded.Patches.VersePatches;
using RimThreaded.Patches.VerseSoundPatches;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RimThreaded.Utilities
{
    internal class OldRimThreaded
    {
        internal class ThreadState
        {
            public EventWaitHandle eventWaitStart = new AutoResetEvent(false);
            public EventWaitHandle eventWaitDone = new AutoResetEvent(false);
            public int timeoutExempt = 0;
            public Thread thread;
            public object[] safeFunctionRequest;
            public object safeFunctionResult;
        }

        public static Dictionary<Bill_Production, List<Pawn>> billFreeColonistsSpawned = new Dictionary<Bill_Production, List<Pawn>>();

        public static int maxThreads => RimThreadedSettings.Instance.MaxThreads;
        public static int timeoutMS => RimThreadedSettings.Instance.TimeoutMilliseconds;
        public static TimeSpan halfTimeoutMS => new(0, 0, 0, 0, RimThreadedSettings.Instance?.TimeoutMilliseconds / 2 ?? 4000);
        public static float timeSpeedNormal => RimThreadedSettings.Instance.TimeSpeedNormal;
        public static float timeSpeedFast => RimThreadedSettings.Instance.TimeSpeedFast;
        public static float timeSpeedSuperfast => RimThreadedSettings.Instance.TimeSpeedSuperfast;
        public static float timeSpeedUltrafast => RimThreadedSettings.Instance.TimeSpeedUltrafast;
        public static DateTime lastTicksCheck = DateTime.Now;
        public static int lastTicksAbs = -1;
        public static int ticksPerSecond = 0;

        public static EventWaitHandle MainWaitHandle = new AutoResetEvent(false);
        public static EventWaitHandle MonitorWaitHandle = new AutoResetEvent(false);
        private static readonly Thread monitorThread;
        private static bool allWorkerThreadsFinished;

        public static ConcurrentQueue<Tuple<SoundDef, SoundInfo>> PlayOneShot = new ConcurrentQueue<Tuple<SoundDef, SoundInfo>>();
        public static ConcurrentQueue<Tuple<SoundDef, Map>> PlayOneShotCamera = new ConcurrentQueue<Tuple<SoundDef, Map>>();

        public static PlantHarvest_Cache plantHarvest_Cache = new PlantHarvest_Cache();
        public static PlantSowing_Cache plantSowing_Cache = new PlantSowing_Cache();

        //ThingListTicks
        public static List<Thing> thingListNormal;
        public static int thingListNormalTicks = 0;
        public static List<Thing> thingListRare;
        public static int thingListRareTicks = 0;
        public static List<Thing> thingListLong;
        public static int thingListLongTicks = 0;

        //WorldObjectsHolder
        public static int worldObjectsTicks = 0;
        public static List<WorldObject> worldObjects = null;

        //WorldPawns
        public static WorldPawns worldPawns = null;

        //FactionManager
        public static List<Faction> allFactions = null;

        public static int currentPrepsDone = -1;
        public static int workingOnDateNotifierTick = -1;
        public static int workingOnWorldTick = -1;
        public static int workingOnMapPostTick = -1;
        public static int workingOnHistoryTick = -1;
        public static int workingOnMiscellaneous = -1;

        public static TickManager callingTickManager;
        public static int listsFullyProcessed;
        public static bool dateNotifierTickComplete;
        public static bool worldTickComplete;
        public static bool mapPostTickComplete;
        public static bool historyTickComplete;
        public static bool miscellaneousComplete;

        public static Dictionary<Thread, ThreadState> allWorkerThreads = new Dictionary<Thread, ThreadState>();
        public static HashSet<int> initializedThreads = new HashSet<int>();

        public static object allSustainersLock = new object();
        //public static object map_AttackTargetReservationManager_reservations_Lock = new object();

        static RimThreadedManager()
        {
            InitializeAllThreadStatics();
            CreateWorkerThreads();
            monitorThread = new Thread(MonitorThreads) { IsBackground = true };
            monitorThread.Start();
        }

        public static void AddNormalTicking(object instance, Action<object> prepare, Action<object> tick)
        {
            Log.Message("Loading TickList: " + instance.ToString());
            threadedTickLists.Insert(2,
            new ThreadedTickList
            {
                prepareAction = () => prepare(instance),
                tickAction = () => tick(instance)
            }
            );
        }

        public static List<ThreadedTickList> threadedTickLists = new List<ThreadedTickList>()
        {
            new ThreadedTickList
            {
                prepareAction = WindManager_Patch.WindManagerPrepare,
                tickAction = WindManager_Patch.WindManagerListTick
            },
            new ThreadedTickList
            {
                prepareAction = TickList_Patch.NormalThingPrepare,
                tickAction = TickList_Patch.NormalThingTick
            },
            new ThreadedTickList
            {
                prepareAction = TickList_Patch.RareThingPrepare,
                tickAction = TickList_Patch.RareThingTick
            },
            new ThreadedTickList
            {
                prepareAction = TickList_Patch.LongThingPrepare,
                tickAction = TickList_Patch.LongThingTick
            },
            new ThreadedTickList
            {
                prepareAction = WorldPawns_Patch.WorldPawnsPrepare,
                tickAction = WorldPawns_Patch.WorldPawnsListTick
            },
            new ThreadedTickList
            {
                prepareAction = WorldObjectsHolder_Patch.WorldObjectsPrepare,
                tickAction = WorldObjectsHolder_Patch.WorldObjectsListTick
            },
            new ThreadedTickList
            {
                prepareAction = FactionManager_Patch.FactionsPrepare,
                tickAction = FactionManager_Patch.FactionsListTick
            },
            new ThreadedTickList
            {
                prepareAction = WorldComponentUtility_Patch.WorldComponentPrepare,
                tickAction = WorldComponentUtility_Patch.WorldComponentListTick
            },
            new ThreadedTickList
            {
                prepareAction = Map_Patch.MapsPostTickPrepare,
                tickAction = Map_Patch.MapPostListTick
            },
            new ThreadedTickList
            {
                prepareAction = TransportShipManager_Patch.ShipObjectsPrepare,
                tickAction = TransportShipManager_Patch.ShipObjectsTick
            },
            new ThreadedTickList
            {
                prepareAction = IdeoManager_Patch.IdeosPrepare,
                tickAction = IdeoManager_Patch.IdeosTick
            }
        };
        public static void RestartAllWorkerThreads()
        {
            foreach (Thread thread in allWorkerThreads.Keys.ToArray())
            {
                thread.Abort();
            }
            allWorkerThreads.Clear();
            CreateWorkerThreads();
        }
        private static void CreateWorkerThreads()
        {
            while (allWorkerThreads.Count < maxThreads)
            {
                ThreadState threadInfo = CreateWorkerThread();
                allWorkerThreads.Add(threadInfo.thread, threadInfo);
            }
        }
        private static ThreadState CreateWorkerThread()
        {
            ThreadState threadInfo = new ThreadState { thread = new Thread(InitializeThread) { IsBackground = true } };
            threadInfo.thread.Start(threadInfo);
            return threadInfo;
        }
        private static void InitializeThread(object threadInfo)
        {
            ThreadState ti = (ThreadState)threadInfo;
            InitializeAllThreadStatics();
            ProcessTicks(ti);
        }

        public static void InitializeAllThreadStatics()
        {



            CellFinder_Patch.InitializeThreadStatics();
            ColoredText_Patch.InitializeThreadStatics();
            CompCauseGameCondition_Patch.InitializeThreadStatics();
            Dijkstra_Patch<IntVec3>.InitializeThreadStatics();
            Dijkstra_Patch<Region>.InitializeThreadStatics();
            Dijkstra_Patch<int>.InitializeThreadStatics();
            GenTemperature_Patch.InitializeThreadStatics();
            HaulingCache.InitializeThreadStatics();
            PathFinder_Patch.InitializeThreadStatics();
            PawnPathPool_Patch.InitializeThreadStatics();
            JumboCell.InitializeThreadStatics();
            PawnTextureAtlas_Patch.InitializeThreadStatics();
            Reachability_Patch.InitializeThreadStatics();
            ReachabilityCache_Patch.InitializeThreadStatics();
            Region_Patch.InitializeThreadStatics();
            RegionAndRoomUpdater_Patch.InitializeThreadStatics();
            RegionDirtyer_Patch.InitializeThreadStatics();
            RegionTraverser_Patch.InitializeThreadStatics();
            SituationalThoughtHandler_Patch.InitializeThreadStatics();
            SustainerManager_Patch.InitializeThreadStatics();
            Toils_Ingest_Patch.InitializeThreadStatics();
            World_Patch.InitializeThreadStatics();
            WorldPawns_Patch.InitializeThreadStatics();
            AttackTargetReservationManager_Patch.InitializeThreadStatics();
            ReservationManager_Patch.InitializeThreadStatics();
            ListerThings_Patch.InitializeThreadStatics();
        }
        private static void ProcessTicks(ThreadState threadInfo)
        {
            while (true)
            {
                threadInfo.eventWaitStart.WaitOne();
                PrepareWorkLists();
                for (int loopsCompleted = listsFullyProcessed; loopsCompleted < threadedTickLists.Count; loopsCompleted++)
                {
                    threadedTickLists[loopsCompleted].prepEventWaitStart.WaitOne();
                    ExecuteTicks();
                }
                CompletePostWorkLists();
                threadInfo.eventWaitDone.Set();
            }
        }
        private static void CompletePostWorkLists()
        {
            if (Interlocked.Increment(ref workingOnDateNotifierTick) == 0)
            {
                try
                {
                    Find.DateNotifier.DateNotifierTick();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
            if (Interlocked.Increment(ref workingOnHistoryTick) == 0)
            {
                try
                {
                    Find.History.HistoryTick();
                }
                catch (Exception ex10)
                {
                    Log.Error(ex10.ToString());
                }
            }
            if (Interlocked.Increment(ref workingOnMiscellaneous) == 0)
            {
                try
                {
                    Find.Scenario.TickScenario();
                }
                catch (Exception ex2)
                {
                    Log.Error(ex2.ToString());
                }

                try
                {
                    Find.StoryWatcher.StoryWatcherTick();
                }
                catch (Exception ex4)
                {
                    Log.Error(ex4.ToString());
                }

                try
                {
                    Find.GameEnder.GameEndTick();
                }
                catch (Exception ex5)
                {
                    Log.Error(ex5.ToString());
                }

                try
                {
                    Find.Storyteller.StorytellerTick();
                }
                catch (Exception ex6)
                {
                    Log.Error(ex6.ToString());
                }

                try
                {
                    Find.TaleManager.TaleManagerTick();
                }
                catch (Exception ex7)
                {
                    Log.Error(ex7.ToString());
                }

                try
                {
                    Find.QuestManager.QuestManagerTick();
                }
                catch (Exception ex8)
                {
                    Log.Error(ex8.ToString());
                }

                try
                {
                    Find.World.WorldPostTick();
                }
                catch (Exception ex9)
                {
                    Log.Error(ex9.ToString());
                }

                GameComponentUtility.GameComponentTick();
                try
                {
                    Find.LetterStack.LetterStackTick();
                }
                catch (Exception ex11)
                {
                    Log.Error(ex11.ToString());
                }

                try
                {
                    Find.Autosaver.AutosaverTick();
                }
                catch (Exception ex12)
                {
                    Log.Error(ex12.ToString());
                }

                try
                {
                    FilthMonitor.FilthMonitorTick();
                    //FilthMonitor2.FilthMonitorTick();
                }
                catch (Exception ex13)
                {
                    Log.Error(ex13.ToString());
                }
            }
        }
        private static void PrepareWorkLists()
        {
            foreach (ThreadedTickList tickList in threadedTickLists)
            {
                if (Interlocked.Increment(ref tickList.preparing) != 0) continue;
                tickList.prepareAction();
                tickList.readyToTick = true;
                threadedTickLists[Interlocked.Increment(ref currentPrepsDone)].prepEventWaitStart.Set();
            }
        }

        private static void ExecuteTicks()
        {
            foreach (ThreadedTickList tickList in threadedTickLists)
            {
                if (!tickList.readyToTick) continue;
                tickList.tickAction();
                tickList.readyToTick = false;
                if (Interlocked.Increment(ref tickList.threadCount) == 0)
                    Interlocked.Increment(ref listsFullyProcessed);
                break;
            }
        }

        private static void MonitorThreads()
        {
            while (true)
            {
                MonitorWaitHandle.WaitOne();

                foreach (ThreadedTickList tickList in threadedTickLists)
                {
                    tickList.preparing = -1;
                    tickList.threadCount = -1;
                }
                //OneTickPools Ticks go here.
                //OneTickPool<List<Thing>>.Tick(null);
                //OneTickPool<List<Region>>.Tick(null);

                listsFullyProcessed = 0;
                workingOnDateNotifierTick = -1;
                workingOnWorldTick = -1;
                workingOnMapPostTick = -1;
                workingOnHistoryTick = -1;
                currentPrepsDone = -1;
                workingOnMiscellaneous = -1;
                dateNotifierTickComplete = false;
                worldTickComplete = false;
                mapPostTickComplete = false;
                historyTickComplete = false;
                miscellaneousComplete = false;
                foreach (ThreadState threadInfo in allWorkerThreads.Values)
                {
                    threadInfo.eventWaitStart.Set();
                }
                List<KeyValuePair<Thread, ThreadState>> threadPairs = allWorkerThreads.ToList();
                foreach (KeyValuePair<Thread, ThreadState> threadPair in threadPairs)
                {
                    ThreadState threadInfo = threadPair.Value;
                    if (!threadInfo.eventWaitDone.WaitOne(timeoutMS))
                    {
                        if (threadInfo.timeoutExempt == 0)
                        {
                            AbortThread(threadInfo, threadPair.Key);
                        }
                        else
                        {
                            if (!threadInfo.eventWaitDone.WaitOne(threadInfo.timeoutExempt))
                            {
                                AbortThread(threadInfo, threadPair.Key);
                            }
                            threadInfo.timeoutExempt = 0;
                        }
                    }
                }
                allWorkerThreadsFinished = true;
                MainWaitHandle.Set();
            }
        }

        public static void AbortThread(ThreadState threadInfo, Thread thread)
        {
            Log.Error("Thread: " + threadInfo.thread + " did not finish within " + timeoutMS + "ms. Restarting thread...");
            thread.Abort();
            allWorkerThreads.Remove(thread);
            CreateWorkerThread();
        }

        public static int frameCount = 0;


        public static void MainThreadWaitLoop(TickManager tickManager)
        {
            frameCount = Time.frameCount;
            callingTickManager = tickManager;
            allWorkerThreadsFinished = false;
            MonitorWaitHandle.Set();

            while (!allWorkerThreadsFinished)
            {
                MainWaitHandle.WaitOne();
                RespondToSafeFunctionRequests();
                MainPlayOneShot(); //TODO: is PlayOneShot section still needed?
            }
        }

        private static void MainPlayOneShot()
        {
            while (PlayOneShot.Count > 0)
            {
                if (PlayOneShot.TryDequeue(out Tuple<SoundDef, SoundInfo> s))
                {
                    s.Item1.PlayOneShot(s.Item2);
                }
            }
            while (PlayOneShotCamera.Count > 0)
            {
                if (PlayOneShotCamera.TryDequeue(out Tuple<SoundDef, Map> s))
                {
                    s.Item1.PlayOneShotOnCamera(s.Item2);
                }
            }
        }

        private static void RespondToSafeFunctionRequests()
        {
            foreach (ThreadState threadInfo in allWorkerThreads.Values)
            {
                object[] functionAndParameters = threadInfo.safeFunctionRequest;
                if (functionAndParameters == null) continue;
                object[] parameters = (object[])functionAndParameters[1];
                switch (functionAndParameters[0])
                {
                    case Func<object[], object> safeFunction:
                        threadInfo.safeFunctionResult = safeFunction(parameters);
                        break;
                    case Action<object[]> safeAction:
                        safeAction(parameters);
                        break;
                    default:
                        Log.Error("First parameter of thread-safe function request was not an action or function");
                        break;
                }
                threadInfo.safeFunctionRequest = null;
                threadInfo.eventWaitStart.Set();
            }
        }
    }
}
