using UnityEngine;

namespace WitchMendokusai
{
	// 끌어당김. 가까이 있는 주인공을 천천히 자기 쪽으로 끎. 끌리는 동안 몇 초마다 고리가 안으로 조여 보임
	// 시키는 행동: 거슬러 걷기, 대시로 벗어나기, 끌려가는 김에 붙어서 치기
	[CreateAssetMenu(fileName = "AF_PullAura", menuName = "WM/Monster/Affix/PullAura")]
	public sealed class PullAuraAffix : MonsterAffix
	{
		[SerializeField, Tooltip("끄는 반지름 (m)"), Min(0.5f)] private float radius = 4.5f;
		[SerializeField, Tooltip("끄는 속도 (주인공 걸음 속도 비율). 1 보다 작아야 거슬러 걸을 수 있음"), Range(0f, 0.9f)] private float pullSpeedRatio = 0.4f;
		[SerializeField, Tooltip("이보다 가까우면 안 끎 (m). 몸에 붙어 떨지 않게"), Min(0f)] private float innerRadius = 1f;
		[SerializeField, Tooltip("한 번 건 끌림이 남는 초. 매 프레임 다시 걺"), Min(0.02f)] private float holdSeconds = 0.15f;
		[SerializeField, Tooltip("끌리는 동안 조이는 고리")] private CombatFx pulseFx;
		[SerializeField, Tooltip("고리 간격 (초)"), Min(0.2f)] private float pulseInterval = 1.2f;
		[SerializeField, Tooltip("고리가 보이는 초"), Min(0.05f)] private float pulseSeconds = 0.6f;

		public override string KindName => "끌어당김";

		public static Vector3 PullVelocity(Vector3 source, Vector3 player, float radius, float innerRadius, float speed)
		{
			Vector3 offset = source - player;
			offset.y = 0f;
			float distance = offset.magnitude;
			if (distance > radius || distance < innerRadius)
				return Vector3.zero;
			return offset / distance * speed;
		}

		// 기억의 Timer 는 다음 고리까지 남은 초
		public override void Tick(MonsterObject monster, AffixMemory memory, float deltaTime)
		{
			PlayerObject player = CurrentPlayer;
			if (player == null || player.IsAlive == false)
				return;
			Vector3 velocity = PullVelocity(monster.transform.position, player.transform.position, radius, innerRadius, player.UnitMovement.WalkSpeed * pullSpeedRatio);
			// 더 센 끌기 (보스 빨아들이기, 다른 끌어당김) 가 걸려 있으면 덮지 않음
			if (velocity == Vector3.zero || player.UnitMovement.ExternalPullVelocity.sqrMagnitude > velocity.sqrMagnitude)
				return;
			player.UnitMovement.SetExternalPull(velocity, holdSeconds);
			memory.Timer -= deltaTime;
			if (memory.Timer > 0f || pulseFx == null)
				return;
			memory.Timer = pulseInterval;
			pulseFx.Play(new CombatFxContext { Position = monster.transform.position, Follow = monster.transform, Radius = radius, Seconds = pulseSeconds });
		}
	}
}
