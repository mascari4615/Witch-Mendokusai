using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace WitchMendokusai
{
    public class WorldClockNetworkBridge : WMNetworkBehaviour, IAuthorityAware
    {
        public Authority RequiredAuthority => Authority.Server;

        private readonly SyncVar<int> _syncYear = new SyncVar<int>();
        private readonly SyncVar<int> _syncSeason = new SyncVar<int>();
        private readonly SyncVar<int> _syncDay = new SyncVar<int>();
        private readonly SyncVar<int> _syncHour = new SyncVar<int>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            WorldClock worldClock = WorldClock.Instance;
            worldClock.OnHourChanged += OnWorldClockChanged;
            worldClock.OnDayChanged += OnWorldClockChanged;
            worldClock.OnSeasonChanged += OnWorldClockChanged;
            PushAll(worldClock);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
                return;

            worldClock.OnHourChanged -= OnWorldClockChanged;
            worldClock.OnDayChanged -= OnWorldClockChanged;
            worldClock.OnSeasonChanged -= OnWorldClockChanged;
        }

        private void OnWorldClockChanged(int changedValue)
        {
            PushAll(WorldClock.Instance);
        }

        private void PushAll(WorldClock worldClock)
        {
            _syncYear.Value = worldClock.Year;
            _syncSeason.Value = worldClock.Season;
            _syncDay.Value = worldClock.Day;
            _syncHour.Value = worldClock.Hour;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestTimeSync()
        {
            PushAll(WorldClock.Instance);
        }
    }
}
