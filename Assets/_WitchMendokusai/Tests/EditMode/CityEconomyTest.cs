using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-4 — <see cref="CityEconomy"/> 자원 재고 원장 회귀 잠금.
	///
	/// 순수 POCO — 미등록=0 / 누적 / 음수 delta 소비. 영속 round-trip 은 WorldStageCitySaveTest 가 형제 배선과
	/// 함께 검증(replace/legacy-null). new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CityEconomyTest
	{
		[Test]
		public void GetStock_UnknownResource_ReturnsZero()
		{
			CityEconomy economy = new();

			Assert.That(economy.GetStock(new ResourceId(0)), Is.EqualTo(0f), "미등록 자원 = 재고 0");
		}

		[Test]
		public void AddStock_Accumulates()
		{
			CityEconomy economy = new();
			ResourceId resource = new(0);

			economy.AddStock(resource, 5f);
			economy.AddStock(resource, 3f);

			Assert.That(economy.GetStock(resource), Is.EqualTo(8f).Within(0.0001f), "같은 자원 누적");
		}

		[Test]
		public void AddStock_NegativeDelta_Subtracts()
		{
			CityEconomy economy = new();
			ResourceId resource = new(1);

			economy.AddStock(resource, 10f);
			economy.AddStock(resource, -4f); // 소비

			Assert.That(economy.GetStock(resource), Is.EqualTo(6f).Within(0.0001f), "음수 delta = 소비");
		}
	}
}
