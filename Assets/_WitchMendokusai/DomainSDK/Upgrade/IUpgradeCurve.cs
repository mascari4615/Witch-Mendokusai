namespace WitchMendokusai.DomainSDK.Upgrade
{
    /// <summary>
    /// 「레벨을 하나 올리면 얼마를 내고 무엇을 얻나」의 단일 출처(SSOT).
    ///
    /// ★ 왜 이 인터페이스가 필요했나 — WM 의 업그레이드는 원래 <b>레벨별 값을 배열로 손에 박는</b> 형태였다
    ///   (`UpgradeData.PricePerLevel[] / ValuePerLevel[]`, MaxLevel 10). 손으로 채우니 레벨 수가 곧 작업량이라
    ///   수백~수천 레벨이 필요한 방치형에서는 쓸 수 없다. 그렇다고 방치형만 따로 만들면 업그레이드가 두 벌이 된다.
    ///   그래서 <b>「값을 어디서 얻나」 한 지점만</b> 갈아끼우게 열었다 — 표로 얻든 수식으로 얻든 위층은 모른다.
    ///
    /// ★ double 인 이유 — 방치형 값은 곧 int·float 범위를 넘긴다(float 는 1e7 부터 1 단위가 사라진다).
    ///
    /// 구현: <see cref="TableUpgradeCurve"/>(표) · <see cref="GeometricUpgradeCurve"/>(수식).
    /// </summary>
    public interface IUpgradeCurve
    {
        /// <summary>더 올릴 수 없는 레벨. 무한 성장은 <see cref="UpgradeLevel.UNBOUNDED"/>.</summary>
        int MaxLevel { get; }

        /// <summary>이 레벨에서 다음 레벨로 갈 때 내는 값. level 은 0 부터.</summary>
        double CostToRaiseFrom(int level);

        /// <summary>이 레벨에 도달했을 때 얻는 효과의 총량(누적값, 레벨 0 이면 0).</summary>
        double TotalValueAt(int level);
    }
}
