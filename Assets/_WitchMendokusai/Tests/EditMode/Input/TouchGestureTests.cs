using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 손가락 몸짓 판정 (TASK-WM-200 모바일 지원).
	///
	/// ★ 왜 이걸 시험으로 못 박나: 「톡 친 것인가 끈 것인가」를 잘못 재면 모바일에서 *지도를 끌었을 뿐인데
	///   건물이 서고 자원이 빠진다*. 손가락 없는 컴퓨터에선 이 판정을 눈으로 확인할 방법이 없어서,
	///   좌표만 먹는 순수 계산으로 떼어내고 여기서 묻는다.
	/// </summary>
	public class TouchGestureTests
	{
		private static readonly float FRAME = 1f / 60f;

		private static List<Vector2> One(float x, float y) => new List<Vector2> { new Vector2(x, y) };
		private static List<Vector2> Two(Vector2 a, Vector2 b) => new List<Vector2> { a, b };
		private static readonly List<Vector2> NONE = new List<Vector2>();

		[Test]
		public void 짧게_눌렀다_떼면_톡이다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(100f, 200f), FRAME);
			gesture.Update(One(101f, 200f), FRAME);
			gesture.Update(NONE, FRAME);

			Assert.IsTrue(gesture.TappedThisFrame, "짧게 눌렀다 뗐는데 톡으로 안 쳤다.");
			Assert.AreEqual(new Vector2(101f, 200f), gesture.TapPosition);
		}

		[Test]
		public void 끌었다_떼면_톡이_아니다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(100f, 200f), FRAME);
			for (int i = 1; i <= 10; i++)
				gesture.Update(One(100f + i * 20f, 200f), FRAME);
			gesture.Update(NONE, FRAME);

			Assert.IsFalse(gesture.TappedThisFrame, "지도를 끌었을 뿐인데 톡으로 쳤다 — 여기서 건물이 선다.");
		}

		[Test]
		public void 오래_누르고_있다_떼면_톡이_아니다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(100f, 200f), FRAME);
			for (int i = 0; i < 60; i++)
				gesture.Update(One(100f, 200f), FRAME);
			gesture.Update(NONE, FRAME);

			Assert.IsFalse(gesture.TappedThisFrame, "1초를 누르고 있었는데 톡으로 쳤다.");
		}

		[Test]
		public void 문턱을_넘으면_끌기로_굳고_멈춰도_안_풀린다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(100f, 200f), FRAME);
			gesture.Update(One(100f, 260f), FRAME);
			Assert.IsTrue(gesture.IsDragging, "충분히 움직였는데 끌기로 안 쳤다.");

			// 손가락이 잠깐 멈춘다 — 끌기가 풀리면 화면이 덜컹거린다.
			gesture.Update(One(100f, 260f), FRAME);
			Assert.IsTrue(gesture.IsDragging, "손가락이 멈췄다고 끌기가 풀렸다.");
		}

		[Test]
		public void 손가락_수가_바뀐_프레임은_움직임이_0이다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(100f, 200f), FRAME);
			gesture.Update(One(110f, 200f), FRAME);
			// 둘째 손가락이 닿는다 — 첫 손가락은 그대로인데 목록이 바뀐다.
			gesture.Update(Two(new Vector2(110f, 200f), new Vector2(300f, 200f)), FRAME);

			Assert.AreEqual(Vector2.zero, gesture.PrimaryDelta, "손가락을 하나 더 얹었을 뿐인데 화면이 튀었다.");
		}

		[Test]
		public void 두_손가락을_벌리면_양수_오므리면_음수다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(Two(new Vector2(100f, 200f), new Vector2(200f, 200f)), FRAME);
			gesture.Update(Two(new Vector2(90f, 200f), new Vector2(210f, 200f)), FRAME);
			Assert.Greater(gesture.PinchDelta, 0f, "벌렸는데 양수가 아니다.");

			gesture.Update(Two(new Vector2(110f, 200f), new Vector2(190f, 200f)), FRAME);
			Assert.Less(gesture.PinchDelta, 0f, "오므렸는데 음수가 아니다.");
		}

		[Test]
		public void 두_손가락을_함께_옮기면_가운데점이_따라간다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(Two(new Vector2(100f, 200f), new Vector2(200f, 200f)), FRAME);
			gesture.Update(Two(new Vector2(130f, 210f), new Vector2(230f, 210f)), FRAME);

			Assert.AreEqual(30f, gesture.TwoFingerPanDelta.x, 0.001f);
			Assert.AreEqual(10f, gesture.TwoFingerPanDelta.y, 0.001f);
			Assert.AreEqual(0f, gesture.PinchDelta, 0.001f, "나란히 옮겼을 뿐인데 확대가 일어났다.");
		}

		[Test]
		public void 두_손가락을_돌리면_각도가_나온다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(Two(new Vector2(0f, 0f), new Vector2(100f, 0f)), FRAME);
			gesture.Update(Two(new Vector2(0f, 0f), new Vector2(0f, 100f)), FRAME);

			Assert.AreEqual(90f, gesture.TwistDelta, 0.001f);
		}

		[Test]
		public void 아무도_안_닿으면_마지막_자리를_지킨다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(150f, 250f), FRAME);
			gesture.Update(NONE, FRAME);

			Assert.AreEqual(new Vector2(150f, 250f), gesture.PrimaryPosition,
				"손을 떼자 좌표가 0 으로 튀었다 — 마지막으로 가리킨 자리가 커서 자리다.");
		}

		[Test]
		public void 톡은_뗀_프레임에만_참이다()
		{
			TouchGesture gesture = new TouchGesture();

			gesture.Update(One(100f, 200f), FRAME);
			gesture.Update(NONE, FRAME);
			Assert.IsTrue(gesture.TappedThisFrame);

			gesture.Update(NONE, FRAME);
			Assert.IsFalse(gesture.TappedThisFrame, "톡이 다음 프레임까지 남아 두 번 처리된다.");
		}
	}
}
