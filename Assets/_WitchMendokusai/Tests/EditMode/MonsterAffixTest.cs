using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 몬스터 특징 (MonsterAffix) 여섯의 계산과 특징 더미 921~927 연결. 정본 memo wm/design/monster-affix-20261126
	public class MonsterAffixTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/AffixDummy";
		private const int FIRST_DUMMY_ID = 921;
		private const int DUMMY_COUNT = 7;

		[Test]
		public void Trail_spills_only_after_walking_spacing()
		{
			Assert.IsFalse(TrailAffix.ShouldSpill(Vector3.zero, new Vector3(0.5f, 3f, 0f), 0.7f), "높이는 안 셈");
			Assert.IsTrue(TrailAffix.ShouldSpill(Vector3.zero, new Vector3(0.5f, 0f, 0.5f), 0.7f));
		}

		[Test]
		public void Death_burst_hits_inside_radius_plus_body()
		{
			Assert.IsTrue(DeathBurstAffix.IsInside(Vector3.zero, new Vector3(2.5f, 0f, 0f), 2.2f, 0.4f));
			Assert.IsFalse(DeathBurstAffix.IsInside(Vector3.zero, new Vector3(2.7f, 0f, 0f), 2.2f, 0.4f));
		}

		[Test]
		public void Split_spreads_children_evenly_around_death_spot()
		{
			Vector3 first = SplitAffix.SpotAround(Vector3.zero, 0, 2, 1f);
			Vector3 second = SplitAffix.SpotAround(Vector3.zero, 1, 2, 1f);
			Assert.AreEqual(1f, first.magnitude, 0.001f);
			Assert.AreEqual(2f, Vector3.Distance(first, second), 0.001f, "둘이면 마주 봄");
		}

		[Test]
		public void Heal_pulse_never_overfills()
		{
			Assert.AreEqual(12, HealPulseAffix.HealAmount(60, 30, 0.2f));
			Assert.AreEqual(5, HealPulseAffix.HealAmount(60, 55, 0.2f), "모자란 만큼만");
			Assert.AreEqual(0, HealPulseAffix.HealAmount(60, 60, 0.2f));
		}

		[Test]
		public void Enrage_triggers_below_threshold_while_alive()
		{
			Assert.IsFalse(EnrageAffix.ShouldEnrage(60, 40, 0.5f));
			Assert.IsTrue(EnrageAffix.ShouldEnrage(60, 20, 0.5f));
			Assert.IsFalse(EnrageAffix.ShouldEnrage(60, 0, 0.5f), "쓰러졌으면 아님");
		}

		[Test]
		public void Pull_aura_pulls_toward_source_only_between_rings()
		{
			Vector3 pull = PullAuraAffix.PullVelocity(Vector3.zero, new Vector3(3f, 0f, 0f), 4.5f, 1f, 1.8f);
			Assert.AreEqual(-1.8f, pull.x, 0.001f, "몬스터 쪽으로");
			Assert.AreEqual(Vector3.zero, PullAuraAffix.PullVelocity(Vector3.zero, new Vector3(6f, 0f, 0f), 4.5f, 1f, 1.8f), "밖이면 안 끎");
			Assert.AreEqual(Vector3.zero, PullAuraAffix.PullVelocity(Vector3.zero, new Vector3(0.5f, 0f, 0f), 4.5f, 1f, 1.8f), "몸에 붙으면 안 끎");
		}

		[Test]
		public void Split_into_itself_is_rejected()
		{
			Monster dummy = Find(FIRST_DUMMY_ID + 3);
			SplitAffix split = (SplitAffix)dummy.Affixes[0];
			List<string> errors = new();
			split.Validate(dummy, errors);
			Assert.IsEmpty(errors, "더미 분열은 진흙 슬라임으로 갈라짐");
			SerializedObject serialized = new(split);
			Object original = serialized.FindProperty("child").objectReferenceValue;
			serialized.FindProperty("child").objectReferenceValue = dummy;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try
			{
				split.Validate(dummy, errors);
				Assert.IsNotEmpty(errors);
			}
			finally
			{
				serialized.FindProperty("child").objectReferenceValue = original;
				serialized.ApplyModifiedPropertiesWithoutUndo();
			}
		}

		[Test]
		public void Dummy_monsters_each_carry_one_valid_affix()
		{
			int found = 0;
			HashSet<MonsterAffix> seen = new();
			foreach (string guid in AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER }))
			{
				Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid));
				Assert.That(monster.ID, Is.InRange(FIRST_DUMMY_ID, FIRST_DUMMY_ID + DUMMY_COUNT - 1), monster.name);
				Assert.AreEqual(1, monster.Affixes.Count, monster.name);
				Assert.IsTrue(seen.Add(monster.Affixes[0]), $"{monster.name} 특징 자산이 겹침");
				List<string> errors = new();
				Dungeon.ValidateAffixes(monster, errors);
				Assert.IsEmpty(errors, string.Join(", ", errors));
				found++;
			}
			Assert.AreEqual(DUMMY_COUNT, found);
		}

		private static Monster Find(int id)
		{
			foreach (string guid in AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER }))
			{
				Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid));
				if (monster.ID == id)
					return monster;
			}
			Assert.Fail($"몬스터 {id} 없음");
			return null;
		}
	}
}
