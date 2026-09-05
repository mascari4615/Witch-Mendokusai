using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 땅이 밭이 되는 규칙 (TASK-WM-410) — 순수 판정 (DomainSDK, 결정적, 엔진 무관).
    ///
    /// ★ 왜 블록을 문자열 id 로 다루나: 블록의 RuntimeId 는 부팅마다 달라지는 <b>임시 번호</b>이고
    ///   영구 이름은 namespaced identifier(`wm:dirt`)다(`BlockData` 주석). 규칙이 임시 번호를 쥐면
    ///   부팅 순서가 밭을 바꾼다. 그래서 여기는 이름으로만 판정하고, 번호 변환은 엔진 쪽에서 한다.
    ///
    /// ★ 왜 규칙을 데이터로 받나: 「무엇이 갈리는가」는 게임·모드·바이옴이 정할 일이다.
    ///   코어가 목록을 소유하면 모드가 새 흙을 못 만든다(6 동기 4·5 — 모딩/UGC).
    /// </summary>
    public sealed class FarmGround
    {
        private readonly HashSet<string> tillableBlocks;

        public FarmGround(string tilledBlock, IReadOnlyList<string> tillableBlocks)
        {
            TilledBlock = tilledBlock;
            this.tillableBlocks = new HashSet<string>();

            if (tillableBlocks == null)
            {
                return;
            }

            for (int i = 0; i < tillableBlocks.Count; i++)
            {
                if (string.IsNullOrEmpty(tillableBlocks[i]) == false)
                {
                    this.tillableBlocks.Add(tillableBlocks[i]);
                }
            }
        }

        /// <summary>갈고 나면 되는 블록 이름.</summary>
        public string TilledBlock { get; }

        /// <summary>이 블록을 갈 수 있나 — 이미 갈린 밭은 다시 안 간다(헛수고에 대가를 물리지 않는다).</summary>
        public bool CanTill(string blockIdentifier)
        {
            return string.IsNullOrEmpty(blockIdentifier) == false
                && blockIdentifier != TilledBlock
                && tillableBlocks.Contains(blockIdentifier);
        }

        /// <summary>
        /// 여기 심을 수 있나 — 갈린 밭 위여야 한다.
        /// [빨강-확인 2026-08-17] 이 판정을 항상 true 로 바꿔 돌렸더니 밭 검증 2건이 빨개졌다
        /// (`CannotPlant_OnUntilledGround`, `PlantingNeeds_TilledGround`) — 초록이 「안 봤음」이 아님을 확인.
        /// </summary>
        public bool CanPlantOn(string blockIdentifier)
        {
            return blockIdentifier == TilledBlock;
        }

        /// <summary>작물이 서는 자리 — 갈린 흙 <b>바로 위</b> 칸.</summary>
        public static FarmCoord PlantSpotAbove(FarmCoord soil) => new FarmCoord(soil.X, soil.Y + 1, soil.Z);

        /// <summary>작물 자리 밑의 흙 — <see cref="PlantSpotAbove"/> 의 역.</summary>
        public static FarmCoord SoilUnder(FarmCoord plant) => new FarmCoord(plant.X, plant.Y - 1, plant.Z);
    }
}
