using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 마도 온실 전체 — 여러 칸(<see cref="GreenhousePlot"/>)을 인형(사역마) carer 들이 매 틱 자동으로
    /// 돌보는 생존 안전망. 순수 POCO (DomainSDK, 결정적, EditMode 직접 테스트).
    ///
    /// 비전 핵심("게으른 욘 대신 인형이 돌본다 = 안전망"):
    ///  - 매 <see cref="TickWithCarers"/> = ① 인형들이 *가장 약한 칸부터* 하나씩 분담 돌봄(triage)
    ///    → ② 전체 시간 경과(Step). 인형이 충분하면 안 시듦, 부족하면 가장 약한 것부터 일부 시듦.
    ///  - 플레이어(Fourth) 관찰=진짜화는 여기 없음 — 그건 플레이어 입력(Observe), 인형은 살리되 진짜화 X.
    ///
    /// triage 결정성: 살아있는 칸을 (Vitality 오름차순, plotId 오름차순)으로 정렬해 앞에서부터
    /// carer 수만큼 1:1 돌봄(중복 X). 동률은 낮은 plotId 우선(TargetingSystem 결정성 선례).
    /// </summary>
    public sealed class Greenhouse
    {
        private readonly Dictionary<FarmCoord, GreenhousePlot> plotsByCoord = new();

        public IReadOnlyDictionary<FarmCoord, GreenhousePlot> Plots => plotsByCoord;

        public int PlotCount => plotsByCoord.Count;

        public GreenhousePlot GetPlot(FarmCoord coord)
        {
            return plotsByCoord.TryGetValue(coord, out GreenhousePlot plot) ? plot : null;
        }

        /// <summary>빈 칸을 추가(또는 기존 칸 교체). 자리(<see cref="FarmCoord"/>)가 곧 식별자다.</summary>
        public GreenhousePlot AddPlot(FarmCoord coord)
        {
            GreenhousePlot plot = new();
            plotsByCoord[coord] = plot;
            return plot;
        }

        /// <summary>좌표 없던 옛 칸 번호로 찾기 (TASK-WM-410 마이그레이션 다리 — 새 코드는 좌표를 쓴다).</summary>
        public GreenhousePlot GetPlot(int legacyPlotId) => GetPlot(FarmCoord.Legacy(legacyPlotId));

        /// <summary>좌표 없던 옛 칸 번호로 추가 (TASK-WM-410 마이그레이션 다리).</summary>
        public GreenhousePlot AddPlot(int legacyPlotId) => AddPlot(FarmCoord.Legacy(legacyPlotId));

        /// <summary>살아있고(시들지 않고) 심긴 칸 수.</summary>
        public int LivingCount()
        {
            int count = 0;
            foreach (GreenhousePlot plot in plotsByCoord.Values)
            {
                if (plot.IsPlanted && plot.Phase != PlotPhase.Withered)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>지금 「진짜화」 자격(관찰+개화+안시듦)을 갖춘 칸 수 — Codex 표본 후보. 「봐준 것만 진짜」 집계.</summary>
        public int SpecimenCount()
        {
            int count = 0;
            foreach (GreenhousePlot plot in plotsByCoord.Values)
            {
                if (plot.IsSpecimenNow)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 한 틱 — 인형 carer 들이 가장 약한 칸부터 분담 돌봄 후 시간 경과.
        /// carerIds = 이번 틱에 돌볼 인형들(각 1칸). minutes = 돌봄 후 흐를 시간.
        /// 돌봄 대상 = 살아있고 심긴 칸 중 (Vitality asc, plotId asc) 상위 carerIds.Length 개.
        /// </summary>
        public void TickWithCarers(IReadOnlyList<int> carerIds, int minutes)
        {
            TickWithCarers(carerIds, minutes, PlantClock.World);
        }

        /// <summary>
        /// 한 시계의 분만 흘린다 (TASK-WM-410) — 세계의 하늘이 흐를 땐 하늘을 탄 작물만,
        /// 바깥 현실이 흐를 땐 현실을 탄 작물만 자란다. 돌봄(triage)도 그 시계의 칸들 사이에서만 나눈다
        /// (자는 사이 인형이 「꺼 놔도 자라는 작물」까지 돌보면 시계가 섞인다).
        /// </summary>
        public void TickWithCarers(IReadOnlyList<int> carerIds, int minutes, PlantClock clock)
        {
            int carerCount = carerIds == null ? 0 : carerIds.Count;

            if (carerCount > 0)
            {
                List<FarmCoord> triage = BuildTriageOrder(clock);
                int tendable = triage.Count < carerCount ? triage.Count : carerCount;

                for (int index = 0; index < tendable; index++)
                {
                    plotsByCoord[triage[index]].Tend(carerIds[index]);
                }
            }

            foreach (GreenhousePlot plot in plotsByCoord.Values)
            {
                if (plot.IsPlanted && plot.Clock != clock)
                {
                    continue;
                }

                plot.Step(minutes);
            }
        }

        // 살아있고 심긴 칸을 (Vitality 오름차순, 자리 오름차순)으로 — 죽기 직전 것부터 구하도록.
        private List<FarmCoord> BuildTriageOrder(PlantClock clock)
        {
            List<FarmCoord> living = new();
            foreach (KeyValuePair<FarmCoord, GreenhousePlot> entry in plotsByCoord)
            {
                if (entry.Value.IsPlanted && entry.Value.Clock == clock && entry.Value.Phase != PlotPhase.Withered)
                {
                    living.Add(entry.Key);
                }
            }

            living.Sort((leftCoord, rightCoord) =>
            {
                float leftVitality = plotsByCoord[leftCoord].Vitality;
                float rightVitality = plotsByCoord[rightCoord].Vitality;

                if (leftVitality != rightVitality)
                {
                    return leftVitality.CompareTo(rightVitality);
                }

                return leftCoord.CompareTo(rightCoord);
            });

            return living;
        }
    }
}
