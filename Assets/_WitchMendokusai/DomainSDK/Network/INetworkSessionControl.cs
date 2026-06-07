namespace WitchMendokusai.DomainSDK.Network
{
    /// <summary>
    /// TASK-WM-190 — 멀티(동기6) 진입 seam (Bridge 패턴).
    ///
    /// Domain UI(로비)가 NetCode 를 *직접* 참조하면 boundary 게이트 위반(Domain↛Network·FishNet,
    /// WM-184). 그래서 DomainSDK 가 세션 제어 인터페이스만 정의하고, WM.Network 가 impl 을
    /// register(NetworkSessionControl) → 게임 UI 는 DomainSDK 경유로 호스트/참가 트리거.
    /// 분리 동기 정합: NetCode 결합은 WM.Network 에만, 게임은 추상 seam 만 본다.
    ///
    /// 멀티 모델 = 드롭인 헬퍼 인형 (Spiritfarer식, 사용자 컨펌 2026-06-07):
    /// 호스트 = 내 Yon 세계를 친구에게 연다 / 참가 = 친구 Yon 세계에 헬퍼 인형으로 합류.
    /// 단수 Yon 로어 유지(여러 Yon X).
    /// </summary>
    public interface INetworkSessionControl
    {
        /// <summary>내 세계를 호스트로 연다 (server + local client). 성공 = true.</summary>
        bool StartHost();

        /// <summary>초대코드로 친구 세계에 참가 (client). 코드 파싱 실패/연결요청 실패 = false.</summary>
        bool JoinByCode(string inviteCode);

        /// <summary>호스트 시 친구에게 줄 초대코드 (로컬 IPv4:port → 짧은 코드).</summary>
        string GetHostInviteCode();

        /// <summary>세션이 떠 있나 (server 또는 client 시작됨).</summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// Domain → DomainSDK → (impl in WM.Network) static accessor. WM.Network 의
    /// NetworkSessionControl 이 AfterAssembliesLoaded 에 Register. WM 의 IXxxBridge 패턴.
    /// </summary>
    public static class NetworkSessionBridge
    {
        public static INetworkSessionControl Instance { get; private set; }

        public static void Register(INetworkSessionControl impl) => Instance = impl;

        public static bool IsAvailable => Instance != null;
    }
}
