using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 마도서 페이지 자료의 이음새. 페이지는 재료 N개를 모으는 퀘스트이고, 채우면 재료를 빼고 다음 페이지를 열어 실행 중으로 넣음.
	/// 수치 (몇 개) 는 기획이 고치는 값이라 잠그지 않고, 끊기면 진행이 멈추는 연결만 잠금
	/// </summary>
	public sealed class MagicBookPageDataTest
	{
		private const int SLIME_BOSS_ID = 1;
		private const float FULL_PERCENT = 100f;

		[Test]
		public void FirstPage_Exists_AndIsMagicBookPage()
		{
			QuestSO first = LoadQuest(QuestManager.FIRST_MAGIC_BOOK_PAGE_ID);
			Assert.That(first, Is.Not.Null, "첫 페이지 퀘스트가 없음. 새 게임이 마도서를 시작하지 못함");
			Assert.That(first.Data.Type, Is.EqualTo(QuestType.MagicBookPage));
		}

		[Test]
		public void Pages_ChainToNext_ByUnlockAndAdd()
		{
			List<QuestSO> pages = WalkPages();
			Assert.That(pages.Count, Is.GreaterThanOrEqualTo(2), "첫 페이지에서 다음 페이지로 이어지지 않음");
			for (int i = 0; i < pages.Count - 1; i++)
			{
				List<EffectInfo> effects = pages[i].Data.CompleteEffects;
				// 열기만 하면 상태만 바뀌고 실행 중 퀘스트가 안 생김. 둘 다 있어야 다음 페이지가 HUD 에 뜸
				Assert.That(effects.Any(effect => effect.Type == EffectType.UnlockQuest && effect.Data == pages[i + 1]), $"{pages[i].name}: 다음 페이지 UnlockQuest 없음");
				Assert.That(effects.Any(effect => effect.Type == EffectType.AddQuest && effect.Data == pages[i + 1]), $"{pages[i].name}: 다음 페이지 AddQuest 없음");
			}
		}

		[Test]
		public void Pages_ConsumeExactlyWhatTheyRequire()
		{
			foreach (QuestSO page in WalkPages())
			{
				Assert.That(page.Data.Type, Is.EqualTo(QuestType.MagicBookPage), $"{page.name}: 갈래가 마도서 페이지가 아님");
				Assert.That(page.Data.Criteria.Count, Is.GreaterThan(0), $"{page.name}: 조건 없음");
				foreach (CriteriaInfo criteria in page.Data.Criteria)
				{
					Assert.That(criteria.Type, Is.EqualTo(CriteriaType.ItemCount), $"{page.name}: 재료 수 조건이 아님");
					Assert.That(criteria.Data, Is.InstanceOf<ItemData>(), $"{page.name}: 조건의 재료 참조 끊김");
					bool consumed = page.Data.CompleteEffects.Any(effect =>
						effect.Type == EffectType.Item
						&& effect.Data == criteria.Data
						&& effect.ArithmeticOperator == ArithmeticOperator.Subtract
						&& effect.Value == criteria.Value);
					Assert.That(consumed, $"{page.name}: {criteria.Data.name} 을 모은 만큼 빼는 효과가 없음");
				}
			}
		}

		[Test]
		public void Pages_AllSitInOneChapter()
		{
			List<QuestSO> pages = WalkPages();
			bool found = false;
			foreach (string guid in AssetDatabase.FindAssets("t:ChapterSO"))
			{
				ChapterSO chapter = AssetDatabase.LoadAssetAtPath<ChapterSO>(AssetDatabase.GUIDToAssetPath(guid));
				List<QuestSO> targets = chapter.Nodes.OfType<QuestNode>().Select(node => node.Target).ToList();
				if (pages.All(targets.Contains))
				{
					found = true;
				}
			}
			Assert.That(found, "페이지 전부를 담은 장이 없음. 마도서 화면에 별이 안 뜸");
		}

		[Test]
		public void BossMaterial_AlwaysDropsFromSlimeBoss()
		{
			Monster boss = LoadMonster(SLIME_BOSS_ID);
			Assert.That(boss, Is.Not.Null, "슬라임 보스 자산을 못 찾음");
			HashSet<DataSO> required = new(WalkPages().SelectMany(page => page.Data.Criteria).Select(criteria => criteria.Data));
			// 드랍 표는 처치 한 번에 하나만 뽑음. 보스 재료가 반드시 나오려면 표 전체가 페이지 재료여야 하고 합이 100 이상
			Assert.That(boss.Loots.Count, Is.GreaterThan(0));
			Assert.That(boss.Loots.All(loot => required.Contains(loot.DataSO)), "보스 표에 페이지 재료가 아닌 것이 섞임. 확정 드랍이 아님");
			Assert.That(boss.Loots.Sum(loot => loot.Percentage), Is.GreaterThanOrEqualTo(FULL_PERCENT), "보스 표 합이 100 미만. 빈손일 수 있음");
		}

		// 첫 페이지에서 AddQuest 효과를 따라 끝까지
		private static List<QuestSO> WalkPages()
		{
			List<QuestSO> pages = new();
			QuestSO current = LoadQuest(QuestManager.FIRST_MAGIC_BOOK_PAGE_ID);
			while (current != null && pages.Contains(current) == false)
			{
				pages.Add(current);
				current = current.Data.CompleteEffects.FirstOrDefault(effect => effect.Type == EffectType.AddQuest).Data as QuestSO;
			}
			return pages;
		}

		private static QuestSO LoadQuest(int id)
		{
			foreach (string guid in AssetDatabase.FindAssets("t:QuestSO"))
			{
				QuestSO quest = AssetDatabase.LoadAssetAtPath<QuestSO>(AssetDatabase.GUIDToAssetPath(guid));
				if (quest != null && quest.ID == id)
					return quest;
			}
			return null;
		}

		private static Monster LoadMonster(int id)
		{
			foreach (string guid in AssetDatabase.FindAssets("t:Monster"))
			{
				Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid));
				if (monster != null && monster.ID == id)
					return monster;
			}
			return null;
		}
	}
}
