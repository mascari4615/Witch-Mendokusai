using System;
using UnityEngine;
using UnityEngine.UIElements;
using UIPointerType = UnityEngine.UIElements.PointerType;

namespace WitchMendokusai.Idle.UI
{
	public sealed class PointerTooltipController
	{
		/// <summary>툴팁 배치 (px). 마우스는 옆에 바짝, 손가락은 위로 멀리 (손가락에 안 가리게)</summary>
		public sealed class Layout
		{
			public long TouchDisplayMilliseconds { get; set; }
			public float MouseGap { get; set; }
			public float TouchGap { get; set; }
			public float EdgeMargin { get; set; }
			/// <summary>아직 안 잰 판과 툴팁의 대체 크기. 첫 표시 프레임에만 쓰임</summary>
			public Vector2 RootFallbackSize { get; set; }
			public Vector2 TipFallbackSize { get; set; }
		}

		private readonly Label tooltip;
		private readonly Layout layout;
		private int version;
		private int touchPointer = -1;

		public PointerTooltipController(Label tooltip, Layout layout)
		{
			this.tooltip = tooltip;
			this.layout = layout;
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
				}).StartingIn(layout.TouchDisplayMilliseconds);
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
			float rootWidth = owner != null ? owner.resolvedStyle.width : layout.RootFallbackSize.x;
			float rootHeight = owner != null ? owner.resolvedStyle.height : layout.RootFallbackSize.y;
			float tipWidth = tooltip.resolvedStyle.width > 0f ? tooltip.resolvedStyle.width : layout.TipFallbackSize.x;
			float tipHeight = tooltip.resolvedStyle.height > 0f ? tooltip.resolvedStyle.height : layout.TipFallbackSize.y;
			float gap = touch ? layout.TouchGap : layout.MouseGap;
			float edge = layout.EdgeMargin;
			float x = touch ? local.x - tipWidth * 0.5f : local.x + gap;
			float y = touch && local.y >= tipHeight + gap + edge
				? local.y - tipHeight - gap
				: local.y + gap;

			if (touch == false && x + tipWidth > rootWidth)
			{
				x = local.x - tipWidth - gap;
			}

			if (touch == false && y + tipHeight > rootHeight)
			{
				y = local.y - tipHeight - gap;
			}

			tooltip.style.left = Mathf.Clamp(x, edge, Mathf.Max(edge, rootWidth - tipWidth - edge));
			tooltip.style.top = Mathf.Clamp(y, edge, Mathf.Max(edge, rootHeight - tipHeight - edge));
		}
	}
}
