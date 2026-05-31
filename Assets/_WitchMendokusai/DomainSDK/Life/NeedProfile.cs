using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 한 캐릭터가 가진 욕구들의 튜닝값 모음 (NeedKind → NeedSpec). 순수 (DomainSDK).
    /// 욕구 종류·속도가 캐릭터마다 다를 수 있도록 데이터로 보유 — 욘은 Social 감소가 느리고(혼자 견딤),
    /// 링은 Mood·Social 감소가 빠른 식의 개성을 여기에 담는다(미래 LifeProfileSO 가 생성).
    ///
    /// 키 순회는 enum 오름차순으로 고정(결정성) — 동률 시급도 타이브레이크가 캐릭터·플랫폼 무관하게 같도록.
    /// (패턴: Farming/PlantGrowthState 의 결정적 순회 의도)
    /// </summary>
    public sealed class NeedProfile
    {
        private readonly Dictionary<NeedKind, NeedSpec> specByKind;
        private readonly List<NeedKind> orderedKinds;

        public NeedProfile(IReadOnlyDictionary<NeedKind, NeedSpec> specs)
        {
            specByKind = new Dictionary<NeedKind, NeedSpec>(specs.Count);
            orderedKinds = new List<NeedKind>(specs.Count);

            foreach (KeyValuePair<NeedKind, NeedSpec> entry in specs)
            {
                specByKind[entry.Key] = entry.Value;
                orderedKinds.Add(entry.Key);
            }

            orderedKinds.Sort();
        }

        /// <summary>이 프로필이 다루는 욕구 종류 — enum 오름차순(결정 순회).</summary>
        public IReadOnlyList<NeedKind> Kinds => orderedKinds;

        /// <summary>욕구 튜닝값 — 등록 안 된 종류 접근 시 FastFail(KeyNotFound).</summary>
        public NeedSpec SpecOf(NeedKind kind) => specByKind[kind];
    }
}
