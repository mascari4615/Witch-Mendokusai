using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터 몸 하나에서 성격 (MonsterTemperament) 을 돌림. 사실을 모아 성격에 묻고, 답을 이동과 몸에 입힘
	// 첫 걸음 때 붙고 (MonsterPoseDriver 와 같은 꼴), 풀에서 다시 켜지거나 재배치되면 기억을 비움. 태어난 자리는 비운 뒤 첫 걸음의 자리
	// 기절, 빙결 중에는 성격을 묻지 않음 (시계가 흐르지 않게). 공포 중에는 성격 대신 공포가 걸음을 정함
	// 감각 (Senses) 을 켠 성격만 SenseSeconds 마다 주변 목록을 훑음. 처치 소식은 그 동안만 구독
	public sealed class MonsterTemperamentDriver : MonoBehaviour
	{
		private readonly TemperamentMemory memory = new();
		private UnitObject unit;
		private MonsterTemperament current;
		private bool needsReset = true;
		private bool isListening;
		private bool holdsAttack;
		private float lastHitTime = float.NegativeInfinity;
		private float playerHeight;
		private float nextSenseTime;
		private bool isWatchingFalls;
		private float lastFallenTime = float.NegativeInfinity;
		private Vector3 lastFallenAt;
		private TemperamentContext sensed;

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
			lastFallenTime = float.NegativeInfinity;
			sensed = default;
		}

		// 재배치, 다시 꺼내짐, 성격 바뀜. 옛 자리의 이웃과 처치를 버리고, 첫 감지 시각을 유닛마다 흩어 같은 프레임에 몰리지 않게
		private void ForgetSenses(MonsterTemperament temperament)
		{
			sensed = default;
			lastFallenTime = float.NegativeInfinity;
			nextSenseTime = Time.time + temperament.SenseSeconds * Random.value;
		}

		private void OnDisable() => WatchFalls(false);

		private void OnDestroy()
		{
			WatchFalls(false);
			if (isListening && unit != null && unit.Health != null)
				unit.Health.OnTakeDamage -= RememberHit;
		}

		private void WatchFalls(bool on)
		{
			if (on == isWatchingFalls)
				return;
			if (on)
				EventBusBridge.Subscribe<MonsterKilledEvent>(RememberFall);
			else
				EventBusBridge.Unsubscribe<MonsterKilledEvent>(RememberFall);
			isWatchingFalls = on;
		}

		private void RememberFall(MonsterKilledEvent killed)
		{
			if (current == null || unit == null)
				return;
			Vector3 offset = TemperamentContext.Flat(killed.Position - unit.transform.position);
			if (offset.sqrMagnitude > current.SenseRadius * current.SenseRadius)
				return;
			lastFallenTime = Time.time;
			lastFallenAt = TemperamentContext.Flat(killed.Position);
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
				ForgetSenses(temperament);
			}
			if (UnitStatus.IsFleeing(unit))
			{
				// 방향은 공포가 정함 (주인공 반대쪽, 제 속도). 성격의 시계는 멈춤
				movement.SetMoveDirection(TemperamentContext.Toward(unit.transform.position, player.transform.position));
				holdsAttack = true;
				Face(temperament, movement.MoveDirectionWorld, keepWhenStill: true);
				return;
			}
			playerHeight = player.UnitMovement.IsGrounded() ? player.transform.position.y : unit.transform.position.y;
			WatchFalls((temperament.Senses & TemperamentSense.Fallen) != 0);
			TemperamentContext context = Gather(temperament, player);
			TemperamentStep step = temperament.Steer(context, memory);
			holdsAttack = step.HoldsAttack;
			Apply(step, temperament, context);
		}

		private TemperamentContext Gather(MonsterTemperament temperament, PlayerObject player)
		{
			int maxHealth = unit.UnitStat[UnitStatType.HP_MAX];
			if (temperament.Senses != TemperamentSense.None && Time.time >= nextSenseTime)
			{
				Sense(temperament);
				nextSenseTime = Time.time + temperament.SenseSeconds;
			}
			return new TemperamentContext
			{
				Self = TemperamentContext.Flat(unit.transform.position),
				Player = TemperamentContext.Flat(player.transform.position),
				PlayerVelocity = SurvivalThreatRules.MoveVelocity(player.UnitMovement),
				PlayerLook = TemperamentContext.Flat(player.UnitMovement.LookDirection),
				Now = Time.time,
				HealthRatio = maxHealth > 0 ? (float)unit.UnitStat[UnitStatType.HP_CUR] / maxHealth : 1f,
				SinceHit = Time.time - lastHitTime,
				KinCount = sensed.KinCount,
				KinCenter = sensed.KinCenter,
				HasAlly = sensed.HasAlly,
				Ally = sensed.Ally,
				HasLoot = sensed.HasLoot,
				Loot = sensed.Loot,
				SinceFallen = Time.time - lastFallenTime,
				FallenAt = lastFallenAt,
			};
		}

		// 몬스터와 전리품 목록을 한 번씩 훑어 반지름 안만 셈. 같은 성격 자산이면 Kin, 아니면 가장 가까운 것이 Ally
		private void Sense(MonsterTemperament temperament)
		{
			TemperamentSense senses = temperament.Senses;
			Vector3 self = TemperamentContext.Flat(unit.transform.position);
			float radiusSquared = temperament.SenseRadius * temperament.SenseRadius;
			sensed = default;
			if ((senses & (TemperamentSense.Kin | TemperamentSense.Ally)) != 0)
			{
				Vector3 kinSum = Vector3.zero;
				float allySquared = float.PositiveInfinity;
				foreach (GameObject other in ObjectBufferManager.GetObjects(ObjectType.Monster))
				{
					if (other == null || other == gameObject || other.activeInHierarchy == false)
						continue;
					Vector3 position = TemperamentContext.Flat(other.transform.position);
					float distanceSquared = (position - self).sqrMagnitude;
					if (distanceSquared > radiusSquared || other.TryGetComponent(out UnitObject otherUnit) == false || otherUnit.UnitStat[UnitStatType.DEAD] > 0)
						continue;
					bool isKin = otherUnit.UnitData is Monster monster && monster.Temperament == temperament;
					if (isKin)
					{
						kinSum += position;
						sensed.KinCount++;
					}
					else if (distanceSquared < allySquared)
					{
						allySquared = distanceSquared;
						sensed.Ally = position;
						sensed.HasAlly = true;
					}
				}
				if (sensed.KinCount > 0)
					sensed.KinCenter = kinSum / sensed.KinCount;
			}
			if ((senses & TemperamentSense.Loot) != 0)
			{
				float lootSquared = radiusSquared;
				foreach (GameObject loot in ObjectBufferManager.GetObjects(ObjectType.Drop))
				{
					// 주인공에게 끌려가는 중인 것 (자석, 줍기 범위) 은 곧 사라지므로 안 셈
					if (loot == null || loot.activeInHierarchy == false || (loot.TryGetComponent(out LootObject lootObject) && lootObject.IsBeingPulled))
						continue;
					Vector3 position = TemperamentContext.Flat(loot.transform.position);
					float distanceSquared = (position - self).sqrMagnitude;
					if (distanceSquared > lootSquared)
						continue;
					lootSquared = distanceSquared;
					sensed.Loot = position;
					sensed.HasLoot = true;
				}
			}
		}

		private void Apply(TemperamentStep step, MonsterTemperament temperament, in TemperamentContext context)
		{
			UnitMovement movement = unit.UnitMovement;
			// 넉백 중이면 튀어 나감을 버림 (넉백이 이김). 성격의 단계는 이미 넘어가 그 한 번은 걷기로 이어짐
			if (step.BurstSeconds > 0f && movement.IsExternallyDriven == false)
				movement.ApplyImpulse(TemperamentContext.Flat(step.BurstVelocity), step.BurstSeconds);
			if (step.Teleport)
				Blink(step.TeleportTo, temperament);
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

		// 넉백, 끌림, 걷기 입력은 Teleport 가 비움. 높이는 땅에 선 주인공 높이 (떠 있으면 내 높이)
		// 경기장 밖이나 장애물 속이면 안쪽으로 밀어냄 (숨는 공격 MonsterHideaway 와 같은 자르기)
		private void Blink(Vector3 flatTarget, MonsterTemperament temperament)
		{
			Vector3 from = unit.transform.position;
			Vector3 to = MonsterHideaway.Clamp(unit, new Vector3(flatTarget.x, playerHeight, flatTarget.z));
			unit.UnitMovement.Teleport(to);
			MonsterSpawnStyle puff = temperament.TeleportPuff;
			if (puff == null)
				return;
			puff.PlayPuff(from, 1f);
			puff.PlayPuff(to, 1f);
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
