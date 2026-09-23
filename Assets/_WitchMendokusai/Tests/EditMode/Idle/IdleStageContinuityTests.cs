using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Idle;

namespace WitchMendokusai.Tests
{
	public sealed class IdleStageContinuityTests
	{
		private const string DATA = "Assets/_WitchMendokusai/Idle/Data/Assets/";
		private GameObject owner;
		private BattleStage stage;
		private IdleSession session;
		private VisualElement veil;
		private Transform Doll => owner.transform.Find("Preview/Battle/World/Doll0");

		[SetUp]
		public void SetUp()
		{
			new IdleDollCatalogFixture().ConfigureCatalog();
			owner = new GameObject("ContinuityTest");
			stage = owner.AddComponent<BattleStage>();
			typeof(BattleStage).GetField("presentationAsset", BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(stage, AssetDatabase.LoadAssetAtPath<BattlePresentationSO>(DATA + "BP_0001_Idle.asset"));
			typeof(BattleStage).GetMethod("BuildPreview", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(stage, null);
			Assert.IsNotNull(owner.transform.Find("Preview/Battle/Ground"), "명시적 시험 무대 생성");
			stage.SetDungeonCatalog(AssetDatabase.LoadAssetAtPath<DungeonCatalogSO>(DATA + "DC_0001_Idle.asset"));
			veil = new VisualElement();
			stage.SetTransitionVeil(veil, 1f);
			session = new IdleSession(new IdleTuning());
			session.AdvanceLive(0.1d);
			stage.Render(session.Capture(), 0f);
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(owner);

		[Test]
		public void Reset_WaitsForOpaqueFrame_ThenSnaps()
		{
			session.State.Battle.X[0] = 15d;
			stage.Render(session.Capture(), 1f);
			float before = Doll.localPosition.x;
			Assert.Greater(before, 10f);
			IdleBattleSim.Reset(session.State, session.Tuning);
			stage.Render(session.Capture(), 10f);
			Assert.AreEqual(before, Doll.localPosition.x, 0.001f, "재배치는 가림 완료 프레임 다음");
			Assert.AreEqual(1f, veil.style.opacity.value);
			stage.Render(session.Capture(), 0f);
			Assert.AreEqual((float)session.Capture().Fighters[0].X, Doll.localPosition.x, 0.001f);
			Assert.AreEqual(1f, veil.style.opacity.value);
			stage.Render(session.Capture(), 1f);
			Assert.AreEqual(0f, veil.style.opacity.value);
		}

		[Test]
		public void Rebase_ShiftsBodyAndCameraTogether_WithoutVeil()
		{
			session.State.Battle.X[0] = 1200d;
			stage.Render(session.Capture(), 1f);
			float before = Doll.localPosition.x;
			Transform cameraTarget = owner.transform.Find("Preview/CameraTarget");
			float cameraBefore = cameraTarget.localPosition.x;
			session.State.Battle.X[0] -= 1000d;
			session.State.Battle.OriginX += 1000d;
			stage.Render(session.Capture(), 0f);
			Assert.AreEqual(before - 1000f, Doll.localPosition.x, 0.001f);
			Assert.AreEqual(cameraBefore - 1000f, cameraTarget.localPosition.x, 0.001f);
			Assert.AreEqual(200f, owner.transform.Find("Preview/Battle/Ground").localPosition.x, 0.001f);
		}

		[TestCase(IdleDungeonKind.Gold, "DG_0001_Gold.asset")]
		[TestCase(IdleDungeonKind.Boss, "DG_0002_Boss.asset")]
		[TestCase(IdleDungeonKind.Gear, "DG_0003_Gear.asset")]
		public void Dungeon_ChangesGroundAndRestoresItOnExit(IdleDungeonKind kind, string asset)
		{
			Renderer floor = owner.transform.Find("Preview/Battle/Ground").GetComponent<Renderer>();
			Color original = floor.sharedMaterial.color;
			session.State.Tickets[(int)kind] = 1;
			Assert.IsTrue(session.TryEnterDungeon(kind, 0, 0));
			stage.Render(session.Capture(), 1f);
			Assert.AreEqual(original, floor.sharedMaterial.color);
			stage.Render(session.Capture(), 0f);
			Color expected = AssetDatabase.LoadAssetAtPath<DungeonSO>(DATA + asset).FloorColor;
			Assert.Less(Vector4.Distance(expected, floor.sharedMaterial.color), 0.00001f);
			Assert.AreNotEqual(original, floor.sharedMaterial.color);
			stage.Render(session.Capture(), 1f);
			Assert.IsTrue(session.TryLeaveDungeon());
			stage.Render(session.Capture(), 1f);
			stage.Render(session.Capture(), 0f);
			Assert.AreEqual(original, floor.sharedMaterial.color);
		}
	}
}
