using FishNet.Object;
using FishNet.Object.Synchronizing;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-191 #4 (액션 동기) first-use — 공유 가마솥 제조 채널. WorldClockNetworkBridge 동형(2번째
    /// shared-world-state 채널: WorldClock=ch1, presence=ch2, cauldron=ch3). "둘이 같은 솥에 재료 넣어
    /// 같이 끓임"(A: co-op 핵심). 채택 = *공유 솥*(둘이 한 brew 에 투입) — 서버 권위 BrewState.
    ///
    /// 서버가 권위 BrewState 보유. 클라(또는 host)가 AddIngredient ServerRpc(방향·갈기) → 서버
    /// BrewEngine.Apply(stateless 순수함수) → 마커 Position·StepCount·부작용 SyncVar → 모든 피어 관측.
    /// BrewSession 이 아닌 BrewState 직접(서버측 stateless 누적) = SyncVar 직렬화 단순(구조체 필드만).
    /// ⚠ UI(CauldronMapElement) 가 이 Synced* 를 소비해 공유 마커 그리기 = 후속 증분(step-4b).
    /// </summary>
    public class CauldronNetworkBridge : WMNetworkBehaviour
    {
        private readonly SyncVar<float> _markerX = new SyncVar<float>();
        private readonly SyncVar<float> _markerY = new SyncVar<float>();
        private readonly SyncVar<int> _stepCount = new SyncVar<int>();
        private readonly SyncVar<float> _sideEffect = new SyncVar<float>();

        // 서버측 권위 누적 상태(stateless BrewEngine.Apply 로 전진). 클라엔 SyncVar 만 도달.
        private BrewState serverState;

        public float MarkerX => _markerX.Value;
        public float MarkerY => _markerY.Value;
        public int SyncedStepCount => _stepCount.Value;
        public float SyncedSideEffect => _sideEffect.Value;

        public override void OnStartServer()
        {
            base.OnStartServer();
            serverState = BrewState.Start;
            PushState();
        }

        /// <summary>재료 한 step 투입(둘 다 같은 솥에 넣음 → 소유 불요). 서버 권위 적용 후 전파.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void AddIngredient(float directionX, float directionY, float grind)
        {
            BrewStep step = new BrewStep
            {
                Direction = new BrewVector(directionX, directionY),
                Grind = grind,
            };
            serverState = BrewEngine.Apply(serverState, step);
            PushState();
        }

        /// <summary>같은 솥 비우고 다시(목표·재료는 UI 측 — 채널은 마커 상태만).</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ResetBrew()
        {
            serverState = BrewState.Start;
            PushState();
        }

        [Server]
        private void PushState()
        {
            _markerX.Value = serverState.Position.X;
            _markerY.Value = serverState.Position.Y;
            _stepCount.Value = serverState.StepCount;
            _sideEffect.Value = serverState.AccruedSideEffect;
        }
    }
}
