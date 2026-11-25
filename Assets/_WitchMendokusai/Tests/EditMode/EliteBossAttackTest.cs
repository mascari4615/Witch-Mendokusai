using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 정예 (30~35) 와 보스 (36~39) 의 연결, 새 공격 부품의 계산. 정본 memo wm/design/elite-boss-20261125
	public class EliteBossAttackTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl";
		private const int FIRST_ELITE = 30;
		private const int FIRST_BOSS = 36;
		private const int LAST_BOSS = 39;
		private readonly List<Object> created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object instance in created)
				Object.DestroyImmediate(instance);
			created.Clear();
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

		private T Make<T>(string assetName) where T : ScriptableObject
		{
			T instance = ScriptableObject.CreateInstance<T>();
			instance.name = assetName;
			created.Add(instance);
			return instance;
		}

		private static void Set(Object target, string field, float value)
		{
			SerializedObject serialized = new(target);
			SerializedProperty property = serialized.FindProperty($"<{field}>k__BackingField");
			Assert.IsNotNull(property, field);
			if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum)
				property.intValue = (int)value;
			else
				property.floatValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		[Test]
		public void Every_elite_and_boss_setup_is_valid()
		{
			List<string> errors = new();
			for (int id = FIRST_ELITE; id <= LAST_BOSS; id++)
			{
				Monster monster = Find(id);
				Assert.IsNotNull(monster.Attack, $"{monster.Name} 공격");
				Assert.IsNotNull(monster.Sprite, $"{monster.Name} 그림");
				monster.Attack.Validate(monster, errors);
				if (monster.BodyFx != null)
					monster.BodyFx.Validate(errors);
			}
			Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
		}

		[Test]
		public void Elites_keep_their_own_colors()
		{
			for (int id = FIRST_ELITE; id < FIRST_BOSS; id++)
			{
				Monster monster = Find(id);
				Assert.IsTrue(monster.KeepsTintAsElite, monster.Name);
				Assert.AreEqual(MonsterType.Normal, monster.Type, monster.Name);
			}
			Assert.IsFalse(Find(17).KeepsTintAsElite, "옛 몬스터는 정예 색을 곱함");
		}

		[Test]
		public void Bosses_share_one_prefab_and_change_phase_by_health()
		{
			GameObject prefab = Find(FIRST_BOSS).Prefab;
			Assert.IsNotNull(prefab.GetComponent<FSMSlime>());
			Assert.IsNull(prefab.GetComponent<FSMSlimeKing>(), "아쿠아쿠 전용 행동 없음");
			for (int id = FIRST_BOSS; id <= LAST_BOSS; id++)
			{
				Monster monster = Find(id);
				Assert.AreEqual(MonsterType.Boss, monster.Type, monster.Name);
				Assert.AreEqual(prefab, monster.Prefab, monster.Name);
				Assert.IsInstanceOf<PhaseMonsterAttack>(monster.Attack, monster.Name);
			}
			Assert.IsNotNull(Find(1).Prefab.GetComponent<FSMSlimeKing>(), "아쿠아쿠는 그대로");
		}

		[Test]
		public void Phase_moves_forward_only()
		{
			PhaseMonsterAttack phase = (PhaseMonsterAttack)Find(FIRST_BOSS).Attack;
			Assert.AreEqual(0, phase.PhaseIndex(1f, 0));
			Assert.AreEqual(0, phase.PhaseIndex(0.51f, 0));
			Assert.AreEqual(1, phase.PhaseIndex(0.5f, 0));
			Assert.AreEqual(1, phase.PhaseIndex(0.9f, 1), "회복해도 안 돌아감");
		}

		[Test]
		public void Line_arrangement_goes_toward_the_player()
		{
			List<Vector3> points = new();
			ScatterZoneMonsterAttack.Arrange(ScatterArrangement.Line, Vector3.zero, new Vector3(0f, 3f, 8f), 3, 1.5f, 0f, 90f, () => 0f, points);
			Assert.AreEqual(3, points.Count);
			Assert.AreEqual(0f, (points[2] - new Vector3(0f, 0f, 4.5f)).magnitude, 0.001f, "높이는 무시");
		}

		[Test]
		public void Scatter_keeps_gaps_between_circles()
		{
			List<Vector3> points = new();
			ScatterZoneMonsterAttack.Arrange(ScatterArrangement.Scatter, Vector3.zero, Vector3.one, 6, 2.8f, 1.2f, 30f, () => 0f, points);
			Assert.AreEqual(6, points.Count);
			Assert.AreEqual(Vector3.one, points[0], "첫 원은 주인공 자리");
			for (int i = 0; i < points.Count; i++)
			{
				for (int j = i + 1; j < points.Count; j++)
					Assert.GreaterOrEqual((points[i] - points[j]).magnitude, 2.4f, $"{i}, {j}");
			}
		}

		[Test]
		public void Overlapping_scatter_is_rejected()
		{
			Monster owner = Find(FIRST_BOSS);
			ScatterZoneMonsterAttack scatter = Make<ScatterZoneMonsterAttack>("Crowded");
			Set(scatter, "Count", 9);
			Set(scatter, "Radius", 1.5f);
			Set(scatter, "Distance", 2f);
			Set(scatter, "WindupSeconds", 0f);
			List<string> errors = new();
			scatter.Validate(owner, errors);
			Assert.AreEqual(2, errors.Count, "예고 없음, 겹침: " + string.Join(", ", errors));
		}

		[Test]
		public void Strip_covers_its_length_and_width_only()
		{
			Assert.IsTrue(LineMonsterAttack.IsInStrip(Vector3.zero, Vector3.forward, 10f, 2f, new Vector3(0.9f, 5f, 9f), 0f));
			Assert.IsFalse(LineMonsterAttack.IsInStrip(Vector3.zero, Vector3.forward, 10f, 2f, new Vector3(1.2f, 0f, 9f), 0f), "폭 밖");
			Assert.IsTrue(LineMonsterAttack.IsInStrip(Vector3.zero, Vector3.forward, 10f, 2f, new Vector3(1.2f, 0f, 9f), 0.3f), "몸이 걸침");
			Assert.IsFalse(LineMonsterAttack.IsInStrip(Vector3.zero, Vector3.forward, 10f, 2f, new Vector3(0f, 0f, 10.5f), 0.3f), "끝 너머");
			Assert.IsFalse(LineMonsterAttack.IsInStrip(Vector3.zero, Vector3.forward, 10f, 2f, new Vector3(0f, 0f, -1f), 0.3f), "뒤");
		}

		[Test]
		public void Summon_stops_at_the_cap()
		{
			Assert.AreEqual(3, SummonMonsterAttack.AllowedCount(0, 3, 6));
			Assert.AreEqual(1, SummonMonsterAttack.AllowedCount(5, 3, 6));
			Assert.AreEqual(0, SummonMonsterAttack.AllowedCount(9, 3, 6));
		}

		[Test]
		public void Sequence_rejects_nested_phase()
		{
			Monster owner = Find(FIRST_BOSS);
			SequenceMonsterAttack sequence = Make<SequenceMonsterAttack>("Sequence");
			Set(sequence, "WindupSeconds", 0f);
			sequence.Steps.Add(owner.Attack);
			List<string> errors = new();
			sequence.Validate(owner, errors);
			Assert.AreEqual(1, errors.Count, string.Join(", ", errors));
		}
	}
}
