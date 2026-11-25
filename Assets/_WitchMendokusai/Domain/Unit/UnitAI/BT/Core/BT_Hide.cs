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
		// 실제로 때리는 자리
		private readonly List<Vector3> strikes = new();
		private readonly List<Action> stopTelegraphs = new();
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
			strikes.Clear();
			float seconds = strikeTime - Time.time;
			Vector3 landing = attack.Count == 1 ? MonsterHideaway.Clamp(unitObject, unitObject.transform.position) : LockRing(seconds);
			if (attack.Count == 1)
				AddStrike(landing, seconds, true);
			MonsterHideaway.Of(unitObject).Land(landing, strikeTime);
		}

		// 주인공 둘레 고리. 진짜 자리 (없으면 첫 자리) 에서 나타남
		private Vector3 LockRing(float seconds)
		{
			Vector3 center = playerProvider.Current.transform.position;
			center.y = unitObject.transform.position.y;
			int safeIndex = Arrange(center);
			for (int i = 0; i < spots.Count; i++)
			{
				if (i == safeIndex)
					Play(attack.SafeFx, spots[i], seconds);
				else
					AddStrike(spots[i], seconds, false);
			}
			Vector3 landing = spots[Mathf.Max(0, safeIndex)];
			return MonsterHideaway.Clamp(unitObject, landing + ScatterZoneMonsterAttack.FlatDirection(center, landing) * attack.EmergeOffset);
		}

		// 막힌 데 걸리지 않은 자리만 진짜로. 다 막혔으면 고리를 돌려 다시
		private int Arrange(Vector3 center)
		{
			HideMonsterAttack.Arrange(center, attack.Count, attack.Distance, Random.value * 360f, spots);
			if (attack.HasSafeSpot == false)
				return -1;
			for (int attempt = 0; attempt < attack.ArrangeTries; attempt++)
			{
				int first = Random.Range(0, spots.Count);
				for (int i = 0; i < spots.Count; i++)
				{
					int index = (first + i) % spots.Count;
					if (IsOpen(spots[index]))
						return index;
				}
				HideMonsterAttack.Arrange(center, attack.Count, attack.Distance, Random.value * 360f, spots);
			}
			return 0;
		}

		private bool IsOpen(Vector3 spot) => (MonsterHideaway.Clamp(unitObject, spot) - spot).sqrMagnitude < attack.Radius * attack.Radius * 0.01f;

		// 예고 판 상한에 걸려 안 보이는 자리는 안 때림. 하나짜리는 그림자가 예고라 때림
		private void AddStrike(Vector3 spot, float seconds, bool isOnly)
		{
			Action stop = Play(attack.WindupTelegraph, spot, seconds);
			if (GroundTelegraph.IsSkipped(stop) && isOnly == false)
				return;
			Play(attack.WindupFx, spot, seconds);
			strikes.Add(spot);
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
			if (attack.StrikeFx != null)
			{
				foreach (Vector3 strike in strikes)
					attack.StrikeFx.Play(new CombatFxContext { Position = strike, Origin = strike, Radius = attack.Radius });
			}
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null || player.IsAlive == false)
				return;
			Vector3 point = player.transform.position;
			if (HideMonsterAttack.Hits(strikes, attack.Radius, point, player.UnitMovement.BodyRadius))
				attack.HitEffect.Strike(unitObject, PushCenter(point), player, MonsterAttack.MakeDamage(unitObject, attack.Damage));
		}

		// 밀어내는 중심. 한가운데 겹쳤으면 아무 쪽으로나 밀리게 살짝 비킴
		private Vector3 PushCenter(Vector3 point)
		{
			Vector3 nearest = strikes[0];
			float best = float.PositiveInfinity;
			foreach (Vector3 strike in strikes)
			{
				float distance = (strike - point).sqrMagnitude;
				if (distance >= best)
					continue;
				best = distance;
				nearest = strike;
			}
			if (MonsterHitEffect.PushDirection(nearest, point) == Vector3.zero)
				nearest += Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
			return nearest;
		}

		private Action Play(CombatFx fx, Vector3 position, float seconds)
		{
			if (fx == null)
				return null;
			Action stop = fx.Play(new CombatFxContext
			{
				Position = position,
				Origin = unitObject.transform.position,
				Radius = attack.Radius,
				Seconds = seconds,
			});
			stopTelegraphs.Add(stop);
			return stop;
		}
	}
}
