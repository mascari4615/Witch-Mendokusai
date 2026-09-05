using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 한 캐릭터의 욕구 충족도 런타임 상태 (NeedKind → 현재 충족도). 순수 POCO (DomainSDK) — NeedModel 이 진행시킨다.
    /// LifeAgentSaveData 가 미래(INC-5)에 이 필드를 흡수 예정 (지금은 모델 first-use 격리, Farming 선례).
    /// </summary>
    [Serializable]
    public sealed class NeedState
    {
        private readonly Dictionary<NeedKind, float> valueByKind = new();

        public NeedState()
        {
        }

        public NeedState(IReadOnlyDictionary<NeedKind, float> initial)
        {
            foreach (KeyValuePair<NeedKind, float> entry in initial)
            {
                valueByKind[entry.Key] = entry.Value;
            }
        }

        /// <summary>현재 충족도 — 미등록 종류는 0(완전 결핍)으로 본다.</summary>
        public float Get(NeedKind kind) => valueByKind.TryGetValue(kind, out float value) ? value : 0f;

        public void Set(NeedKind kind, float value) => valueByKind[kind] = value;

        public IReadOnlyDictionary<NeedKind, float> Values => valueByKind;
    }
}
