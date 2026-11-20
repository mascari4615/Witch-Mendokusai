using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 직선 투척 (패턴 이름 Projectile). 조준 띠 예고가 차오른 뒤 그 띠를 따라 슬라임 하나를 낮게 던짐
	/// - 방향은 조준 시작 때 플레이어 쪽으로 고정. 띠가 실제 탄 길과 같음
	/// - 날아가는 슬라임에 닿으면 피해 한 번. 끝에 닿으면 그 자리에 슬라임 소환 (발사와 같은 상한)
	/// - 이미 던진 것은 보스가 사라져도 끝까지 날아감. 보스가 없으면 피해 없음
	/// - 수치는 SlimeKingAttackConfig 직선 투척 칸
	/// </summary>
	public sealed class SlimeKingShotPattern
	{
		private enum Phase
		{
			Aim = 0,
			Flight = 1,
			Recovery = 2,
			Done = 3,
		}

		private readonly UnitObject boss;
		private readonly PlayerProvider playerProvider;
		private readonly SlimeKingAttackConfig config;
		private readonly SlimeKingSummons summons;
		private Phase phase;
		private float phaseElapsed;
		private Vector3 origin;
		private Vector3 direction;
		private Action stopAim;

		public SlimeKingShotPattern(UnitObject boss, PlayerProvider playerProvider, SlimeKingAttackConfig config, SlimeKingSummons summons)
		{
			this.boss = boss;
			this.playerProvider = playerProvider;
			this.config = config;
			this.summons = summons;
		}

		public void Begin()
		{
			StopAim();
			phase = Phase.Aim;
			phaseElapsed = 0f;
			boss.UnitMovement.SetMoveDirection(Vector3.zero);
			origin = boss.transform.position;
			direction = ResolveDirection();
			if (config.ShotTelegraph != null)
			{
				stopAim = config.ShotTelegraph.Play(new CombatFxContext
				{
					Position = origin,
					Direction = direction,
					Length = config.ShotRange,
					Width = config.ShotWidth,
					Seconds = config.ShotWindup,
				});
			}
		}

		/// <summary> 끝났으면 true. deltaTime 은 FSM tick </summary>
		public bool Tick(float deltaTime)
		{
			phaseElapsed += deltaTime;
			boss.UnitMovement.SetMoveDirection(Vector3.zero);
			switch (phase)
			{
				case Phase.Aim:
					if (phaseElapsed >= config.ShotWindup)
						Fire();
					break;
				case Phase.Flight:
					if (phaseElapsed >= config.ShotFlightSeconds)
						EnterPhase(Phase.Recovery);
					break;
				case Phase.Recovery:
					if (phaseElapsed >= config.ShotRecovery)
						EnterPhase(Phase.Done);
					break;
			}
			return phase == Phase.Done;
		}

		/// <summary> 조준 중이면 예고를 치우고 안 던짐. 이미 던진 것은 그대로 날아감 </summary>
		public void Cancel()
		{
			StopAim();
			phase = Phase.Done;
		}

		private void EnterPhase(Phase next)
		{
			phase = next;
			phaseElapsed = 0f;
		}

		// 플레이어 쪽 수평 방향. 겹쳐 있으면 보스가 보는 쪽, 그것도 없으면 앞
		private Vector3 ResolveDirection()
		{
			Vector3 toPlayer = playerProvider.Current != null ? playerProvider.Current.transform.position - origin : Vector3.zero;
			toPlayer.y = 0f;
			if (toPlayer.sqrMagnitude > SlimeKingPatternMath.DIRECTION_EPSILON_SQR)
				return toPlayer.normalized;
			Vector3 forward = boss.transform.forward;
			forward.y = 0f;
			return forward.sqrMagnitude > SlimeKingPatternMath.DIRECTION_EPSILON_SQR ? forward.normalized : Vector3.forward;
		}

		private void Fire()
		{
			// 조준 예고는 끝 번쩍임이 남도록 끊지 않음
			stopAim = null;
			EnterPhase(Phase.Flight);
			Vector3 from = origin;
			Vector3 to = origin + direction * config.ShotRange;
			GameObject shot = summons.RentShot();
			CombatFxRunner.Instance.Run(Fly(shot, from, to), () => summons.ReturnShot(shot));
		}

		private IEnumerator Fly(GameObject shot, Vector3 from, Vector3 to)
		{
			bool hit = false;
			float flight = 0f;
			while (flight < config.ShotFlightSeconds)
			{
				Vector3 position = SlimeKingPatternMath.ArcPoint(from, to, config.ShotArcHeight, flight / config.ShotFlightSeconds);
				shot.transform.position = position;
				if (Camera.main != null)
					shot.transform.rotation = Camera.main.transform.rotation;
				if (hit == false)
					hit = TryHitPlayer(position);
				yield return null;
				flight += Time.deltaTime;
			}
			if (hit == false)
				TryHitPlayer(to);
			if (config.ShotSummonsSlime)
				summons.Summon(to);
		}

		private bool TryHitPlayer(Vector3 shotPosition)
		{
			if (config.ShotDamage <= 0 || boss == null || boss.isActiveAndEnabled == false || boss.IsAlive == false)
				return false;
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return false;
			if (SlimeKingPatternMath.IsWithin(shotPosition, player.transform.position, config.ShotHitRadius + player.UnitMovement.BodyRadius) == false)
				return false;
			player.ReceiveDamage(SlimeKingDamage.Make(boss, config.ShotDamage));
			return true;
		}

		private void StopAim()
		{
			stopAim?.Invoke();
			stopAim = null;
		}
	}
}
