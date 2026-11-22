using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 수선 대상. 판의 장비 목록과 장비별 이번 판에 받은 카드 수
	public interface IMendTarget
	{
		IReadOnlyList<EquipmentData> MendCandidates();
		int PickCount(EquipmentData equipment);
	}

	// 판 하나의 수선. 수선을 주는 장비 (반짇고리) 가 판에 있으면 GameManager 가 걸고, 다음 판 시작이나 던전 이탈 때 걷음
	// 실 카드 (MEND_FREQUENCY) 를 받기 전에는 아무것도 안 함
	public class MendRunner : MonoBehaviour
	{
		private MendData data;
		private PlayerObject player;
		private GameEventManager gameEventManager;
		private IMendTarget target;
		private bool seenDungeon;

		public int MendCount { get; private set; }

		public static MendRunner Begin(PlayerObject player, MendData data, GameEventManager gameEventManager, IMendTarget target)
		{
			GameObject host = new($"Mend_{data.name}");
			MendRunner runner = host.AddComponent<MendRunner>();
			runner.player = player;
			runner.data = data;
			runner.target = target;
			runner.gameEventManager = gameEventManager;
			gameEventManager.RegisterCallback(GameEventType.OnLevelUp, runner.HandleLevelUp);
			return runner;
		}

		public void Release()
		{
			if (this == null)
				return;
			Unsubscribe();
			Destroy(gameObject);
		}

		private void OnDestroy() => Unsubscribe();

		private void Unsubscribe()
		{
			if (gameEventManager != null)
				gameEventManager.UnregisterCallback(GameEventType.OnLevelUp, HandleLevelUp);
			gameEventManager = null;
		}

		// 판이 시작된 뒤 던전 밖이 되면 스스로 걷음
		private void Update()
		{
			if (DungeonManagerBridge.IsDungeon)
				seenDungeon = true;
			else if (seenDungeon)
				Release();
		}

		// 레벨업 누적이 주기의 배수면 받은 카드가 가장 적은 장비 하나를 수선. MendStats 가 빈 장비는 후보 아님
		private void HandleLevelUp()
		{
			int frequencyCards = player.UnitStat[UnitStatType.MEND_FREQUENCY];
			if (MendRules.IsActive(frequencyCards) == false)
				return;
			int levelUps = player.UnitStat[UnitStatType.LEVEL_CUR] - 1;
			int interval = MendRules.IntervalFromCards(data.Interval, frequencyCards, data.MinInterval);
			if (MendRules.TriggersAt(levelUps, interval) == false)
				return;

			IReadOnlyList<EquipmentData> candidates = target.MendCandidates();
			List<int> picks = new(candidates.Count);
			foreach (EquipmentData equipment in candidates)
				picks.Add(equipment != null && equipment.MendStats.Count > 0 ? target.PickCount(equipment) : -1);
			int picked = MendRules.PickLeastGrown(picks);
			if (picked < 0)
				return;

			EquipmentData mended = candidates[picked];
			int amount = MendRules.Amount(data.Amount, player.UnitStat[UnitStatType.MEND_POTENCY]);
			foreach (UnitStatType stat in mended.MendStats)
				player.UnitStat[stat] += amount;
			MendCount++;
			Debug.Log($"[Mend] 레벨업 {levelUps}회째, {mended.Name} +{amount} (주기 {interval})");
			if (UIManager.TryGetExistingInstance(out UIManager uiManager))
				uiManager.PopText(string.Format(data.TextFormat, mended.Name), TextType.Normal, player.transform.position);
		}
	}
}
