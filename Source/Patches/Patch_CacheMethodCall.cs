using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patches
{
    // TODO: use `object[] __args` in a harmony prefix as a key to cache the result of some method executing.
    //       Maybe depending on some user info about how some method should be cached, the cache can invalidate every tick or n ticks.
    public static class Patch_CacheMethodCall
    {
    }
}
