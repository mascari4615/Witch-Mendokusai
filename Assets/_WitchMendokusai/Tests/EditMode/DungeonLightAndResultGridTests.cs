using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class DungeonLightAndResultGridTests
	{
		private const string WORLD_SCENE_PATH = "Assets/_WitchMendokusai/Scenes/World/World.unity";
		private const string LIGHT_PREFAB_GUID = "6953d165cf383e7419677887bd3617e8";
		private const string LIGHT_ROOT_FILE_ID = "6899038483242530734";

		private static readonly Vector2 CARD_AREA = new(890f, 352f);
		private static readonly Vector2 CELL = new(138f, 172f);
		private static readonly Vector2 SPACING = new(12f, 8f);

		[Test]
		public void WorldScene_KeepsSunActiveAndNamed()
		{
			string scene = File.ReadAllText(WORLD_SCENE_PATH);
			string lightRootOff = $"target: {{fileID: {LIGHT_ROOT_FILE_ID}, guid: {LIGHT_PREFAB_GUID}, type: 3}}\\s+propertyPath: m_IsActive\\s+value: 0";
			Assert.IsFalse(Regex.IsMatch(scene, lightRootOff), "Light 프리팹 부모가 꺼지면 던전 직접광이 0개가 된다");
			Assert.IsFalse(scene.Contains("m_Sun: {fileID: 0}"), "Sun Source가 비면 SkyDirector가 태양을 못 고른다");
		}

		[TestCase(-0.3f, 50f)]
		[TestCase(0f, 50f)]
		[TestCase(0.36f, 50f)]
		[TestCase(1f, 90f)]
		public void LightPitch_StaysAboveGround(float altitude, float expected)
		{
			Assert.AreEqual(expected, SkyDirector.ComputeLightPitch(altitude, 50f), 0.001f);
		}

		[TestCase("Assets/_WitchMendokusai/Domain/World/Stage/0000_Home/[Stage] [0] Home.prefab", true)]
		[TestCase("Assets/_WitchMendokusai/Domain/World/Stage/0005_StartTown/[Stage] [5] StartTown.prefab", true)]
		[TestCase("Assets/_WitchMendokusai/Domain/World/Stage/0004_Lab/[Stage] [4] Lab.prefab", false)]
		[TestCase("Assets/_WitchMendokusai/Domain/World/Stage/1000_TempCombat/Stage_TempCombat.prefab", false)]
		public void StageOwnDirectionalLight_DecidesSceneSun(string prefabPath, bool expected)
		{
			StageObject stage = AssetDatabase.LoadAssetAtPath<StageObject>(prefabPath);
			Assert.AreEqual(expected, SkyDirector.HasOwnDirectionalLight(stage));
		}

		[TestCase(0, 1f)]
		[TestCase(12, 1f)]
		public void GridFit_KeepsFullSizeWhenItFits(int count, float expected)
		{
			GridFit fit = GridFit.Compute(count, CARD_AREA, CELL, SPACING);
			Assert.AreEqual(6, fit.Columns);
			Assert.AreEqual(expected, fit.Scale, 0.001f);
		}

		[TestCase(13)]
		[TestCase(15)]
		[TestCase(18)]
		[TestCase(40)]
		[TestCase(120)]
		public void GridFit_NeverOverflowsArea(int count)
		{
			GridFit fit = GridFit.Compute(count, CARD_AREA, CELL, SPACING);
			int rows = Mathf.CeilToInt((float)count / fit.Columns);
			float width = (fit.Columns * CELL.x + (fit.Columns - 1) * SPACING.x) * fit.Scale;
			float height = (rows * CELL.y + (rows - 1) * SPACING.y) * fit.Scale;
			Assert.Less(fit.Scale, 1f);
			Assert.LessOrEqual(width, CARD_AREA.x + 0.01f);
			Assert.LessOrEqual(height, CARD_AREA.y + 0.01f);
		}

		[Test]
		public void RunEquipmentHud_AlwaysShowsSignatureBesideAcquiredSlots()
		{
			EquipmentData signature = ScriptableObject.CreateInstance<EquipmentData>();
			RunEquipmentHud hud = new();
			hud.Refresh(signature, new EquipmentData[0], 4);
			Assert.AreEqual(5, hud.childCount);
			Assert.IsTrue(hud[0].ClassListContains("wm-run-equipment__slot--signature"));
			Assert.IsTrue(hud[0].ClassListContains("wm-run-equipment__slot--filled"));
			Assert.IsFalse(hud[1].ClassListContains("wm-run-equipment__slot--filled"));
			Object.DestroyImmediate(signature);
		}
	}
}
