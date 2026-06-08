namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// TASK-WM-191 #4 step-4b — 공유 가마솥 채널 seam. Domain UI(CauldronMapElement)는 WM.Network 를
    /// 직접 참조 못 함(asmdef 단방향: WM.Domain ↛ WM.Network, 둘 다 DomainSDK 만 참조) → 네트워크
    /// 가마솥(CauldronNetworkBridge)을 DomainSDK 인터페이스로 추상화. LocalPlayerProbeBridge 동형:
    /// Network layer 가 구현체 등록, Domain 이 인터페이스로 소비.
    ///
    /// 비네트워크(솔로) = 채널 미등록 → IsActive=false → UI 는 로컬 BrewSession 그대로(경로 0 변경).
    /// 네트워크(co-op) = 양 피어가 자기 로컬 replica 등록 → AddStep 이 ServerRpc 로 서버 권위 brew 전진,
    /// TryGetState 가 SyncVar 마커 read = "둘이 같은 솥". 폴링 소비(WorldClock 동기 패턴 — OnChange 불요).
    /// </summary>
    public interface ISharedBrewChannel
    {
        /// <summary>네트워크 가마솥 채널이 스폰·활성인가(= co-op 세션 진행 중). false 면 UI 는 로컬 경로.</summary>
        bool IsActive { get; }

        /// <summary>재료 한 step 투입 — 서버 권위 brew 에 전진(소유 불요, 둘 다 같은 솥에 넣음).</summary>
        void AddStep(BrewStep step);

        /// <summary>같은 솥 비우고 다시(서버 권위 리셋). 이름 Reset X = NetworkBehaviour.Reset() magic 메서드 충돌 회피.</summary>
        void ResetBrew();

        /// <summary>현재 공유 마커 상태(서버 BrewEngine 누적 → SyncVar). 비활성이면 false.</summary>
        bool TryGetState(out BrewVector position, out int stepCount, out float accruedSideEffect);
    }

    /// <summary>
    /// 공유 가마솥 채널 static accessor — LocalPlayerProbeBridge 동형. Network layer(CauldronNetworkBridge)가
    /// OnStartClient 에 Register, OnStopClient 에 Clear. Domain UI 는 IsActive 로 로컬/공유 분기.
    /// </summary>
    public static class SharedBrewChannelBridge
    {
        private static ISharedBrewChannel channel;

        public static void Register(ISharedBrewChannel sharedBrewChannel)
        {
            channel = sharedBrewChannel;
        }

        public static void Clear(ISharedBrewChannel sharedBrewChannel)
        {
            // 자기 자신만 해제(다른 인스턴스가 이미 갱신했으면 건드리지 X — race 안전).
            if (channel == sharedBrewChannel)
            {
                channel = null;
            }
        }

        /// <summary>네트워크 가마솥 채널 활성 여부(미등록 or 비활성 = false → UI 로컬 경로).</summary>
        public static bool IsActive => channel != null && channel.IsActive;

        /// <summary>활성 채널(IsActive 확인 후 사용). 비활성 시 null 가능.</summary>
        public static ISharedBrewChannel Channel => channel;
    }
}
