namespace WitchMendokusai.DomainSDK.Upgrade
{
    /// <summary>올리기 실패 사유.</summary>
    public enum UpgradeRaiseFailure
    {
        None = 0,
        AtMaxLevel = 1,
        NotEnoughFunds = 2,
    }

    /// <summary>내리기 실패 사유.</summary>
    public enum UpgradeLowerFailure
    {
        None = 0,
        AtMinLevel = 1,
    }

    /// <summary>
    /// 한 축의 레벨과 그 위의 올리기·내리기·되돌리기 판정. 순수 계산이라 EditMode 에서 그대로 돌아간다.
    ///
    /// ★ 지갑을 안 안다 — 값을 <b>받아서</b> 판정하고 <b>얼마 썼는지 돌려준다</b>. 어느 화폐인지는 부르는 쪽이 정한다.
    ///   (기존 `UpgradeData` 는 `DataManager.Instance.GameStat[NYANG]` 을 직접 만져서 화폐가 하나로 못 박혀 있었다.
    ///    방치형 자원처럼 다른 지갑을 쓰는 순간 그 결합이 벽이 된다.)
    /// </summary>
    public sealed class UpgradeLevel
    {
        /// <summary>상한 없음 — 방치형처럼 끝없이 오르는 축.</summary>
        public const int UNBOUNDED = int.MaxValue;

        /// <summary>지금 레벨.</summary>
        public int Level { get; set; }

        /// <summary>이 축이 지금 주는 효과 총량.</summary>
        public double TotalValue(IUpgradeCurve curve)
        {
            return curve.TotalValueAt(Level);
        }

        /// <summary>다음 레벨 값. 상한이면 false.</summary>
        public bool TryGetNextCost(IUpgradeCurve curve, out double cost)
        {
            if (Level >= curve.MaxLevel)
            {
                cost = 0d;
                return false;
            }

            cost = curve.CostToRaiseFrom(Level);
            return true;
        }

        /// <summary>가진 값으로 한 단계 올린다. 성공 시 쓴 값을 돌려준다.</summary>
        public bool TryRaise(IUpgradeCurve curve, double funds, out UpgradeRaiseFailure failure, out double spent)
        {
            spent = 0d;

            if (TryGetNextCost(curve, out double cost) == false)
            {
                failure = UpgradeRaiseFailure.AtMaxLevel;
                return false;
            }

            if (funds < cost)
            {
                failure = UpgradeRaiseFailure.NotEnoughFunds;
                return false;
            }

            Level++;
            spent = cost;
            failure = UpgradeRaiseFailure.None;
            return true;
        }

        /// <summary>한 단계 내린다. 성공 시 돌려받는 값을 준다(직전 레벨의 값 그대로).</summary>
        public bool TryLower(IUpgradeCurve curve, out UpgradeLowerFailure failure, out double refunded)
        {
            if (Level <= 0)
            {
                failure = UpgradeLowerFailure.AtMinLevel;
                refunded = 0d;
                return false;
            }

            Level--;
            refunded = curve.CostToRaiseFrom(Level);
            failure = UpgradeLowerFailure.None;
            return true;
        }

        /// <summary>0 으로 되돌린다. 그동안 쓴 값을 전부 돌려준다.</summary>
        public bool TryReset(IUpgradeCurve curve, out UpgradeLowerFailure failure, out double refunded)
        {
            if (Level <= 0)
            {
                failure = UpgradeLowerFailure.AtMinLevel;
                refunded = 0d;
                return false;
            }

            refunded = 0d;
            for (int spentLevel = 0; spentLevel < Level; spentLevel++)
            {
                refunded += curve.CostToRaiseFrom(spentLevel);
            }

            Level = 0;
            failure = UpgradeLowerFailure.None;
            return true;
        }
    }
}
