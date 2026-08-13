using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 말 예산이 <b>성한 사람</b>을 자르지 않나 (TASK-WM-348).
	///
	/// ★ 왜 이 시험이 생겼나: 예산은 「움직임 10/초 + 여유」로 잡혀 있었는데 창은 실제로 20/초를
	///   보내고 있었다. 실측(나쁜 회선·12초) 초당 23.9 마디 — 예산 30 의 80%. 줍기 한 번이
	///   겹치면 성한 사람의 말이 <b>조용히</b> 버려진다(끊지 않으니 아무 데도 안 적힌다).
	///   그래서 숫자를 손으로 적지 말고 <b>제품 상수에서 유도</b>하고, 그 여유를 여기서 지킨다.
	/// </summary>
	public sealed class MessageBudgetRoomTests
	{
		/// <summary>걸으면서 두드리는 사람 = 걸음 20 + 숨소리 4 + 손짓 몇.</summary>
		private const float WALKING_AND_DOING = MessageBudget.STEPS_PER_SECOND + MessageBudget.BEATS_PER_SECOND;

		[Test]
		public void 걷기만_해도_예산의_절반을_안_넘는다()
		{
			Assert.That(WALKING_AND_DOING, Is.LessThanOrEqualTo(MessageBudget.REFILL_PER_SECOND * 0.6f),
				$"걷는 것만으로 초당 {WALKING_AND_DOING} 마디다 — 예산 {MessageBudget.REFILL_PER_SECOND} 에서 손으로 할 몫이 안 남는다");
		}

		[Test]
		public void 손으로_하는_일에_초당_열_마디는_남는다()
		{
			float left = MessageBudget.REFILL_PER_SECOND - WALKING_AND_DOING;

			Assert.That(left, Is.GreaterThanOrEqualTo(10f),
				$"남는 몫 초당 {left} 마디 — 줍기·때리기·말하기가 겹치면 성한 사람의 말이 조용히 버려진다");
		}

		[Test]
		public void 걸으면서_손도_쓰는_한_판을_그대로_받아_준다()
		{
			MessageBudget budget = new MessageBudget();
			int spoken = 0;

			// 1초를 100 조각으로 나눠, 사람이 걸으면서(20) 숨쉬고(4) 손도 쓰는(6) 판.
			for (int slice = 0; slice < 100; slice++)
			{
				budget.Refill(0.01f);
				int saying = (slice % 100 < 30) ? 1 : 0;   // 30 마디를 1초에 걸쳐
				if (saying == 1 && budget.TrySpend())
					spoken++;
			}

			Assert.That(spoken, Is.EqualTo(30), "초당 30 마디는 한 마디도 안 버려져야 한다");
		}
	}
}
