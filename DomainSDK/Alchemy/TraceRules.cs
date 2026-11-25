using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    public enum TraceCellState
    {
        Unknown = 0,
        Magic = 1,
        // 맞지 않는 칸. 귀찮음 하나가 남음
        Hassle = 2,
    }

    /// <summary>조합판의 재료 한 줄. 자취 칸 하나를 밝히는 데 드는 횟수</summary>
    public readonly struct TraceMaterial
    {
        public readonly int ItemId;
        public readonly int Need;

        public TraceMaterial(int itemId, int need)
        {
            ItemId = itemId;
            Need = need;
        }
    }

    /// <summary>마법이 숨은 칸</summary>
    public readonly struct TraceCombo
    {
        public readonly int ItemId;
        public readonly TraceType Trace;

        public TraceCombo(int itemId, TraceType trace)
        {
            ItemId = itemId;
            Trace = trace;
        }
    }

    public readonly struct TraceProgress
    {
        public readonly int Magics;
        public readonly int Hassles;
        public readonly int Warped;

        public TraceProgress(int magics, int hassles, int warped)
        {
            Magics = magics;
            Hassles = hassles;
            Warped = warped;
        }
    }

    /// <summary>자취와 조합판의 판정. 엔진 무관</summary>
    public static class TraceRules
    {
        public static IReadOnlyList<TraceType> ALL { get; } = new[]
        {
            TraceType.Clean,
            TraceType.Bond,
            TraceType.Story,
            TraceType.Uproar,
            TraceType.Company,
            TraceType.Calm,
        };

        /// <summary>칸의 상태. 필요 횟수를 채우기 전에는 무엇이 숨었는지 모름</summary>
        public static TraceCellState CellOf(TraceNotebook notebook, TraceMaterial material, TraceType trace, IReadOnlyList<TraceCombo> combos)
        {
            if (material.Need <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(material), material.Need, "필요 횟수는 1 이상");
            }

            if (notebook.CountOf(material.ItemId, trace) < material.Need)
            {
                return TraceCellState.Unknown;
            }

            for (int i = 0; i < combos.Count; i++)
            {
                if (combos[i].ItemId == material.ItemId && combos[i].Trace == trace)
                {
                    return TraceCellState.Magic;
                }
            }

            return TraceCellState.Hassle;
        }

        /// <summary>찾은 마법, 귀찮음, 귀찮음이 모여 나온 삐뚤어진 마법의 수</summary>
        public static TraceProgress ProgressOf(TraceNotebook notebook, IReadOnlyList<TraceMaterial> materials, IReadOnlyList<TraceCombo> combos, int hasslesPerWarped)
        {
            int magics = 0;
            int hassles = 0;
            for (int i = 0; i < materials.Count; i++)
            {
                foreach (TraceType trace in ALL)
                {
                    TraceCellState state = CellOf(notebook, materials[i], trace, combos);
                    if (state == TraceCellState.Magic)
                    {
                        magics++;
                    }
                    else if (state == TraceCellState.Hassle)
                    {
                        hassles++;
                    }
                }
            }

            int warped = hasslesPerWarped > 0 ? hassles / hasslesPerWarped : 0;
            return new TraceProgress(magics, hassles, warped);
        }

        /// <summary>
        /// 이번 판에 가장 많이 잡은 자취 있는 도구의 자취. 보스 재료에 씀 (마지막 한 방의 운을 줄임)
        /// 처치 수가 같으면 번호가 작은 도구. 자취 있는 도구가 없으면 None
        /// </summary>
        public static TraceType MainTrace(IReadOnlyDictionary<int, int> killsByTool, Func<int, TraceType> traceOf)
        {
            TraceType main = TraceType.None;
            int mainTool = 0;
            int mainKills = 0;
            foreach (KeyValuePair<int, int> pair in killsByTool)
            {
                TraceType trace = traceOf(pair.Key);
                if (trace == TraceType.None || pair.Value <= 0)
                {
                    continue;
                }

                bool better = pair.Value > mainKills || (pair.Value == mainKills && pair.Key < mainTool);
                if (better == false)
                {
                    continue;
                }

                main = trace;
                mainTool = pair.Key;
                mainKills = pair.Value;
            }

            return main;
        }
    }
}
