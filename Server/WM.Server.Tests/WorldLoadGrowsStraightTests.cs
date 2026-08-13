using System.Diagnostics;
using NUnit.Framework;
using WitchMendokusai;


namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 되살리기가 <b>세계 크기에 곧게</b> 자라나 (TASK-WM-353).
	///
	/// ★ 왜 이 시험이 생겼나 (2026-08-14 실측): 되살릴 때 집 하나마다 <b>칸 장부를 통째로 베끼고</b>
	///   있었다. 그래서 집 6만 채는 뜨는 데 36초, 30만 채는 <b>5분이 지나도 안 떴다</b> —
	///   배포 때마다 그만큼 세계가 닫혀 있고, 어느 크기부터는 영영 못 뜬다(기억은 멀쩡한데).
	///   고친 뒤: 6만 0.7초 · 30만 1.1초.
	///
	/// ★ 왜 <b>비율</b>로 자르나: 밀리초로 자르면 느린 기계에서 태생적 빨강이다.
	///   자료를 네 배로 주고 <b>여덟 배 안</b>이면 곧다(제곱이면 열여섯 배가 나온다).
	/// </summary>
	public sealed class WorldLoadGrowsStraightTests
	{
		private static WorldSaveData WorldWith(int houses)
		{
			BuildingSaveData[] buildings = new BuildingSaveData[houses];
			for (int i = 0; i < houses; i++)
			{
				buildings[i] = new BuildingSaveData
				{
					x = (i % 700) - 350,
					z = (i / 700) - 350,
					y = 0,
					w = 1,
					l = 1,
					buildingId = 4001,
				};
			}

			return new WorldSaveData { buildings = buildings, year = 1, season = 0, day = 1, hour = 8, minute = 0 };
		}

		private static long MillisecondsToLoad(int houses)
		{
			WorldSim world = new WorldSim();
			WorldSaveData data = WorldWith(houses);

			Stopwatch clock = Stopwatch.StartNew();
			int restored = world.Load(data);
			clock.Stop();

			Assert.That(restored, Is.EqualTo(houses), "되살린 집 수가 맞아야 이 시험이 뜻이 있다");
			return clock.ElapsedMilliseconds;
		}

		[Test]
		public void 자료가_네_배면_되살리기도_네_배쯤이다()
		{
			// 먼저 한 번 돌려 기계를 데운다(첫 판은 JIT 때문에 늘 느리다).
			MillisecondsToLoad(2000);

			long small = System.Math.Max(1, MillisecondsToLoad(20000));
			long big = MillisecondsToLoad(80000);

			Assert.That(big, Is.LessThanOrEqualTo(small * 8),
				$"집 2만 {small}ms → 8만 {big}ms. 곧으면 네 배쯤이고, 제곱이면 열여섯 배다");
		}
	}
}
