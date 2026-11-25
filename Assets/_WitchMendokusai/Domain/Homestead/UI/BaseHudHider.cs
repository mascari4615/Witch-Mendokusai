using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 본편 HUD (UI Toolkit 층과 씬의 캔버스) 를 잠시 숨김. 개척 화면과 같은 방식
	public sealed class BaseHudHider
	{
		private readonly List<Canvas> hidden = new();
		private VisualElement layer;
		private StyleEnum<DisplayStyle> layerDisplay;

		public void Hide(UIRoot uiRoot)
		{
			Restore();
			layer = uiRoot.HudLayer;
			layerDisplay = layer.style.display;
			layer.style.display = DisplayStyle.None;
			foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
			{
				if (canvas.isRootCanvas == false || canvas.enabled == false || canvas.renderMode == RenderMode.WorldSpace)
					continue;
				canvas.enabled = false;
				hidden.Add(canvas);
			}
		}

		public void Restore()
		{
			if (layer != null)
				layer.style.display = layerDisplay;
			layer = null;
			foreach (Canvas canvas in hidden)
			{
				if (canvas != null)
					canvas.enabled = true;
			}
			hidden.Clear();
		}
	}
}
