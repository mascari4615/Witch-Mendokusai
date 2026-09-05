using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>
    /// 갈래별 해금 출처 등록소. 도감 화면이 여기에 묻는다.
    ///
    /// ★ 출처가 안 꽂힌 갈래는 <b>열림</b>으로 답한다. 그 갈래의 조건을 아는 쪽이 아직 없다는 뜻이고,
    ///   그때 전부 보이는 것이 지금까지의 거동이다 (잠금 층 도입 전 골격).
    ///
    /// 호스트(Unity 부팅, 서버)가 <see cref="Register"/> 로 꽂는다. 판정 층은 누가 꽂는지 모른다.
    /// </summary>
    public static class DiscoveryUnlocks
    {
        private static readonly Dictionary<string, IDiscoveryUnlockSource> sources = new Dictionary<string, IDiscoveryUnlockSource>();

        /// <summary>갈래 하나의 출처를 꽂는다. 같은 갈래를 다시 꽂으면 뒤엣것이 이긴다.</summary>
        public static void Register(IDiscoveryUnlockSource source)
        {
            sources[source.CatalogId] = source;
        }

        /// <summary>꽂힌 출처를 전부 뗀다. 씬 재시작과 시험용.</summary>
        public static void Clear()
        {
            sources.Clear();
        }

        /// <summary>이 항목이 열렸나. 출처가 없는 갈래는 열림.</summary>
        public static bool IsUnlocked(string catalogId, string entryId)
        {
            if (sources.TryGetValue(catalogId, out IDiscoveryUnlockSource source) == false)
            {
                return true;
            }

            return source.IsUnlocked(entryId);
        }
    }
}
