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
		[field: SerializeField, Tooltip("바닥 띠 예고를 돌진 길이보다 이만큼 더 길게 (m). 큰 몸의 앞면"), Min(0f)] public float TelegraphExtraLength { get; private set; }
		[field: SerializeField, Tooltip("돌진 계획 이동의 이 비율도 못 나가고 닿아 있으면 부딪혀 멈춤"), Range(0f, 1f)] public float BlockedProgressRatio { get; private set; } = 0.3f;
		[field: SerializeField, Tooltip("주인공 쪽에서 벌어진 돌진 각도 (도). 0 이면 똑바로, 크면 옆걸음"), Range(-90f, 90f)] public float AngleDegrees { get; private set; }

		[field: SerializeField, Tooltip("돌진 길 위에 이 유닛이 남긴 물체 (Linger) 를 지나가며 터뜨림. 터질 반지름과 예고는 남긴 공격의 값")] public bool CracksLinger { get; private set; }

		[field: Header("벽에 박기")]
		[field: SerializeField, Tooltip("지형이나 맵 물체 (유닛 말고) 에 막혀 멈추면 숨 고르기 대신 이 초 동안 박혀 있음. 0 이면 지금처럼 숨 고르기 (RecoverySeconds)"), Min(0f)] public float WallSlamSeconds { get; private set; }
		[field: SerializeField, Tooltip("박혀 있는 동안 발밑 표시 (긴 반격 창). 비우면 RecoveryFx")] public CombatFx WallSlamFx { get; private set; }
		[field: SerializeField, Tooltip("벽에 박힌 순간 이펙트 (부딪힌 먼지, 별). 비우면 없음")] public CombatFx WallImpactFx { get; private set; }

		protected override bool PlaysWindupFx => true;
		protected override bool PlaysStrikeFx => true;

		// 돌진이 끝난 뒤 멈춰 있는 초. 지형에 박혔으면 벽에 박기 초 (켜졌을 때만)
		public float RecoveryAfter(bool slammedTerrain) => slammedTerrain && WallSlamSeconds > 0f ? WallSlamSeconds : RecoverySeconds;

		// 막은 것이 유닛 (주인공, 다른 몬스터) 이 아닌 지형이나 맵 물체인가
		public static bool IsTerrain(Collider blocker) => blocker != null && blocker.GetComponentInParent<UnitObject>() == null;

		public override void Validate(Monster owner, System.Collections.Generic.List<string> errors)
		{
			base.Validate(owner, errors);
			if (WallSlamSeconds > 0f && BlockedProgressRatio <= 0f)
				errors.Add($"{name}: 벽에 박기 초가 있는데 막힘 판정 ({nameof(BlockedProgressRatio)}) 이 0 이라 박히지 않음");
			if (WallSlamSeconds <= 0f && (WallSlamFx != null || WallImpactFx != null))
				errors.Add($"{name}: 벽에 박기 이펙트가 있는데 박기 초가 0");
			if (WallSlamSeconds > 0f && WallSlamSeconds < RecoverySeconds)
				errors.Add($"{name}: 벽에 박힌 경직 {WallSlamSeconds} 초가 평소 숨 고르기 {RecoverySeconds} 초보다 짧음");
		}

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_DashCooldown(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_DashCooldown)behaviour).Interrupt();

		// BT_Dash 한 벌. 끝나면 onDashEnd 로 대기 시작
		public BT_Dash CreateDash(UnitObject unitObject, PlayerProvider playerProvider, System.Action onDashEnd) =>
			new(unitObject, playerProvider, TriggerRange, DashSpeed, DashDuration, onDashEnd,
				dashPreDelay: WindupSeconds, dashPostDelay: RecoverySeconds,
				telegraph: WindupTelegraph, telegraphWidth: TelegraphWidth,
				recoveryFx: RecoveryFx, recoveryRadius: RecoveryFxRadius, blockedProgressRatio: BlockedProgressRatio,
				angleDegrees: AngleDegrees, look: this);
	}
}
