using System;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class ScreenRootController : IDisposable
	{
		private readonly PanelRenderer panelRenderer;
		private readonly VisualTreeAsset screenAsset;
		private readonly Action<VisualElement> reloaded;
		private int loadedVersion = -1;
		private bool registered;

		public ScreenRootController(
			PanelRenderer panelRenderer,
			VisualTreeAsset screenAsset,
			Action<VisualElement> reloaded)
		{
			this.panelRenderer = panelRenderer ?? throw new ArgumentNullException(nameof(panelRenderer));
			this.screenAsset = screenAsset ?? throw new ArgumentNullException(nameof(screenAsset));
			this.reloaded = reloaded ?? throw new ArgumentNullException(nameof(reloaded));
		}

		public void Enable()
		{
			if (registered)
			{
				return;
			}

			registered = true;
			panelRenderer.RegisterUIReloadCallback(OnReloaded);
			panelRenderer.visualTreeAsset = screenAsset;
		}

		public void Dispose()
		{
			if (registered == false)
			{
				return;
			}

			panelRenderer.UnregisterUIReloadCallback(OnReloaded);
			registered = false;
			loadedVersion = -1;
		}

		private void OnReloaded(PanelRenderer renderer, VisualElement rootElement, int version)
		{
			if (renderer != panelRenderer || version == loadedVersion)
			{
				return;
			}

			loadedVersion = version;
			reloaded(rootElement);
		}
	}
}
