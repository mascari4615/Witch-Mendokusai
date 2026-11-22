using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>업적 화면들이 같이 쓰는 작은 요소 짓기. 모두 누르기 대상이 아님 (pickingMode Ignore). 모양은 USS 클래스</summary>
	public static class AchievementElements
	{
		/// <summary>빈 칸 하나. 클래스는 빈칸으로 여럿</summary>
		public static VisualElement Part(VisualElement parent, string ussClasses)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			AddClasses(element, ussClasses);
			parent.Add(element);
			return element;
		}

		public static Label Text(VisualElement parent, string text, string ussClasses)
		{
			Label label = new(text) { pickingMode = PickingMode.Ignore };
			AddClasses(label, ussClasses);
			parent.Add(label);
			return label;
		}

		/// <summary>진행 막대. 돌려주는 것은 채움 칸 (style.width 를 % 로)</summary>
		public static VisualElement Bar(VisualElement parent, string ussClasses)
		{
			VisualElement bar = Part(parent, "wm-ach-bar " + ussClasses);
			return Part(bar, "wm-ach-bar__fill");
		}

		public static void AddClasses(VisualElement element, string ussClasses)
		{
			foreach (string ussClass in ussClasses.Split(' '))
			{
				element.AddToClassList(ussClass);
			}
		}
	}
}
