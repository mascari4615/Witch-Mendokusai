using System.Collections.Generic;
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
    ///
    /// step-4b: ISharedBrewChannel 구현 → Domain UI(CauldronMapElement)가 seam 경유로 이 채널을 소비
    /// (asmdef 단방향이라 직접참조 불가). OnStartClient 에 자기를 SharedBrewChannelBridge 에 등록 →
    /// UI 가 IsActive 면 AddStep 을 ServerRpc 로 라우팅하고 마커를 SyncVar 에서 폴링 read = 공유 솥.
    /// </summary>
    public class CauldronNetworkBridge : WMNetworkBehaviour, ISharedBrewChannel
    {
        private readonly SyncVar<float> _markerX = new SyncVar<float>();
        private readonly SyncVar<float> _markerY = new SyncVar<float>();
        private readonly SyncVar<int> _stepCount = new SyncVar<int>();
        private readonly SyncVar<float> _sideEffect = new SyncVar<float>();

        // step-4b 완결: 전체 step 경로 동기(SyncList) — 마커뿐 아니라 *경로선*까지 양 피어 동일.
        // BrewStep([Serializable] 구조체) FishNet 자동 직렬화. AddIngredient 서 마커 SyncVar 와 함께 갱신(일관).
        private readonly SyncList<BrewStep> _steps = new SyncList<BrewStep>();

        // 서버측 권위 누적 상태(stateless BrewEngine.Apply 로 전진). 클라엔 SyncVar 만 도달.
        private BrewState serverState;

        // seam 활성 플래그 — 스폰·클라 시작 후 true. UI 의 IsActive 분기 근거(스폰 전 호출 차단).
        private bool _channelActive;

        public float MarkerX => _markerX.Value;
        public float MarkerY => _markerY.Value;
        public int SyncedStepCount => _stepCount.Value;
        public float SyncedSideEffect => _sideEffect.Value;

        // ── ISharedBrewChannel (Domain UI seam) ──────────────────────────────
        public bool IsActive => _channelActive;

        public override void OnStartClient()
        {
            base.OnStartClient();
            _channelActive = true;
            SharedBrewChannelBridge.Register(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _channelActive = false;
            SharedBrewChannelBridge.Clear(this);
        }

        /// <summary>UI seam: 재료 한 step 투입 → AddIngredient ServerRpc(둘 다 같은 솥, 소유 불요).</summary>
        public void AddStep(BrewStep step)
        {
            AddIngredient(step.Direction.X, step.Direction.Y, step.Grind);
        }

        // ISharedBrewChannel.ResetBrew() = 아래 [ServerRpc] ResetBrew() 가 직접 충족(별도 wrapper 불요).

        /// <summary>UI seam: 현재 공유 마커 상태(서버 누적 → SyncVar). 항상 true(스폰 후 SyncVar 유효).</summary>
        public bool TryGetState(out BrewVector position, out int stepCount, out float accruedSideEffect)
        {
            position = new BrewVector(_markerX.Value, _markerY.Value);
            stepCount = _stepCount.Value;
            accruedSideEffect = _sideEffect.Value;
            return true;
        }

        /// <summary>UI seam: 이 피어가 서버(host)인가 — 「완성」 보상 host-권위 분기 근거(이름 IsServer/IsHost X = FishNet 충돌 회피).</summary>
        public bool IsServerPeer => base.IsServerInitialized;

        /// <summary>UI seam: 동기된 전체 경로 step 을 buffer 에 복사(경로선 렌더용, FishNet 타입 미노출).</summary>
        public void ReadSteps(List<BrewStep> buffer)
        {
            buffer.Clear();
            for (int index = 0; index < _steps.Count; index++)
            {
                buffer.Add(_steps[index]);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            serverState = BrewState.Start;
            _steps.Clear();
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
            _steps.Add(step);
            PushState();
        }

        /// <summary>같은 솥 비우고 다시(목표·재료는 UI 측 — 채널은 마커·경로 상태만).</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ResetBrew()
        {
            serverState = BrewState.Start;
            _steps.Clear();
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
