using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 방치형 인형 도감의 해금 출처. 가진 인형만 열림
    ///
    /// 도감은 조건을 안 정함 (본편과 같은 계약). 무엇이 열렸나는 판을 아는 여기가 답하고, 화면은 등록소에 물음
    /// 호스트 (Idle 화면) 가 판을 만들 때 <see cref="DiscoveryUnlocks.Register"/> 로 꽂음
    /// </summary>
    public sealed class IdleHeroDiscovery : IDiscoveryUnlockSource
    {
        public const string CATALOG_ID = "idleHero";

        private readonly IdleState state;

        public IdleHeroDiscovery(IdleState state)
        {
            this.state = state;
        }

        public string CatalogId => CATALOG_ID;

        /// <summary>항목 id 는 인형 id 의 십진 글자</summary>
        public static string EntryIdOf(int heroId) => heroId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public bool IsUnlocked(string entryId)
        {
            if (int.TryParse(entryId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int heroId) == false)
            {
                return false;
            }

            return state.IndexOfHero(heroId) >= 0;
        }

        /// <summary>얼마나 모았나. 전체는 카탈로그 종류 수, 열린 것은 가진 종류 수</summary>
        public static DiscoveryProgress ProgressOf(IdleState state) => new DiscoveryProgress(IdleHeroes.Count, state.Heroes.Count);
    }
}
