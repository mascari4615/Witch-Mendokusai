using System.Collections.Generic;
using System.Linq;
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
				Assert.Greater(((PhaseMonsterAttack)monster.Attack).OpeningSeconds, 0f, "이름 배너 동안 공격 없음: " + monster.Name);
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

		private static T FromJson<T>(string json) where T : new()
		{
			T value = new();
			JsonUtility.FromJsonOverwrite(json.Replace("<", "\"<").Replace(">", ">k__BackingField\""), value);
			return value;
		}

		[Test]
		public void Band_hits_the_ring_and_spares_the_hole()
		{
			AttackBand band = FromJson<AttackBand>("{<Inner>:2,<Outer>:4}");
			Assert.IsFalse(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 360f, new Vector3(0f, 5f, 1.9f), 0.3f), "안쪽 빈 곳");
			Assert.IsTrue(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 360f, new Vector3(0f, 5f, -3f), 0.3f), "높이는 무시, 온 둘레");
			Assert.IsTrue(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 360f, new Vector3(4.2f, 0f, 0f), 0.3f), "바깥에 몸이 걸침");
			Assert.IsFalse(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 360f, new Vector3(4.4f, 0f, 0f), 0.3f), "바깥");
		}

		[Test]
		public void Arc_spares_the_back()
		{
			AttackBand band = FromJson<AttackBand>("{<Inner>:0,<Outer>:4}");
			Assert.IsTrue(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 200f, new Vector3(3f, 0f, 0f), 0.3f), "옆");
			Assert.IsFalse(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 200f, new Vector3(0f, 0f, -3f), 0.3f), "등 뒤");
			Assert.IsTrue(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 90f, Quaternion.Euler(0f, 49f, 0f) * Vector3.forward * 3f, 0.3f), "변에 몸이 걸침");
			Assert.IsFalse(BandMonsterAttack.IsInBand(Vector3.zero, band, Vector3.forward, 90f, Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 3f, 0.3f), "변 밖");
		}

		[Test]
		public void Spin_arm_reverses_and_comes_back()
		{
			SpinArm arm = FromJson<SpinArm>("{<DegreesPerSecond>:40,<SpinUpSeconds>:0.5,<ReverseAtSeconds>:3,<ReversePauseSeconds>:0.5}");
			Assert.AreEqual(0f, arm.AngleAt(-1f), 0.001f, "예고 동안은 안 돎");
			Assert.AreEqual(arm.AngleAt(3f), arm.AngleAt(3.5f), 0.001f, "멈춤");
			Assert.Less(arm.AngleAt(4.5f), arm.AngleAt(3.5f), "반대로");
			Assert.AreEqual(0f, arm.AngleAt(6.5f), 0.001f, "같은 초만큼 돌면 제자리");
		}

		// 준비 때 주인공이 있던 쪽 (0도) 을 모든 바늘이 지나가야 서 있기만 해서는 못 피함
		[Test]
		public void Every_spin_arm_passes_the_player()
		{
			int checkedCount = 0;
			foreach (string guid in AssetDatabase.FindAssets("t:SpinMonsterAttack", new[] { MONSTER_FOLDER }))
			{
				SpinMonsterAttack spin = AssetDatabase.LoadAssetAtPath<SpinMonsterAttack>(AssetDatabase.GUIDToAssetPath(guid));
				foreach (SpinArm arm in spin.Arms)
				{
					bool passes = false;
					for (float time = 0f; time < spin.SpinSeconds - spin.FoldSeconds; time += 0.05f)
						passes |= Mathf.Abs(Mathf.DeltaAngle(0f, arm.StartAngle + arm.AngleAt(time))) < 4f;
					Assert.IsTrue(passes, $"{spin.name} 길이 {arm.Length}");
					checkedCount++;
				}
			}
			Assert.Greater(checkedCount, 0);
		}

		[Test]
		public void Lingering_object_is_hot_then_cools()
		{
			Assert.IsNull(new LingerSettings().Open(1f), "초가 0 이면 안 남음");
			FxHold hold = FromJson<LingerSettings>("{<Seconds>:2,<CoolSeconds>:0.5,<Damage>:1}").Open(10f);
			Assert.IsFalse(hold.IsHot(9.9f), "때리기 전");
			Assert.IsTrue(hold.IsHot(11.4f));
			Assert.IsFalse(hold.IsHot(11.6f), "식음");
			Assert.AreEqual(12f, hold.EndTime, 0.001f);
			hold.IsGone = true;
			Assert.IsFalse(hold.IsHot(11f), "터짐");
		}

		[Test]
		public void Only_the_real_spot_is_safe()
		{
			int checkedCount = 0;
			foreach (string guid in AssetDatabase.FindAssets("t:HideMonsterAttack", new[] { MONSTER_FOLDER }))
			{
				HideMonsterAttack hide = AssetDatabase.LoadAssetAtPath<HideMonsterAttack>(AssetDatabase.GUIDToAssetPath(guid));
				if (hide.HasSafeSpot == false)
					continue;
				List<Vector3> spots = new();
				HideMonsterAttack.Arrange(Vector3.one, hide.Count, hide.Distance, 17f, spots);
				Assert.AreEqual(hide.Count, spots.Count);
				List<Vector3> strikes = spots.GetRange(1, spots.Count - 1);
				Assert.IsTrue(HideMonsterAttack.Hits(strikes, hide.Radius, Vector3.one, 0.2f), hide.name + " 가운데");
				Assert.IsFalse(HideMonsterAttack.Hits(strikes, hide.Radius, spots[0], 0.3f), hide.name + " 진짜 자리");
				foreach (Vector3 strike in strikes)
					Assert.IsTrue(HideMonsterAttack.Hits(strikes, hide.Radius, strike, 0f), hide.name + " 가짜 자리");
				checkedCount++;
			}
			Assert.Greater(checkedCount, 0);
		}

		[Test]
		public void Dive_strikes_where_the_shadow_stopped()
		{
			List<Vector3> spots = new() { Vector3.zero };
			HideMonsterAttack.Arrange(new Vector3(3f, 0f, 0f), 1, 9f, 50f, spots);
			Assert.AreEqual(1, spots.Count, "앞 자리를 비움");
			Assert.AreEqual(new Vector3(3f, 0f, 0f), spots[0]);
			Assert.IsTrue(HideMonsterAttack.Hits(spots, 2f, new Vector3(5.2f, 4f, 0f), 0.3f), "몸이 걸침, 높이는 무시");
			Assert.IsFalse(HideMonsterAttack.Hits(spots, 2f, new Vector3(5.4f, 0f, 0f), 0.3f));
		}

		// 원에 갇힌 채 멈춰 있던 주인공이 걸어서 나갈 틈 (걷기 2.9 m/s, 몸 0.3m, 반응 0.2초, 행동 틱 0.1초)
		[Test]
		public void Dives_leave_time_to_walk_out()
		{
			foreach (string guid in AssetDatabase.FindAssets("t:HideMonsterAttack", new[] { MONSTER_FOLDER }))
			{
				HideMonsterAttack hide = AssetDatabase.LoadAssetAtPath<HideMonsterAttack>(AssetDatabase.GUIDToAssetPath(guid));
				float distance = hide.HasSafeSpot ? hide.Distance : hide.Radius + 0.3f;
				Assert.GreaterOrEqual(hide.LockSeconds, distance / 2.9f + 0.3f, hide.name);
			}
		}

		// 주인공 자리 장판 (예측 없음) 은 멈춰 있던 주인공이 걸어서 나갈 틈. 정예와 보스 폴더만
		[Test]
		public void Elite_zones_leave_time_to_walk_out()
		{
			int checkedCount = 0;
			for (int id = FIRST_ELITE; id <= LAST_BOSS; id++)
			{
				foreach (MonsterAttack step in StepsOf(Find(id).Attack))
				{
					if (step is not ZoneMonsterAttack zone || zone.Center != ZoneCenter.Target || zone.LeadSeconds > 0f)
						continue;
					Assert.GreaterOrEqual(zone.WindupSeconds, (zone.Radius + 0.3f) / 2.9f + 0.3f, zone.name);
					checkedCount++;
				}
			}
			Assert.Greater(checkedCount, 0);
		}

		private static IEnumerable<MonsterAttack> StepsOf(MonsterAttack attack) => attack switch
		{
			SequenceMonsterAttack sequence => sequence.Steps,
			PhaseMonsterAttack phase => phase.Phases.SelectMany(each => StepsOf(each.Attack)),
			PackMonsterAttack pack => StepsOf(pack.SoloAttack),
			_ => new[] { attack },
		};

		[Test]
		public void Links_go_around_without_crossing()
		{
			List<Vector3> points = new() { new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f) };
			LinkMonsterAttack.SortAround(points);
			for (int i = 0; i < points.Count; i++)
				Assert.AreEqual(2f, Vector3.Distance(points[i], points[(i + 1) % points.Count]), 0.001f, "대각선 없이 변으로만 " + i);
		}

		[Test]
		public void Remade_bosses_have_an_entrance()
		{
			foreach (int id in new[] { 36, 37, 38, 39 })
			{
				PhaseMonsterAttack phase = (PhaseMonsterAttack)Find(id).Attack;
				Assert.IsNull(phase.Phases[0].Entrance, "첫 단계는 등장 멈춤: " + id);
				Assert.IsNotNull(phase.Phases[1].Entrance, "2단계 전환 연출: " + id);
			}
		}
	}
}
