namespace WitchMendokusai.DomainSDK.Network
{
    /// <summary>
    /// TASK-WM-191 step-1 — 네트워크 플레이어 프록시(WM.Network)가 *로컬 플레이어 위치*를 알아야
    /// 하나, boundary 게이트(Domain↛Network·FishNet)로 직접참조 불가. DomainSDK 가 위치 probe seam 만
    /// 정의 → Domain(PlayerProvider 백) impl·register → Network 프록시가 read. 순수 float(UnityEngine 무의존).
    ///
    /// 채택 아키텍처: 로컬 플레이어(씬배치 싱글톤)는 *불변*, 멀티 시 owner 프록시가 이 probe 로 자기
    /// 위치를 따라가 브로드캐스트(NetworkTransform) → 상대 클라가 원격 아바타로 관측. 단일플레이어 회귀 0.
    /// </summary>
    public interface ILocalPlayerProbe
    {
        /// <summary>로컬 플레이어가 존재하면 위치(x,y,z) + facing yaw(도) 반환. 없으면(로비/사망 등) false.</summary>
        bool TryGetPose(out float x, out float y, out float z, out float yaw);
    }

    /// <summary>Domain impl → Network 프록시 static accessor (WM IXxxBridge 패턴).</summary>
    /// <summary>세계가 아는 자리로 로컬 플레이어를 옮길 수 있는 것 (TASK-WM-217).</summary>
    public interface ILocalPlayerPull
    {
        /// <summary>여기로 옮긴다(높이는 게임이 알아서 — 세계는 y 를 모른다).</summary>
        void PullTo(float x, float z);
    }

    public static class LocalPlayerProbeBridge
    {
        public static ILocalPlayerProbe Instance { get; private set; }

        public static void Register(ILocalPlayerProbe probe) => Instance = probe;

        /// <summary>
        /// 세계가 아는 자리로 <b>끌어당길</b> 수 있나 (TASK-WM-217). 읽기만 하던 probe 의 반쪽 —
        /// 서버가 걸음을 자르는데 화면의 나만 앞서가면 남에게는 내가 딴 데 있다.
        /// </summary>
        public static bool TryPullTo(float x, float z)
        {
            if (Instance is ILocalPlayerPull pull)
            {
                pull.PullTo(x, z);
                return true;
            }

            return false;
        }

        public static bool TryGetPose(out float x, out float y, out float z, out float yaw)
        {
            if (Instance != null)
            {
                return Instance.TryGetPose(out x, out y, out z, out yaw);
            }
            x = 0f;
            y = 0f;
            z = 0f;
            yaw = 0f;
            return false;
        }
    }
}
