using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 보스 부품 셋 (갈래 선택, 부술 물체, 벽에 박기) 과 보스 더미 961~964, 부술 기둥 969. 정본 memo wm/design/boss-choice-20261126
	public class BossChoiceDummyTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/ChoiceDummy";
		private const string ADDRESS_GROUP = "Assets/AddressableAssetsData/AssetGroups/Monster.asset";
		// 주인공 걷기 (m/s) 와 반응 (초). EliteBossAttackTest 와 같은 값
		private const float WALK_METERS_PER_SECOND = 2.9f;
		private const float REACTION_SECONDS = 0.2f;

		private static readonly int[] bossIds = { 961, 962, 963, 964 };
		private const int PILLAR_ID = 969;

		private static List<Monster> LoadDummies() =>
			AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER })
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.OrderBy(monster => monster.ID)
				.ToList();

		private static Monster Dummy(int id) => LoadDummies().Single(monster => monster.ID == id);

		[Test]
		public void Every_dummy_exists_with_an_address_and_no_drops()
		{
			List<Monster> dummies = LoadDummies();
			CollectionAssert.AreEquivalent(bossIds.Append(PILLAR_ID), dummies.Select(monster => monster.ID));
			string group = File.ReadAllText(ADDRESS_GROUP).Replace("\r\n", "\n");
			foreach (Monster monster in dummies)
			{
				string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(monster));
				StringAssert.Contains($"m_GUID: {guid}\n    m_Address: Monster/{monster.ID}\n", group, monster.name);
				Assert.IsEmpty(monster.Loots, $"{monster.name} 더미는 드랍 없음");
			}
		}

		[Test]
		public void Boss_dummy_attacks_pass_validation()
		{
			foreach (int id in bossIds)
			{
				Monster monster = Dummy(id);
				Assert.IsNotNull(monster.Attack, $"{monster.name} 공격 없음");
				List<string> errors = new();
				monster.Attack.Validate(monster, errors);
				Assert.IsEmpty(errors, string.Join("\n", errors));
			}
		}

		[Test]
		public void Choice_picks_branches_by_weight()
		{
			ChoiceMonsterAttack choice = (ChoiceMonsterAttack)Dummy(961).Attack;
			Assert.AreEqual(2, choice.Branches.Count);
			float firstShare = choice.Branches[0].Weight / choice.Branches.Sum(branch => branch.Weight);
			Assert.AreEqual(0, choice.PickBranch(0f));
			Assert.AreEqual(0, choice.PickBranch(firstShare * 0.99f));
			Assert.AreEqual(1, choice.PickBranch(firstShare * 1.01f));
			Assert.AreEqual(choice.Branches.Count - 1, choice.PickBranch(1f));
		}

		// 단서는 갈래마다 다르게 보이고 (물체, 개수, 색), 갈래 공격의 예고보다 먼저 혼자 보임. 몸짓은 갈래와 상관없이 하나
		[TestCase(961)]
		[TestCase(962)]
		public void Choice_cues_differ_and_come_before_the_telegraph(int id)
		{
			ChoiceMonsterAttack choice = (ChoiceMonsterAttack)Dummy(id).Attack;
			Assert.Greater(choice.CueSeconds, REACTION_SECONDS, "단서를 읽을 틈");
			Assert.IsFalse(choice.Pose.IsEmpty, "같은 준비 몸짓");
			for (int i = 0; i < choice.Branches.Count; i++)
			{
				ChoiceBranch branch = choice.Branches[i];
				List<Vector3> offsets = new();
				choice.CueOffsets(branch.CueCount, offsets);
				Assert.AreEqual(branch.CueCount, offsets.Count);
				Assert.AreEqual(branch.CueCount, offsets.Distinct().Count(), "단서 물체가 겹치지 않음");
				foreach (Vector3 offset in offsets)
					Assert.AreEqual(choice.CueDistance, offset.magnitude, 0.001f);
				for (int j = 0; j < i; j++)
				{
					Assert.IsFalse(branch.LooksLike(choice.Branches[j]), $"{j}번과 {i}번 단서가 같음");
					Assert.AreNotSame(branch.Attack, choice.Branches[j].Attack, "갈래 결과가 같음");
				}
			}
		}

		[Test]
		public void Ward_cancels_only_when_all_broken_before_the_strike()
		{
			Assert.IsTrue(WardMonsterAttack.IsCancelled(0, 1f, 2f));
			Assert.IsFalse(WardMonsterAttack.IsCancelled(1, 1f, 2f), "하나라도 서 있으면 큰 공격");
			Assert.IsFalse(WardMonsterAttack.IsCancelled(0, 2f, 2f), "큰 공격이 때린 뒤에는 취소 없음");
			Assert.IsTrue(WardMonsterAttack.IsCancelled(0, 1f, float.PositiveInfinity), "예고 동안");
		}

		// 기둥 사이를 걸어 다 돌 수 있는 시간이고, 부쉈을 때 주저앉는 것이 큰 공격 뒤 숨 고르기보다 김 (보상)
		[Test]
		public void Ward_window_leaves_time_to_reach_every_pillar()
		{
			Monster boss = Dummy(963);
			WardMonsterAttack ward = (WardMonsterAttack)boss.Attack;
			Assert.AreSame(Dummy(PILLAR_ID), ward.Ward);
			List<Vector3> positions = new();
			ward.WardPositions(Vector3.zero, Vector3.forward * ward.TriggerRange, positions);
			Assert.AreEqual(ward.Count, positions.Count);
			Assert.AreEqual(ward.Distance, positions[0].z, 0.001f, "첫 기둥은 주인공 쪽");
			// 주인공은 사거리 끝에서 첫 기둥까지 걸어온 뒤 차례로
			float walk = ward.TriggerRange - ward.Distance;
			for (int i = 1; i < positions.Count; i++)
				walk += Vector3.Distance(positions[i - 1], positions[i]);
			Assert.Less(walk / WALK_METERS_PER_SECOND + REACTION_SECONDS, ward.MinBreakSeconds);
			Assert.Greater(ward.RecoverySeconds, ward.Payoff.RecoverySeconds);
			Assert.IsNotNull(ward.RecoveryFx, "반격 창");
		}

		[Test]
		public void Pillar_stands_still_without_attack()
		{
			Monster pillar = Dummy(PILLAR_ID);
			Assert.IsNull(pillar.Attack);
			Assert.AreNotEqual(MonsterType.Boss, pillar.Type);
			SerializedProperty stats = new SerializedObject(pillar).FindProperty("<InitStatInfos>k__BackingField.initStats");
			bool hasSpeed = false;
			for (int i = 0; i < stats.arraySize; i++)
			{
				SerializedProperty stat = stats.GetArrayElementAtIndex(i);
				if (stat.FindPropertyRelative("unitStat").intValue != (int)UnitStatType.MOVEMENT_SPEED)
					continue;
				hasSpeed = true;
				Assert.AreEqual(0, stat.FindPropertyRelative("value").intValue, "움직이지 않음");
			}
			Assert.IsTrue(hasSpeed, "이동 속도 칸이 있어야 기본값을 덮음");
		}

		[Test]
		public void Wall_slam_stays_longer_only_on_terrain()
		{
			DashMonsterAttack dash = (DashMonsterAttack)Dummy(964).Attack;
			Assert.Greater(dash.WallSlamSeconds, dash.RecoverySeconds);
			Assert.AreEqual(dash.WallSlamSeconds, dash.RecoveryAfter(true));
			Assert.AreEqual(dash.RecoverySeconds, dash.RecoveryAfter(false));
			Assert.IsFalse(DashMonsterAttack.IsTerrain(null));
			GameObject rock = new("Rock");
			try
			{
				Assert.IsTrue(DashMonsterAttack.IsTerrain(rock.AddComponent<BoxCollider>()), "유닛이 아닌 물체는 지형");
			}
			finally
			{
				Object.DestroyImmediate(rock);
			}
		}

		// 기존 돌진 자산은 벽에 박기 칸이 0 이라 지금과 같음
		[Test]
		public void Existing_dashes_keep_the_old_recovery()
		{
			string[] guids = AssetDatabase.FindAssets("t:DashMonsterAttack");
			Assert.Greater(guids.Length, 1, "돌진 자산을 못 찾으면 이 시험은 아무것도 안 봄");
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.StartsWith(MONSTER_FOLDER))
					continue;
				DashMonsterAttack dash = AssetDatabase.LoadAssetAtPath<DashMonsterAttack>(path);
				Assert.AreEqual(0f, dash.WallSlamSeconds, path);
				Assert.AreEqual(dash.RecoverySeconds, dash.RecoveryAfter(true), path);
			}
		}
	}
}
