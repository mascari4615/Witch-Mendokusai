using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 끌기의 박자 (PullMonsterAttack). 추적 -> 예고 -> 끌어당김 -> 숨 고르기 -> 대기 동안 추적
	public class BT_PullZone : BT_StancedAttack
	{
		private readonly PullMonsterAttack attack;
		private float nextCoreHitTime;
		private bool holdsPull;
		private Action stopZone;
		private Action stopCore;
		private Action stopInhale;
		private Action uncrack;

		public BT_PullZone(UnitObject unitObject, PlayerProvider playerProvider, PullMonsterAttack attack) : base(unitObject, playerProvider, attack)
		{
			this.attack = attack;
		}

		protected override float StanceSeconds => attack.PullSeconds;

		protected override void OnWindup()
		{
			stopZone = Play(attack.WindupTelegraph, attack.Radius, attack.WindupSeconds);
			stopCore = Play(attack.CoreTelegraph, attack.CoreRadius, attack.WindupSeconds + attack.PullSeconds);
		}

		// 앞 예고는 끝 번쩍임이 남도록 끊지 않음 (끝나면 스스로 치움)
		protected override void OnStanceBegin()
		{
			stopZone = Play(attack.WindupTelegraph, attack.Radius, attack.PullSeconds);
			nextCoreHitTime = Time.time;
			stopInhale = Play(attack.StrikeFx, attack.Radius, attack.PullSeconds);
			if (attack.CracksLinger)
				uncrack = LingerField.Instance.CrackAround(unitObject, attack.Radius, Time.time + attack.CrackDelaySeconds, attack.CrackSpeed);
		}

		protected override void OnStanceTick()
		{
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;
			Vector3 center = unitObject.transform.position;
			Vector3 target = player.transform.position;
			float speed = SlimeKingPatternMath.PullSpeed(player.UnitMovement.WalkSpeed, attack.SpeedRatio);
			Vector3 velocity = SlimeKingPatternMath.PullVelocity(center, target, attack.Radius, speed);
			// 범위 밖이면 남이 건 끌림을 덮지 않음 (끌림 칸은 주인공에 하나)
			if (velocity != Vector3.zero)
			{
				player.UnitMovement.SetExternalPull(velocity, attack.HoldSeconds);
				holdsPull = true;
			}
			if (Time.time < nextCoreHitTime || SlimeKingPatternMath.IsWithin(center, target, attack.CoreRadius + player.UnitMovement.BodyRadius) == false)
				return;
			nextCoreHitTime = Time.time + attack.CoreInterval;
			player.ReceiveDamage(attack.MakeCoreDamage(unitObject));
		}

		protected override void OnStanceEnd(bool interrupted)
		{
			// 끝까지 갔으면 끝 번쩍임이 남도록 끊지 않음
			if (interrupted)
			{
				stopZone?.Invoke();
				stopCore?.Invoke();
				stopInhale?.Invoke();
				uncrack?.Invoke();
			}
			stopZone = null;
			stopCore = null;
			stopInhale = null;
			uncrack = null;
			if (holdsPull == false)
				return;
			holdsPull = false;
			PlayerObject player = playerProvider.CurrentObject;
			if (player != null)
				player.UnitMovement.ClearExternalPull();
		}

		private Action Play(CombatFx telegraph, float radius, float seconds) => telegraph == null ? null : telegraph.Play(new CombatFxContext
		{
			Position = unitObject.transform.position,
			Follow = unitObject.transform,
			Radius = radius,
			Seconds = seconds,
		});
	}
}
