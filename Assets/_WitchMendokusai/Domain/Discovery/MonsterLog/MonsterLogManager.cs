using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 기록. 던전의 처치와 쓰러짐 사건을 종별로 장부에 적고, 단계가 오르면 알림 사건을 냄.
	/// 목록과 사는 곳은 던전 웨이브에서 (<see cref="MonsterHabitats"/>). 도감 화면과 해금 출처가 여기를 읽음
	/// </summary>
	public sealed class MonsterLogManager : IDisposable
	{
		private const string DATE_FORMAT = "yyyy-MM-dd";

		private readonly ResourceManager resourceManager;
		private readonly List<Monster> catalog = new();
		private Dictionary<Monster, List<MonsterHabitat>> habitats = new();
		private bool subscribed;

		/// <summary>로드 전이면 null</summary>
		public MonsterLogBook Book { get; private set; }

		/// <summary>도감에 실리는 종. 보스는 뒤, 그 안에서는 ID 순</summary>
		public IReadOnlyList<Monster> Catalog => catalog;

		/// <summary>화면이 다시 그릴 때. 처치마다가 아니라 단계가 오르거나 확인 표시가 바뀔 때</summary>
		public event Action Changed = delegate { };

		[Inject]
		public MonsterLogManager(ResourceManager resourceManager)
		{
			this.resourceManager = resourceManager;
		}

		private MonsterLogRules Rules => resourceManager.MonsterLog;

		/// <summary>세이브 로드 끝에 SaveManager 가 부름. 다시 불러도 됨 (로드할 때마다 조각이 새것)</summary>
		public void Begin(MonsterLogSaveSlice slice)
		{
			Rules.ValidateTargets();
			Book = new MonsterLogBook(slice.Records);

			List<Dungeon> dungeons = new();
			SOHelper.ForEach<Dungeon>(dungeon => dungeons.Add(dungeon));
			habitats = MonsterHabitats.Collect(dungeons, DevContent.IsShown);
			catalog.Clear();
			catalog.AddRange(habitats.Keys);
			catalog.Sort(Compare);

			if (subscribed == false)
			{
				EventBusBridge.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
				EventBusBridge.Subscribe<PlayerDefeatedEvent>(OnPlayerDefeated);
				subscribed = true;
			}
			Changed();
		}

		public void Dispose()
		{
			if (subscribed)
			{
				EventBusBridge.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
				EventBusBridge.Unsubscribe<PlayerDefeatedEvent>(OnPlayerDefeated);
				subscribed = false;
			}
		}

		/// <summary>이 종의 단계 목표. 흔한 종, 드문 종, 보스 표가 따로</summary>
		public IReadOnlyList<int> TargetsOf(Monster monster) => Rules.TargetsOf(monster, HasStory(monster));

		/// <summary>이야기 글이 적힌 종인지. 없으면 이야기 칸도 단계도 없음. 글이 생기면 처치 수로 다시 매겨짐</summary>
		public static bool HasStory(Monster monster) => string.IsNullOrWhiteSpace(monster.Description) == false;

		public int StageOf(Monster monster) => Book.StageOf(monster.ID, TargetsOf(monster));

		public bool HasNews(Monster monster) => Book.HasNews(monster.ID, TargetsOf(monster));

		public IReadOnlyList<MonsterHabitat> HabitatsOf(Monster monster) =>
			habitats.TryGetValue(monster, out List<MonsterHabitat> list) ? list : Array.Empty<MonsterHabitat>();

		/// <summary>판 밖의 기본 능력치 (위협 단계와 정예 배율 전)</summary>
		public UnitStat BaseStatOf(Monster monster) => monster.InitStatInfos.GetUnitStat(resourceManager.UnitBaseStats);

		public void MarkSeen(Monster monster)
		{
			if (HasNews(monster))
			{
				Book.MarkSeen(monster.ID, TargetsOf(monster));
				Changed();
			}
		}

		private void OnMonsterKilled(MonsterKilledEvent evt)
		{
			if (Book == null || evt.Monster == null)
			{
				return;
			}
			int dungeonId = DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) && dungeonManager.CurDungeon != null
				? dungeonManager.CurDungeon.ID
				: MonsterLogBook.NO_DUNGEON;
			IReadOnlyList<int> targets = TargetsOf(evt.Monster);
			// 처치마다 부르므로 날짜 글자는 첫 처치 때만
			string today = Book.KillsOf(evt.Monster.ID) == 0 ? DateTime.Now.ToString(DATE_FORMAT) : null;
			(int before, int after) = Book.AddKill(evt.Monster.ID, evt.IsElite, dungeonId, today, targets);
			// 목록에 없는 종 (연습 인형 등) 은 세기만
			if (after > before && habitats.ContainsKey(evt.Monster))
			{
				EventBusBridge.Publish(new MonsterLogStageEvent { Monster = evt.Monster, Stage = after, MaxStage = targets.Count });
				Changed();
			}
		}

		private void OnPlayerDefeated(PlayerDefeatedEvent evt)
		{
			if (Book == null || evt.Monster == null)
			{
				return;
			}
			Book.AddDefeat(evt.Monster.ID);
			Changed();
		}

		private static int Compare(Monster left, Monster right)
		{
			int byType = left.Type.CompareTo(right.Type);
			return byType != 0 ? byType : left.ID.CompareTo(right.ID);
		}
	}
}
