using System;
using VContainer;
using VContainer.Unity;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 정예와 보스 처치에서 봉인된 두루마리를 떨굼. 처치 드롭 표와 따로 굴림 (ScrollDropRules).
	/// 두루마리를 푸는 창이 개발 빌드 전용인 동안은 떨구기도 개발 빌드에서만 (DevContent.IsShown). 출시판에 쓸 수 없는 아이템이 쌓이지 않게
	/// </summary>
	public sealed class ScrollDropper : IStartable, IDisposable
	{
		private readonly ResourceManager resourceManager;
		private readonly GameLogic gameLogic;
		private bool subscribed;

		[Inject]
		public ScrollDropper(ResourceManager resourceManager, GameLogic gameLogic)
		{
			this.resourceManager = resourceManager;
			this.gameLogic = gameLogic;
		}

		public void Start()
		{
			if (DevContent.IsShown == false)
			{
				return;
			}
			EventBusBridge.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
			subscribed = true;
		}

		public void Dispose()
		{
			if (subscribed)
			{
				EventBusBridge.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
				subscribed = false;
			}
		}

		private void OnMonsterKilled(MonsterKilledEvent killed)
		{
			ScrollTableSO table = resourceManager.Scrolls;
			ScrollDropChances chances = new(table.EliteDropPercent, table.BossDropPercent, table.PaperShare);
			bool isBoss = killed.Monster.Type == MonsterType.Boss;
			if (ScrollDropRules.TryDrop(isBoss, killed.IsElite, chances, UnityEngine.Random.value, UnityEngine.Random.value, out bool isPaper))
			{
				gameLogic.SpawnItem(isPaper ? table.PaperScroll : table.TurnScroll, killed.Position);
			}
		}
	}
}
