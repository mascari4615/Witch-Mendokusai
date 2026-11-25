using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터 몸 하나에서 성격 (MonsterTemperament) 을 돌림. 사실을 모아 성격에 묻고, 답을 이동과 몸에 입힘
	// 첫 걸음 때 붙고 (MonsterPoseDriver 와 같은 꼴), 풀에서 다시 켜지면 기억을 비움. 태어난 자리는 다시 켜진 뒤 첫 걸음의 자리
	public sealed class MonsterTemperamentDriver : MonoBehaviour
	{
		// 카메라 옆 방향 성분이 이보다 작으면 (거의 위아래로 걸음) 좌우를 안 바꿈. 떨림 방지
		private const float FACE_DEAD_ZONE = 0.05f;

		private readonly TemperamentMemory memory = new();
		private UnitObject unit;
		private MonsterTemperament current;
		private bool needsReset = true;
		private float lastHitTime = float.NegativeInfinity;

		public TemperamentMemory Memory => memory;

		/// <summary>몬스터 자산에 성격이 있으면 그것으로 한 틱 걷고 true. 없으면 아무것도 안 하고 false (곧장 쫓기는 부르는 쪽)</summary>
		public static bool TryMove(UnitObject unitObject, PlayerProvider playerProvider)
		{
			MonsterTemperament temperament = unitObject.UnitData is Monster monster ? monster.Temperament : null;
			if (temperament == null || playerProvider.CurrentObject == null)
				return false;
			Of(unitObject).Move(temperament, playerProvider.CurrentObject);
			return true;
		}

		public static MonsterTemperamentDriver Of(UnitObject unitObject)
		{
			if (unitObject.TryGetComponent(out MonsterTemperamentDriver driver) == false)
			{
				driver = unitObject.gameObject.AddComponent<MonsterTemperamentDriver>();
				driver.unit = unitObject;
				unitObject.Health.OnTakeDamage += driver.RememberHit;
			}
			return driver;
		}

		private void OnEnable()
		{
			needsReset = true;
			lastHitTime = float.NegativeInfinity;
		}

		private void OnDestroy()
		{
			if (unit != null && unit.Health != null)
				unit.Health.OnTakeDamage -= RememberHit;
		}

		private void RememberHit(DamageInfo damageInfo) => lastHitTime = Time.time;

		private void Move(MonsterTemperament temperament, PlayerObject player)
		{
			if (needsReset || temperament != current)
			{
				memory.Reset(unit.transform.position, Random.value);
				current = temperament;
				needsReset = false;
			}
			TemperamentContext context = Gather(player);
			TemperamentStep step = temperament.Steer(context, memory);
			Apply(step, temperament, context);
		}

		private TemperamentContext Gather(PlayerObject player)
		{
			int maxHealth = unit.UnitStat[UnitStatType.HP_MAX];
			return new TemperamentContext
			{
				Self = TemperamentContext.Flat(unit.transform.position),
				Player = TemperamentContext.Flat(player.transform.position),
				PlayerVelocity = SurvivalThreatRules.MoveVelocity(player.UnitMovement),
				PlayerLook = TemperamentContext.Flat(player.UnitMovement.LookDirection),
				Now = Time.time,
				HealthRatio = maxHealth > 0 ? (float)unit.UnitStat[UnitStatType.HP_CUR] / maxHealth : 1f,
				SinceHit = Time.time - lastHitTime,
			};
		}

		private void Apply(TemperamentStep step, MonsterTemperament temperament, in TemperamentContext context)
		{
			UnitMovement movement = unit.UnitMovement;
			if (step.BurstSeconds > 0f && movement.IsExternallyDriven == false)
				movement.ApplyImpulse(TemperamentContext.Flat(step.BurstVelocity), step.BurstSeconds);
			Vector3 direction = TemperamentContext.Flat(step.Direction);
			if (direction.sqrMagnitude <= 0f || step.Speed <= 0f)
				movement.SetMoveDirection(Vector3.zero);
			else if (step.Speed >= 1f)
				movement.SetMoveDirection(direction.normalized);
			else
			{
				// 느린 걸음도 공포 (상태이상) 를 따름. 세기는 그 뒤에 곱함
				Vector2 input = UnitStatus.ApplyFear(unit, new Vector2(direction.x, direction.z).normalized);
				movement.SetAnalogMoveDirection(input * step.Speed);
			}
			if (step.Punch > 1f)
				unit.Health.PunchScale(step.Punch, temperament.PunchSeconds);
			Face(step.FacesPlayer || direction.sqrMagnitude <= 0f ? context.ToPlayer : direction);
		}

		// 그림은 오른쪽을 봄. 카메라 기준 왼쪽으로 가면 뒤집음. 거의 위아래로 가면 그대로 둠 (떨림 방지)
		private void Face(Vector3 worldDirection)
		{
			Camera view = Camera.main;
			if (view == null)
				return;
			float side = Vector3.Dot(worldDirection, view.transform.right);
			if (Mathf.Abs(side) > FACE_DEAD_ZONE)
				unit.SpriteRenderer.flipX = side < 0f;
		}
	}
}
