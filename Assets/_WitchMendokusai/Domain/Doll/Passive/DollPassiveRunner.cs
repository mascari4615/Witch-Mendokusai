using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 판 하나의 인형 고유 패시브. 판 시작에 GameManager 가 만들고 다음 판 시작이나 던전 이탈 때 걷음
	public class DollPassiveRunner : MonoBehaviour
	{
		private readonly Dictionary<UnitStatType, int> applied = new();

		private DollPassiveData data;
		private PlayerObject player;
		private GameEventManager gameEventManager;
		private int hitCount;
		private bool seenDungeon;

		public int HitCount => hitCount;
		public int TiersReached { get; private set; }

		public static DollPassiveRunner Begin(PlayerObject player, DollPassiveData data, GameEventManager gameEventManager)
		{
			if (data == null)
				return null;

			GameObject host = new($"DollPassive_{data.name}");
			DollPassiveRunner runner = host.AddComponent<DollPassiveRunner>();
			runner.Bind(player, data, gameEventManager);
			return runner;
		}

		// 스탯은 다음 판 Init 에서 초기화되므로 되돌리지 않고 구독만 해제
		public void Release()
		{
			if (this == null)
				return;
			Unsubscribe();
			Destroy(gameObject);
		}

		private void Bind(PlayerObject playerObject, DollPassiveData passiveData, GameEventManager eventManager)
		{
			player = playerObject;
			data = passiveData;
			gameEventManager = eventManager;

			if (data.Kind == DollPassiveKind.HitStack)
				player.Health.OnTakeDamage += HandleDamage;
			else
			{
				gameEventManager.RegisterCallback(GameEventType.OnLevelUp, RefreshTiers);
				RefreshTiers();
			}
		}

		private void OnDestroy() => Unsubscribe();

		private void Unsubscribe()
		{
			if (player != null && player.Health != null)
				player.Health.OnTakeDamage -= HandleDamage;
			if (gameEventManager != null)
				gameEventManager.UnregisterCallback(GameEventType.OnLevelUp, RefreshTiers);
			gameEventManager = null;
		}

		// 던전을 나오면 스스로 걷음. 마을에서 쌓이지 않게
		private void Update()
		{
			// 판이 시작된 뒤 던전 밖이 되면 걷음. 시작 전환 중에는 아직 거짓
			if (DungeonManagerBridge.IsDungeon)
				seenDungeon = true;
			else if (seenDungeon)
				Release();
		}

		private void HandleDamage(DamageInfo damageInfo)
		{
			// 상태이상 틱은 맞은 것으로 세지 않음
			if (damageInfo.type == DamageType.StatusTick)
				return;
			hitCount++;
			int bonus = DollPassiveRules.HitStackBonus(hitCount, data.PercentPerHit, data.MaxStacks);
			SetApplied(data.HitStat, bonus);
		}

		private void RefreshTiers()
		{
			TiersReached = DollPassiveRules.TiersReached(player.UnitStat[UnitStatType.LEVEL_CUR], data.TierLevels);
			foreach (DollPassiveGrant grant in data.TierGrants)
				SetApplied(grant.stat, grant.amount * TiersReached);
		}

		// 목표 총량과 지금 얹은 양의 차이만 스탯에 더함
		private void SetApplied(UnitStatType stat, int target)
		{
			applied.TryGetValue(stat, out int current);
			if (current == target)
				return;
			applied[stat] = target;
			player.UnitStat[stat] += target - current;
		}
	}
}
