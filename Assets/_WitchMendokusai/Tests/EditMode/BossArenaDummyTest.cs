using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 보스 큰 부품 더미 (981~986): 표식 누적, 따라다니는 안전 원, 무대 변화, 부위. 정본 memo wm/design/boss-arena-20261126
	public class BossArenaDummyTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/ArenaDummy";
		private const string ADDRESS_GROUP = "Assets/AddressableAssetsData/AssetGroups/Monster.asset";

		private static List<Monster> LoadDummies() =>
			AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER })
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.OrderBy(monster => monster.ID)
				.ToList();

		private static Monster Find(int id) => LoadDummies().Single(monster => monster.ID == id);

		private static UnitStatusConfig LoadStatusConfig()
		{
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(UnitStatusConfig));
			Assert.IsNotEmpty(guids);
			return AssetDatabase.LoadAssetAtPath<UnitStatusConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
		}

		private static IEnumerable<MonsterAttack> Leaves(MonsterAttack attack)
		{
			if (attack is SequenceMonsterAttack sequence)
				return sequence.Steps;
			return new[] { attack };
		}

		[Test]
		public void Every_dummy_has_an_address_and_no_loot()
		{
			List<Monster> dummies = LoadDummies();
			Assert.IsNotEmpty(dummies);
			string group = File.ReadAllText(ADDRESS_GROUP).Replace("\r\n", "\n");
			foreach (Monster monster in dummies)
			{
				Assert.That(monster.ID, Is.InRange(981, 990), monster.name);
				string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(monster));
				StringAssert.Contains($"m_GUID: {guid}\n    m_Address: Monster/{monster.ID}\n", group, monster.name);
				Assert.IsEmpty(monster.Loots, $"{monster.name} 더미는 드랍 없음");
				StringAssert.StartsWith("보스 더미 ", monster.Name);
			}
		}

		[Test]
		public void Attacks_and_affixes_pass_validation()
		{
			foreach (Monster monster in LoadDummies())
			{
				List<string> errors = new();
				if (monster.Attack != null)
					monster.Attack.Validate(monster, errors);
				foreach (MonsterAffix affix in monster.Affixes)
				{
					Assert.IsNotNull(affix, $"{monster.name} 특징 빈 칸");
					affix.Validate(monster, errors);
				}
				Assert.IsEmpty(errors, string.Join("\n", errors));
			}
		}

		// ---- 1. 표식 누적

		[Test]
		public void Brand_threshold_is_the_stack_cap()
		{
			Assert.IsFalse(BrandSettings.IsFull(4, 5));
			Assert.IsTrue(BrandSettings.IsFull(5, 5));
			// 상한 1 은 쌓이지 않음. 매번 터지면 안 됨
			Assert.IsFalse(BrandSettings.IsFull(1, 1));
			// 쌓임 규칙: 걸린 동안 다시 걸리면 하나씩, 풀린 뒤면 처음부터
			Assert.AreEqual(5, UnitStatusRules.NextStacks(4, true, 5));
			Assert.AreEqual(1, UnitStatusRules.NextStacks(4, false, 5));
		}

		[Test]
		public void Brand_status_stacks_and_lasts()
		{
			UnitStatusStyle style = LoadStatusConfig().Style(UnitStatusType.Brand);
			Assert.Greater(style.MaxStacks, 1, "낙인은 쌓여야 문턱이 있음");
			Assert.Greater(style.Seconds, 0f);
			Assert.AreEqual(0f, style.DamageTakenPercent, "낙인은 받는 피해를 안 바꿈 (표식과 섞이지 않게)");
		}

		[Test]
		public void Brand_boss_stacks_brand_and_calls_a_cleansing_orb()
		{
			Monster boss = Find(981);
			Monster orb = Find(986);
			List<MonsterAttack> steps = Leaves(boss.Attack).ToList();
			int branding = 0;
			foreach (MonsterAttack step in steps)
			{
				MonsterHitEffect hitEffect = step switch
				{
					ZoneMonsterAttack zone => zone.HitEffect,
					BandMonsterAttack band => band.HitEffect,
					_ => null,
				};
				if (hitEffect != null && hitEffect.Brand.Enabled)
				{
					branding++;
					Assert.Greater(hitEffect.Brand.StunSeconds, 0f, $"{step.name} 문턱에서 기절");
					Assert.IsNotNull(hitEffect.Brand.BurstFx, $"{step.name} 문턱 이펙트");
				}
			}
			Assert.GreaterOrEqual(branding, 2, "낙인을 거는 공격이 둘 이상 (시키는 행동이 다르게)");
			Assert.IsTrue(steps.OfType<SummonMonsterAttack>().Any(summon => summon.Summoned == orb), "구슬을 부름");
			Assert.IsNull(orb.Attack, "구슬은 공격하지 않음");
			Assert.IsTrue(orb.Affixes.OfType<CleanseAffix>().Any(cleanse => cleanse.Status == UnitStatusType.Brand), "구슬이 쓰러지면 낙인을 지움");
		}

		// ---- 2. 따라다니는 안전 원

		[Test]
		public void Safe_circle_follows_its_path_and_stops_at_the_end()
		{
			Vector3 start = new(1f, 0f, 1f);
			List<Vector2> path = new() { new Vector2(0f, 4f), new Vector2(4f, 4f) };
			// 앞이 +z 면 오른쪽은 +x
			Assert.AreEqual(start, SafeCircleMonsterAttack.PositionAt(start, Vector3.forward, path, 2f, 0f));
			AssertNear(start + new Vector3(0f, 0f, 2f), SafeCircleMonsterAttack.PositionAt(start, Vector3.forward, path, 2f, 1f));
			AssertNear(start + new Vector3(2f, 0f, 4f), SafeCircleMonsterAttack.PositionAt(start, Vector3.forward, path, 2f, 3f));
			AssertNear(start + new Vector3(4f, 0f, 4f), SafeCircleMonsterAttack.PositionAt(start, Vector3.forward, path, 2f, 100f));
			// 앞이 +x 면 오른쪽은 -z
			AssertNear(start + new Vector3(4f, 0f, -4f), SafeCircleMonsterAttack.PositionAt(start, Vector3.right, path, 2f, 100f));
			// 길이 비면 제자리
			Assert.AreEqual(start, SafeCircleMonsterAttack.PositionAt(start, Vector3.forward, new List<Vector2>(), 2f, 5f));
			Assert.IsTrue(SafeCircleMonsterAttack.IsInside(start, 2f, start + new Vector3(2f, 3f, 0f)), "높이는 무시, 테두리는 안");
			Assert.IsFalse(SafeCircleMonsterAttack.IsInside(start, 2f, start + new Vector3(2.01f, 0f, 0f)));
		}

		[Test]
		public void Safe_circle_dummy_can_be_followed()
		{
			SafeCircleMonsterAttack circle = Leaves(Find(982).Attack).OfType<SafeCircleMonsterAttack>().Single();
			// 주인공 걸음 (기본 이동 속도 약 2.9 m/s) 보다 느려야 따라감
			Assert.Less(circle.Speed, 2.9f * 0.7f, "원이 너무 빨라 따라갈 수 없음");
			Assert.IsNotEmpty(circle.Path, "제자리 원은 안전 섬 (다른 부품) 과 같음");
			Assert.Greater(circle.Seconds, circle.OutsideInterval * 3f, "원이 남는 동안 피해가 여러 번");
			Assert.AreEqual(SafeCircleStart.Target, circle.Start, "처음에는 주인공이 원 안");
		}

		// ---- 3. 무대 변화

		[Test]
		public void Arena_cell_contains_only_its_sector_and_pushes_out()
		{
			ArenaCell cell = new(Vector3.zero, Vector3.forward, 60f, 2f, 8f);
			Assert.IsTrue(cell.Contains(new Vector3(0f, 0f, 5f), 0f));
			Assert.IsFalse(cell.Contains(new Vector3(0f, 0f, 1f), 0f), "안쪽 빈 곳");
			Assert.IsFalse(cell.Contains(new Vector3(0f, 0f, 9f), 0f), "바깥");
			Assert.IsFalse(cell.Contains(new Vector3(5f, 0f, 0f), 0f), "옆 칸");
			Assert.IsTrue(cell.Contains(new Vector3(0f, 0f, 8.3f), 0.5f), "몸이 걸치면 안");
			Vector3 pushed = cell.PushOut(new Vector3(0f, 0f, 5f), 0.4f);
			Assert.IsFalse(cell.Contains(pushed, 0.4f), $"밀어낸 자리 {pushed} 가 아직 칸 안");
			Assert.Greater(pushed.magnitude, 8f, "안쪽 빈 곳 (주인공이 숨는 자리) 말고 바깥으로");
			ArenaCell ring = new(Vector3.zero, Vector3.forward, 360f, 0f, 3f);
			Vector3 outside = ring.PushOut(new Vector3(1f, 0f, 0f), 0.4f);
			Assert.IsFalse(ring.Contains(outside, 0.4f), "바깥으로");
		}

		[Test]
		public void Locked_cells_are_spread_and_never_all()
		{
			List<Vector3> directions = new();
			ArenaShiftMonsterAttack.CellDirections(6, 3, 0f, directions);
			Assert.AreEqual(3, directions.Count);
			// 하나 걸러 고르면 이웃끼리 120도
			Assert.AreEqual(120f, Vector3.Angle(directions[0], directions[1]), 0.01f);
			Assert.AreEqual(30f, Vector3.Angle(Vector3.forward, directions[0]), 0.01f, "칸 가운데 방향");
			foreach (ArenaShiftMonsterAttack shift in Leaves(Find(983).Attack).OfType<ArenaShiftMonsterAttack>())
			{
				Assert.Less(shift.LockCount, shift.Mode == ArenaShiftMode.Cells ? shift.CellCount : int.MaxValue, $"{shift.name} 칸을 다 잠금");
				Assert.Greater(shift.InnerRadius, 0f, $"{shift.name} 가운데가 남아야 함");
				Assert.Greater(shift.SlowSeconds, shift.TickInterval, $"{shift.name} 칸 안 둔화가 끊김");
			}
		}

		[Test]
		public void Closing_rings_shrink_from_outside_without_gaps()
		{
			ArenaShiftMonsterAttack closing = Leaves(Find(983).Attack).OfType<ArenaShiftMonsterAttack>().Single(shift => shift.Mode == ArenaShiftMode.Closing);
			float previousInner = closing.OuterRadius;
			for (int i = 0; i < closing.Steps; i++)
			{
				closing.RingOf(i, out float inner, out float outer);
				Assert.AreEqual(previousInner, outer, 0.001f, $"{i} 겹 사이 틈");
				Assert.Less(inner, outer);
				Assert.Greater(closing.StrikeDelay(i), i > 0 ? closing.StrikeDelay(i - 1) : -1f, "바깥부터 차례로");
				previousInner = inner;
			}
			Assert.AreEqual(closing.InnerRadius, previousInner, 0.001f, "마지막 겹 안이 가운데 안전 자리");
			Assert.IsTrue(closing.TelegraphInTurn, "늦은 겹의 예고는 앞 겹이 잠긴 뒤");
		}

		[Test]
		public void Spawn_positions_avoid_locked_cells_only_when_cells_exist()
		{
			Vector3 point = new(0f, 0f, 5f);
			// 칸이 없으면 그대로 (필드를 만들지 않음)
			Assert.AreEqual(point, ArenaShiftField.PushOutOfLocked(point, 0.4f));
			Assert.IsFalse(ArenaShiftField.IsLocked(point, 0.4f));
		}

		// ---- 4. 부위와 약점

		[Test]
		public void Part_damage_share_and_slot_position()
		{
			Assert.AreEqual(0, BossParts.SharedDamage(10, 0f), "몫 0 이면 부위 체력만");
			Assert.AreEqual(5, BossParts.SharedDamage(10, 0.5f));
			Assert.AreEqual(1, BossParts.SharedDamage(1, 0.1f), "작은 피해도 하나는 넘김");
			Assert.AreEqual(new Vector3(-2f, 1f, 3f), BossParts.SlotPosition(new Vector3(0f, 1f, 0f), new Vector2(-2f, 3f)), "x 오른쪽, y 앞 (+z)");
		}

		[Test]
		public void Parts_boss_gates_one_pattern_per_part()
		{
			Monster boss = Find(984);
			PartsAffix parts = boss.Affixes.OfType<PartsAffix>().Single();
			Assert.GreaterOrEqual(parts.Slots.Count, 2, "최소 판은 부위 둘");
			List<MonsterAttack> steps = Leaves(boss.Attack).ToList();
			foreach (BossPartSlot slot in parts.Slots)
			{
				Assert.IsTrue(steps.OfType<PartGatedMonsterAttack>().Any(gated => gated.Part == slot.Part), $"부위 {slot.Part.name} 에 걸린 패턴 없음");
				Assert.IsNull(slot.Part.Attack, "부위는 따로 공격하지 않음");
				Assert.IsTrue(slot.Part.Affixes.OfType<PartBodyAffix>().Any(), $"{slot.Part.name} 부위 일 없음");
			}
			Assert.IsTrue(steps.Any(step => step is PartGatedMonsterAttack == false), "부위를 다 꺾어도 남는 몸통 패턴");
			// 부위를 세우지 않은 몸 (시험) 에서는 부위 패턴이 늘 건너뜀
			Assert.IsFalse(BossParts.HasLivingPart(null, parts.Slots[0].Part));
		}

		private static void AssertNear(Vector3 expected, Vector3 actual) =>
			Assert.Less(Vector3.Distance(expected, actual), 0.001f, $"{expected} 이어야 하는데 {actual}");
	}
}
