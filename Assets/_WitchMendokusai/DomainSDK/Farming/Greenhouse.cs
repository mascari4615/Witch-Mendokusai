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
        private readonly Dictionary<int, GreenhousePlot> plotsById = new();

        public IReadOnlyDictionary<int, GreenhousePlot> Plots => plotsById;

        public int PlotCount => plotsById.Count;

        public GreenhousePlot GetPlot(int plotId)
        {
            return plotsById.TryGetValue(plotId, out GreenhousePlot plot) ? plot : null;
        }

        /// <summary>빈 칸을 추가(또는 기존 칸 교체). plotId = 안정 식별자(triage 타이브레이크 키).</summary>
        public GreenhousePlot AddPlot(int plotId)
        {
            GreenhousePlot plot = new();
            plotsById[plotId] = plot;
            return plot;
        }

        /// <summary>살아있고(시들지 않고) 심긴 칸 수.</summary>
        public int LivingCount()
        {
            int count = 0;
            foreach (GreenhousePlot plot in plotsById.Values)
            {
                if (plot.IsPlanted && plot.Phase != PlotPhase.Withered)
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
            int carerCount = carerIds == null ? 0 : carerIds.Count;

            if (carerCount > 0)
            {
                List<int> triage = BuildTriageOrder();
                int tendable = triage.Count < carerCount ? triage.Count : carerCount;

                for (int index = 0; index < tendable; index++)
                {
                    plotsById[triage[index]].Tend(carerIds[index]);
                }
            }

            foreach (GreenhousePlot plot in plotsById.Values)
            {
                plot.Step(minutes);
            }
        }

        // 살아있고 심긴 칸을 (Vitality 오름차순, plotId 오름차순)으로 — 죽기 직전 것부터 구하도록.
        private List<int> BuildTriageOrder()
        {
            List<int> living = new();
            foreach (KeyValuePair<int, GreenhousePlot> entry in plotsById)
            {
                if (entry.Value.IsPlanted && entry.Value.Phase != PlotPhase.Withered)
                {
                    living.Add(entry.Key);
                }
            }

            living.Sort((leftId, rightId) =>
            {
                float leftVitality = plotsById[leftId].Vitality;
                float rightVitality = plotsById[rightId].Vitality;

                if (leftVitality != rightVitality)
                {
                    return leftVitality.CompareTo(rightVitality);
                }

                return leftId.CompareTo(rightId);
            });

            return living;
        }
    }
}
