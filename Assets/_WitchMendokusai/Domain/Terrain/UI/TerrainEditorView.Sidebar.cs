using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TerrainEditorView 의 옆 화면 짜기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TerrainEditorView.cs 를 본다.
	public partial class TerrainEditorView : VisualElement
	{
		private static readonly Dictionary<string, (string ko, string en)> Labels = new()
		{
			{ "title", ("지형 에디터", "Terrain Editor") },
			{ "regenerate", ("재생성", "Regenerate") },
			{ "randomSeed", ("무작위 시드", "Random Seed") },
			{ "reset", ("기본값", "Defaults") },
			{ "seed", ("시드", "Seed") },
			{ "heightmap", ("높이맵", "Heightmap") },
			{ "octaves", ("옥타브", "Octaves") },
			{ "frequency", ("주파수", "Frequency") },
			{ "amplitude", ("진폭", "Amplitude") },
			{ "persistence", ("지속도", "Persistence") },
			{ "lacunarity", ("주파수 배수", "Lacunarity") },
			{ "preview", ("미리보기", "Preview") },
			{ "previewHeightmap", ("높이맵", "Heightmap") },
			{ "previewSlope", ("경사", "Slope") },
			{ "previewBiome", ("바이옴", "Biome") },
			{ "previewMesh3D", ("3D", "3D") },
			{ "mesh3dUnavailable", ("3D 미리보기는 에디터에서만 동작", "3D preview is editor-only") },
			{ "mesh3dSize", ("미리보기 크기 (m)", "Preview Size (m)") },
			{ "biome", ("바이옴", "Biome") },
			{ "biomeFrequency", ("바이옴 빈도", "Biome Freq") },
			{ "empty", ("TerrainParameters가 비어있음.", "TerrainParameters is null.") },
			{ "statusSeed", ("시드", "Seed") },
			{ "statusOct", ("옥타브", "Oct") },
			{ "statusFreq", ("주파수", "Freq") },
			{ "statusAmp", ("진폭", "Amp") },
			{ "statusPers", ("지속도", "Pers") },
			{ "statusLac", ("배수", "Lac") },
		};

		private void BuildSidebar(VisualElement sidebar)
		{
			VisualElement headerRow = new();
			headerRow.style.flexDirection = FlexDirection.Row;
			headerRow.style.alignItems = Align.Center;
			headerRow.style.marginBottom = 8;
			sidebar.Add(headerRow);

			titleLabel = new Label();
			titleLabel.style.fontSize = 14;
			titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			titleLabel.style.flexGrow = 1;
			headerRow.Add(titleLabel);

			langToggleButton = new Button(ToggleLang);
			langToggleButton.style.minWidth = 44;
			headerRow.Add(langToggleButton);

			VisualElement buttonRow = new();
			buttonRow.AddToClassList(BUTTON_ROW_CLASS);
			buttonRow.style.flexDirection = FlexDirection.Row;
			buttonRow.style.marginBottom = 8;
			sidebar.Add(buttonRow);

			regenerateButton = new Button(Regenerate);
			regenerateButton.style.flexGrow = 1;
			buttonRow.Add(regenerateButton);

			randomSeedButton = new Button(RandomizeSeed);
			randomSeedButton.style.flexGrow = 1;
			randomSeedButton.style.marginLeft = 4;
			buttonRow.Add(randomSeedButton);

			resetButton = new Button(ResetToDefault);
			resetButton.style.flexGrow = 1;
			resetButton.style.marginLeft = 4;
			buttonRow.Add(resetButton);

			seedField = new IntegerField { value = parameters.Seed };
			seedField.RegisterValueChangedCallback(evt =>
			{
				parameters.SetSeed(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(seedField);

			heightHeader = new Label();
			heightHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
			heightHeader.style.marginTop = 12;
			heightHeader.style.marginBottom = 4;
			sidebar.Add(heightHeader);

			octavesSlider = new SliderInt(1, 8) { value = parameters.Octaves, showInputField = true };
			octavesSlider.RegisterValueChangedCallback(evt =>
			{
				parameters.SetOctaves(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(octavesSlider);

			frequencyField = new FloatField { value = parameters.Frequency };
			frequencyField.RegisterValueChangedCallback(evt =>
			{
				parameters.SetFrequency(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(frequencyField);

			amplitudeField = new FloatField { value = parameters.Amplitude };
			amplitudeField.RegisterValueChangedCallback(evt =>
			{
				parameters.SetAmplitude(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(amplitudeField);

			persistenceSlider = new Slider(0f, 1f) { value = parameters.Persistence, showInputField = true };
			persistenceSlider.RegisterValueChangedCallback(evt =>
			{
				parameters.SetPersistence(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(persistenceSlider);

			lacunarityField = new FloatField { value = parameters.Lacunarity };
			lacunarityField.RegisterValueChangedCallback(evt =>
			{
				parameters.SetLacunarity(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(lacunarityField);

			biomeHeader = new Label();
			biomeHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
			biomeHeader.style.marginTop = 12;
			biomeHeader.style.marginBottom = 4;
			sidebar.Add(biomeHeader);

			biomeFrequencyField = new FloatField { value = parameters.BiomeFrequency };
			biomeFrequencyField.RegisterValueChangedCallback(evt =>
			{
				parameters.SetBiomeFrequency(evt.newValue);
				HandleChanged();
			});
			sidebar.Add(biomeFrequencyField);

			previewHeader = new Label();
			previewHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
			previewHeader.style.marginTop = 12;
			previewHeader.style.marginBottom = 4;
			sidebar.Add(previewHeader);

			VisualElement previewModeRow = new();
			previewModeRow.style.flexDirection = FlexDirection.Row;
			sidebar.Add(previewModeRow);

			previewHeightmapButton = new Button(() => SetPreviewMode(TerrainPreviewMode.Heightmap));
			previewHeightmapButton.style.flexGrow = 1;
			previewModeRow.Add(previewHeightmapButton);

			previewSlopeButton = new Button(() => SetPreviewMode(TerrainPreviewMode.Slope));
			previewSlopeButton.style.flexGrow = 1;
			previewSlopeButton.style.marginLeft = 4;
			previewModeRow.Add(previewSlopeButton);

			previewBiomeButton = new Button(() => SetPreviewMode(TerrainPreviewMode.Biome));
			previewBiomeButton.style.flexGrow = 1;
			previewBiomeButton.style.marginLeft = 4;
			previewModeRow.Add(previewBiomeButton);

			previewMesh3DButton = new Button(() => SetPreviewMode(TerrainPreviewMode.Mesh3D));
			previewMesh3DButton.style.flexGrow = 1;
			previewMesh3DButton.style.marginLeft = 4;
			previewMesh3DButton.SetEnabled(renderMesh3D != null);
			previewModeRow.Add(previewMesh3DButton);

			// Mesh3D preview 영역 크기 슬라이더 — 큰 값에서 biome 분포·지형 모양 보임. 16 (1 청크) ~ 256 (대형 영역).
			mesh3dSizeHeader = new Label();
			mesh3dSizeHeader.style.marginTop = 8;
			mesh3dSizeHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
			sidebar.Add(mesh3dSizeHeader);

			mesh3dSizeSlider = new SliderInt(16, 256) { value = mesh3dSize, showInputField = true };
			mesh3dSizeSlider.RegisterValueChangedCallback(evt =>
			{
				int snapped = Mathf.Clamp(Mathf.RoundToInt(evt.newValue / 16f) * 16, 16, 256);
				if (snapped == mesh3dSize)
					return;
				mesh3dSize = snapped;
				if (mesh3dSizeSlider.value != snapped)
					mesh3dSizeSlider.SetValueWithoutNotify(snapped);
				if (previewMode == TerrainPreviewMode.Mesh3D)
					Regenerate();
			});
			sidebar.Add(mesh3dSizeSlider);
		}

		private void ApplyLang()
		{
			titleLabel.text = T("title");
			regenerateButton.text = T("regenerate");
			randomSeedButton.text = T("randomSeed");
			resetButton.text = T("reset");
			heightHeader.text = T("heightmap");
			biomeHeader.text = T("biome");
			previewHeader.text = T("preview");
			previewHeightmapButton.text = T("previewHeightmap");
			previewSlopeButton.text = T("previewSlope");
			previewBiomeButton.text = T("previewBiome");
			previewMesh3DButton.text = T("previewMesh3D");
			mesh3dSizeHeader.text = T("mesh3dSize");

			seedField.label = T("seed");
			octavesSlider.label = T("octaves");
			frequencyField.label = T("frequency");
			amplitudeField.label = T("amplitude");
			persistenceSlider.label = T("persistence");
			lacunarityField.label = T("lacunarity");
			biomeFrequencyField.label = T("biomeFrequency");

			langToggleButton.text = lang == TerrainEditorLang.Korean ? "EN" : "한";

			RefreshStatusLabel();
		}

		private void ToggleLang()
		{
			lang = lang == TerrainEditorLang.Korean ? TerrainEditorLang.English : TerrainEditorLang.Korean;
			SaveLang(lang);
			ApplyLang();
		}

		private string T(string key)
		{
			if (Labels.TryGetValue(key, out (string ko, string en) pair))
				return lang == TerrainEditorLang.Korean ? pair.ko : pair.en;
			return key;
		}

		private void SyncFromParameters()
		{
			seedField.SetValueWithoutNotify(parameters.Seed);
			octavesSlider.SetValueWithoutNotify(parameters.Octaves);
			frequencyField.SetValueWithoutNotify(parameters.Frequency);
			amplitudeField.SetValueWithoutNotify(parameters.Amplitude);
			persistenceSlider.SetValueWithoutNotify(parameters.Persistence);
			lacunarityField.SetValueWithoutNotify(parameters.Lacunarity);
			biomeFrequencyField.SetValueWithoutNotify(parameters.BiomeFrequency);
		}

		private static TerrainEditorLang LoadLang()
		{
#if UNITY_EDITOR
			return (TerrainEditorLang)UnityEditor.EditorPrefs.GetInt(LANG_PREF_KEY, (int)TerrainEditorLang.Korean);
#else
			return (TerrainEditorLang)PlayerPrefs.GetInt(LANG_PREF_KEY, (int)TerrainEditorLang.Korean);
#endif
		}

		private static void SaveLang(TerrainEditorLang value)
		{
#if UNITY_EDITOR
			UnityEditor.EditorPrefs.SetInt(LANG_PREF_KEY, (int)value);
#else
			PlayerPrefs.SetInt(LANG_PREF_KEY, (int)value);
			PlayerPrefs.Save();
#endif
		}
	}
}
