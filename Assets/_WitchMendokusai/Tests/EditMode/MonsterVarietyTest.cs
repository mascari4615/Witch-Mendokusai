using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	// 실제 일반 몬스터 16~29, 40~43 에 입힌 걷는 성격과 공격. 정본 memo wm/design/monster-variety-20261127 (임시 결정, 사용자 판정 전)
	// 종마다 성격이 서로 다르고, 더미 자산을 직접 물지 않으며, 성격의 거리 띠와 공격 사거리가 맞물리는지
	public class MonsterVarietyTest
	{
		private const string MONSTER_ROOT = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl";
		private const string DUMMY_MARK = "Dummy/";

		// 짝짓기 표 (memo README) 의 성격. 27 마나 위스프는 무리 공격이 걸음을 쥐어, 18 가시 선인장은 이동 속도 0 이라 빠짐
		private static readonly Dictionary<int, Type> temperaments = new()
		{
			{ 16, typeof(CowardTemperament) },
			{ 17, typeof(GreedyTemperament) },
			{ 19, typeof(FlankTemperament) },
			{ 20, typeof(ZigzagTemperament) },
			{ 21, typeof(TideTemperament) },
			{ 22, typeof(GuardTemperament) },
			{ 23, typeof(BashfulTemperament) },
			{ 24, typeof(PatrolTemperament) },
			{ 25, typeof(OrbitTemperament) },
			{ 26, typeof(BlinkTemperament) },
			{ 28, typeof(KiteTemperament) },
			{ 29, typeof(MirrorTemperament) },
			{ 40, typeof(ShyTemperament) },
			{ 41, typeof(AmbushTemperament) },
			{ 42, typeof(HopTemperament) },
			{ 43, typeof(SleeperTemperament) },
		};

		// 공격이 없던 종에 새로 붙인 공격
		private static readonly int[] newAttacks = { 16, 18, 22, 29 };

		private static Dictionary<int, Monster> LoadTargets() =>
			AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_ROOT })
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.Where(monster => temperaments.ContainsKey(monster.ID) || newAttacks.Contains(monster.ID))
				.ToDictionary(monster => monster.ID);

		private static IEnumerable<Monster> LoadTempered() => LoadTargets().Values.Where(monster => temperaments.ContainsKey(monster.ID));

		private static string PathOf(UnityEngine.Object asset) => AssetDatabase.GetAssetPath(asset);

		[Test]
		public void Each_target_walks_with_its_own_temperament()
		{
			Dictionary<int, Monster> targets = LoadTargets();
			CollectionAssert.IsSubsetOf(temperaments.Keys, targets.Keys);
			foreach ((int id, Type expected) in temperaments)
			{
				Monster monster = targets[id];
				Assert.IsNotNull(monster.Temperament, $"{monster.name} 성격 없음");
				Assert.AreEqual(expected, monster.Temperament.GetType(), monster.name);
				// 더미 성격을 물면 더미 수치를 고칠 때 실제 종도 바뀜
				StringAssert.DoesNotContain(DUMMY_MARK, PathOf(monster.Temperament), monster.name);
			}
			Assert.AreEqual(temperaments.Count, temperaments.Values.Distinct().Count(), "두 종이 같은 성격");
		}

		[Test]
		public void New_attacks_are_species_copies_and_pass_validation()
		{
			Dictionary<int, Monster> targets = LoadTargets();
			foreach (int id in newAttacks)
			{
				Monster monster = targets[id];
				Assert.IsNotNull(monster.Attack, $"{monster.name} 공격 없음");
				StringAssert.DoesNotContain(DUMMY_MARK, PathOf(monster.Attack), monster.name);
			}
			foreach (Monster monster in targets.Values.Where(monster => monster.Attack != null))
			{
				List<string> errors = new();
				monster.Attack.Validate(monster, errors);
				Assert.IsEmpty(errors, $"{monster.name}\n{string.Join("\n", errors)}");
			}
		}

		// 기다리는 걸음에서 성격을 묻는 공격만. 숨기, 무리 같은 공격은 성격을 붙여도 안 따름
		[Test]
		public void Attack_waits_by_the_temperament()
		{
			foreach (Monster monster in LoadTempered())
			{
				bool asksTemperament = monster.Attack == null
					|| monster.Attack is WindupMonsterAttack
					|| monster.Attack is DashMonsterAttack
					|| monster.Attack is SequenceMonsterAttack;
				Assert.IsTrue(asksTemperament, $"{monster.name} 의 공격 {monster.Attack?.GetType().Name} 은 기다릴 때 성격을 안 물음");
			}
		}

		// 물러나는 성격은 지키는 거리가 사거리 안이어야 쏘고, 나타나는 성격은 나타난 자리에서 닿아야 함. 맴돌기는 사거리 밖에서 돌기 시작해야 원이 보임
		[Test]
		public void Temperament_distances_mesh_with_attack_range()
		{
			foreach (Monster monster in LoadTempered())
			{
				if (monster.Attack == null)
					continue;
				float range = monster.Attack.TriggerRange;
				switch (monster.Temperament)
				{
					case KiteTemperament kite:
						Assert.Less(FloatOf(kite, "farDistance"), range, $"{monster.name} 거리 두기 바깥 경계가 사거리 밖");
						break;
					case ShyTemperament shy:
						Assert.Less(FloatOf(shy, "returnRadius"), range, $"{monster.name} 수줍음 돌아오는 경계가 사거리 밖");
						break;
					case BlinkTemperament blink:
						Assert.Less(FloatOf(blink, "behindDistance"), range, $"{monster.name} 점멸로 나타난 자리가 사거리 밖");
						break;
					case OrbitTemperament orbit:
						Assert.Greater(FloatOf(orbit, "startRadius"), range, $"{monster.name} 맴돌기가 사거리 안에서 시작해 곧장 공격");
						break;
				}
			}
		}

		private static float FloatOf(UnityEngine.Object asset, string field)
		{
			SerializedProperty property = new SerializedObject(asset).FindProperty(field);
			Assert.IsNotNull(property, $"{asset.name} 에 {field} 칸 없음 (이름이 바뀜)");
			return property.floatValue;
		}
	}
}
