using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class ScreenLayoutController
	{
		private const string SPLIT_PREFERENCE = "idle.split";

		private readonly VisualElement root;
		private readonly SidePanelController sidePanel;
		private readonly BattleHudController battleHud;
		private readonly UIContentSO content;
		private bool split;
		private bool sideOpen;

		public ScreenLayoutController(
			VisualElement root,
			SidePanelController sidePanel,
			BattleHudController battleHud,
			UIContentSO content)
		{
			this.root = root;
			this.sidePanel = sidePanel;
			this.battleHud = battleHud;
			this.content = content;
			split = PlayerPrefs.GetInt(SPLIT_PREFERENCE, 1) == 1;
			root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
		}

		public bool ContentVisible => split || sideOpen;

		public void OpenSide(int openTab)
		{
			sideOpen = true;
			Apply(openTab);
		}

		public void CloseSide(int openTab)
		{
			sideOpen = false;
			Apply(openTab);
		}

		public void ToggleSplit(int openTab)
		{
			split = split == false;
			PlayerPrefs.SetInt(SPLIT_PREFERENCE, split ? 1 : 0);
			sideOpen = false;
			Apply(openTab);
		}

		public void Apply(int openTab)
		{
			sidePanel.Apply(openTab, split, sideOpen);
			battleHud.SetSplit(split);
			AimCamera();
			ApplySafeArea();
		}

		public void Dispose()
		{
			root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
		}

		private void OnGeometryChanged(GeometryChangedEvent moment)
		{
			AimCamera();
			ApplySafeArea();
		}

		private void AimCamera()
		{
			Camera main = Camera.main;
			if (main == null)
			{
				return;
			}

			if (split == false)
			{
				main.rect = new Rect(0f, 0f, 1f, 1f);
				return;
			}

			float share = content.BattleWidthShare;
			float sideWidth = sidePanel.ResolvedWidth;
			if (float.IsNaN(sideWidth) == false && sideWidth > 0f)
			{
				float whole = root.resolvedStyle.width;
				if (float.IsNaN(whole) == false && whole > sideWidth)
				{
					share = 1f - sideWidth / whole;
				}
			}

			main.rect = new Rect(0f, 0f, share, 1f);
		}

		private void ApplySafeArea()
		{
			Rect safe = Screen.safeArea;
			float wide = Screen.width;
			float high = Screen.height;
			if (wide <= 0f || high <= 0f)
			{
				return;
			}

			float resolvedWidth = root.resolvedStyle.width;
			float scale = resolvedWidth > 0f && float.IsNaN(resolvedWidth) == false
				? resolvedWidth / wide
				: 1f;
			root.style.paddingLeft = safe.xMin * scale;
			root.style.paddingRight = (wide - safe.xMax) * scale;
			root.style.paddingBottom = safe.yMin * scale;
			root.style.paddingTop = (high - safe.yMax) * scale;
		}
	}
}
