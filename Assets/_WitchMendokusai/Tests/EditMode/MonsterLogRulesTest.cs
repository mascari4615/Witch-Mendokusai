using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary>몬스터 기록의 단계 목표 자산. 드문 종 번호가 실제 몬스터인지, 종마다 맞는 표를 받는지</summary>
	public sealed class MonsterLogRulesTest
	{
		private const string RULES_PATH = "Assets/_WitchMendokusai/Domain/Discovery/MonsterLog/SET_0_MonsterLog.asset";

		private MonsterLogRules rules;
		private List<Monster> monsters;

		[SetUp]
		public void SetUp()
		{
			rules = AssetDatabase.LoadAssetAtPath<MonsterLogRules>(RULES_PATH);
			monsters = AssetDatabase.FindAssets($"t:{nameof(Monster)}")
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.ToList();
		}

		[Test]
		public void Target_tables_are_valid() => Assert.DoesNotThrow(() => rules.ValidateTargets());

		[Test]
		public void Rare_ids_are_normal_monsters_without_duplicates()
		{
			Assert.That(rules.RareMonsterIds, Is.Not.Empty);
			Assert.That(rules.RareMonsterIds, Is.Unique);
			foreach (int id in rules.RareMonsterIds)
			{
				List<Monster> found = monsters.FindAll(monster => monster.ID == id);
				Assert.That(found.Count, Is.EqualTo(1), $"드문 종 번호 {id} 의 몬스터 자산이 하나가 아님");
				Assert.That(found[0].Type, Is.Not.EqualTo(MonsterType.Boss), $"드문 종 번호 {id} 가 보스");
			}
		}

		[Test]
		public void Every_rare_target_is_at_most_the_normal_one()
		{
			Monster rare = monsters.Find(monster => rules.IsRare(monster.ID));
			Monster normal = monsters.First(monster => monster.Type != MonsterType.Boss && rules.IsRare(monster.ID) == false);
			IReadOnlyList<int> rareTargets = rules.TargetsOf(rare, true);
			IReadOnlyList<int> normalTargets = rules.TargetsOf(normal, true);

			// 첫 칸은 둘 다 한 마리. 그 뒤로는 드문 쪽이 적어야 함
			Assert.That(rareTargets.Count, Is.EqualTo(normalTargets.Count));
			for (int i = 1; i < rareTargets.Count; i++)
			{
				Assert.That(rareTargets[i], Is.LessThan(normalTargets[i]), $"{i} 번째 칸");
			}
			Assert.That(rules.TargetsOf(rare, false).Count, Is.EqualTo(rareTargets.Count - 1));
		}
	}
}
