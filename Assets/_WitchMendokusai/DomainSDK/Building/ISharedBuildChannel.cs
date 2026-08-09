using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Building
{
    /// <summary>
    /// TASK-WM-191 #4 건설(Like 마인크래프트 co-op) seam — 공유 건설 채널. Domain BuildManager(namespace
    /// WitchMendokusai)는 WM.Network 직접참조 불가(asmdef 단방향) → 네트워크 건설 채널(BuildingNetworkBridge)을
    /// DomainSDK 인터페이스로 추상화. SharedBrewChannelBridge(가마솥) 동형: Network layer 가 OnStartClient 에
    /// 구현체 등록, Domain 이 인터페이스로 소비.
    ///
    /// 비네트워크(솔로) = 미등록 → IsActive=false → BuildManager 가 기존 로컬 GridData 경로 그대로(0 변경).
    /// 네트워크(co-op) = 양 피어가 자기 replica 등록 → PlaceBuilding/RemoveBuilding ServerRpc 로 서버 권위
    /// 배치맵 전진, Version 폴링으로 변경 감지 → BuildManager 가 ReadPlacements 로 자기 스폰 동기 = "둘이 같이 짓기".
    /// </summary>
    public interface ISharedBuildChannel
    {
        /// <summary>네트워크 건설 채널이 스폰·활성인가(= co-op 세션 진행 중). false 면 BuildManager 로컬 경로.</summary>
        bool IsActive { get; }

        /// <summary>배치맵 변경 카운터(놓기/부수기마다 +1). 폴링 변경 감지 — 바뀌면 BuildManager 재동기 스폰.</summary>
        int Version { get; }

        /// <summary>건물 한 채 배치(서버 권위, 소유 불요 — 둘 다 같은 World). 중복 셀 = 서버서 무시.</summary>
        void PlaceBuilding(int cellX, int cellY, int cellZ, int buildingId);

        /// <summary>
        /// 건물 한 채 배치 -- <b>크기까지</b> (TASK-WM-217). 여러 칸 건물을 한 칸으로 보내면
        /// 남의 화면에선 한 칸만 서고, 세계의 겹침 판정도 한 칸 기준이 된다(그 옆에 겹쳐 지어진다).
        /// </summary>
        void PlaceBuilding(int cellX, int cellY, int cellZ, int width, int length, int buildingId);

        /// <summary>셀의 건물 제거(서버 권위).</summary>
        void RemoveBuilding(int cellX, int cellY, int cellZ);

        /// <summary>현재 동기된 전체 배치맵을 buffer 에 복사(피어별 스폰 동기용, FishNet 타입 미노출).</summary>
        void ReadPlacements(List<BuildingPlacement> buffer);
    }

    /// <summary>
    /// 공유 건설 채널 static accessor — SharedBrewChannelBridge 동형. Network layer(BuildingNetworkBridge)가
    /// OnStartClient 에 Register, OnStopClient 에 Clear. Domain BuildManager 가 IsActive 로 로컬/공유 분기.
    /// </summary>
    public static class SharedBuildChannelBridge
    {
        private static ISharedBuildChannel channel;

        public static void Register(ISharedBuildChannel sharedBuildChannel)
        {
            channel = sharedBuildChannel;
        }

        public static void Clear(ISharedBuildChannel sharedBuildChannel)
        {
            if (channel == sharedBuildChannel)
            {
                channel = null;
            }
        }

        /// <summary>네트워크 건설 채널 활성 여부(미등록 or 비활성 = false → BuildManager 로컬 경로).</summary>
        public static bool IsActive => channel != null && channel.IsActive;

        /// <summary>활성 채널(IsActive 확인 후 사용). 비활성 시 null 가능.</summary>
        public static ISharedBuildChannel Channel => channel;
    }
}
