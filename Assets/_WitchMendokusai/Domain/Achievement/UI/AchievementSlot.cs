using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 칸 하나. 분류의 모양 (도장, 자수 패치, 물약병 ...) 을 따름.
	/// 틀 그림이 있으면 그 그림, 없으면 wm-ach-slot--열쇠 와 --tierN USS 임시 모양.
	/// 숨김 업적은 달성 전 실루엣과 ? 만
	/// </summary>
	public class AchievementSlot : VisualElement
	{
		public const string USS_CLASS = "wm-ach-slot";
		public const string USS_FRAME = "wm-ach-slot__frame";
		public const string USS_ICON = "wm-ach-slot__icon";
		public const string USS_MARK = "wm-ach-slot__mark";
		public const string USS_CAPTION = "wm-ach-slot__caption";
		public const string USS_SELECTED = "wm-ach-slot--selected";
		public const string USS_EMPTY = "wm-ach-slot--empty";
		public const string USS_HIDDEN = "wm-ach-slot--hidden";
		public const string USS_MOTIF_PREFIX = "wm-ach-slot--";
		public const string USS_TIER_PREFIX = "wm-ach-slot--tier";
		public const string HIDDEN_NAME = "???";
		public const string HIDDEN_MARK = "?";

		private readonly VisualElement frame;
		private readonly VisualElement icon;
		private readonly Label mark;
		private readonly Label caption;
		private string motifClass;
		private string tierClass;

		public AchievementSO Achievement { get; private set; }

		public AchievementSlot(bool withCaption)
		{
			AddToClassList(USS_CLASS);
			focusable = true;
			pickingMode = PickingMode.Position;

			frame = new VisualElement();
			frame.AddToClassList(USS_FRAME);
			frame.pickingMode = PickingMode.Ignore;
			Add(frame);

			icon = new VisualElement();
			icon.AddToClassList(USS_ICON);
			icon.pickingMode = PickingMode.Ignore;
			frame.Add(icon);

			mark = new Label();
			mark.AddToClassList(USS_MARK);
			mark.pickingMode = PickingMode.Ignore;
			frame.Add(mark);

			caption = new Label();
			caption.AddToClassList(USS_CAPTION);
			caption.pickingMode = PickingMode.Ignore;
			caption.style.display = withCaption ? DisplayStyle.Flex : DisplayStyle.None;
			Add(caption);
		}

		public void Bind(AchievementSO achievement, int tier)
		{
			Achievement = achievement;
			AchievementCategorySO category = achievement.Category;
			bool unlocked = tier > 0;
			bool concealed = achievement.Hidden && unlocked == false;

			SwapClass(ref motifClass, USS_MOTIF_PREFIX + category.MotifKey);
			SwapClass(ref tierClass, USS_TIER_PREFIX + tier);
			EnableInClassList(USS_EMPTY, unlocked == false && concealed == false);
			EnableInClassList(USS_HIDDEN, concealed);

			Sprite frameSprite = unlocked ? category.FrameOf(tier) : (concealed ? category.HiddenSlot : category.EmptySlot);
			frame.style.backgroundImage = frameSprite != null ? new StyleBackground(frameSprite) : new StyleBackground(StyleKeyword.Null);

			icon.style.backgroundImage = concealed || achievement.Sprite == null ? new StyleBackground(StyleKeyword.Null) : new StyleBackground(achievement.Sprite);
			mark.text = concealed ? HIDDEN_MARK : string.Empty;
			caption.text = concealed ? HIDDEN_NAME : achievement.Name;
		}

		public void SetSelected(bool selected) => EnableInClassList(USS_SELECTED, selected);

		private void SwapClass(ref string current, string next)
		{
			if (current == next)
			{
				return;
			}
			if (current != null)
			{
				RemoveFromClassList(current);
			}
			current = next;
			AddToClassList(current);
		}
	}
}
