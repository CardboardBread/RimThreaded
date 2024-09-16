using System;
using System.Collections.Generic;
using Verse;

namespace RimThreaded;

[Obsolete]
public abstract class JumboCell_Cache
{
    public Dictionary<Map, List<HashSet<IntVec3>[]>> positionsAwaitingAction = new Dictionary<Map, List<HashSet<IntVec3>[]>>();

    public abstract bool IsActionableObject(Map map, IntVec3 location);
}