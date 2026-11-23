using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 판 하나의 레벨 기준 성장. 성장 자료를 가진 장비가 판에 있으면 GameManager 가 걸고, 다음 판 시작이나 던전 이탈 때 걷음
	// 카드 (켜짐 스탯) 를 받기 전에는 아무것도 안 함. 받은 뒤 레벨업마다 대상 스탯을 올리고 상한에서 멈춤
	public class LevelGrowthRunner : MonoBehaviour
	{
		private LevelGrowthData data;
		private PlayerObject player;
		private GameEventManager gameEventManager;
		private bool seenDungeon;
		// 항목별 이번 판에 더한 양
		private readonly List<int> granted = new();

		public static LevelGrowthRunner Begin(PlayerObject player, LevelGrowthData data, GameEventManager gameEventManager)
		{
			GameObject host = new($"LevelGrowth_{data.name}");
			LevelGrowthRunner runner = host.AddComponent<LevelGrowthRunner>();
			runner.player = player;
			runner.data = data;
			runner.gameEventManager = gameEventManager;
			foreach (LevelGrowthData.Entry _ in data.Entries)
				runner.granted.Add(0);
			gameEventManager.RegisterCallback(GameEventType.OnLevelUp, runner.HandleLevelUp);
			return runner;
		}

		public int Granted(int entryIndex) => granted[entryIndex];

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

		private void HandleLevelUp()
		{
			for (int i = 0; i < data.Entries.Count; i++)
			{
				LevelGrowthData.Entry entry = data.Entries[i];
				int amount = LevelGrowthRules.Grant(granted[i], player.UnitStat[entry.CardStat], entry.AmountPerLevel, entry.Cap);
				if (amount <= 0)
					continue;
				player.UnitStat[entry.TargetStat] += amount;
				granted[i] += amount;
			}
		}
	}
}
