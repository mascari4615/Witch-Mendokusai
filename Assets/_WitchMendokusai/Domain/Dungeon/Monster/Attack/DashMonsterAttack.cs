using UnityEngine;

namespace WitchMendokusai
{
	// 돌진. 기존 돌진 행동 (BT_Dash, 보스와 같은 부품) 을 일반 적 수치로, 대기 (Cooldown) 는 BT_DashCooldown 이 씌움. 피해는 몸 접촉
	// WindupSeconds 가 돌진 전 멈칫, RecoverySeconds 가 돌진 뒤 숨 고르기, WindupTelegraph 는 바닥 띠 예고
	[CreateAssetMenu(fileName = "MA_Dash", menuName = "WM/Variable/MonsterAttack/" + nameof(DashMonsterAttack))]
	public class DashMonsterAttack : MonsterAttack
	{
		public override string KindName => "돌진";

		[field: Header("돌진")]
		[field: SerializeField, Tooltip("돌진 속도 (m/s)"), Min(0.1f)] public float DashSpeed { get; private set; } = 9f;
		[field: SerializeField, Tooltip("돌진 시간 (초). 길이는 속도 x 시간"), Min(0.05f)] public float DashDuration { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("바닥 띠 예고 폭 (m)"), Min(0.1f)] public float TelegraphWidth { get; private set; } = 1f;
		[field: SerializeField, Tooltip("돌진 계획 이동의 이 비율도 못 나가고 닿아 있으면 부딪혀 멈춤"), Range(0f, 1f)] public float BlockedProgressRatio { get; private set; } = 0.3f;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_DashCooldown(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_DashCooldown)behaviour).Interrupt();

		// BT_Dash 한 벌. 끝나면 onDashEnd 로 대기 시작
		public BT_Dash CreateDash(UnitObject unitObject, PlayerProvider playerProvider, System.Action onDashEnd) =>
			new(unitObject, playerProvider, TriggerRange, DashSpeed, DashDuration, onDashEnd,
				dashPreDelay: WindupSeconds, dashPostDelay: RecoverySeconds,
				telegraph: WindupTelegraph, telegraphWidth: TelegraphWidth,
				blockedProgressRatio: BlockedProgressRatio);
	}
}
