using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>
    /// 기록 단계. 쌓인 값이 목표 표의 몇 번째까지 닿았나. 단계마다 무엇을 여는지는 갈래 몫
    ///
    /// 확률 없이 정해진 만큼 쌓이면 열림. 같은 것을 거듭 만나는 일이 늘 다음 칸에 가까워지게
    /// </summary>
    public static class DiscoveryStages
    {
        /// <summary>닿은 가장 높은 단계. 아무것도 못 닿으면 0</summary>
        public static int Resolve(int value, IReadOnlyList<int> targets)
        {
            int stage = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (value < targets[i])
                {
                    break;
                }
                stage = i + 1;
            }
            return stage;
        }

        /// <summary>목표 표가 비지 않고 0보다 크며 오름차순인지. 틀리면 소유자 이름과 함께 던짐</summary>
        public static void Validate(string owner, IReadOnlyList<int> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                throw new ArgumentException($"{owner}: 단계 목표값이 비었음");
            }
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] <= 0)
                {
                    throw new ArgumentException($"{owner}: 단계 목표값은 0보다 커야 함 ({targets[i]})");
                }
                if (i > 0 && targets[i] <= targets[i - 1])
                {
                    throw new ArgumentException($"{owner}: 단계 목표값은 오름차순 ({targets[i - 1]} 다음 {targets[i]})");
                }
            }
        }
    }
}
