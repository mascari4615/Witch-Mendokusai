using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.DomainSDK.Care
{
    /// <summary>
    /// TASK-WM-171 — 한 소원의 진행 상태 (가변, DomainSDK POCO).
    ///
    /// 모인 재료 개수와 채워진 충족도를 *누적* 한다. <see cref="WishResolver"/> 가 본 상태와
    /// <see cref="WishSpec"/> 을 대조해 완성 여부와 결말을 판정. 본 클래스는 *저장* 만 — 판정 X.
    ///
    /// 충족도 키는 string(abstract 채널 이름). WM-168 NeedKind 와 직접 결합 X (DEFERRED).
    /// 값은 0..1 클램프 — 누가 채우든(돌봄·아이템·시간) 한 채널은 0..1 안에서만 변한다.
    /// (패턴: <see cref="WitchMendokusai.DomainSDK.Life.NeedState"/> — Dictionary 기반 가변 상태)
    /// </summary>
    public sealed class WishProgress
    {
        private const float SATISFACTION_FLOOR = 0f;
        private const float SATISFACTION_CEIL = 1f;

        private readonly Dictionary<string, int> collectedMaterials = new();
        private readonly Dictionary<string, float> satisfactionLevels = new();

        /// <summary>아이템 id 별 모인 개수 (읽기 전용 스냅 뷰).</summary>
        public IReadOnlyDictionary<string, int> CollectedMaterials => collectedMaterials;

        /// <summary>충족 채널별 현재 값 0..1 (읽기 전용 스냅 뷰).</summary>
        public IReadOnlyDictionary<string, float> SatisfactionLevels => satisfactionLevels;

        /// <summary>재료를 누적 (음수·0 거절). 같은 itemId 반복 호출은 합산.</summary>
        public void AddMaterial(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId 는 비어있을 수 없다", nameof(itemId));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "추가 개수는 1 이상이어야 한다");
            }

            collectedMaterials.TryGetValue(itemId, out int current);
            collectedMaterials[itemId] = current + count;
        }

        /// <summary>충족 채널 절대값 설정(0..1 클램프). 채우든 비우든 *현재값* 을 직접 박는다.</summary>
        public void SetSatisfaction(string channel, float value)
        {
            if (string.IsNullOrEmpty(channel))
            {
                throw new ArgumentException("channel 은 비어있을 수 없다", nameof(channel));
            }

            satisfactionLevels[channel] = Mathf.Clamp(value, SATISFACTION_FLOOR, SATISFACTION_CEIL);
        }

        /// <summary>한 채널의 현재 충족도(미설정 키는 0 반환). 결정성 보장.</summary>
        public float GetSatisfaction(string channel)
        {
            return satisfactionLevels.TryGetValue(channel, out float value) ? value : 0f;
        }

        /// <summary>한 아이템의 누적 개수(미설정 키는 0 반환).</summary>
        public int GetMaterialCount(string itemId)
        {
            return collectedMaterials.TryGetValue(itemId, out int count) ? count : 0;
        }
    }
}
