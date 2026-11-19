using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 빨아들이기. 예고 -> 끌어당김 -> 경직 (반격 창)
	/// - 예고: 보스 둘레 넓은 원 (안쪽으로 흐르는 화살) 이 차오름. 안쪽 접촉 피해 원도 같이
	/// - 끌어당김: 범위 안 플레이어를 보스 쪽으로. 세기는 플레이어 걷기 속도보다 약간 낮아 반대로 걸으면 벗어남
	///   안쪽 원에 닿으면 간격마다 피해. 같은 원 예고가 남은 시간만큼 다시 차오름
	/// - 경직: 보스가 멈춰 서 있음. 끌림은 바로 풀림
	/// - 끌림은 짧은 유지 시간으로 매 tick 다시 걺. 보스가 사라지면 그 시간 뒤 저절로 풀림
	/// </summary>
	public sealed class SlimeKingPullPattern
	{
		private enum Phase
		{
			Warning,
			Pulling,
			Stun,
			Done,
		}

		private readonly UnitObject boss;
		private readonly PlayerProvider playerProvider;
		private readonly SlimeKingAttackConfig config;
		private Phase phase;
		private float phaseElapsed;
		private float damageCooldown;
		private Action stopZone;
		private Action stopCore;
		// 이 패턴이 플레이어에게 끌림을 걸었는지. 다른 보스의 끌림은 안 건드림
		private bool holdsPull;

		public SlimeKingPullPattern(UnitObject boss, PlayerProvider playerProvider, SlimeKingAttackConfig config)
		{
			this.boss = boss;
			this.playerProvider = playerProvider;
			this.config = config;
		}

		public bool IsPulling => phase == Phase.Pulling;
		public bool IsStunned => phase == Phase.Stun;

		public void Begin()
		{
			StopTelegraphs();
			phase = Phase.Warning;
			phaseElapsed = 0f;
			damageCooldown = 0f;
			boss.UnitMovement.SetMoveDirection(Vector3.zero);
			stopZone = PlayZone(config.PullWarningSeconds);
			stopCore = PlayCore(config.PullWarningSeconds + config.PullSeconds);
		}

		/// <summary> 끝났으면 true. deltaTime 은 FSM tick </summary>
		public bool Tick(float deltaTime)
		{
			phaseElapsed += deltaTime;
			boss.UnitMovement.SetMoveDirection(Vector3.zero);
			switch (phase)
			{
				case Phase.Warning:
					if (phaseElapsed >= config.PullWarningSeconds)
						EnterPulling();
					break;
				case Phase.Pulling:
					PullPlayer(deltaTime);
					if (phaseElapsed >= config.PullSeconds)
						EnterStun();
					break;
				case Phase.Stun:
					if (phaseElapsed >= config.PullStunSeconds)
						phase = Phase.Done;
					break;
			}
			return phase == Phase.Done;
		}

		public void Cancel()
		{
			StopTelegraphs();
			ReleasePlayer();
			phase = Phase.Done;
		}

		private void EnterPulling()
		{
			phase = Phase.Pulling;
			phaseElapsed = 0f;
			stopZone = PlayZone(config.PullSeconds);
		}

		private void EnterStun()
		{
			phase = Phase.Stun;
			phaseElapsed = 0f;
			ReleasePlayer();
		}

		private void PullPlayer(float deltaTime)
		{
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;

			Vector3 center = boss.transform.position;
			Vector3 target = player.transform.position;
			float speed = SlimeKingPatternMath.PullSpeed(player.UnitMovement.WalkSpeed, config.PullSpeedRatio);
			Vector3 velocity = SlimeKingPatternMath.PullVelocity(center, target, config.PullRadius, speed);
			player.UnitMovement.SetExternalPull(velocity, config.PullHoldSeconds);
			holdsPull = true;

			damageCooldown -= deltaTime;
			float touchRadius = config.PullCoreRadius + player.UnitMovement.BodyRadius;
			if (config.PullCoreDamage > 0 && damageCooldown <= 0f && SlimeKingPatternMath.IsWithin(center, target, touchRadius))
			{
				player.ReceiveDamage(SlimeKingDamage.Make(boss, config.PullCoreDamage));
				damageCooldown = config.PullCoreDamageInterval;
			}
		}

		private void ReleasePlayer()
		{
			if (holdsPull == false)
				return;
			holdsPull = false;
			PlayerObject player = playerProvider.CurrentObject;
			if (player != null)
				player.UnitMovement.ClearExternalPull();
		}

		// 앞 예고는 끝 번쩍임이 남도록 끊지 않음 (끝나면 스스로 치움)
		private Action PlayZone(float seconds)
		{
			if (config.PullTelegraph == null)
				return null;
			return config.PullTelegraph.Play(new CombatFxContext
			{
				Position = boss.transform.position,
				Follow = boss.transform,
				Radius = config.PullRadius,
				Seconds = seconds,
			});
		}

		private Action PlayCore(float seconds)
		{
			if (config.PullCoreTelegraph == null || config.PullCoreRadius <= 0f)
				return null;
			return config.PullCoreTelegraph.Play(new CombatFxContext
			{
				Position = boss.transform.position,
				Follow = boss.transform,
				Radius = config.PullCoreRadius,
				Seconds = seconds,
			});
		}

		private void StopTelegraphs()
		{
			stopZone?.Invoke();
			stopZone = null;
			stopCore?.Invoke();
			stopCore = null;
		}
	}
}
