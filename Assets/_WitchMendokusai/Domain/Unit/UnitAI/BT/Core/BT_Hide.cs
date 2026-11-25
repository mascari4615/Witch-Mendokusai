using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 사라졌다 나타나기의 박자 (HideMonsterAttack). 예고 -> 사라져 쫓음 -> 자리 고정과 예고 -> 나타나며 침 -> 숨 고르기
	public class BT_Hide : BT_StancedAttack
	{
		private readonly HideMonsterAttack attack;
		private readonly List<Vector3> spots = new();
		private readonly List<Action> stopTelegraphs = new();
		private int safeIndex;
		private float lockTime;
		private float strikeTime;
		private bool isLocked;
		private Action stopShadow;

		public BT_Hide(UnitObject unitObject, PlayerProvider playerProvider, HideMonsterAttack attack) : base(unitObject, playerProvider, attack)
		{
			this.attack = attack;
		}

		protected override float StanceSeconds => attack.HiddenSeconds;
		protected override bool StrikesAtBegin => false;

		protected override void OnWindup() => unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();

		protected override void OnStanceBegin()
		{
			isLocked = false;
			lockTime = Time.time + attack.ChaseSeconds;
			strikeTime = Time.time + attack.HiddenSeconds;
			Vector3 position = unitObject.transform.position;
			if (attack.LeaveFx != null)
				attack.LeaveFx.Play(new CombatFxContext { Position = position, Origin = position, Radius = attack.LeaveFxRadius });
			MonsterHideaway.Of(unitObject).Leave(attack, playerProvider);
			stopShadow = attack.ShadowFx == null ? null : attack.ShadowFx.Play(new CombatFxContext
			{
				Position = position,
				Follow = unitObject.transform,
				Radius = attack.Radius,
				Seconds = attack.HiddenSeconds,
			});
			if (attack.ChaseSeconds <= 0f)
				Lock();
		}

		protected override void OnStanceTick()
		{
			if (isLocked == false && Time.time >= lockTime)
				Lock();
		}

		private void Lock()
		{
			isLocked = true;
			safeIndex = attack.PickSafeIndex();
			Vector3 self = unitObject.transform.position;
			Vector3 player = playerProvider.Current.transform.position;
			player.y = self.y;
			bool isOne = attack.Count == 1;
			HideMonsterAttack.Arrange(isOne ? self : player, attack.Count, attack.Distance, Random.value * 360f, spots);
			float seconds = strikeTime - Time.time;
			for (int i = 0; i < spots.Count; i++)
			{
				bool isSafe = i == safeIndex;
				if (isSafe == false)
					Play(attack.WindupTelegraph, spots[i], seconds);
				Play(isSafe ? attack.SafeFx : attack.WindupFx, spots[i], seconds);
			}
			Vector3 landing = spots[Mathf.Max(0, safeIndex)];
			if (attack.EmergeOffset > 0f)
				landing += ScatterZoneMonsterAttack.FlatDirection(player, landing) * attack.EmergeOffset;
			if (DungeonManagerBridge.IsDungeon)
				landing = DungeonManager.Instance.MonsterSpawner.ClampToArena(landing);
			MonsterHideaway.Of(unitObject).Land(landing, strikeTime);
		}

		protected override void OnStanceEnd(bool interrupted)
		{
			if (unitObject.TryGetComponent(out MonsterHideaway hideaway))
				hideaway.Return();
			// 끝까지 갔으면 끝 번쩍임과 물체가 남도록 끊지 않음
			if (interrupted)
			{
				foreach (Action stop in stopTelegraphs)
					stop?.Invoke();
				stopShadow?.Invoke();
			}
			stopTelegraphs.Clear();
			stopShadow = null;
			bool strikes = interrupted == false && isLocked;
			isLocked = false;
			if (strikes)
				Strike();
		}

		private void Strike()
		{
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Strike();
			for (int i = 0; i < spots.Count; i++)
			{
				if (i != safeIndex && attack.StrikeFx != null)
					attack.StrikeFx.Play(new CombatFxContext { Position = spots[i], Origin = spots[i], Radius = attack.Radius });
			}
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;
			Vector3 point = player.transform.position;
			if (HideMonsterAttack.Hits(spots, safeIndex, attack.Radius, point, player.UnitMovement.BodyRadius))
				attack.HitEffect.Strike(unitObject, NearestSpot(point), player, MonsterAttack.MakeDamage(unitObject, attack.Damage));
		}

		// 밀어내는 중심
		private Vector3 NearestSpot(Vector3 point)
		{
			Vector3 nearest = spots[0];
			float best = float.PositiveInfinity;
			for (int i = 0; i < spots.Count; i++)
			{
				float distance = (spots[i] - point).sqrMagnitude;
				if (i == safeIndex || distance >= best)
					continue;
				best = distance;
				nearest = spots[i];
			}
			return nearest;
		}

		private void Play(CombatFx fx, Vector3 position, float seconds)
		{
			if (fx == null)
				return;
			stopTelegraphs.Add(fx.Play(new CombatFxContext
			{
				Position = position,
				Origin = unitObject.transform.position,
				Radius = attack.Radius,
				Seconds = seconds,
			}));
		}
	}
}
