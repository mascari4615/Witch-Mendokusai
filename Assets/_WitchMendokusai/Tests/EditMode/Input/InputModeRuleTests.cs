using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 건설과 평소의 키 모드 (사용자 2026-11-27 "건설모드랑 일반 상태랑 키 모드를 다르게").
	/// 큰 포화 (R) 가 건설 돌리기 (R) 와 같이 나가던 것의 회귀 막이
	/// </summary>
	public class InputModeRuleTests
	{
		[Test]
		public void 평소에는_전투_입력이_나간다()
		{
			Assert.IsTrue(InputModeRule.Allows(InputMode.Normal, InputEventType.Barrage, InputMapType.Player, InputEventResponseType.Performed));
		}

		[Test]
		public void 건설_중에는_큰_포화가_안_나가고_돌리기는_나간다()
		{
			Assert.IsFalse(InputModeRule.Allows(InputMode.Build, InputEventType.Barrage, InputMapType.Player, InputEventResponseType.Performed));
			Assert.IsTrue(InputModeRule.Allows(InputMode.Build, InputEventType.VillageTurn, InputMapType.UI, InputEventResponseType.Performed));
		}

		[TestCase(InputEventType.Space)]
		[TestCase(InputEventType.Jump)]
		[TestCase(InputEventType.Sprint)]
		[TestCase(InputEventType.Crouch)]
		[TestCase(InputEventType.ChangeMode)]
		[TestCase(InputEventType.BuildModeToggle)]
		[TestCase(InputEventType.Gather)]
		[TestCase(InputEventType.Scroll)]
		public void 건설_중에는_전투와_이동_입력이_안_나간다(InputEventType inputEventType)
		{
			Assert.IsFalse(InputModeRule.Allows(InputMode.Build, inputEventType, InputMapType.Player, InputEventResponseType.Started));
			Assert.IsFalse(InputModeRule.Allows(InputMode.Build, inputEventType, InputMapType.Player, InputEventResponseType.Performed));
		}

		[TestCase(InputEventType.Click0)]
		[TestCase(InputEventType.Click1)]
		[TestCase(InputEventType.HotbarSlot1)]
		[TestCase(InputEventType.HotbarSlot9)]
		public void 건설이_쓰는_클릭과_칸_키는_나간다(InputEventType inputEventType)
		{
			Assert.IsTrue(InputModeRule.Allows(InputMode.Build, inputEventType, InputMapType.Player, InputEventResponseType.Started));
		}

		[TestCase(InputEventType.Cancel)]
		[TestCase(InputEventType.CameraViewCycle)]
		[TestCase(InputEventType.Inventory)]
		public void 건설_중에도_UI_입력은_나간다(InputEventType inputEventType)
		{
			Assert.IsTrue(InputModeRule.Allows(InputMode.Build, inputEventType, InputMapType.UI, InputEventResponseType.Performed));
		}

		[Test]
		public void 뗌은_건설_중에도_나가_달리기가_켜진_채_남지_않는다()
		{
			Assert.IsTrue(InputModeRule.Allows(InputMode.Build, InputEventType.Sprint, InputMapType.Player, InputEventResponseType.Canceled));
		}
	}
}
