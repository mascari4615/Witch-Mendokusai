using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 옮긴 조작 장치가 화면 밖으로 못 나가는지 (TASK-WM-200).
	///
	/// ★ 이게 이 기능의 유일한 안전장치다 — 버튼이 화면 밖으로 나가면 다시 잡을 수가 없어서
	///   되돌리기조차 누르지 못한다. 그래서 계산만 따로 떼어 시험한다.
	/// </summary>
	public class MobileLayoutStoreTest
	{
		private static readonly Vector2 Screen1080 = new Vector2(2340f, 1080f);
		private const float MinVisible = 44f;

		[Test]
		public void 안_넘치는_이동은_그대로_둔다()
		{
			Rect element = new Rect(100f, 800f, 220f, 220f);
			Vector2 result = MobileLayoutStore.ClampToScreen(new Vector2(50f, -30f), element, Screen1080, MinVisible);
			Assert.AreEqual(new Vector2(50f, -30f), result);
		}

		[Test]
		public void 왼쪽_밖으로_끌면_최소한만_남기고_멈춘다()
		{
			Rect element = new Rect(100f, 800f, 220f, 220f); // xMax = 320
			Vector2 result = MobileLayoutStore.ClampToScreen(new Vector2(-9999f, 0f), element, Screen1080, MinVisible);
			Assert.AreEqual(-320f + MinVisible, result.x, 0.001f);
		}

		[Test]
		public void 오른쪽_밖으로_끌면_최소한만_남기고_멈춘다()
		{
			Rect element = new Rect(100f, 800f, 220f, 220f); // xMin = 100
			Vector2 result = MobileLayoutStore.ClampToScreen(new Vector2(9999f, 0f), element, Screen1080, MinVisible);
			Assert.AreEqual(Screen1080.x - 100f - MinVisible, result.x, 0.001f);
		}

		[Test]
		public void 아래로_끌어도_화면_안에_남는다()
		{
			Rect element = new Rect(100f, 800f, 220f, 220f); // yMin = 800
			Vector2 result = MobileLayoutStore.ClampToScreen(new Vector2(0f, 9999f), element, Screen1080, MinVisible);
			Assert.AreEqual(Screen1080.y - 800f - MinVisible, result.y, 0.001f);
		}

		[Test]
		public void 화면보다_큰_것도_범위_안에서는_움직인다()
		{
			// ★ 이 시험은 원래 「안 움직인다」를 기대했는데 실제로는 움직였다 — 기대 쪽이 틀렸다.
			//   화면보다 커도 화면 안에 남는 범위가 있으면 옮길 수 있어야 맞다(못 옮기면 그게 버그).
			Rect huge = new Rect(0f, 0f, 5000f, 5000f);
			Vector2 result = MobileLayoutStore.ClampToScreen(new Vector2(300f, 300f), huge, Screen1080, MinVisible);
			Assert.AreEqual(new Vector2(300f, 300f), result);
		}

		[Test]
		public void 화면_크기를_모를_땐_원래_자리에_둔다()
		{
			// 화면이 0 이면 「어디까지가 화면 안인가」가 정의되지 않는다 — 아무렇게나 자르면
			// 조작 장치가 엉뚱한 자리로 튀어 다시 못 잡는다. 그때는 안 움직이는 편이 안전하다.
			Rect element = new Rect(100f, 100f, 220f, 220f);
			Vector2 result = MobileLayoutStore.ClampToScreen(new Vector2(300f, 300f), element, Vector2.zero, MinVisible);
			Assert.AreEqual(Vector2.zero, result);
		}
	}
}
