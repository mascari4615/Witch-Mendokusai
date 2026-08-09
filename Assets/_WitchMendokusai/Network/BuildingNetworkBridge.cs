using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using WitchMendokusai.DomainSDK.Building;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-191 #4 건설(Like 마인크래프트 co-op) substrate — 공유 건설 채널. 4번째 shared-world-state
    /// 채널(WorldClock=ch1, presence=ch2, cauldron=ch3, building=ch4). CauldronNetworkBridge 동형:
    /// 서버 권위 배치맵(SyncList&lt;BuildingPlacement&gt;) + PlaceBuilding/RemoveBuilding ServerRpc → 모든 피어
    /// 관측. "둘이 같은 World 에 건물 배치/제거 → 서로 보임"(co-op 핵심, A: MC/Stardew식).
    ///
    /// ISharedBuildChannel 구현 → Domain BuildManager(namespace WitchMendokusai)가 seam 경유 소비
    /// (asmdef 단방향이라 직접참조 불가). OnStartClient register. UI 가 IsActive 면 Place/Remove 를 ServerRpc
    /// 라우팅하고, Version 폴링으로 변경 감지해 ReadPlacements 로 자기 스폰 동기.
    ///
    /// ⚠ step-1 substrate = 채널만(서버 권위 배치맵 + RPC + SyncList + 검증). BuildManager 라우팅·피어별
    /// BuildingObject 스폰 동기 = 후속 증분(step-2). 복셀 지형(ChunkManager.SetBlock) 동기 = 별 채널 후속.
    /// </summary>
    public class BuildingNetworkBridge : WMNetworkBehaviour, ISharedBuildChannel
    {
        // 서버 권위 배치맵 — 셀+건물ID 리스트. BrewStep SyncList 동형(plain int 구조체 FishNet 자동 직렬화).
        private readonly SyncList<BuildingPlacement> _placements = new SyncList<BuildingPlacement>();

        // 변경 카운터(놓기/부수기마다 +1) — Domain 폴링 변경 감지(SyncList OnChange 대신 WorldClock 폴링 패턴).
        private readonly SyncVar<int> _version = new SyncVar<int>();

        // seam 활성 플래그 — 스폰·클라 시작 후 true.
        private bool _channelActive;

        public bool IsActive => _channelActive;
        public int Version => _version.Value;

        public override void OnStartClient()
        {
            base.OnStartClient();
            _channelActive = true;
            SharedBuildChannelBridge.Register(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _channelActive = false;
            SharedBuildChannelBridge.Clear(this);
        }

        /// <summary>UI seam: 건물 배치 → ServerRpc(클라 또는 clientHost 에서 호출 → 서버 적용).</summary>
        [ServerRpc(RequireOwnership = false)]
        public void PlaceBuilding(int cellX, int cellY, int cellZ, int buildingId)
        {
            ServerPlace(cellX, cellY, cellZ, buildingId);
        }

        /// <summary>서버 권위 배치(직접 — pure server 도 호출 가능, ServerRpc 자기호출 no-op 회피). 중복 셀 무시.</summary>
        [Server]
        public void ServerPlace(int cellX, int cellY, int cellZ, int buildingId)
        {
            for (int index = 0; index < _placements.Count; index++)
            {
                BuildingPlacement existing = _placements[index];
                if (existing.CellX == cellX && existing.CellY == cellY && existing.CellZ == cellZ)
                {
                    return; // 이미 점유 — 서버 권위로 무시.
                }
            }
            _placements.Add(new BuildingPlacement { CellX = cellX, CellY = cellY, CellZ = cellZ, BuildingId = buildingId });
            _version.Value = _version.Value + 1;
        }

        /// <summary>
        /// 크기를 받는 자리 (TASK-WM-217). ⚠ 이 통로의 배치맵은 칸 단위라 크기를 못 나른다 —
        /// 한 칸으로 떨어뜨린다. 크기가 필요한 쪽은 WS 통로(WorldLinkBuildChannel)를 쓴다.
        /// </summary>
        public void PlaceBuilding(int cellX, int cellY, int cellZ, int width, int length, int buildingId)
        {
            PlaceBuilding(cellX, cellY, cellZ, buildingId);
        }

        /// <summary>UI seam: 셀 건물 제거 → ServerRpc.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RemoveBuilding(int cellX, int cellY, int cellZ)
        {
            ServerRemove(cellX, cellY, cellZ);
        }

        /// <summary>서버 권위 제거(직접).</summary>
        [Server]
        public void ServerRemove(int cellX, int cellY, int cellZ)
        {
            for (int index = 0; index < _placements.Count; index++)
            {
                BuildingPlacement existing = _placements[index];
                if (existing.CellX == cellX && existing.CellY == cellY && existing.CellZ == cellZ)
                {
                    _placements.RemoveAt(index);
                    _version.Value = _version.Value + 1;
                    return;
                }
            }
        }

        /// <summary>UI seam: 동기된 전체 배치맵을 buffer 에 복사(피어별 스폰 동기용, FishNet 타입 미노출).</summary>
        public void ReadPlacements(List<BuildingPlacement> buffer)
        {
            buffer.Clear();
            for (int index = 0; index < _placements.Count; index++)
            {
                buffer.Add(_placements[index]);
            }
        }
    }
}
