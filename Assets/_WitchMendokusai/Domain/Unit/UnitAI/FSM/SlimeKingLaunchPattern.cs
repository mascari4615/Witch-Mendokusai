using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 슬라임 발사. 보스가 멈춰서 일반 슬라임을 포물선으로 던지고, 착지 자리에 예고 원, 착지하면 그 자리에 소환
	/// - 날아가는 판과 착지 소환은 SlimeKingSummons (직선 투척과 같이 씀)
	/// - 날아가는 판과 착지는 실행기 (CombatFxRunner) 가 돌림. 보스가 도중에 사라져도 이미 던진 것은 착지
	/// - 이 보스가 소환해 살아 있는 수가 상한 (LaunchMaxAlive) 이면 착지해도 소환 안 함
	/// - 보스가 죽거나 풀로 돌아간 뒤 착지한 것은 피해 없음 (소환은 함)
	/// - 수치는 SlimeKingAttackConfig
	/// </summary>
	public sealed class SlimeKingLaunchPattern
	{
		private readonly UnitObject boss;
		private readonly PlayerProvider playerProvider;
		private readonly SlimeKingAttackConfig config;
		private float elapsed;
		private int shotsFired;
		private float startAngle;
		private readonly SlimeKingSummons summons;

		public SlimeKingLaunchPattern(UnitObject boss, PlayerProvider playerProvider, SlimeKingAttackConfig config, SlimeKingSummons summons)
		{
			this.boss = boss;
			this.playerProvider = playerProvider;
			this.config = config;
			this.summons = summons;
		}

		/// <summary> 다 던지고 마지막 착지와 회복까지 걸리는 시간 (초) </summary>
		public float TotalSeconds => config.LaunchWindup + config.LaunchInterval * (config.LaunchCount - 1) + config.LaunchFlightSeconds + config.LaunchRecovery;

		public void Begin()
		{
			elapsed = 0f;
			shotsFired = 0;
			startAngle = Random.Range(0f, 360f);
			boss.UnitMovement.SetMoveDirection(Vector3.zero);
		}

		/// <summary> 끝났으면 true </summary>
		public bool Tick(float deltaTime)
		{
			elapsed += deltaTime;
			boss.UnitMovement.SetMoveDirection(Vector3.zero);
			while (shotsFired < config.LaunchCount && elapsed >= config.LaunchWindup + config.LaunchInterval * shotsFired)
			{
				Fire(shotsFired);
				shotsFired++;
			}
			return shotsFired >= config.LaunchCount && elapsed >= TotalSeconds;
		}

		/// <summary> 남은 발사 취소. 이미 날아가는 것은 그대로 착지 </summary>
		public void Cancel() => shotsFired = config.LaunchCount;

		private void Fire(int index)
		{
			Vector3 from = boss.transform.position;
			Vector3 around = playerProvider.Current != null ? playerProvider.Current.transform.position : from;
			Vector3 landing = SlimeKingPatternMath.LandingPoint(around, index, config.LaunchCount, startAngle, Random.value, Random.value, config.LaunchLandMinDistance, config.LaunchLandMaxDistance, config.LaunchAngleJitter);
			landing.y = around.y;

			// 착지 예고는 날아가는 시간 동안 차오르고 착지 순간 번쩍임. 끊지 않음
			if (config.LaunchTelegraph != null)
			{
				config.LaunchTelegraph.Play(new CombatFxContext
				{
					Position = landing,
					Radius = config.LaunchMarkerRadius,
					Seconds = config.LaunchFlightSeconds,
				});
			}

			GameObject shot = summons.RentShot();
			CombatFxRunner.Instance.Run(Fly(shot, from, landing), () => summons.ReturnShot(shot));
		}

		private IEnumerator Fly(GameObject shot, Vector3 from, Vector3 landing)
		{
			float flight = 0f;
			while (flight < config.LaunchFlightSeconds)
			{
				shot.transform.position = SlimeKingPatternMath.ArcPoint(from, landing, config.LaunchArcHeight, flight / config.LaunchFlightSeconds);
				if (Camera.main != null)
					shot.transform.rotation = Camera.main.transform.rotation;
				yield return null;
				flight += Time.deltaTime;
			}
			Land(landing);
		}

		private void Land(Vector3 landing)
		{
			HitPlayerInMarker(landing);
			summons.Summon(landing);
		}

		private void HitPlayerInMarker(Vector3 landing)
		{
			if (config.LaunchLandingDamage <= 0 || boss == null || boss.isActiveAndEnabled == false || boss.IsAlive == false)
				return;
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;
			if (SlimeKingPatternMath.IsWithin(landing, player.transform.position, config.LaunchMarkerRadius + player.UnitMovement.BodyRadius) == false)
				return;
			player.ReceiveDamage(SlimeKingDamage.Make(boss, config.LaunchLandingDamage));
		}
	}
}
