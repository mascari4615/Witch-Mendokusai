using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 도는 바늘의 박자 (SpinMonsterAttack). 예고 동안 시작 자리에 띠를 보이고 바늘이 돋음, 그 뒤 돌며 닿은 주인공에게 피해
	public class BT_Spin : BT_StancedAttack
	{
		// 행동 틱 (0.1초) 사이를 나눠 보는 수. 빠른 바늘이 주인공을 건너뛰지 않게
		private const int SWEEP_STEPS = 4;

		private readonly SpinMonsterAttack attack;
		private readonly List<Action> stops = new();
		private Vector3 forward;
		private float spinStartTime;
		private float lastElapsed;
		private float nextHitTime;

		public BT_Spin(UnitObject unitObject, PlayerProvider playerProvider, SpinMonsterAttack attack) : base(unitObject, playerProvider, attack)
		{
			this.attack = attack;
		}

		protected override float StanceSeconds => attack.SpinSeconds;

		protected override void OnWindup()
		{
			stops.Clear();
			forward = ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerProvider.Current.transform.position);
			spinStartTime = Time.time + attack.WindupSeconds;
			foreach (SpinArm arm in attack.Arms)
			{
				CombatFxContext context = new()
				{
					Position = unitObject.transform.position,
					Origin = unitObject.transform.position,
					Follow = unitObject.transform,
					Direction = Direction(arm, 0f),
					Length = arm.Length,
					Width = arm.Width,
					InnerRadius = arm.InnerRadius,
					Seconds = attack.WindupSeconds,
				};
				if (attack.WindupTelegraph != null)
					stops.Add(attack.WindupTelegraph.Play(context));
				if (attack.SweepTelegraph != null)
				{
					float half = Mathf.Sign(arm.DegreesPerSecond) * attack.SweepDegrees * 0.5f;
					stops.Add(attack.SweepTelegraph.Play(new CombatFxContext
					{
						Position = unitObject.transform.position,
						Follow = unitObject.transform,
						Direction = Quaternion.Euler(0f, half, 0f) * context.Direction,
						Radius = arm.Length,
						ArcDegrees = attack.SweepDegrees,
						Seconds = attack.WindupSeconds,
					}));
				}
				// 바늘은 예고부터 끝까지 한 물체. 예고 동안 각도 0
				context.Seconds = attack.WindupSeconds + attack.SpinSeconds;
				context.Turn = seconds => arm.AngleAt(seconds - attack.WindupSeconds);
				stops.Add(arm.Fx.Play(context));
			}
		}

		protected override void OnStanceBegin()
		{
			// 행동 틱이 늦게 와도 바늘 그림의 시계로
			lastElapsed = 0f;
			if (attack.StrikeFx != null)
				attack.StrikeFx.Play(new CombatFxContext { Position = unitObject.transform.position, Radius = attack.StrikeFxRadius });
		}

		protected override void OnStanceTick()
		{
			float elapsed = Time.time - spinStartTime;
			PlayerObject player = playerProvider.CurrentObject;
			if (player != null && player.IsAlive && Time.time >= nextHitTime && elapsed <= attack.SpinSeconds - attack.FoldSeconds)
				TryHit(player, elapsed);
			lastElapsed = elapsed;
		}

		private void TryHit(PlayerObject player, float elapsed)
		{
			Vector3 origin = unitObject.transform.position;
			Vector3 point = player.transform.position;
			float bodyRadius = player.UnitMovement.BodyRadius;
			foreach (SpinArm arm in attack.Arms)
			{
				if (SlimeKingPatternMath.IsWithin(origin, point, arm.InnerRadius))
					continue;
				for (int i = 1; i <= SWEEP_STEPS; i++)
				{
					Vector3 direction = Direction(arm, Mathf.Lerp(lastElapsed, elapsed, (float)i / SWEEP_STEPS));
					if (LineMonsterAttack.IsInStrip(origin, direction, arm.Length, arm.Width, point, bodyRadius) == false)
						continue;
					nextHitTime = Time.time + attack.HitInterval;
					attack.HitEffect.Strike(unitObject, origin, player, MonsterAttack.MakeDamage(unitObject, attack.Damage));
					return;
				}
			}
		}

		private Vector3 Direction(SpinArm arm, float elapsed) => Quaternion.Euler(0f, arm.StartAngle + arm.AngleAt(elapsed), 0f) * forward;

		// 끝까지 갔으면 바늘이 같은 길이라 스스로 사라짐
		protected override void OnStanceEnd(bool interrupted)
		{
			if (interrupted)
			{
				foreach (Action stop in stops)
					stop();
			}
			stops.Clear();
		}
	}
}
