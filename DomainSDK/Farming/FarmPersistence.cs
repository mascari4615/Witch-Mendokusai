using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 밭을 적고 되살린다 (TASK-WM-410) — 순수 함수 (DomainSDK, 결정적, EditMode 직접 테스트).
    ///
    /// ★ 되살리기의 핵심은 <b>못 본 사이를 메우는 것</b>이다. 게임을 끄면 세계의 하늘은 멈추지만
    ///   바깥 현실은 안 멈춘다. 그래서 칸마다 「마지막으로 본 시각」을 제 시계의 단위로 적어 두고,
    ///   돌아왔을 때 그 차이만큼 자라게 한다 — 방치 수확이 성립하는 자리다.
    ///
    /// ★ 시간은 <b>되감지 않는다</b>: 저장이 미래를 가리키면(시계 조작·기기 시각 차) 0 으로 본다.
    ///   음수 분을 흘리면 자란 것이 도로 어려지고, 그건 저장이 세계를 망가뜨리는 길이다.
    /// </summary>
    public static class FarmPersistence
    {
        public const int SECONDS_PER_MINUTE = 60;

        /// <summary>
        /// 지금 상태를 적는다. 시각은 두 시계 모두 받는다 — 칸마다 <b>제 시계의 것</b>만 골라 적는다.
        /// </summary>
        public static FarmSaveData Save(Greenhouse greenhouse, long worldMinutesNow, long realUnixSecondsNow)
        {
            FarmSaveData save = new();

            if (greenhouse == null)
            {
                return save;
            }

            List<FarmCoord> coords = new(greenhouse.Plots.Keys);
            coords.Sort(); // 적히는 순서를 고정 — 같은 밭은 같은 파일이 된다(diff·재현성).

            for (int i = 0; i < coords.Count; i++)
            {
                GreenhousePlot plot = greenhouse.GetPlot(coords[i]);
                if (plot == null || plot.IsPlanted == false)
                {
                    continue;
                }

                save.Plots.Add(new FarmPlotSaveData
                {
                    X = coords[i].X,
                    Y = coords[i].Y,
                    Z = coords[i].Z,
                    PlantDataId = plot.PlantDataId,
                    Clock = (int)plot.Clock,
                    Vitality = plot.Vitality,
                    GrowthMinutes = plot.GrowthMinutes,
                    Withered = plot.IsWithered,
                    Observed = plot.Observed,
                    LastSeenStamp = plot.Clock == PlantClock.Real ? realUnixSecondsNow : worldMinutesNow,
                });
            }

            return save;
        }

        /// <summary>
        /// 기억을 되살리고 못 본 사이를 메운다. <paramref name="growthParamsOf"/> 는 작물 id → 성장 수치
        /// (수치는 게임 데이터라 코어가 안 들고 있다). 모르는 작물은 조용히 버리지 않고 건너뛴 수를 돌려준다.
        /// </summary>
        public static int Load(
            Greenhouse greenhouse,
            FarmSaveData save,
            long worldMinutesNow,
            long realUnixSecondsNow,
            float realSecondsPerGrowthMinute,
            Func<int, PlantGrowthParams?> growthParamsOf)
        {
            if (greenhouse == null || save == null || save.Plots == null || growthParamsOf == null)
            {
                return 0;
            }

            int skipped = 0;

            for (int i = 0; i < save.Plots.Count; i++)
            {
                FarmPlotSaveData saved = save.Plots[i];
                PlantGrowthParams? parameters = growthParamsOf(saved.PlantDataId);

                if (parameters.HasValue == false)
                {
                    skipped++;
                    continue;
                }

                PlantGrowthState state = new()
                {
                    Vitality = saved.Vitality,
                    GrowthMinutes = saved.GrowthMinutes,
                    Withered = saved.Withered,
                    Observed = saved.Observed,
                };

                GreenhousePlot plot = greenhouse.GetPlot(saved.Coord) ?? greenhouse.AddPlot(saved.Coord);
                if (plot.Restore(saved.PlantDataId, parameters.Value, state, saved.PlantClock) == false)
                {
                    skipped++;
                    continue;
                }

                int missedMinutes = MissedMinutes(saved, worldMinutesNow, realUnixSecondsNow, realSecondsPerGrowthMinute);
                if (missedMinutes > 0)
                {
                    plot.Step(missedMinutes);
                }
            }

            return skipped;
        }

        /// <summary>못 본 사이가 몇 분인가 — 제 시계로 잰다. 미래를 가리키는 저장은 0(시간은 안 되감는다).</summary>
        public static int MissedMinutes(FarmPlotSaveData saved, long worldMinutesNow, long realUnixSecondsNow, float realSecondsPerGrowthMinute)
        {
            if (saved == null)
            {
                return 0;
            }

            if (saved.PlantClock == PlantClock.Real)
            {
                if (realSecondsPerGrowthMinute <= 0f)
                {
                    return 0;
                }

                long elapsedSeconds = realUnixSecondsNow - saved.LastSeenStamp;
                return elapsedSeconds <= 0 ? 0 : (int)(elapsedSeconds / realSecondsPerGrowthMinute);
            }

            long elapsedMinutes = worldMinutesNow - saved.LastSeenStamp;
            return elapsedMinutes <= 0 ? 0 : (int)elapsedMinutes;
        }
    }
}
