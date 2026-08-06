using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 신호장 — 「사슬을 타고 번진다 · 시간이 걸린다 · 끊기면 그 너머가 죽는다」 셋을 못 박는다 (TASK-WM-194).
	///
	/// ★ 이 셋이 컨트롤넷의 전부다. 하나라도 빠지면 그냥 「반경 큰 전기」로 되돌아간다 —
	///   그건 지금도 있던 것이라 바꾼 의미가 없다. 셋 다 화면 없이 잴 수 있다.
	/// </summary>
	public class TowerDefenseSignalFieldTests
	{
		private static TowerDefenseSignalField Make(params TowerDefenseSignalField.Node[] nodes)
		{
			TowerDefenseSignalField field = new();
			field.Configure(new List<TowerDefenseSignalField.Node>(nodes));
			return field;
		}

		private static void Settle(TowerDefenseSignalField field, float seconds)
		{
			// 0.1초씩 흘린다 — 한 번에 큰 걸음으로 흘리면 「한 겹씩 퍼진다」를 건너뛰어 시험이 무의미해진다.
			for (float elapsed = 0f; elapsed < seconds; elapsed += 0.1f)
				field.Tick(0.1f, 1f, 1f);
		}

		[Test]
		public void 코어는_스스로_찬다()
		{
			TowerDefenseSignalField field = Make(new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true));

			Assert.AreEqual(0f, field.ChargeAt(0), "세우자마자 가득 차 있으면 「점점 채워진다」가 아니다.");
			Settle(field, 2f);
			Assert.AreEqual(1f, field.ChargeAt(0), 1e-3f);
		}

		[Test]
		public void 즉시_안_켜지고_시간이_걸린다()
		{
			TowerDefenseSignalField field = Make(new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true));

			field.Tick(0.2f, 1f, 1f);
			float early = field.ChargeAt(0);
			Assert.Greater(early, 0f, "흐르긴 해야 한다.");
			Assert.Less(early, 1f, "0.2초 만에 가득 차면 즉시 켜지는 것과 같다.");
		}

		[Test]
		public void 덮는_반경이_차면서_자란다()
		{
			TowerDefenseSignalField field = Make(new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true));

			Settle(field, 0.5f);
			float half = field.LiveRadiusAt(0);
			Settle(field, 2f);
			float full = field.LiveRadiusAt(0);

			Assert.Greater(full, half, "원이 안 자라면 「번져 나간다」가 안 보인다.");
			Assert.AreEqual(10f, full, 1e-3f);
		}

		[Test]
		public void 중계탑은_사슬로_이어져야_산다()
		{
			// 코어 — (닿음) 중계1 — (닿음) 중계2. 중계2는 코어에서 직접은 절대 안 닿는다.
			TowerDefenseSignalField field = Make(
				new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true),
				new TowerDefenseSignalField.Node(2, new Vector3(8f, 0f, 0f), 10f, false),
				new TowerDefenseSignalField.Node(3, new Vector3(16f, 0f, 0f), 10f, false));

			Settle(field, 6f);

			Assert.AreEqual(1f, field.ChargeAt(2), 1e-2f, "사슬 끝까지 신호가 가야 한다.");
			Assert.IsTrue(field.IsCovered(new Vector3(20f, 0f, 0f)), "사슬이 끌고 나간 만큼 판이 덮여야 한다.");
		}

		[Test]
		public void 사슬은_앞에서부터_순서대로_찬다()
		{
			TowerDefenseSignalField field = Make(
				new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true),
				new TowerDefenseSignalField.Node(2, new Vector3(8f, 0f, 0f), 10f, false),
				new TowerDefenseSignalField.Node(3, new Vector3(16f, 0f, 0f), 10f, false));

			Settle(field, 1.6f);

			Assert.Greater(field.ChargeAt(0), field.ChargeAt(1), "코어가 먼저 차야 한다.");
			Assert.Greater(field.ChargeAt(1), field.ChargeAt(2), "가까운 탑이 먼저 차야 한다 — 동시에 켜지면 사슬이 안 보인다.");
		}

		[Test]
		public void 중간_탑이_사라지면_그_너머가_죽는다()
		{
			TowerDefenseSignalField field = Make(
				new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true),
				new TowerDefenseSignalField.Node(2, new Vector3(8f, 0f, 0f), 10f, false),
				new TowerDefenseSignalField.Node(3, new Vector3(16f, 0f, 0f), 10f, false));
			Settle(field, 6f);
			Assert.IsTrue(field.IsCovered(new Vector3(20f, 0f, 0f)));

			// 가운데 탑이 부서졌다 — 목록에서 빠진다. 남은 둘은 자리가 같으니 충전값을 이어받는다.
			field.Configure(new List<TowerDefenseSignalField.Node>
			{
				new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true),
				new TowerDefenseSignalField.Node(3, new Vector3(16f, 0f, 0f), 10f, false),
			});

			Settle(field, 3f);
			Assert.AreEqual(0f, field.ChargeAt(1), 1e-3f, "앞이 끊겼는데도 살아 있으면 사슬이 아니다.");
			Assert.IsFalse(field.IsCovered(new Vector3(20f, 0f, 0f)), "그 너머 땅이 죽어야 한다.");
		}

		[Test]
		public void 끊기면_뚝_꺼지지_않고_서서히_빠진다()
		{
			TowerDefenseSignalField field = Make(
				new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true),
				new TowerDefenseSignalField.Node(2, new Vector3(8f, 0f, 0f), 10f, false));
			Settle(field, 6f);

			field.Configure(new List<TowerDefenseSignalField.Node>
			{
				new TowerDefenseSignalField.Node(2, new Vector3(8f, 0f, 0f), 10f, false),
			});

			field.Tick(0.2f, 1f, 1f);
			float fading = field.ChargeAt(0);
			Assert.Greater(fading, 0f, "한 프레임 만에 0 이면 「물 빠지듯」이 아니라 스위치를 끈 것이다.");
			Assert.Less(fading, 1f, "빠지긴 해야 한다.");
		}

		[Test]
		public void 살아있는_코어가_미세하게_흔들려도_충전값을_안_잃는다()
		{
			// ★ 회귀: 충전값을 *좌표*로 짝지었더니 코어·인형이 조금만 움직여도 매 프레임 「처음 보는
			//   노드」가 되어 0 으로 되돌아갔다. 그 상태의 증상은 **아무것도 안 켜지는 것**이다
			//   (사용자 실증: "코어 건물에 전기가 안 나와? 파동 링도 없고"). 이름표로 짝지어야 한다.
			TowerDefenseSignalField field = Make(new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true));
			Settle(field, 6f);

			for (int step = 0; step < 20; step++)
			{
				// 살아 있는 인형처럼 아주 조금씩 흔들린다.
				Vector3 jitter = new Vector3(step * 0.003f, 0f, step * 0.002f);
				field.Configure(new List<TowerDefenseSignalField.Node>
				{
					new TowerDefenseSignalField.Node(1, jitter, 10f, true),
				});
				field.Tick(0.1f, 1f, 1f);
			}

			Assert.AreEqual(1f, field.ChargeAt(0), 1e-3f, "흔들렸다고 충전값을 잃으면 신호가 영영 안 선다.");
			Assert.Greater(field.LiveRadiusAt(0), 0f, "코어가 아무것도 못 덮으면 판 전체가 정지한다.");
		}

		[Test]
		public void 이름표가_같은_노드는_충전값을_이어받는다()
		{
			// 목록이 다시 만들어질 때마다 0 부터 시작하면 판 전체가 매 프레임 깜빡인다.
			TowerDefenseSignalField field = Make(new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true));
			Settle(field, 6f);

			field.Configure(new List<TowerDefenseSignalField.Node>
			{
				new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true),
				new TowerDefenseSignalField.Node(2, new Vector3(8f, 0f, 0f), 10f, false),
			});

			Assert.AreEqual(1f, field.ChargeAt(0), 1e-3f, "그대로 서 있던 코어가 다시 0 부터 차면 판이 깜빡인다.");
			Assert.AreEqual(0f, field.ChargeAt(1), 1e-3f, "새로 세운 탑은 0 에서 시작해야 한다.");
		}

		[Test]
		public void 덮이지_않은_자리는_덮이지_않았다고_말한다()
		{
			TowerDefenseSignalField field = Make(new TowerDefenseSignalField.Node(1, Vector3.zero, 10f, true));
			Settle(field, 6f);

			Assert.IsTrue(field.IsCovered(new Vector3(9f, 0f, 0f)));
			Assert.IsFalse(field.IsCovered(new Vector3(11f, 0f, 0f)), "반경 밖이 덮였다고 하면 판정이 무의미하다.");
		}
	}
}
