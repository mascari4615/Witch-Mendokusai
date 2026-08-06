using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class FloatingTextItem : VisualElement
	{
		public const string USS_CLASS = "wm-floating-text";
		public const string USS_ACTIVE = "wm-floating-text--active";
		public const string USS_NORMAL = "wm-floating-text--normal";
		public const string USS_CRITICAL = "wm-floating-text--critical";
		public const string USS_HEAL = "wm-floating-text--heal";
		public const string USS_EXP = "wm-floating-text--exp";
		public const string USS_WARNING = "wm-floating-text--warning";

		private readonly Label label;

		public FloatingTextItem()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;

			label = new Label();
			label.pickingMode = PickingMode.Ignore;
			Add(label);
		}

		public void Show(TextType textType, string message)
		{
			RemoveFromClassList(USS_NORMAL);
			RemoveFromClassList(USS_CRITICAL);
			RemoveFromClassList(USS_HEAL);
			RemoveFromClassList(USS_EXP);
			RemoveFromClassList(USS_WARNING);

			AddToClassList(GetVariantClass(textType));

			label.text = textType == TextType.Critical ? $"크리티컬!\n{message}" : message;

			RemoveFromClassList(USS_ACTIVE);
			schedule.Execute(() => AddToClassList(USS_ACTIVE)).StartingIn(0);
		}

		public void Deactivate()
		{
			RemoveFromClassList(USS_ACTIVE);
		}

		/// <summary>
		/// 화면 좌표를 *판 좌표*로 옮겨 앉힌다.
		///
		/// ★ 예전엔 화면 픽셀을 그대로 넣었다. UI 배율이 1 일 때는 화면 픽셀 = 판 픽셀이라 맞아떨어졌지만,
		///   배율이 1 이 아닌 순간(기기·해상도 대응으로 화면비례 배율이 켜졌다) 그만큼 어긋난다 —
		///   1920 화면에 판이 1422 면 숫자가 1.35 배만큼 밀린 자리에 뜬다(사용자 실증: "자원 텍스트는 여전한데").
		///   세로는 카메라가 아래를 0 으로 주고 top 은 위가 0 이라 뒤집는 것까지 같이 해야 한다.
		/// </summary>
		public void SetScreenPosition(Vector3 screenPos)
		{
			Rect box = panel?.visualTree != null ? panel.visualTree.worldBound : Rect.zero;
			float panelWidth = box.width > 0f ? box.width : Screen.width;
			float panelHeight = box.height > 0f ? box.height : Screen.height;
			float screenWidth = Mathf.Max(1f, Screen.width);
			float screenHeight = Mathf.Max(1f, Screen.height);

			style.left = screenPos.x * panelWidth / screenWidth;
			style.top = (screenHeight - screenPos.y) * panelHeight / screenHeight;
		}

		private static string GetVariantClass(TextType textType)
		{
			return textType switch
			{
				TextType.Critical => USS_CRITICAL,
				TextType.Heal => USS_HEAL,
				TextType.Exp => USS_EXP,
				TextType.Warning => USS_WARNING,
				_ => USS_NORMAL,
			};
		}
	}
}
