using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 밭 칸의 키가 「번호」에서 「자리」로 바뀌었다.
	/// 땅은 이미 복셀 격자라 밭이 별도 격자를 가질 이유가 없다(두 격자를 평생 맞춰야 한다).
	/// 이미 저장된 온실이 안 깨지도록 옛 칸 번호는 <see cref="FarmCoord.Legacy"/> 다리로 남는다.
	/// </summary>
	public sealed class FarmCoordTest
	{
		private const float FULL_VITALITY = 100f;
		private const int PLANT_ID = 1;

		private static PlantGrowthParams CozyPlant()
		{
			// Drain 0 = 절대 안 시듦(코지 작물) — 이 테스트의 관심은 자리이지 시듦이 아니다.
			return new PlantGrowthParams(60, 2, FULL_VITALITY, 0f, 30f);
		}

		[Test]
		public void SameCoord_IsSamePlot_DifferentCoord_IsNot()
		{
			Greenhouse greenhouse = new();
			greenhouse.AddPlot(new FarmCoord(3, 64, -7));

			Assert.That(greenhouse.GetPlot(new FarmCoord(3, 64, -7)), Is.Not.Null, "같은 자리는 같은 칸");
			Assert.That(greenhouse.GetPlot(new FarmCoord(3, 64, -6)), Is.Null, "한 칸 옆은 다른 칸");
			Assert.That(greenhouse.GetPlot(new FarmCoord(3, 65, -7)), Is.Null, "한 층 위도 다른 칸");
		}

		[Test]
		public void LegacyPlotId_StillFindsItsPlot()
		{
			// 이미 저장된 온실(좌표 없는 칸 번호)이 그대로 열려야 한다.
			Greenhouse greenhouse = new();
			greenhouse.AddPlot(2).Plant(PLANT_ID, CozyPlant(), FULL_VITALITY);

			Assert.That(greenhouse.GetPlot(2), Is.Not.Null);
			Assert.That(greenhouse.GetPlot(2).IsPlanted, Is.True);
			Assert.That(greenhouse.GetPlot(FarmCoord.Legacy(2)), Is.Not.Null, "옛 번호 = 옛 자리");
			Assert.That(greenhouse.GetPlot(new FarmCoord(2, 0, 0)), Is.Null, "옛 칸이 진짜 땅(y=0)을 차지하지 않는다");
		}

		[Test]
		public void LegacyCoords_NeverCollide_WithRealGround()
		{
			Greenhouse greenhouse = new();
			greenhouse.AddPlot(5);
			greenhouse.AddPlot(new FarmCoord(5, 0, 0));

			Assert.That(greenhouse.PlotCount, Is.EqualTo(2), "옛 5번 칸과 땅 (5,0,0) 은 서로 다른 칸");
			Assert.That(FarmCoord.Legacy(5).IsLegacy, Is.True);
			Assert.That(new FarmCoord(5, 0, 0).IsLegacy, Is.False);
		}

		[Test]
		public void CoordOrder_IsDeterministic()
		{
			// 같은 밭은 어느 기계에서도 같은 순서로 돌봄·순회된다(triage 결정성의 근거).
			FarmCoord low = new(0, 0, 0);
			FarmCoord sameRowNextX = new(1, 0, 0);
			FarmCoord nextZ = new(0, 0, 1);
			FarmCoord upstairs = new(0, 1, 0);

			Assert.That(low.CompareTo(sameRowNextX) < 0, Is.True);
			Assert.That(low.CompareTo(nextZ) < 0, Is.True);
			Assert.That(low.CompareTo(upstairs) < 0, Is.True);
			Assert.That(low.CompareTo(new FarmCoord(0, 0, 0)), Is.EqualTo(0));
		}

		[Test]
		public void CarersTriage_StillWorks_OnCoordKeys()
		{
			// 자리 키로 바뀌어도 「가장 약한 칸부터 돌본다」는 안전망은 그대로여야 한다.
			Greenhouse greenhouse = new();
			PlantGrowthParams draining = new(60, 2, FULL_VITALITY, 1f, 50f);
			GreenhousePlot weak = greenhouse.AddPlot(new FarmCoord(0, 0, 0));
			GreenhousePlot strong = greenhouse.AddPlot(new FarmCoord(1, 0, 0));
			weak.Plant(PLANT_ID, draining, 10f);
			strong.Plant(PLANT_ID, draining, FULL_VITALITY);

			greenhouse.TickWithCarers(new[] { 7 }, 5);

			Assert.That(weak.Vitality > 10f, Is.True, "인형 하나면 죽기 직전 칸부터 구한다");
			Assert.That(greenhouse.LivingCount(), Is.EqualTo(2));
		}

		[Test]
		public void WorldPoint_FallsIntoTheBlockItIsInside()
		{
			// 내림이지 반올림이 아니다 — 음수 쪽에서 한 칸 밀리면 원점 근처에서만 티가 나 늦게 잡힌다.
			Assert.That(FarmCoord.FromWorld(0.9f, 64.2f, 0.1f), Is.EqualTo(new FarmCoord(0, 64, 0)));
			Assert.That(FarmCoord.FromWorld(-0.2f, 64f, -0.9f), Is.EqualTo(new FarmCoord(-1, 64, -1)));
			Assert.That(FarmCoord.FromWorld(-1f, 64f, -1f), Is.EqualTo(new FarmCoord(-1, 64, -1)), "경계는 그 칸의 시작");
			Assert.That(FarmCoord.FromWorld(3f, 0f, 7f), Is.EqualTo(new FarmCoord(3, 0, 7)));
		}
	}
}
