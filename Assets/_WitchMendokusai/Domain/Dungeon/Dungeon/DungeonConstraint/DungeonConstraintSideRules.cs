using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 제약마다 붙는 고유 규칙 (사용자 판정 2026-11-21: 단순 배율 대신 제약을 켜면 판이 달라지는 규칙)
	/// 회복 감소는 잃은 회복의 일부를 냥으로, 빠른 난이도는 난이도 오를 때 경험치 구슬, 적 수는 처치 수마다 상자, 정예 추가는 정예 처치 상자 확률
	/// 선택지 감소의 희귀도 운은 CardManager 가 DungeonManagerBridge.SideEffect 로 읽음
	/// 판 시작 Begin, 판 끝 End. DungeonManager 가 소유
	/// </summary>
	public class DungeonConstraintSideRules
	{
		private const float ROLL_RANGE = 100f;

		private readonly DataManager dataManager;
		private readonly PlayerProvider playerProvider;
		private readonly IObjectResolver container;
		private GameLogic gameLogic;

		private bool isActive;
		private int killsAtStart;
		private int chestsGiven;
		private double nyangCarry;

		public DungeonConstraintSideEffect Effect { get; private set; } = DungeonConstraintSideEffect.Identity;

		public DungeonConstraintSideRules(DataManager dataManager, PlayerProvider playerProvider, IObjectResolver container)
		{
			this.dataManager = dataManager;
			this.playerProvider = playerProvider;
			this.container = container;
		}

		// 상자와 경험치 구슬 생성. 처음 쓸 때 꺼냄 (주입 순서와 무관)
		private GameLogic GameLogic => gameLogic ??= container.Resolve<GameLogic>();

		public void Begin(DungeonConstraintSideEffect effect)
		{
			End();
			Effect = effect;
			isActive = true;
			killsAtStart = dataManager.DungeonStat[DungeonStatType.MONSTER_KILL];
			chestsGiven = 0;
			nyangCarry = 0d;
			dataManager.DungeonStat.AddListener(DungeonStatType.MONSTER_KILL, OnKill);
			EventBusBridge.Subscribe<DifficultyRaisedEvent>(OnDifficultyRaised);
			EventBusBridge.Subscribe<EliteKilledEvent>(OnEliteKilled);
		}

		public void End()
		{
			if (isActive == false)
				return;
			isActive = false;
			Effect = DungeonConstraintSideEffect.Identity;
			dataManager.DungeonStat.RemoveListener(DungeonStatType.MONSTER_KILL, OnKill);
			EventBusBridge.Unsubscribe<DifficultyRaisedEvent>(OnDifficultyRaised);
			EventBusBridge.Unsubscribe<EliteKilledEvent>(OnEliteKilled);
		}

		public void OnHealLost(int lostHeal)
		{
			if (isActive == false || Effect.HealLossToNyangPercent == 0)
				return;
			int nyang = Effect.HealLossNyang(lostHeal, ref nyangCarry);
			if (nyang > 0)
				dataManager.GameStat[GameStatType.NYANG] += nyang;
		}

		private void OnKill()
		{
			int kills = dataManager.DungeonStat[DungeonStatType.MONSTER_KILL] - killsAtStart;
			while (Effect.IsKillChestDue(kills, chestsGiven))
			{
				chestsGiven++;
				GameLogic.SpawnChest(PlayerPosition);
			}
		}

		private void OnDifficultyRaised(DifficultyRaisedEvent raised)
		{
			if (Effect.DifficultyExpOrbs > 0)
				GameLogic.SpawnExpOrb(PlayerPosition, Effect.DifficultyExpOrbs);
		}

		private void OnEliteKilled(EliteKilledEvent killed)
		{
			if (Effect.IsEliteChest(Random.value * ROLL_RANGE))
				GameLogic.SpawnChest(PlayerPosition);
		}

		private Vector3 PlayerPosition => playerProvider.CurrentObject.transform.position;
	}
}
