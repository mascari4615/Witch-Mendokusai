using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.Presentation;

namespace WitchMendokusai.Idle.UI
{
	public sealed class ModalController : IDisposable
	{
		private readonly VisualElement root;
		private readonly long repaintMilliseconds;
		private readonly List<VisualElement> overlays = new List<VisualElement>();
		private bool blurRequested;
		private IVisualElementScheduledItem repaintItem;

		public ModalController(VisualElement root, long repaintMilliseconds)
		{
			this.root = root;
			this.repaintMilliseconds = repaintMilliseconds;
		}

		/// <summary>등록한 덮개 중 하나라도 떠 있나. Esc 가 닫을지 열지 가르는 자리</summary>
		public bool IsAnyOpen => overlays.Exists(IsVisible);

		public void Register(VisualElement overlay, Action close)
		{
			overlays.Add(overlay);
			// 흐림에 3D 장면만 담김. 판이 ScreenSpaceOverlay 라 카메라 색 버퍼 밖 (근원과 길은 memo ui-system.md 아직 안 정한 것)
			RenderTexture blurOutput = Resources.Load<RenderTexture>("Rendering/CustomBlurOutput");
			if (blurOutput != null)
			{
				overlay.style.backgroundImage = Background.FromRenderTexture(blurOutput);
			}

			overlay.RegisterCallback<PointerDownEvent>(moment =>
			{
				if (ReferenceEquals(moment.target, overlay))
				{
					close();
					moment.StopImmediatePropagation();
				}
			});
		}

		public void Show(VisualElement overlay)
		{
			if (overlay == null)
			{
				return;
			}

			overlay.style.display = DisplayStyle.Flex;
			RefreshBlur();
		}

		public void Hide(VisualElement overlay)
		{
			if (overlay == null)
			{
				return;
			}

			overlay.style.display = DisplayStyle.None;
			RefreshBlur();
		}

		public void Dispose()
		{
			repaintItem?.Pause();
			repaintItem = null;
			if (blurRequested)
			{
				BlurDemand.Remove();
				blurRequested = false;
			}
		}

		private void RefreshBlur()
		{
			bool visible = overlays.Exists(IsVisible);
			if (visible && blurRequested == false)
			{
				BlurDemand.Add();
				blurRequested = true;
				repaintItem = root.schedule.Execute(() => root.MarkDirtyRepaint()).Every(repaintMilliseconds);
			}
			else if (visible == false)
			{
				Dispose();
			}
		}

		private static bool IsVisible(VisualElement overlay)
		{
			return overlay != null && overlay.style.display == DisplayStyle.Flex;
		}
	}
}
