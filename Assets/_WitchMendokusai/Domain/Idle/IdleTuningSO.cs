using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai
{
    /// <summary>
    /// 방치 판의 숫자를 <b>인스펙터에서</b> 만지는 자리 (TASK-WM-406).
    ///
    /// ★ 수치 노출 룰의 방치형 쪽 형태 — 코어(DomainSDK)는 Unity 를 모르므로 SO 가 될 수 없다.
    ///   그래서 이 SO 가 값을 들고 있다가 <see cref="ToTuning"/> 으로 코어에 넘긴다.
    ///   숫자를 고치려고 코드를 여는 일이 없어야 한다 — 밸런싱은 코드 작업이 아니다.
    ///
    /// ★ 비용·성장 배수가 이 게임의 난이도 손잡이다:
    ///   비용 배수 &gt; 값 배수 여야 뒤로 갈수록 한 번 사는 게 사건이 된다(성취감).
    ///   반대면 금세 숫자가 폭발해 무의미해지고, 차이가 너무 크면 하염없이 기다린다(지루).
    /// </summary>
    [CreateAssetMenu(fileName = "IdleTuning", menuName = "WM/Idle/Tuning")]
    public sealed class IdleTuningSO : ScriptableObject
    {
        [Header("대상")]
        [Tooltip("대상 하나의 체력 — 이만큼 깎으면 처치.")]
        [SerializeField] private double targetHealth = 10d;

        [Tooltip("처치 하나당 들어오는 자원.")]
        [SerializeField] private double rewardPerKill = 1d;

        [Header("기본 능력치 (레벨 0)")]
        [SerializeField] private double baseDamage = 1d;
        [SerializeField] private double baseAttackSpeed = 1d;

        [Header("공격력 곡선")]
        [Tooltip("0→1 비용.")]
        [SerializeField] private double damageBaseCost = 10d;

        [Tooltip("레벨마다 비용에 곱해지는 배수.")]
        [SerializeField] private double damageCostRatio = 1.22d;

        [Tooltip("레벨 하나가 주는 공격력의 첫 값.")]
        [SerializeField] private double damageBaseValue = 1d;

        [Tooltip("레벨마다 효과에 곱해지는 배수. 1 이면 매 레벨 같은 양이 더해진다.")]
        [SerializeField] private double damageValueRatio = 1.15d;

        [Header("공격속도 곡선")]
        [SerializeField] private double speedBaseCost = 25d;
        [SerializeField] private double speedCostRatio = 1.28d;
        [SerializeField] private double speedBaseValue = 0.5d;
        [SerializeField] private double speedValueRatio = 1.12d;

        /// <summary>코어가 쓰는 형태로 넘긴다.</summary>
        public IdleTuning ToTuning()
        {
            return new IdleTuning
            {
                TargetHealth = targetHealth,
                RewardPerKill = rewardPerKill,
                BaseDamage = baseDamage,
                BaseAttackSpeed = baseAttackSpeed,
                DamageCurve = new GeometricUpgradeCurve
                {
                    BaseCost = damageBaseCost,
                    CostRatio = damageCostRatio,
                    BaseValue = damageBaseValue,
                    ValueRatio = damageValueRatio,
                },
                AttackSpeedCurve = new GeometricUpgradeCurve
                {
                    BaseCost = speedBaseCost,
                    CostRatio = speedCostRatio,
                    BaseValue = speedBaseValue,
                    ValueRatio = speedValueRatio,
                },
            };
        }
    }
}
