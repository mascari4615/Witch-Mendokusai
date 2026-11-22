using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 판정. 게임 시작 (세이브 로드) 부터 모든 업적을 늘 봄. 퀘스트처럼 목록에 넣을 필요 없음.
	///
	/// - 재는 값 (기존 Criteria 의 GetCurValue) 을 OnTick 마다 읽어 AchievementTiers 로 단계를 고름
	/// - 단계가 오르면 장부 AchievementBook 에 적고, 오른 단계마다 AchievementUnlockedEvent 를 냄
	/// - 로드 직후 첫 판정은 조용히 (옛 세이브 따라잡기로 알림이 쏟아지지 않게). 플랫폼은 장부 재동기화로 받음
	/// - 장부를 AchievementLedgerBridge 에 등록해 플랫폼 어댑터가 읽게 함 (Domain 을 모르는 채로)
	/// </summary>
	public sealed class AchievementManager : IDisposable
	{
		private const string DATE_FORMAT = "yyyy-MM-dd";

		private sealed class Watched
		{
			public AchievementSO Achievement { get; }
			public Criteria Measure { get; }

			public Watched(AchievementSO achievement, Criteria measure)
			{
				Achievement = achievement;
				Measure = measure;
			}
		}

		private readonly List<Watched> watched = new();
		private readonly List<AchievementSO> ordered = new();
		private bool ticking;

		public AchievementBook Book { get; private set; }

		/// <summary>분류 순서, 그다음 ID 순서</summary>
		public IReadOnlyList<AchievementSO> Achievements => ordered;

		/// <summary>장부가 바뀜 (로드, 단계 오름). 화면이 다시 그림</summary>
		public event Action Changed = delegate { };

		/// <summary>세이브 로드 끝에 SaveManager 가 부름. 다시 불러도 됨 (로드할 때마다 조각이 새것)</summary>
		public void Begin(AchievementSaveSlice slice, CriteriaContext context)
		{
			Book = new AchievementBook(slice.Records);

			watched.Clear();
			SOHelper.ForEach<AchievementSO>(achievement =>
			{
				AchievementTiers.Validate(achievement.name, achievement.TierTargets);
				watched.Add(new Watched(achievement, CriteriaFactory.Create(achievement.ToMeasureCriteria(), context)));
			});
			watched.Sort(CompareWatched);
			ordered.Clear();
			foreach (Watched entry in watched)
			{
				ordered.Add(entry.Achievement);
			}

			ApplyLegacy(slice);
			Evaluate(announce: false);

			if (ticking == false)
			{
				GameEventBridge.RegisterCallback(GameEventType.OnTick, OnTick);
				ticking = true;
			}
			AchievementLedgerBridge.Register(Book);
			Changed();
		}

		public void Dispose()
		{
			if (ticking)
			{
				GameEventBridge.UnregisterCallback(GameEventType.OnTick, OnTick);
				ticking = false;
			}
			AchievementLedgerBridge.Unregister(Book);
		}

		/// <summary>지금 재는 값. 화면의 진행 x/y</summary>
		public int CurrentValue(AchievementSO achievement)
		{
			foreach (Watched entry in watched)
			{
				if (entry.Achievement == achievement)
				{
					return entry.Measure.GetCurValue();
				}
			}
			throw new KeyNotFoundException($"{achievement.name} 은 판정 목록에 없음. Begin 전이거나 로드되지 않은 업적");
		}

		private void OnTick() => Evaluate(announce: true);

		private void Evaluate(bool announce)
		{
			bool changed = false;
			string today = DateTime.Now.ToString(DATE_FORMAT);
			foreach (Watched entry in watched)
			{
				AchievementSO achievement = entry.Achievement;
				int tier = AchievementTiers.Resolve(entry.Measure.GetCurValue(), achievement.TierTargets);
				int before = Book.Advance(achievement.ID, tier, today);
				if (tier <= before)
				{
					continue;
				}
				changed = true;
				if (announce == false)
				{
					continue;
				}
				for (int reached = before + 1; reached <= tier; reached++)
				{
					EventBusBridge.Publish(new AchievementUnlockedEvent(achievement.ID, reached));
				}
			}
			if (changed)
			{
				Changed();
			}
		}

		/// <summary>업적이 퀘스트였던 세이브: 끝낸 퀘스트를 자산이 가리키는 업적 단계로 옮김. 한 번만</summary>
		private void ApplyLegacy(AchievementSaveSlice slice)
		{
			if (slice.LegacyCompletedQuestIds.Count == 0)
			{
				return;
			}
			string today = DateTime.Now.ToString(DATE_FORMAT);
			foreach (Watched entry in watched)
			{
				AchievementSO achievement = entry.Achievement;
				if (achievement.LegacyQuestId != AchievementSO.NO_LEGACY_QUEST && slice.LegacyCompletedQuestIds.Contains(achievement.LegacyQuestId))
				{
					Book.Advance(achievement.ID, achievement.LegacyTier, today);
				}
			}
			slice.LegacyCompletedQuestIds.Clear();
		}

		private static int CompareWatched(Watched left, Watched right)
		{
			int byCategory = left.Achievement.Category.Order.CompareTo(right.Achievement.Category.Order);
			return byCategory != 0 ? byCategory : left.Achievement.ID.CompareTo(right.Achievement.ID);
		}
	}
}
