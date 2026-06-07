using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-188 first-use rung — mock IModContentRegistry 주입 → HelloMod.Initialize(ctx) → quest/effect 1개 등록됨 assert.
	/// 껍데기(Debug.Log only) → 실기능(IModContext 콘텐츠 등록) 전환 증명.
	/// </summary>
	[TestFixture]
	public class HelloModRegistryTest
	{
		private class FakeModContentRegistry : IModContentRegistry
		{
			public readonly List<RuntimeQuestSaveData> RegisteredQuests = new();
			public readonly List<EffectInfoData> RegisteredEffects = new();

			public void RegisterQuest(RuntimeQuestSaveData questSaveData) => RegisteredQuests.Add(questSaveData);
			public void RegisterEffect(EffectInfoData effectInfoData) => RegisteredEffects.Add(effectInfoData);
		}

		[Test]
		public void Initialize_WithIModContext_RegistersOneQuestAndOneEffect()
		{
			FakeModContentRegistry registry = new FakeModContentRegistry();
			ModContext context = new ModContext(registry);
			Mods.Sample.HelloMod mod = new Mods.Sample.HelloMod();

			mod.Initialize(context);

			Assert.AreEqual(1, registry.RegisteredQuests.Count, "HelloMod 가 quest 1개 등록해야 함 (껍데기 → 실기능).");
			Assert.AreEqual("Hello Mod Quest", registry.RegisteredQuests[0].Name);
			Assert.AreEqual(QuestType.Normal, registry.RegisteredQuests[0].Type);
			Assert.AreEqual(RuntimeQuestState.InProgress, registry.RegisteredQuests[0].State);
			Assert.IsNotNull(registry.RegisteredQuests[0].Guid);

			Assert.AreEqual(1, registry.RegisteredEffects.Count, "HelloMod 가 effect 1개 등록해야 함.");
			Assert.AreEqual(EffectType.UnitStat, registry.RegisteredEffects[0].Type);
			Assert.AreEqual(ArithmeticOperator.Add, registry.RegisteredEffects[0].ArithmeticOperator);
		}

		[Test]
		public void HelloMod_Kind_IsBehavior()
		{
			Mods.Sample.HelloMod mod = new Mods.Sample.HelloMod();
			Assert.AreEqual(ModKind.Behavior, mod.Kind, "in-process C# .dll 모드 = Behavior kind.");
		}

		[Test]
		public void ModKind_Behavior_IsDistinctFrom_AssetOverlay()
		{
			Assert.AreNotEqual(ModKind.Behavior, ModKind.AssetOverlay,
				"통합 taxonomy = 2-kind 분류 (Behavior in-process .dll / AssetOverlay manifest+bundle).");
		}

		[Test]
		public void ModContext_ExposesInjectedContentRegistry()
		{
			FakeModContentRegistry registry = new FakeModContentRegistry();
			ModContext context = new ModContext(registry);

			Assert.AreSame(registry, context.ContentRegistry, "ModContext 가 주입된 registry 를 노출 (DI seam).");
		}

		[Test]
		public void ModContentRegistryBridge_RegisterRoutesToInstance()
		{
			FakeModContentRegistry registry = new FakeModContentRegistry();
			ModContentRegistryBridge.Register(registry);

			EffectInfoData effectInfoData = new EffectInfoData
			{
				Type = EffectType.IntVariable,
				DataSoID = -1,
				ArithmeticOperator = ArithmeticOperator.Add,
				Value = 7,
			};
			ModContentRegistryBridge.RegisterEffect(effectInfoData);

			Assert.AreEqual(1, registry.RegisteredEffects.Count, "Bridge static accessor 가 Instance 로 라우팅.");
			Assert.AreEqual(7, registry.RegisteredEffects[0].Value);
		}
	}
}
