namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>마도 온실 한 칸의 생애 단계. (기존 FarmFieldObject.State 의 마도 확장: Withered 추가)</summary>
    public enum PlotPhase
    {
        Empty,
        Growing,
        Bloomed,
        Withered,
    }

    /// <summary>수확 결과 — 무엇을(PlantDataId), 진짜인가(IsSpecimen=관찰된 영구표본), 누가 길렀나(변이 입력).</summary>
    public readonly struct HarvestResult
    {
        public readonly int PlantDataId;
        public readonly bool IsSpecimen;
        public readonly bool HasDominantCarer;
        public readonly int DominantCarerId;

        public HarvestResult(int plantDataId, bool isSpecimen, bool hasDominantCarer, int dominantCarerId)
        {
            PlantDataId = plantDataId;
            IsSpecimen = isSpecimen;
            HasDominantCarer = hasDominantCarer;
            DominantCarerId = dominantCarerId;
        }
    }

    /// <summary>
    /// 마도 온실 한 칸 — 심기/생장/돌봄/관찰/수확의 순수 상태머신 (DomainSDK, EditMode 직접 테스트).
    /// 기존 <c>FarmFieldObject</c>(MonoBehaviour, Empty/Growing/Ready)의 순수 대응물 — Phase 1b 에서
    /// 얇은 MonoBehaviour 가 이 plot 을 래핑(dual 구조: 로직=POCO / 씬·입력·연출=MonoBehaviour).
    ///
    /// 톤 = 절충: 일반 작물(<see cref="PlantGrowthParams.DrainPerMinute"/> = 0)은 이 칸에서도 절대 안
    /// 시듦(코지 보존). 마도 작물(Drain &gt; 0)만 돌봄 안 하면 Withered. 「봐줘야 진짜(Specimen)」는
    /// 플레이어 관찰(<see cref="Observe"/>)로만 — 인형 돌봄(Tend)은 살리지만 진짜로 만들진 못함.
    /// </summary>
    public sealed class GreenhousePlot
    {
        private PlantGrowthState state;
        private PlantGrowthParams parameters;
        private int plantDataId;
        private bool planted;

        public bool IsPlanted => planted;

        public int PlantDataId => plantDataId;

        public float Vitality => planted ? state.Vitality : 0f;

        /// <summary>시들기 전 Fourth(플레이어)가 관찰했는가 — 「진짜화」 자격 + 시각(gold) 신호.</summary>
        public bool Observed => planted && state.Observed;

        /// <summary>지금 이 순간 「진짜화」 자격(관찰+개화+안시듦)을 갖췄는가 — Codex 표본 후보 시각/집계.</summary>
        public bool IsSpecimenNow => planted && WitchPlantGrowth.IsSpecimen(state, parameters);

        public PlotPhase Phase
        {
            get
            {
                if (planted == false)
                {
                    return PlotPhase.Empty;
                }

                if (state.Withered)
                {
                    return PlotPhase.Withered;
                }

                if (WitchPlantGrowth.IsHarvestable(state, parameters))
                {
                    return PlotPhase.Bloomed;
                }

                return PlotPhase.Growing;
            }
        }

        /// <summary>빈 칸에 작물을 심는다. 이미 점유면 거부(false).</summary>
        public bool Plant(int plantDataId, PlantGrowthParams parameters, float startVitality)
        {
            if (planted)
            {
                return false;
            }

            this.plantDataId = plantDataId;
            this.parameters = parameters;
            state = new PlantGrowthState(startVitality);
            planted = true;
            return true;
        }

        /// <summary>시간 경과(분). 마도 작물은 생기 소모→시듦, 살아있으면 생장.</summary>
        public void Step(int minutes)
        {
            if (planted == false)
            {
                return;
            }

            WitchPlantGrowth.Step(state, parameters, minutes);
        }

        /// <summary>돌봄(인형 또는 플레이어) — 생기 회복 + 돌봄자 기록(변이 입력). 빈 칸/시든 칸엔 무효.</summary>
        public void Tend(int carerId)
        {
            if (planted == false)
            {
                return;
            }

            WitchPlantGrowth.Tend(state, parameters, carerId);
        }

        /// <summary>플레이어(Fourth) 관찰 — 「진짜화(영구 표본)」 자격 부여. 빈 칸/시든 칸엔 무효.</summary>
        public void Observe()
        {
            if (planted == false || state.Withered)
            {
                return;
            }

            state.Observed = true;
        }

        /// <summary>개화한 작물을 수확한다. 개화 전/시듦/빈 칸이면 거부(false). 성공 시 칸을 비운다.</summary>
        public bool TryHarvest(out HarvestResult result)
        {
            result = default;

            if (Phase != PlotPhase.Bloomed)
            {
                return false;
            }

            bool isSpecimen = WitchPlantGrowth.IsSpecimen(state, parameters);
            bool hasCarer = WitchPlantGrowth.TryGetDominantCarer(state, out int carerId);
            result = new HarvestResult(plantDataId, isSpecimen, hasCarer, carerId);
            Clear();
            return true;
        }

        /// <summary>시든 작물을 치워 빈 칸으로 되돌린다(재심기 가능). 안 시들었으면 거부(false).</summary>
        public bool ClearWithered()
        {
            if (planted == false || state.Withered == false)
            {
                return false;
            }

            Clear();
            return true;
        }

        private void Clear()
        {
            planted = false;
            plantDataId = 0;
            state = null;
        }
    }
}
