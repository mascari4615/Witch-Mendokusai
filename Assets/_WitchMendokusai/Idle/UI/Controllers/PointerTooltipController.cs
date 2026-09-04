using System;
using UnityEngine;
using UnityEngine.UIElements;
using UIPointerType = UnityEngine.UIElements.PointerType;

namespace WitchMendokusai.Idle.UI
{
	public sealed class PointerTooltipController
	{
		private readonly Label tooltip;
		private readonly long touchDisplayMilliseconds;
		private int version;
		private int touchPointer = -1;

		public PointerTooltipController(Label tooltip, long touchDisplayMilliseconds)
		{
			this.tooltip = tooltip;
			this.touchDisplayMilliseconds = touchDisplayMilliseconds;
		}

		public void Bind(VisualElement target, Func<string> text)
		{
			target.RegisterCallback<PointerDownEvent>(moment =>
			{
				if (moment.pointerType == UIPointerType.mouse)
				{
					return;
				}

				touchPointer = moment.pointerId;
				Show(text());
				Move(moment.position, true);
				int scheduledVersion = ++version;
				tooltip.schedule.Execute(() =>
				{
					if (scheduledVersion == version)
					{
						Hide();
					}
				}).StartingIn(touchDisplayMilliseconds);
			});

			target.RegisterCallback<PointerEnterEvent>(moment =>
			{
				if (moment.pointerType == UIPointerType.mouse)
				{
					Show(text());
					Move(moment.position, false);
				}
			});

			target.RegisterCallback<PointerMoveEvent>(moment =>
			{
				if (moment.pointerType == UIPointerType.mouse || moment.pointerId == touchPointer)
				{
					Move(moment.position, moment.pointerType != UIPointerType.mouse);
				}
			});

			target.RegisterCallback<PointerLeaveEvent>(moment =>
			{
				if (moment.pointerType == UIPointerType.mouse)
				{
					Hide();
				}
			});
		}

		private void Show(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				tooltip.style.display = DisplayStyle.None;
				return;
			}

			tooltip.text = text;
			tooltip.style.display = DisplayStyle.Flex;
			tooltip.BringToFront();
		}

		private void Hide()
		{
			version++;
			touchPointer = -1;
			tooltip.style.display = DisplayStyle.None;
		}

		private void Move(Vector2 at, bool touch)
		{
			VisualElement owner = tooltip.parent;
			Vector2 local = owner != null ? owner.WorldToLocal(at) : at;
			float rootWidth = owner != null ? owner.resolvedStyle.width : 1920f;
			float rootHeight = owner != null ? owner.resolvedStyle.height : 1080f;
			float tipWidth = tooltip.resolvedStyle.width > 0f ? tooltip.resolvedStyle.width : 300f;
			float tipHeight = tooltip.resolvedStyle.height > 0f ? tooltip.resolvedStyle.height : 120f;
			float x = touch ? local.x - tipWidth * 0.5f : local.x + 18f;
			float y = touch && local.y >= tipHeight + 84f
				? local.y - tipHeight - 72f
				: local.y + (touch ? 72f : 18f);

			if (touch == false && x + tipWidth > rootWidth)
			{
				x = local.x - tipWidth - 18f;
			}

			if (touch == false && y + tipHeight > rootHeight)
			{
				y = local.y - tipHeight - 18f;
			}

			tooltip.style.left = Mathf.Clamp(x, 12f, Mathf.Max(12f, rootWidth - tipWidth - 12f));
			tooltip.style.top = Mathf.Clamp(y, 12f, Mathf.Max(12f, rootHeight - tipHeight - 12f));
		}
	}
}
