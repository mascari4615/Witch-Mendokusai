namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>
    /// 한 갈래의 해금을 답하는 자리.
    ///
    /// 도감은 조건을 정하지 않는다 — 무엇을 봐야 열리는지는 그 갈래를 아는 쪽이 안다
    /// (식물은 온실의 표본, 아이템은 레시피 해금). 도감은 묻고 세고 보여줄 뿐.
    /// </summary>
    public interface IDiscoveryUnlockSource
    {
        /// <summary>어느 갈래인가. 화면 쪽 provider 의 Id 와 같은 값 (예: "plant").</summary>
        string CatalogId { get; }

        /// <summary>이 항목이 열렸나.</summary>
        bool IsUnlocked(string entryId);
    }
}
