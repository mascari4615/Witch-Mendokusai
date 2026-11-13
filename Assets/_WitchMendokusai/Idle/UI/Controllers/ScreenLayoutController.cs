using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.Presentation;

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
		private bool dungeon;

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

		/// <summary>관리 열이 보이나. 던전 판 동안은 전투 창만 (사용자 2026-09-08)</summary>
		public bool ContentVisible => split && dungeon == false;

		/// <summary>던전 판 들어가고 나옴. 분할 설정은 건드리지 않고 판 동안만 전투 창만</summary>
		public void SetDungeon(bool inside, int openTab)
		{
			if (dungeon == inside)
			{
				return;
			}

			dungeon = inside;
			Apply(openTab);
		}

		public void OpenSide(int openTab)
		{
			split = true;
			PlayerPrefs.SetInt(SPLIT_PREFERENCE, 1);
			Apply(openTab);
		}

		public void ToggleSplit(int openTab)
		{
			split = split == false;
			PlayerPrefs.SetInt(SPLIT_PREFERENCE, split ? 1 : 0);
			Apply(openTab);
		}

		public void Apply(int openTab)
		{
			sidePanel.Apply(openTab, ContentVisible);
			battleHud.SetSplit(ContentVisible);
			AimCamera();
			SafeArea.ApplyPadding(root, SafeArea.Normalized);
		}

		public void Dispose()
		{
			root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
			Camera main = Camera.main;
			if (main != null)
			{
				main.ResetProjectionMatrix();
			}
		}

		private void OnGeometryChanged(GeometryChangedEvent moment)
		{
			AimCamera();
			SafeArea.ApplyPadding(root, SafeArea.Normalized);
		}

		private void AimCamera()
		{
			Camera main = Camera.main;
			if (main == null)
			{
				return;
			}

			main.rect = new Rect(0f, 0f, 1f, 1f);
			main.ResetProjectionMatrix();
			if (ContentVisible == false)
			{
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

			// 전투 중심은 왼쪽 영역에 유지, 오른쪽 패널 사이까지 전장 렌더
			Matrix4x4 projection = main.projectionMatrix;
			if (main.orthographic)
			{
				projection.m03 -= 1f - share;
			}
			else
			{
				projection.m02 += 1f - share;
			}
			main.projectionMatrix = projection;
		}
	}
}
