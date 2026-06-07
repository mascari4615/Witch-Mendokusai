using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace WitchMendokusai
{
    public class WorldClockNetworkBridge : WMNetworkBehaviour
    {

        private readonly SyncVar<int> _syncYear = new SyncVar<int>();
        private readonly SyncVar<int> _syncSeason = new SyncVar<int>();
        private readonly SyncVar<int> _syncDay = new SyncVar<int>();
        private readonly SyncVar<int> _syncHour = new SyncVar<int>();

        // 동기6 first-use — synced 값 노출 (client 측 소비 + sync 검증용. 이전엔 아무도 안 읽어 inert).
        public int SyncedYear => _syncYear.Value;
        public int SyncedSeason => _syncSeason.Value;
        public int SyncedDay => _syncDay.Value;
        public int SyncedHour => _syncHour.Value;

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
