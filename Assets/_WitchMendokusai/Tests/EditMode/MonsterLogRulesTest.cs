using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>몬스터 기록의 단계 목표 자산. 드문 종 번호가 실제 몬스터인지, 종마다 맞는 표를 받는지</summary>
	public sealed class MonsterLogRulesTest
	{
		private const string RULES_PATH = "Assets/_WitchMendokusai/Domain/Discovery/MonsterLog/SET_0_MonsterLog.asset";

		private MonsterLogRules rules;
		private Dictionary<int, Monster> monsters;

		[SetUp]
		public void SetUp()
		{
			rules = AssetDatabase.LoadAssetAtPath<MonsterLogRules>(RULES_PATH);
			monsters = AssetDatabase.FindAssets($"t:{nameof(Monster)}")
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.GroupBy(monster => monster.ID)
				.ToDictionary(group => group.Key, group => group.First());
		}

		[Test]
		public void Target_tables_are_valid() => Assert.DoesNotThrow(() => rules.ValidateTargets());

		[Test]
		public void Rare_ids_are_normal_monsters_without_duplicates()
		{
			Assert.That(rules.RareMonsterIds, Is.Unique);
			foreach (int id in rules.RareMonsterIds)
			{
				Assert.That(monsters.ContainsKey(id), Is.True, $"드문 종 번호 {id} 의 몬스터 자산이 없음");
				Assert.That(monsters[id].Type, Is.Not.EqualTo(MonsterType.Boss), $"드문 종 번호 {id} 가 보스");
			}
		}

		[Test]
		public void Rare_monster_needs_fewer_kills_than_normal()
		{
			Monster rare = monsters[rules.RareMonsterIds[0]];
			Monster normal = monsters.Values.First(monster => monster.Type != MonsterType.Boss && rules.IsRare(monster.ID) == false);
			IReadOnlyList<int> rareTargets = rules.TargetsOf(rare, true);
			IReadOnlyList<int> normalTargets = rules.TargetsOf(normal, true);

			Assert.That(rareTargets[rareTargets.Count - 1], Is.LessThan(normalTargets[normalTargets.Count - 1]));
			Assert.That(rules.TargetsOf(rare, false).Count, Is.EqualTo(rareTargets.Count - 1));
		}
	}
}
