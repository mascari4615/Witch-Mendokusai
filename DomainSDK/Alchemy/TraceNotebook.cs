using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 연구 기록. 재료마다 어떤 자취로 몇 번 얻었나
    /// - 한 번에 재료 하나만 연구. 연구 중인 재료만 오름
    /// - 기록은 줄지 않음. 재료를 팔거나 써도 어긋나지 않음
    /// - 자취는 숫자로 저장. 이름을 바꿔도 세이브가 열리게
    /// </summary>
    [Serializable]
    public sealed class TraceNotebook
    {
        public const int NO_STUDY = -1;

        public int StudyItemId { get; set; } = NO_STUDY;

        /// <summary>재료 ID, 자취 번호, 횟수</summary>
        public Dictionary<int, Dictionary<int, int>> Counts { get; set; } = new();

        public int CountOf(int itemId, TraceType trace)
        {
            if (Counts.TryGetValue(itemId, out Dictionary<int, int> byTrace) == false)
            {
                return 0;
            }

            return byTrace.TryGetValue((int)trace, out int count) ? count : 0;
        }

        /// <summary>연구 중인 재료이고 자취가 있을 때만 하나 올림. 올렸으면 참</summary>
        public bool Record(int itemId, TraceType trace)
        {
            if (itemId != StudyItemId || trace == TraceType.None)
            {
                return false;
            }

            if (Counts.TryGetValue(itemId, out Dictionary<int, int> byTrace) == false)
            {
                byTrace = new Dictionary<int, int>();
                Counts.Add(itemId, byTrace);
            }

            byTrace[(int)trace] = CountOf(itemId, trace) + 1;
            return true;
        }
    }
}
