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
	/// <remarks>
	/// [빨강-확인] 되살릴 때 칸 장부를 <b>집마다 통째로 베끼게</b> 되돌리니 빨강 —
	/// 「집 40000채 3365ms → 160000채 80434ms」(스물네 배 = 제곱). 되돌리니 4만 &lt;20ms (2026-08-14).
	/// </remarks>
	public sealed class WorldLoadGrowsStraightTests
	{
		/// <summary>이만큼은 걸려야 비율에 뜻이 있다 — 그 아래는 잡음이 곧 결론이 된다.</summary>
		private const int MEANINGFUL_MS = 20;

		/// <summary>바닥을 여기까지만 키운다 — 네 배를 곱해도 기억이 감당할 만큼.</summary>
		private const int MOST_HOUSES = 160000;

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

		/// <summary>
		/// 같은 크기를 <b>세 번</b> 재고 가장 빠른 판을 쓴다 — 시간 재기의 잡음은 늘 <b>느린 쪽</b>으로만
		/// 튄다(GC·다른 프로세스). 가장 빠른 판이 그 기계의 실력에 가장 가깝다.
		/// </summary>
		private static long BestOfThree(int houses)
		{
			long best = long.MaxValue;
			for (int turn = 0; turn < 3; turn++)
				best = System.Math.Min(best, MillisecondsToLoad(houses));

			return best;
		}

		[Test]
		public void 자료가_네_배면_되살리기도_네_배쯤이다()
		{
			// 먼저 한 번 돌려 기계를 데운다(첫 판은 JIT 때문에 늘 느리다).
			MillisecondsToLoad(2000);

			// ⚠ <b>바닥이 너무 작으면 비율은 잡음이다</b> (2026-08-14 CI 빨강 실측: 9ms → 83ms —
			//   9ms 짜리 바닥에서는 GC 한 번이 곧 「제곱」으로 보인다).
			//   그렇다고 큰 수를 못 박으면 느린 기계에서 오래 걸린다 — <b>기계에 맞춰 키운다</b>:
			//   바닥이 뜻을 가질 때까지 두 배씩 올린다(관문 규율 ④: 환경 몫을 빼고 자른다).
			int houses = 40000;
			long small = BestOfThree(houses);
			while (small < MEANINGFUL_MS && houses < MOST_HOUSES)
			{
				houses *= 2;
				small = BestOfThree(houses);
			}

			if (small < MEANINGFUL_MS)
				Assert.Ignore($"집 {houses}채를 {small}ms 에 되살리는 기계다 — 비율을 못 믿어 이번 판은 못 쟀다");

			long big = BestOfThree(houses * 4);

			Assert.That(big, Is.LessThanOrEqualTo(small * 8),
				$"집 {houses}채 {small}ms → {houses * 4}채 {big}ms. 곧으면 네 배쯤이고, 제곱이면 열여섯 배다");
		}
	}
}
