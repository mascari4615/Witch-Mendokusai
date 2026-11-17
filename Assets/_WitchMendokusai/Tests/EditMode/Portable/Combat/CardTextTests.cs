using System;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class CardTextTests
	{
		[Test]
		public void Format_FillsSlotsInOrder()
		{
			Assert.AreEqual("최대 체력 +5, 체력 7 회복", CardText.Format("최대 체력 +{0}, 체력 {1} 회복", new[] { 5, 7 }));
		}

		[Test]
		public void Format_SameSlotTwice_UsesSameValue()
		{
			Assert.AreEqual("9 그리고 9", CardText.Format("{0} 그리고 {0}", new[] { 9 }));
		}

		[Test]
		public void Format_NoSlots_ReturnsTemplate()
		{
			Assert.AreEqual("모든 무기의 공격 속도를 강화합니다.", CardText.Format("모든 무기의 공격 속도를 강화합니다.", new[] { 1 }));
			Assert.AreEqual("수치 없는 글", CardText.Format("수치 없는 글", null));
		}

		[Test]
		public void Format_MissingValue_Throws()
		{
			Assert.Throws<ArgumentException>(() => CardText.Format("수 +{0}, 데미지 +{1}", new[] { 1 }));
			Assert.Throws<ArgumentException>(() => CardText.Format("+{0}", null));
			Assert.Throws<ArgumentException>(() => CardText.Format("+{0}", new int[0]));
		}

		[Test]
		public void Format_OtherBraces_AreKept()
		{
			Assert.AreEqual("{이름} 3 {} {a1} {1", CardText.Format("{이름} {0} {} {a1} {1", new[] { 3, 4 }));
		}

		[Test]
		public void Format_NullOrEmpty_ReturnsEmpty()
		{
			Assert.AreEqual(string.Empty, CardText.Format(null, new[] { 1 }));
			Assert.AreEqual(string.Empty, CardText.Format(string.Empty, new[] { 1 }));
		}

		[Test]
		public void Format_NegativeValue_KeepsSign()
		{
			Assert.AreEqual("이동속도 -2", CardText.Format("이동속도 {0}", new[] { -2 }));
		}

		[Test]
		public void Format_TwoDigitSlot_ReadsWholeNumber()
		{
			int[] values = new int[11];
			values[10] = 42;
			Assert.AreEqual("42", CardText.Format("{10}", values));
		}
	}
}
