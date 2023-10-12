using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    public class TickChangeMonitor : ChangeMonitor
    {
        public class TickMonitor : GameComponent
        {
            private Dictionary<int, TickChangeMonitor>
            private int _lastTick;

            public override void ExposeData()
            {
            }

            public override void FinalizeInit()
            {
            }

            public override void GameComponentTick()
            {
                try
                {
                    var currentTick = Find.TickManager.TicksGame;

                }
                finally
                {
                    _lastTick = Find.TickManager.TicksGame;
                }
            }

            public override void LoadedGame()
            {
            }

            public override void StartedNewGame()
            {
            }
        }

        public enum Expiry
        {
            NextTick = default,
            ExactTick,
            TickOffset,
        }

        [Flags]
        public enum Options
        {

        }

        private Expiry _expiry;
        private long _tickValue;

        public TickChangeMonitor(Expiry expiry = default, long tickValue = 1)
        {
            if (tickValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickValue));
            }

            _expiry = expiry;
            _tickValue = tickValue;
        }

        public override string UniqueId => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {

            throw new NotImplementedException();
        }
    }
}
