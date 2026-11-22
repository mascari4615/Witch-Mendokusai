using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 수선 대상. 판의 장비 목록과 장비별 이번 판에 받은 카드 수
	public interface IDollMendTarget
	{
		IReadOnlyList<EquipmentData> MendCandidates();
		int PickCount(EquipmentData equipment);
	}

	// 판 하나의 인형 고유 패시브. 판 시작에 GameManager 가 만들고 다음 판 시작이나 던전 이탈 때 걷음
	public class DollPassiveRunner : MonoBehaviour
	{
		private readonly Dictionary<UnitStatType, int> applied = new();

		private DollPassiveData data;
		private PlayerObject player;
		private GameEventManager gameEventManager;
		private IDollMendTarget mendTarget;
		private int hitCount;
		private bool seenDungeon;

		public int HitCount => hitCount;
		public int TiersReached { get; private set; }
		public int MendCount { get; private set; }

		public static DollPassiveRunner Begin(PlayerObject player, DollPassiveData data, GameEventManager gameEventManager, IDollMendTarget mendTarget)
		{
			if (data == null)
				return null;

			GameObject host = new($"DollPassive_{data.name}");
			DollPassiveRunner runner = host.AddComponent<DollPassiveRunner>();
			runner.mendTarget = mendTarget;
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
			else if (data.Kind == DollPassiveKind.Mend)
				gameEventManager.RegisterCallback(GameEventType.OnLevelUp, HandleMendLevelUp);
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
			{
				gameEventManager.UnregisterCallback(GameEventType.OnLevelUp, RefreshTiers);
				gameEventManager.UnregisterCallback(GameEventType.OnLevelUp, HandleMendLevelUp);
			}
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

		// 레벨업 누적이 주기의 배수면 가장 덜 키운 장비 하나를 수선. MendStats 가 빈 장비는 후보 아님
		private void HandleMendLevelUp()
		{
			int levelUps = player.UnitStat[UnitStatType.LEVEL_CUR] - 1;
			int reduction = data.MendIntervalStat != UnitStatType.NONE ? player.UnitStat[data.MendIntervalStat] : 0;
			int interval = DollMendRules.Interval(data.MendInterval, reduction, data.MendMinInterval);
			if (DollMendRules.TriggersAt(levelUps, interval) == false)
				return;

			IReadOnlyList<EquipmentData> candidates = mendTarget.MendCandidates();
			List<int> picks = new(candidates.Count);
			foreach (EquipmentData equipment in candidates)
				picks.Add(equipment != null && equipment.MendStats.Count > 0 ? mendTarget.PickCount(equipment) : -1);
			int picked = DollMendRules.PickLeastGrown(picks);
			if (picked < 0)
				return;

			EquipmentData target = candidates[picked];
			int bonus = data.MendAmountStat != UnitStatType.NONE ? player.UnitStat[data.MendAmountStat] : 0;
			int amount = DollMendRules.Amount(data.MendAmount, bonus);
			foreach (UnitStatType stat in target.MendStats)
				player.UnitStat[stat] += amount;
			MendCount++;
			Debug.Log($"[Mend] 레벨업 {levelUps}회째, {target.Name} +{amount} (주기 {interval})");
			if (UIManager.TryGetExistingInstance(out UIManager uiManager))
				uiManager.PopText(string.Format(data.MendTextFormat, target.Name), TextType.Normal, player.transform.position);
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
