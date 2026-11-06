using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 설정 창 쉐이더팩 탭
	public partial class SettingView
	{
		private VisualElement BuildShaderPackContent()
		{
			VisualElement root = new VisualElement();

			// 헤더 + 액션 버튼 (폴더 열기 / 재스캔)
			VisualElement header = new VisualElement();
			header.AddToClassList("wm-setting-shaderpack-header");

			Label title = new Label("쉐이더팩");
			title.AddToClassList("wm-setting-header");
			header.Add(title);

			Button btnOpenFolder = new Button(OnOpenShaderPacksFolder) { text = "폴더 열기" };
			btnOpenFolder.AddToClassList("wm-setting-btn");
			btnOpenFolder.AddToClassList(NAV_CLASS);
			header.Add(btnOpenFolder);

			Button btnRescan = new Button(OnRescanShaderPacks) { text = "재스캔" };
			btnRescan.AddToClassList("wm-setting-btn");
			btnRescan.AddToClassList(NAV_CLASS);
			header.Add(btnRescan);

			root.Add(header);

			// Split: 좌측 list / 우측 detail
			VisualElement split = new VisualElement();
			split.AddToClassList("wm-setting-shaderpack-split");

			shaderPackListContainer = new VisualElement();
			shaderPackListContainer.AddToClassList("wm-setting-shaderpack-list");
			split.Add(shaderPackListContainer);

			shaderPackDetailContainer = new VisualElement();
			shaderPackDetailContainer.AddToClassList("wm-setting-shaderpack-detail");
			split.Add(shaderPackDetailContainer);

			root.Add(split);

			RebuildShaderPackList();
			RebuildShaderPackDetail();

			return root;
		}

		private void RebuildShaderPackList()
		{
			if (shaderPackListContainer == null)
				return;

			shaderPackListContainer.Clear();

			IReadOnlyList<ShaderPackEntry> packs = shaderPackManager.AvailablePacks;

			if (packs.Count == 0)
			{
				Label emptyLabel = new Label("(셰이더팩 없음, '폴더 열기' 로 셰이더팩 추가)");
				emptyLabel.AddToClassList("wm-setting-shaderpack-empty");
				shaderPackListContainer.Add(emptyLabel);
				return;
			}

			ShaderPackEntry activePack = shaderPackManager.ActivePack;

			foreach (ShaderPackEntry pack in packs)
			{
				ShaderPackEntry capturedPack = pack;
				Button packButton = new Button(() => SelectShaderPack(capturedPack)) { text = pack.Manifest.name };
				packButton.AddToClassList("wm-setting-shaderpack-item");
				packButton.AddToClassList(NAV_CLASS);
				if (activePack == pack)
					packButton.AddToClassList("wm-setting-shaderpack-item--active");
				if (selectedShaderPack == pack)
					packButton.AddToClassList("wm-setting-shaderpack-item--selected");
				shaderPackListContainer.Add(packButton);
			}
		}

		private void SelectShaderPack(ShaderPackEntry pack)
		{
			selectedShaderPack = pack;
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private void RebuildShaderPackDetail()
		{
			if (shaderPackDetailContainer == null)
				return;

			shaderPackDetailContainer.Clear();

			if (selectedShaderPack == null)
			{
				Label hint = new Label("좌측에서 셰이더팩을 선택하세요");
				hint.AddToClassList("wm-setting-shaderpack-hint");
				shaderPackDetailContainer.Add(hint);
				return;
			}

			ShaderPackManifest manifest = selectedShaderPack.Manifest;

			Label nameLabel = new Label(manifest.name);
			nameLabel.AddToClassList("wm-setting-shaderpack-detail-name");
			shaderPackDetailContainer.Add(nameLabel);

			Label authorLabel = new Label($"by {manifest.author}");
			authorLabel.AddToClassList("wm-setting-shaderpack-detail-author");
			shaderPackDetailContainer.Add(authorLabel);

			Label versionLabel = new Label($"v{manifest.version}");
			versionLabel.AddToClassList("wm-setting-shaderpack-detail-version");
			shaderPackDetailContainer.Add(versionLabel);

			if (string.IsNullOrEmpty(manifest.description) == false)
			{
				Label descLabel = new Label(manifest.description);
				descLabel.AddToClassList("wm-setting-shaderpack-detail-desc");
				shaderPackDetailContainer.Add(descLabel);
			}

			bool isActive = shaderPackManager.ActivePack == selectedShaderPack;

			if (isActive)
			{
				Button btnRevert = new Button(OnRevertShaderPack) { text = "언로드" };
				btnRevert.AddToClassList("wm-setting-btn");
				btnRevert.AddToClassList(NAV_CLASS);
				shaderPackDetailContainer.Add(btnRevert);
			}
			else
			{
				Button btnApply = new Button(OnApplyShaderPack) { text = "적용" };
				btnApply.AddToClassList("wm-setting-btn");
				btnApply.AddToClassList(NAV_CLASS);
				shaderPackDetailContainer.Add(btnApply);
			}
		}

		private void OnOpenShaderPacksFolder()
		{
			string folder = shaderPackManager.ShaderPacksDirectory;
			if (System.IO.Directory.Exists(folder) == false)
				System.IO.Directory.CreateDirectory(folder);
			Application.OpenURL("file://" + folder);
		}

		private void OnRescanShaderPacks()
		{
			shaderPackManager.ScanShaderPacks();
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private void OnApplyShaderPack()
		{
			shaderPackManager.Apply(selectedShaderPack.Id);
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private void OnRevertShaderPack()
		{
			shaderPackManager.Revert();
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}
	}
}
