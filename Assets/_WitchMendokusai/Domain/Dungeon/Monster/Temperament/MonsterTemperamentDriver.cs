using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터 몸 하나에서 성격 (MonsterTemperament) 을 돌림. 사실을 모아 성격에 묻고, 답을 이동과 몸에 입힘
	// 첫 걸음 때 붙고 (MonsterPoseDriver 와 같은 꼴), 풀에서 다시 켜지거나 재배치되면 기억을 비움. 태어난 자리는 비운 뒤 첫 걸음의 자리
	// 기절, 빙결 중에는 성격을 묻지 않음 (시계가 흐르지 않게). 공포 중에는 성격 대신 공포가 걸음을 정함
	public sealed class MonsterTemperamentDriver : MonoBehaviour
	{
		private readonly TemperamentMemory memory = new();
		private UnitObject unit;
		private MonsterTemperament current;
		private bool needsReset = true;
		private bool isListening;
		private bool holdsAttack;
		private float lastHitTime = float.NegativeInfinity;

		public TemperamentMemory Memory => memory;

		/// <summary>몬스터 자산에 성격이 있으면 그것으로 한 틱 걷고 true. 없으면 아무것도 안 하고 false (곧장 쫓기는 부르는 쪽)</summary>
		public static bool TryMove(UnitObject unitObject, PlayerProvider playerProvider)
		{
			MonsterTemperament temperament = TemperamentOf(unitObject);
			if (temperament == null || playerProvider.CurrentObject == null)
				return false;
			Of(unitObject).Move(temperament, playerProvider.CurrentObject);
			return true;
		}

		/// <summary>성격이 지금 공격을 막는가 (잠듦, 모른 척, 겁먹음, 튀어 나가는 중). 성격이 없으면 false</summary>
		public static bool HoldsAttack(UnitObject unitObject)
		{
			if (TemperamentOf(unitObject) == null || unitObject.TryGetComponent(out MonsterTemperamentDriver driver) == false)
				return false;
			return driver.holdsAttack || unitObject.UnitMovement.IsExternallyDriven;
		}

		/// <summary>재배치처럼 켜진 채 자리가 바뀔 때. 다음 걸음에서 태어난 자리와 단계를 새로 잡음</summary>
		public static void Forget(UnitObject unitObject)
		{
			if (unitObject.TryGetComponent(out MonsterTemperamentDriver driver))
				driver.needsReset = true;
		}

		public static MonsterTemperamentDriver Of(UnitObject unitObject)
		{
			if (unitObject.TryGetComponent(out MonsterTemperamentDriver driver) == false)
				driver = unitObject.gameObject.AddComponent<MonsterTemperamentDriver>();
			driver.Bind(unitObject);
			return driver;
		}

		private static MonsterTemperament TemperamentOf(UnitObject unitObject) => unitObject.UnitData is Monster monster ? monster.Temperament : null;

		private void Bind(UnitObject unitObject)
		{
			unit = unitObject;
			if (isListening)
				return;
			unit.Health.OnTakeDamage += RememberHit;
			isListening = true;
		}

		private void OnEnable()
		{
			needsReset = true;
			holdsAttack = false;
			lastHitTime = float.NegativeInfinity;
		}

		private void OnDestroy()
		{
			if (isListening && unit != null && unit.Health != null)
				unit.Health.OnTakeDamage -= RememberHit;
		}

		// 독, 화상 같은 상태이상 틱은 맞은 것으로 안 침 (순둥이가 계속 화나고 잠꾸러기가 틱에 깨지 않게)
		private void RememberHit(DamageInfo damageInfo)
		{
			if (damageInfo.type != DamageType.StatusTick)
				lastHitTime = Time.time;
		}

		private void Move(MonsterTemperament temperament, PlayerObject player)
		{
			UnitMovement movement = unit.UnitMovement;
			if (unit.IsActionSuspended)
			{
				movement.SetMoveDirection(Vector3.zero);
				holdsAttack = true;
				return;
			}
			if (needsReset || temperament != current)
			{
				memory.Reset(unit.transform.position, Random.value);
				current = temperament;
				needsReset = false;
			}
			if (UnitStatus.IsFleeing(unit))
			{
				// 방향은 공포가 정함 (주인공 반대쪽, 제 속도). 성격의 시계는 멈춤
				movement.SetMoveDirection(TemperamentContext.Toward(unit.transform.position, player.transform.position));
				holdsAttack = true;
				Face(temperament, movement.MoveDirectionWorld, keepWhenStill: true);
				return;
			}
			TemperamentContext context = Gather(player);
			TemperamentStep step = temperament.Steer(context, memory);
			holdsAttack = step.HoldsAttack;
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
			// 넉백 중이면 튀어 나감을 버림 (넉백이 이김). 성격의 단계는 이미 넘어가 그 한 번은 걷기로 이어짐
			if (step.BurstSeconds > 0f && movement.IsExternallyDriven == false)
				movement.ApplyImpulse(TemperamentContext.Flat(step.BurstVelocity), step.BurstSeconds);
			Vector3 direction = TemperamentContext.Flat(step.Direction);
			bool isStill = direction.sqrMagnitude <= 0f || step.Speed <= 0f;
			if (isStill)
				movement.SetMoveDirection(Vector3.zero);
			else if (step.Speed >= 1f)
				movement.SetMoveDirection(direction.normalized);
			else
				movement.SetAnalogMoveDirection(new Vector2(direction.x, direction.z).normalized * step.Speed);
			if (step.Punch > 1f)
				unit.Health.PunchScale(step.Punch, temperament.PunchSeconds);
			if (step.FacesPlayer)
				Face(temperament, context.ToPlayer, keepWhenStill: false);
			else
				Face(temperament, isStill ? Vector3.zero : direction, keepWhenStill: true);
		}

		// 그림은 오른쪽을 봄. 카메라 기준 왼쪽으로 가면 뒤집음. 거의 위아래로 가거나 멈췄으면 그대로 둠
		private void Face(MonsterTemperament temperament, Vector3 worldDirection, bool keepWhenStill)
		{
			Camera view = Camera.main;
			if (view == null || (keepWhenStill && worldDirection.sqrMagnitude <= 0f))
				return;
			float side = Vector3.Dot(worldDirection, view.transform.right);
			if (Mathf.Abs(side) > temperament.FaceDeadZone)
				unit.SpriteRenderer.flipX = side < 0f;
		}
	}
}
