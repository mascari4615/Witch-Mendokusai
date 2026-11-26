using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 탄막 (VolleyMonsterAttack) 이 쏜 탄들. 공격 자산은 여러 유닛이 나눠 쓰고 상태를 안 둬서 여기서 날리고 판정함. 씬마다 하나, 처음 쓸 때 만들어짐
	// 그림은 FxSpritePool 재사용. 주인이 죽거나 꺼지면 (판 끝, 풀 반납) 그 주인 것은 그 프레임에 회수, 화면 청소는 ClearWithin
	public sealed class VolleyField : MonoBehaviour
	{
		private static VolleyField instance;

		public static VolleyField Instance
		{
			get
			{
				if (instance == null)
					instance = new GameObject(nameof(VolleyField)).AddComponent<VolleyField>();
				return instance;
			}
		}

		private sealed class Bullet
		{
			public UnitObject Source;
			public VolleyMonsterAttack Attack;
			public SpriteRenderer Body;
			public Vector3 Position;
			public Vector3 Velocity;
			public float Remaining;
			public float BornTime;
			// 쏜 자리의 바닥 높이. 박힐 때 그 높이에 앉음
			public float GroundY;
			// 박혀 남은 뒤의 수명. 나는 동안 null
			public FxHold Hold;
			// 사라지는 중이면 시작 시각
			public float FadeTime = float.PositiveInfinity;
		}

		// 실행 뒤 아직 안 나간 물결
		private sealed class Pending
		{
			public UnitObject Source;
			public VolleyMonsterAttack Attack;
			// 준비를 시작한 순간 고정한 주인공 자리
			public Vector3 Target;
			public float AimDegrees;
			public int NextWave;
			public float NextTime;
		}

		private readonly List<Bullet> bullets = new();
		private readonly List<Pending> pendings = new();
		private readonly List<VolleyShot> shots = new();

		public int BulletCount => bullets.Count;

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
			// 그림은 CombatFxRunner 아래라 씬과 같이 사라짐. 여기서 실행기를 새로 만들지 않음
			bullets.Clear();
			pendings.Clear();
		}

		// 첫 물결은 지금, 나머지는 WaveInterval 마다. 쏘는 쪽은 지금 시전자에서 target 쪽, 분열은 target 자리에서 퍼짐
		public void Fire(VolleyMonsterAttack attack, UnitObject source, Vector3 target)
		{
			Pending pending = new()
			{
				Source = source,
				Attack = attack,
				Target = target,
				AimDegrees = VolleyPatternMath.AngleOf(VolleyMonsterAttack.AimOf(source.transform.position, target)),
				NextTime = Time.time,
			};
			if (EmitDue(pending) == false)
				pendings.Add(pending);
		}

		// 화면 청소. 자리에서 반지름 안의 탄 (나는 것과 박힌 것) 과 그 안 시전자의 남은 물결을 걷음
		public static void ClearWithin(Vector3 center, float radius)
		{
			if (instance == null)
				return;
			instance.pendings.RemoveAll(pending => IsGone(pending.Source) || SlimeKingPatternMath.IsWithin(center, pending.Source.transform.position, radius));
			foreach (Bullet bullet in instance.bullets)
			{
				if (SlimeKingPatternMath.IsWithin(center, bullet.Position, radius))
					Retire(bullet);
			}
		}

		// 주인이 쏜 탄과 남은 물결을 모두 걷음 (재배치, 단계 전환 다시 하기)
		public static void Abandon(UnitObject source)
		{
			if (instance == null)
				return;
			instance.pendings.RemoveAll(pending => pending.Source == source);
			foreach (Bullet bullet in instance.bullets)
			{
				if (bullet.Source == source)
					Retire(bullet);
			}
		}

		private static bool IsGone(UnitObject source) => source == null || source.gameObject.activeInHierarchy == false || source.IsAlive == false;

		// 때가 된 물결을 내보냄. 끝났으면 true
		private bool EmitDue(Pending pending)
		{
			VolleyMonsterAttack attack = pending.Attack;
			while (pending.NextWave < attack.Waves && Time.time >= pending.NextTime)
			{
				// 쏘는 중 기절, 빙결이면 남은 물결 취소
				if (IsGone(pending.Source) || (pending.NextWave > 0 && pending.Source.IsActionSuspended))
					return true;
				EmitWave(pending);
				pending.NextWave++;
				pending.NextTime += attack.WaveInterval;
			}
			return pending.NextWave >= attack.Waves;
		}

		private void EmitWave(Pending pending)
		{
			VolleyMonsterAttack attack = pending.Attack;
			Vector3 origin = attack.Shape == VolleyShape.Split ? pending.Target : pending.Source.transform.position;
			VolleyPatternMath.Wave(attack.Shape, attack.Count, attack.ArcDegrees, attack.PerSpoke, pending.AimDegrees, pending.NextWave,
				attack.TurnDegreesPerWave, attack.Speed, attack.SpeedStep, shots, attack.ReverseAtWave);
			// 물결은 통째로. 상한에 걸려 고리 한쪽만 비면 예고와 다른 모양이 됨
			if (bullets.Count + shots.Count > attack.FieldCap)
				return;
			// 첫 물결의 타격 이펙트는 행동 (BT_WindupAttack) 이 냄. 뒤 물결도 같은 자리와 크기
			if (pending.NextWave > 0 && attack.StrikeFx != null)
			{
				CombatFxContext context = attack.WindupContext(pending.Source, pending.Target, 0);
				context.Origin = pending.Source.transform.position;
				attack.StrikeFx.Play(context);
			}
			float height = origin.y + attack.Look.Height;
			foreach (VolleyShot shot in shots)
			{
				Vector3 position = origin + shot.Direction * attack.SpawnOffset;
				position.y = height;
				SpriteRenderer body = FxSpritePool.Rent(CombatFxRunner.Instance, attack.Look.Fly[0], attack.Look.Material);
				body.transform.position = position;
				body.transform.localScale = Vector3.one * attack.Look.Scale;
				bullets.Add(new Bullet
				{
					Source = pending.Source,
					Attack = attack,
					Body = body,
					Position = position,
					Velocity = shot.Direction * shot.Speed,
					Remaining = attack.Range - attack.SpawnOffset,
					BornTime = Time.time,
					GroundY = origin.y,
				});
			}
		}

		private void Update()
		{
			for (int i = pendings.Count - 1; i >= 0; i--)
			{
				if (EmitDue(pendings[i]))
					pendings.RemoveAt(i);
			}
			if (bullets.Count == 0)
				return;
			PlayerObject player = PlayerProvider.TryGetExistingInstance(out PlayerProvider playerProvider) ? playerProvider.CurrentObject : null;
			for (int i = bullets.Count - 1; i >= 0; i--)
			{
				if (Tick(bullets[i], player) == false)
				{
					FxSpritePool.Return(CombatFxRunner.Instance, bullets[i].Body);
					bullets.RemoveAt(i);
				}
			}
		}

		// 더 그릴 것이 없으면 false
		private static bool Tick(Bullet bullet, PlayerObject player)
		{
			VolleyBulletLook look = bullet.Attack.Look;
			if (float.IsPositiveInfinity(bullet.FadeTime) && IsGone(bullet.Source))
				Retire(bullet);
			if (float.IsPositiveInfinity(bullet.FadeTime) == false)
				return Fade(bullet, look);
			if (bullet.Hold != null)
				return Stand(bullet, look);

			// 사거리 끝을 넘지 않게 이번 걸음을 자름
			float speed = bullet.Velocity.magnitude;
			float step = Mathf.Min(speed * Time.deltaTime, bullet.Remaining);
			Vector3 from = bullet.Position;
			bullet.Position += bullet.Velocity / speed * step;
			bullet.Remaining -= step;
			bullet.Body.transform.position = bullet.Position;
			bullet.Body.sprite = look.Fly[(int)((Time.time - bullet.BornTime) * look.FramesPerSecond) % look.Fly.Length];
			if (TryHit(bullet, from, player))
				return true;
			if (bullet.Remaining <= 0f)
				Land(bullet);
			return true;
		}

		// 이번 프레임에 지나간 길 (from 에서 지금) 이 주인공 몸에 닿았나. 프레임이 늘어져도 뚫고 지나가지 않게 선분으로 잼
		private static bool TryHit(Bullet bullet, Vector3 from, PlayerObject player)
		{
			if (player == null || player.IsAlive == false)
				return false;
			// 피격 무적 동안은 탄이 지나감 (터지지 않음)
			if (player.Health.IsHitInvincible)
				return false;
			float reach = bullet.Attack.BulletRadius + player.UnitMovement.BodyRadius;
			if (VolleyPatternMath.SegmentDistance(from, bullet.Position, player.transform.position) > reach)
				return false;
			bullet.Attack.HitEffect.Strike(bullet.Source, bullet.Position, player, MonsterAttack.MakeDamage(bullet.Source, bullet.Attack.Damage));
			Pop(bullet);
			Retire(bullet);
			return true;
		}

		// 사거리 끝. 박히는 탄이면 남고 아니면 사라짐
		private static void Land(Bullet bullet)
		{
			LingerSettings planted = bullet.Attack.Planted;
			FxHold hold = planted.Open(Time.time);
			if (hold == null)
			{
				Retire(bullet);
				return;
			}
			bullet.Hold = hold;
			bullet.Position.y = bullet.GroundY;
			bullet.Body.transform.position = bullet.Position + Vector3.up * bullet.Attack.Look.PlantedHeight;
			if (bullet.Attack.Look.Planted != null)
				bullet.Body.sprite = bullet.Attack.Look.Planted;
			Pop(bullet);
			if (planted.IsDangerous)
				LingerField.Instance.Add(bullet.Source, planted, bullet.Position, hold);
		}

		private static bool Stand(Bullet bullet, VolleyBulletLook look)
		{
			if (bullet.Hold.IsGone || Time.time >= bullet.Hold.EndTime)
			{
				Retire(bullet);
				return true;
			}
			// 깜빡임은 LobFx 의 남은 물체와 같은 결
			bool isCool = Time.time >= bullet.Hold.HarmlessTime;
			bullet.Body.color = isCool ? look.CoolTint : Color.Lerp(Color.white, look.HotTint, Mathf.PingPong(Time.time * look.HotPulseSpeed / Mathf.PI, 1f));
			return true;
		}

		// 줄어들며 사라짐
		private static bool Fade(Bullet bullet, VolleyBulletLook look)
		{
			float progress = (Time.time - bullet.FadeTime) / look.FadeSeconds;
			bullet.Body.transform.localScale = Vector3.one * (look.Scale * (1f - Mathf.Clamp01(progress)));
			return progress < 1f;
		}

		private static void Retire(Bullet bullet)
		{
			if (float.IsPositiveInfinity(bullet.FadeTime) == false)
				return;
			bullet.FadeTime = Time.time;
			// 박힌 탄은 판정 (LingerField) 도 같이 식음. 박힌 탄은 터지지 않으므로 (검사가 막음) 금 간 예고는 없음
			bullet.Hold?.Cancel(Time.time);
		}

		private static void Pop(Bullet bullet)
		{
			if (bullet.Attack.PopFx != null)
				bullet.Attack.PopFx.Play(new CombatFxContext { Position = bullet.Position, Origin = bullet.Position, Radius = bullet.Attack.PopFxRadius });
		}
	}
}
