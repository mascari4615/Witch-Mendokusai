using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 몬스터 특기 부품 (적중 효과, 공격 순서, 무리, 적대 인형, 몸 효과) 과 번호 17~29 의 연결. 정본 memo wm/design/monster-traits-20261125
	public class MonsterTraitTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl";
		private const int FIRST_ID = 17;
		private const int LAST_ID = 29;
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

		private static int Stat(Monster monster, UnitStatType type)
		{
			UnitStat unitStat = new();
			SerializedProperty stats = new SerializedObject(monster).FindProperty("<InitStatInfos>k__BackingField.initStats");
			for (int i = 0; i < stats.arraySize; i++)
			{
				SerializedProperty stat = stats.GetArrayElementAtIndex(i);
				if (stat.FindPropertyRelative("unitStat").intValue == (int)type)
					return stat.FindPropertyRelative("value").intValue;
			}
			return unitStat[type];
		}

		private T Make<T>(string assetName, params (string Field, object Value)[] values) where T : ScriptableObject
		{
			T instance = ScriptableObject.CreateInstance<T>();
			instance.name = assetName;
			created.Add(instance);
			SerializedObject serialized = new(instance);
			foreach ((string field, object value) in values)
			{
				SerializedProperty property = serialized.FindProperty(field);
				Assert.IsNotNull(property, field);
				switch (value)
				{
					case float number:
						property.floatValue = number;
						break;
					case int number:
						property.intValue = number;
						break;
					case bool flag:
						property.boolValue = flag;
						break;
					case Object reference:
						property.objectReferenceValue = reference;
						break;
				}
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return instance;
		}

		[Test]
		public void Every_new_monster_setup_is_valid()
		{
			List<string> errors = new();
			for (int id = FIRST_ID; id <= LAST_ID; id++)
			{
				Monster monster = Find(id);
				if (monster.Attack != null)
					monster.Attack.Validate(monster, errors);
				if (monster.BodyFx != null)
					monster.BodyFx.Validate(errors);
			}
			Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
		}

		[Test]
		public void Traits_use_the_expected_parts()
		{
			Assert.IsInstanceOf<DashMonsterAttack>(Find(17).Attack, "식인 꽃 물기");
			Assert.AreEqual(0, Stat(Find(18), UnitStatType.MOVEMENT_SPEED), "선인장은 서 있음");
			Assert.Greater(Stat(Find(18), UnitStatType.DAMAGE_BONUS), 0, "선인장 접촉 피해");
			Assert.IsInstanceOf<ZoneMonsterAttack>(Find(19).Attack, "게 집게");
			Assert.IsInstanceOf<SequenceMonsterAttack>(Find(24).Attack, "자동인형 패턴");
			Assert.IsInstanceOf<DashMonsterAttack>(Find(25).Attack, "독수리 급습");
			Assert.IsInstanceOf<PackMonsterAttack>(Find(27).Attack, "마나 위스프 무리");
			Assert.IsInstanceOf<RangedMonsterAttack>(Find(28).Attack, "별빛 위스프 임시");
			Assert.Greater(Stat(Find(22), UnitStatType.ARMOR), 0, "골렘 방어력");

			ZoneMonsterAttack jelly = (ZoneMonsterAttack)Find(21).Attack;
			Assert.AreEqual(ZoneCenter.Caster, jelly.Center);
			Assert.AreEqual(Find(27).BodyFx.HasGlow, true, "마나 위스프 빛 번짐");
			Assert.AreEqual(Find(29).BodyFx.HasGround, true, "그림자 잔재 색 빠짐");
			Assert.AreEqual(UnitStatusType.Shock, jelly.HitEffect.Status);
			ZoneMonsterAttack vine = (ZoneMonsterAttack)Find(23).Attack;
			Assert.AreEqual(ZoneCenter.Target, vine.Center);
			Assert.AreEqual(UnitStatusType.Slow, vine.HitEffect.Status);
			Assert.Greater(vine.HitEffect.SlowRatio, 0.5f, "발 묶기");
			ZoneMonsterAttack wind = (ZoneMonsterAttack)Find(26).Attack;
			Assert.IsTrue(wind.HitEffect.Pushes, "밀어내기");
		}

		[Test]
		public void Existing_zone_has_no_hit_effect()
		{
			ZoneMonsterAttack seed = (ZoneMonsterAttack)Find(14).Attack;
			Assert.AreEqual(ZoneCenter.Target, seed.Center);
			Assert.IsFalse(seed.HitEffect.AppliesStatus);
			Assert.IsFalse(seed.HitEffect.Pushes);
		}

		[Test]
		public void Monsters_without_grudge_hate_nobody()
		{
			Doll ring = ScriptableObject.CreateInstance<Doll>();
			created.Add(ring);
			Assert.IsFalse(Find(0).Grudge.Hates(ring), "옛 자산의 기본값은 없음");
		}

		[Test]
		public void Shadow_hates_only_its_doll()
		{
			Doll ring = AssetDatabase.LoadAssetAtPath<Doll>("Assets/_WitchMendokusai/Domain/Doll/Impl/Ring/DOL_4_링.asset");
			Doll alisa = AssetDatabase.LoadAssetAtPath<Doll>("Assets/_WitchMendokusai/Domain/Doll/Impl/Alisa/DOL_0_알리사.asset");
			MonsterGrudge grudge = Find(29).Grudge;
			Assert.IsTrue(grudge.Hates(ring));
			Assert.IsFalse(grudge.Hates(alisa));
			Assert.IsFalse(grudge.Hates(null));
			UnitStat unitStat = new();
			unitStat[UnitStatType.MOVEMENT_SPEED] = 20;
			grudge.Apply(unitStat);
			Assert.AreEqual(26, unitStat[UnitStatType.MOVEMENT_SPEED]);
			Assert.AreEqual(50, unitStat[UnitStatType.DAMAGE_BONUS]);
		}

		[Test]
		public void Lure_keeps_distance()
		{
			Vector3 player = new(1f, 2f, 1f);
			Assert.AreEqual(Vector3.left, PackMonsterAttack.LureDirection(new Vector3(9f, 0f, 1f), player, 4f, 0.5f), "멀면 다가감");
			Assert.AreEqual(Vector3.right, PackMonsterAttack.LureDirection(new Vector3(2f, 0f, 1f), player, 4f, 0.5f), "가까우면 물러남");
			Vector3 around = PackMonsterAttack.LureDirection(new Vector3(5f, 0f, 1f), player, 4f, 0.5f);
			Assert.AreEqual(0f, Vector3.Dot(around, Vector3.right), 0.001f, "알맞으면 둘레");
			Assert.AreEqual(1f, around.magnitude, 0.001f);
			Assert.AreEqual(Vector3.right, PackMonsterAttack.LureDirection(player, player, 4f, 0.5f), "겹쳐도 방향 있음");
		}

		[Test]
		public void Push_goes_outward_and_ignores_height()
		{
			Assert.AreEqual(Vector3.forward, MonsterHitEffect.PushDirection(new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 3f)));
			Assert.AreEqual(Vector3.zero, MonsterHitEffect.PushDirection(Vector3.one, Vector3.one));
		}

		[Test]
		public void Validation_catches_bad_hit_effects()
		{
			Monster owner = Find(23);
			CombatFx telegraph = owner.Attack.WindupTelegraph;
			const string HIT = "<HitEffect>k__BackingField.";
			ZoneMonsterAttack noSeconds = Make<ZoneMonsterAttack>("NoSeconds", ("<WindupTelegraph>k__BackingField", telegraph),
				(HIT + "<AppliesStatus>k__BackingField", true), (HIT + "<SlowRatio>k__BackingField", 0.5f));
			ZoneMonsterAttack noRatio = Make<ZoneMonsterAttack>("NoRatio", ("<WindupTelegraph>k__BackingField", telegraph),
				(HIT + "<AppliesStatus>k__BackingField", true), (HIT + "<StatusSeconds>k__BackingField", 1f));
			ZoneMonsterAttack halfPush = Make<ZoneMonsterAttack>("HalfPush", ("<WindupTelegraph>k__BackingField", telegraph),
				(HIT + "<PushSpeed>k__BackingField", 5f));
			foreach (ZoneMonsterAttack zone in new[] { noSeconds, noRatio, halfPush })
			{
				List<string> errors = new();
				zone.Validate(owner, errors);
				Assert.AreEqual(1, errors.Count, $"{zone.name}: {string.Join(", ", errors)}");
			}
		}

		[Test]
		public void Validation_catches_bad_sequence_and_pack()
		{
			Monster owner = Find(24);
			List<string> errors = new();
			SequenceMonsterAttack empty = Make<SequenceMonsterAttack>("Empty", ("<WindupSeconds>k__BackingField", 0f));
			empty.Validate(owner, errors);
			Assert.AreEqual(1, errors.Count, string.Join(", ", errors));

			errors.Clear();
			empty.Steps.Add(empty);
			empty.Validate(owner, errors);
			Assert.AreEqual(1, errors.Count, "순서 안의 순서: " + string.Join(", ", errors));

			errors.Clear();
			PackMonsterAttack pack = Make<PackMonsterAttack>("Pack", ("<WindupSeconds>k__BackingField", 0f));
			pack.Validate(owner, errors);
			Assert.AreEqual(1, errors.Count, "무리 공격 없음: " + string.Join(", ", errors));
		}

		[Test]
		public void Body_fx_pieces_blink_inside_the_visible_part()
		{
			MonsterBodyFxStyle style = Make<MonsterBodyFxStyle>("Style", ("<PieceVisible>k__BackingField", 0.5f), ("<PieceCount>k__BackingField", 2), ("<PieceRate>k__BackingField", 1f));
			Assert.AreEqual(1f, style.PieceAlpha(0.25f), 0.001f);
			Assert.AreEqual(0f, style.PieceAlpha(0.75f));
			(int cycle, float progress) = style.PieceCycle(1, 2.25f, 0f);
			Assert.AreEqual(2, cycle);
			Assert.AreEqual(0.75f, progress, 0.001f, "조각마다 엇갈림");
		}
	}
}
