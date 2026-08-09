using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 파도 방향 — 「예고할 수 있다」가 말뿐이 아닌지 못 박는다 (TASK-WM-194).
	///
	/// ★ 예고의 조건은 딱 둘이다: ① 같은 판이면 항상 같은 방향이어야 하고(안 그러면 예고가 거짓말),
	///   ② 다음 파도 방향을 *지금* 물어볼 수 있어야 한다. 둘 다 화면 없이 잴 수 있다.
	/// </summary>
	public class TowerDefenseWaveOriginTests
	{
		[Test]
		public void 같은_판_같은_파도면_항상_같은_방향이다()
		{
			for (int wave = 0; wave < 20; wave++)
			{
				Assert.AreEqual(
					TowerDefenseWaveOrigin.AngleDegrees(wave, 7),
					TowerDefenseWaveOrigin.AngleDegrees(wave, 7),
					"방향이 흔들리면 예고가 거짓말이 된다.");
			}
		}

		[Test]
		public void 다음_파도_방향을_미리_물어볼_수_있다()
		{
			// 예고 = 「지금 파도를 치르는 중에 다음 파도 방향을 안다」. 미래 값이 계산 가능해야 성립한다.
			float current = TowerDefenseWaveOrigin.AngleDegrees(3, 42);
			float next = TowerDefenseWaveOrigin.AngleDegrees(4, 42);
			Assert.AreNotEqual(current, next, "연달아 같은 방향이면 예고가 무의미해진다.");
		}

		[Test]
		public void 방향은_한쪽으로_쏠리지_않는다()
		{
			// 스무 파도 동안 8방위가 최소 다섯 종류는 나와야 「매번 다른 쪽을 막는다」가 성립한다.
			HashSet<string> names = new();
			for (int wave = 0; wave < 20; wave++)
				names.Add(TowerDefenseWaveOrigin.DirectionName(TowerDefenseWaveOrigin.AngleDegrees(wave, 1)));

			Assert.GreaterOrEqual(names.Count, 5, $"방향이 {names.Count}종류뿐이면 결국 같은 자리만 막게 된다.");
		}

		[Test]
		public void 출현_지점은_판_안에_있다()
		{
			// 판 밖에 떨구면 그 마리는 시작부터 갈 수 없는 자리에 굳는다 = 파도가 영영 안 끝난다.
			List<Vector3> points = new();
			for (int wave = 0; wave < 12; wave++)
			{
				TowerDefenseWaveOrigin.Sample(wave, 3, 90f, 20f, 12f, 1f, 9, points);
				Assert.AreEqual(9, points.Count);
				foreach (Vector3 point in points)
				{
					Assert.LessOrEqual(Mathf.Abs(point.x), 19f + 1e-3f, "가로로 판을 벗어났다.");
					Assert.LessOrEqual(Mathf.Abs(point.z), 11f + 1e-3f, "세로로 판을 벗어났다.");
				}
			}
		}

		[Test]
		public void 출현_지점은_테두리에_붙는다()
		{
			// 안쪽에서 솟아나면 「밖에서 밀려온다」가 아니다 — 두 축 중 하나는 반드시 테두리에 닿아야 한다.
			List<Vector3> points = new();
			TowerDefenseWaveOrigin.Sample(5, 0, 120f, 20f, 12f, 1f, 7, points);

			foreach (Vector3 point in points)
			{
				bool onEdge = Mathf.Abs(Mathf.Abs(point.x) - 19f) < 1e-3f
					|| Mathf.Abs(Mathf.Abs(point.z) - 11f) < 1e-3f;
				Assert.IsTrue(onEdge, $"{point} 은 테두리가 아니라 판 안쪽이다.");
			}
		}

		[Test]
		public void 토막이_넓을수록_전선이_길어진다()
		{
			// 「한 곳만 막아선 못 버틴다」가 폭으로 조절돼야 한다 — 안 그러면 넓히는 손잡이가 죽은 값이다.
			List<Vector3> narrow = new();
			List<Vector3> wide = new();
			TowerDefenseWaveOrigin.Sample(2, 0, 20f, 20f, 20f, 0f, 5, narrow);
			TowerDefenseWaveOrigin.Sample(2, 0, 160f, 20f, 20f, 0f, 5, wide);

			float narrowSpan = Vector3.Distance(narrow[0], narrow[narrow.Count - 1]);
			float wideSpan = Vector3.Distance(wide[0], wide[wide.Count - 1]);
			Assert.Greater(wideSpan, narrowSpan, "폭을 키웠는데 전선이 안 길어지면 그 값은 안 먹히는 것이다.");
		}

		[Test]
		public void 성격_이름에_조사가_맞게_붙는다()
		{
			// ★ 예고는 한 문장으로 읽혀야 한다("떼거리가 북동에서 온다"). 조사가 틀리면 읽는 사람이
			//   문장이 아니라 *버그*를 먼저 본다. 받침 있는 이름과 없는 이름을 둘 다 건다.
			Assert.AreEqual("떼거리가 ", TowerDefenseWaveEvent.SubjectPhrase(TowerDefenseWaveEventKind.Swarm));
			Assert.AreEqual("정예가 ", TowerDefenseWaveEvent.SubjectPhrase(TowerDefenseWaveEventKind.Elite));
			Assert.AreEqual("돌진이 ", TowerDefenseWaveEvent.SubjectPhrase(TowerDefenseWaveEventKind.Rush));
			Assert.AreEqual("어스름이 ", TowerDefenseWaveEvent.SubjectPhrase(TowerDefenseWaveEventKind.Gloom));
			Assert.AreEqual(string.Empty, TowerDefenseWaveEvent.SubjectPhrase(TowerDefenseWaveEventKind.None),
				"성격 없는 파도에 조사만 남으면 「가 북동에서 온다」가 된다.");
		}

		[Test]
		public void 방위_이름이_각도와_맞는다()
		{
			Assert.AreEqual("북", TowerDefenseWaveOrigin.DirectionName(0f));
			Assert.AreEqual("동", TowerDefenseWaveOrigin.DirectionName(90f));
			Assert.AreEqual("남", TowerDefenseWaveOrigin.DirectionName(180f));
			Assert.AreEqual("서", TowerDefenseWaveOrigin.DirectionName(270f));
			Assert.AreEqual("북", TowerDefenseWaveOrigin.DirectionName(360f), "한 바퀴 돌면 제자리다.");
			Assert.AreEqual("북서", TowerDefenseWaveOrigin.DirectionName(-45f), "음수 각도도 말이 돼야 한다.");
		}
	}
}
