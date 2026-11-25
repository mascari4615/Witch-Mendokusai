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

		public BT_PullZone(UnitObject unitObject, PlayerProvider playerProvider, PullMonsterAttack attack) : base(unitObject, playerProvider, attack)
		{
			this.attack = attack;
		}

		protected override float StanceSeconds => attack.PullSeconds;

		protected override void OnWindup() => stopZone = PlayZone(attack.WindupSeconds);

		// 앞 예고는 끝 번쩍임이 남도록 끊지 않음 (끝나면 스스로 치움)
		protected override void OnStanceBegin()
		{
			stopZone = PlayZone(attack.PullSeconds);
			nextCoreHitTime = Time.time;
		}

		protected override void OnStanceTick()
		{
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;
			Vector3 center = unitObject.transform.position;
			Vector3 target = player.transform.position;
			float speed = SlimeKingPatternMath.PullSpeed(player.UnitMovement.WalkSpeed, attack.SpeedRatio);
			player.UnitMovement.SetExternalPull(SlimeKingPatternMath.PullVelocity(center, target, attack.Radius, speed), attack.HoldSeconds);
			holdsPull = true;
			if (Time.time < nextCoreHitTime || SlimeKingPatternMath.IsWithin(center, target, attack.CoreRadius + player.UnitMovement.BodyRadius) == false)
				return;
			nextCoreHitTime = Time.time + attack.CoreInterval;
			player.ReceiveDamage(attack.MakeCoreDamage(unitObject));
		}

		protected override void OnStanceEnd()
		{
			stopZone?.Invoke();
			stopZone = null;
			if (holdsPull == false)
				return;
			holdsPull = false;
			PlayerObject player = playerProvider.CurrentObject;
			if (player != null)
				player.UnitMovement.ClearExternalPull();
		}

		private Action PlayZone(float seconds) => attack.WindupTelegraph == null ? null : attack.WindupTelegraph.Play(new CombatFxContext
		{
			Position = unitObject.transform.position,
			Follow = unitObject.transform,
			Radius = attack.Radius,
			Seconds = seconds,
		});
	}
}
