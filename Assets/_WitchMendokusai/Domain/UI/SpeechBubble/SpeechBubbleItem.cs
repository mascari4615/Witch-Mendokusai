using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class SpeechBubbleItem : VisualElement
	{
		public const string USS_CLASS = "wm-speech-bubble";
		public const string USS_ACTIVE = "wm-speech-bubble--active";
		public const string USS_TEXT = "wm-speech-bubble__text";
		public const string USS_EMOJI = "wm-speech-bubble__emoji";

		private const float WORLD_OFFSET_Y = 1.5f;
		private const float SCREEN_PADDING = 30f;

		private readonly Label textLabel;
		private readonly VisualElement emojiIcon;

		public SpeechBubbleItem()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;

			textLabel = new Label();
			textLabel.AddToClassList(USS_TEXT);
			textLabel.pickingMode = PickingMode.Ignore;
			Add(textLabel);

			emojiIcon = new VisualElement();
			emojiIcon.AddToClassList(USS_EMOJI);
			emojiIcon.pickingMode = PickingMode.Ignore;
			Add(emojiIcon);
		}

		public void ShowText(string text)
		{
			textLabel.text = text;
			textLabel.style.display = DisplayStyle.Flex;
			emojiIcon.style.display = DisplayStyle.None;
			AddToClassList(USS_ACTIVE);
		}

		public void ShowEmoji(Sprite emoji)
		{
			textLabel.style.display = DisplayStyle.None;
			emojiIcon.style.display = DisplayStyle.Flex;
			emojiIcon.style.backgroundImage = emoji != null ? new StyleBackground(emoji) : StyleKeyword.None;
			AddToClassList(USS_ACTIVE);
		}

		public void Deactivate()
		{
			RemoveFromClassList(USS_ACTIVE);
		}

		public void FollowTarget(Transform target)
		{
			if (target == null || Camera.main == null)
				return;

			Vector3 worldPos = target.position + Vector3.up * WORLD_OFFSET_Y;
			Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

			float panelX = Mathf.Clamp(screenPos.x, SCREEN_PADDING, Screen.width - SCREEN_PADDING);
			float panelY = Mathf.Clamp(Screen.height - screenPos.y, SCREEN_PADDING, Screen.height - SCREEN_PADDING);

			style.left = panelX;
			style.top = panelY;
		}
	}
}
