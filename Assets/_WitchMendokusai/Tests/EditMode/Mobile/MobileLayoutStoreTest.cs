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
		[Test]
		public void 조밀한_폰일수록_최소_크기가_커진다()
		{
			// 같은 화면 좌표라도 밀도가 높으면 실제로는 더 작다 — 그래서 바닥이 더 높아야 한다.
			float low = MobileLayoutStore.MinimumTouchSize(160f, 1080f, 800f);
			float high = MobileLayoutStore.MinimumTouchSize(400f, 1080f, 800f);
			Assert.Greater(high, low);
		}

		[Test]
		public void 실제로_구박_구밀리를_넘는다()
		{
			// 400dpi 폰 기준 — 기준(48dp ≈ 9.5mm)에 닿는지 실제 길이로 센다.
			const float dpi = 400f;
			const float screenHeight = 1080f;
			float logical = MobileLayoutStore.MinimumTouchSize(dpi, screenHeight, 800f);
			float physicalPixels = logical * (screenHeight / 800f);
			float millimetres = physicalPixels / dpi * 25.4f;
			Assert.GreaterOrEqual(millimetres, 7.5f);
		}

		[Test]
		public void 화면을_모르면_바닥을_안_깐다()
		{
			// 모르는 채로 키우면 버튼이 화면을 덮는다 — 모를 땐 아무것도 강요하지 않는다.
			Assert.AreEqual(0f, MobileLayoutStore.MinimumTouchSize(400f, 0f, 800f));
		}

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
