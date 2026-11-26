using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 일반 몬스터 공격 패턴 더미 (931~938). 정본 memo wm/design/monster-pattern-20261126
	// 더미마다 공격의 짜임 (종류와 배치) 과 시키는 행동이 서로 다르고, 공격 검사에 오류가 없는지
	public class MonsterPatternDummyTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/PatternDummy";
		private const string ADDRESS_GROUP = "Assets/AddressableAssetsData/AssetGroups/Monster.asset";
		// 주인공 몸 반지름. EliteBossAttackTest 와 같은 값 (UnitMovement.BodyRadius 는 장면이 있어야 읽힘)
		private const float PLAYER_BODY_RADIUS = 0.3f;

		// 시키는 행동. 설계 표 (memo README) 와 같은 말. 겹치면 같은 패턴으로 보임
		private static readonly Dictionary<int, string> asks = new()
		{
			{ 931, "줄에서 옆으로 비키기" },
			{ 932, "부풀면 멀어지기" },
			{ 933, "그림자가 닿기 전에 꺾어 달리기" },
			{ 934, "몸에 붙어 부채 안쪽에 서기" },
			{ 935, "가던 길을 바꾸기" },
			{ 936, "들어갔다 다시 나가기" },
			{ 937, "세 번 세고 반격" },
			{ 938, "가운데 가만히 서기" },
		};

		private static List<Monster> LoadDummies() =>
			AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER })
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.OrderBy(monster => monster.ID)
				.ToList();

		[Test]
		public void Every_pattern_dummy_exists_with_an_address()
		{
			List<Monster> dummies = LoadDummies();
			CollectionAssert.AreEquivalent(asks.Keys, dummies.Select(monster => monster.ID));
			string group = File.ReadAllText(ADDRESS_GROUP);
			foreach (Monster monster in dummies)
			{
				string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(monster));
				StringAssert.Contains($"m_GUID: {guid}\n    m_Address: Monster/{monster.ID}\n", group.Replace("\r\n", "\n"), monster.name);
				Assert.IsEmpty(monster.Loots, $"{monster.name} 더미는 드랍 없음");
			}
		}

		[Test]
		public void Pattern_attacks_pass_validation()
		{
			foreach (Monster monster in LoadDummies())
			{
				Assert.IsNotNull(monster.Attack, $"{monster.name} 공격 없음");
				List<string> errors = new();
				monster.Attack.Validate(monster, errors);
				Assert.IsEmpty(errors, string.Join("\n", errors));
			}
		}

		[Test]
		public void Each_strike_has_a_body_motion_and_a_strike_effect()
		{
			foreach (Monster monster in LoadDummies())
			{
				foreach (MonsterAttack attack in Leaves(monster.Attack))
				{
					Assert.IsFalse(attack.Pose.IsEmpty, $"{attack.name} 몸동작 없음");
					Assert.IsNotNull(attack.StrikeFx, $"{attack.name} 타격 이펙트 없음");
					Assert.IsNotNull(attack.WindupTelegraph, $"{attack.name} 예고 없음");
				}
			}
		}

		[Test]
		public void Shapes_and_asks_do_not_repeat()
		{
			HashSet<string> shapes = new();
			HashSet<string> seenAsks = new();
			foreach (Monster monster in LoadDummies())
			{
				Assert.IsTrue(shapes.Add(Shape(monster.Attack)), $"{monster.name} 공격 짜임 {Shape(monster.Attack)} 이 다른 더미와 겹침");
				Assert.IsTrue(seenAsks.Add(asks[monster.ID]), $"{monster.name} 시키는 행동이 겹침");
			}
		}

		[Test]
		public void Burrow_ring_is_closed_and_the_middle_is_safe()
		{
			HideMonsterAttack burrow = (HideMonsterAttack)Find(938).Attack;
			// 이웃한 두 원이 겹쳐 고리에 틈이 없음
			Assert.LessOrEqual(2f * burrow.Distance * Mathf.Sin(Mathf.PI / burrow.Count), burrow.Radius * 2f, "고리에 빠져나갈 틈");
			// 가운데에 선 주인공 몸이 어느 원에도 안 닿음
			Assert.Greater(burrow.Distance, burrow.Radius + PLAYER_BODY_RADIUS, "가운데 서 있으면 안 맞아야 함");
		}

		[Test]
		public void Blink_shadow_reaches_the_player_from_trigger_range()
		{
			HideMonsterAttack blink = (HideMonsterAttack)Find(933).Attack;
			// 쫓기는 떠오르는 초 뒤부터 자리를 정할 때까지 (MonsterHideaway)
			float chaseReach = (blink.ChaseSeconds - blink.RiseSeconds) * blink.ChaseSpeed;
			Assert.GreaterOrEqual(chaseReach, blink.TriggerRange - blink.Radius - PLAYER_BODY_RADIUS, "사거리 끝에서 시작하면 그림자가 못 닿음");
		}

		[Test]
		public void Seed_line_reaches_the_trigger_range()
		{
			SequenceMonsterAttack volley = (SequenceMonsterAttack)Find(931).Attack;
			ScatterZoneMonsterAttack line = volley.Steps.OfType<ScatterZoneMonsterAttack>().Single();
			Assert.GreaterOrEqual(line.Distance * line.Count + line.Radius + PLAYER_BODY_RADIUS, line.TriggerRange, "사거리 끝에 선 주인공에게 줄이 안 닿음");
		}

		private static Monster Find(int id) => LoadDummies().Single(monster => monster.ID == id);

		// 순서 공격은 낱개로 풂
		private static IEnumerable<MonsterAttack> Leaves(MonsterAttack attack) =>
			attack is SequenceMonsterAttack sequence ? sequence.Steps : new[] { attack };

		// 종류 이름에 배치를 더한 짜임. 같은 부품이라도 배치가 다르면 다른 패턴
		private static string Shape(MonsterAttack attack) => attack switch
		{
			SequenceMonsterAttack sequence => "순서(" + string.Join(",", sequence.Steps.Select(Shape)) + ")",
			ZoneMonsterAttack zone => $"{zone.KindName}/{zone.Center}/{(zone.LeadSeconds > 0f ? "예측" : "지금")}",
			BandMonsterAttack band => $"{band.KindName}/{(band.ArcDegrees >= 360f ? "고리" : "부채")}{band.Bands.Count}",
			HideMonsterAttack hide => $"{hide.KindName}/{(hide.ChaseSeconds > 0f ? "쫓음" : "자리" + hide.Count)}",
			ScatterZoneMonsterAttack scatter => $"{scatter.KindName}/{scatter.Arrangement}",
			DashMonsterAttack dash => $"{dash.KindName}/{(dash.AngleDegrees == 0f ? "곧장" : "옆")}",
			_ => attack.KindName,
		};
	}
}
