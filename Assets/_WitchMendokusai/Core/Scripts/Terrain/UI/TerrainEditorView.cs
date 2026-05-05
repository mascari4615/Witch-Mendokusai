using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public enum TerrainEditorLang
	{
		Korean = 0,
		English = 1,
	}

	public enum TerrainPreviewMode
	{
		Heightmap = 0,
		Biome = 1,
		Mesh3D = 2,
	}

	/// <summary>
	/// 지형 에디터 View. EditorWindow / 런타임 UIDocument 양쪽에서 호스팅.
	/// Host는 onParameterChanged 콜백으로 SO 변경 처리(EditorUtility.SetDirty 등) 담당.
	/// 라벨 한글/영어 토글 — EditorPrefs(Editor) / PlayerPrefs(Runtime)로 영속.
	/// </summary>
	public class TerrainEditorView : VisualElement
	{
		public const string ROOT_CLASS = "wm-terrain-editor";
		public const string PREVIEW_CLASS = "wm-terrain-editor__preview";
		public const string SIDEBAR_CLASS = "wm-terrain-editor__sidebar";
		public const string BUTTON_ROW_CLASS = "wm-terrain-editor__button-row";

		private const int PREVIEW_MIN_SIZE = 64;
		private const int PREVIEW_MAX_SIZE = 1024;
		private const string LANG_PREF_KEY = "WM.TerrainEditor.Lang";

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

		private readonly TerrainParameters parameters;
		private readonly Action onParameterChanged;
		private readonly Func<Mesh, Vector2, float, int, int, Texture> renderMesh3D;

		private Vector2 mesh3dRotation = new(30f, 45f);
		private float mesh3dZoom = 1f;
		// Mesh3D preview 영역 한 변 길이 (m). 큰 값일수록 biome 분포가 넓게 보임. UInt32 인덱스 사용 — 안전.
		private int mesh3dSize = 128;
		private bool isDragging;
		private Vector2 lastPointerPosition;

		private int previewPixelWidth = 256;
		private int previewPixelHeight = 256;

		private TerrainEditorLang lang;
		private TerrainPreviewMode previewMode = TerrainPreviewMode.Heightmap;

		private VisualElement previewImage;
		private Label statusLabel;

		private Label titleLabel;
		private Label previewHeader;
		private Label heightHeader;
		private Label biomeHeader;
		private Button regenerateButton;
		private Button randomSeedButton;
		private Button resetButton;
		private Button langToggleButton;
		private Button previewHeightmapButton;
		private Button previewBiomeButton;
		private Button previewMesh3DButton;
		private SliderInt mesh3dSizeSlider;
		private Label mesh3dSizeHeader;

		private IntegerField seedField;
		private SliderInt octavesSlider;
		private FloatField frequencyField;
		private FloatField amplitudeField;
		private Slider persistenceSlider;
		private FloatField lacunarityField;
		private FloatField biomeFrequencyField;

		public TerrainEditorView(TerrainParameters parameters, Action onParameterChanged = null, Func<Mesh, Vector2, float, int, int, Texture> renderMesh3D = null)
		{
			this.parameters = parameters;
			this.onParameterChanged = onParameterChanged ?? (() => { });
			this.renderMesh3D = renderMesh3D;

			lang = LoadLang();

			AddToClassList(ROOT_CLASS);
			style.flexDirection = FlexDirection.Row;
			style.flexGrow = 1;

			VisualElement sidebar = new();
			sidebar.AddToClassList(SIDEBAR_CLASS);
			sidebar.style.width = 280;
			sidebar.style.paddingLeft = 8;
			sidebar.style.paddingRight = 8;
			sidebar.style.paddingTop = 8;
			sidebar.style.paddingBottom = 8;
			Add(sidebar);

			BuildSidebar(sidebar);

			VisualElement previewArea = new();
			previewArea.style.flexGrow = 1;
			previewArea.style.alignItems = Align.Center;
			previewArea.style.justifyContent = Justify.Center;
			previewArea.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
			Add(previewArea);

			previewImage = new VisualElement();
			previewImage.AddToClassList(PREVIEW_CLASS);
			previewImage.style.flexGrow = 1;
			previewImage.style.width = new StyleLength(Length.Percent(100));
			previewImage.style.height = new StyleLength(Length.Percent(100));
			previewImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
			previewImage.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown);
			previewImage.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
			previewImage.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);
			previewImage.RegisterCallback<WheelEvent>(OnPreviewWheel);
			previewImage.RegisterCallback<GeometryChangedEvent>(OnPreviewGeometryChanged);
			previewArea.Add(previewImage);

			statusLabel = new Label();
			statusLabel.style.whiteSpace = WhiteSpace.Normal;
			statusLabel.style.position = Position.Absolute;
			statusLabel.style.bottom = 8;
			statusLabel.style.left = 8;
			statusLabel.style.right = 8;
			statusLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
			previewArea.Add(statusLabel);

			ApplyLang();
			ApplyPreviewModeButtonStyle();
			Regenerate();
		}

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

		private void SetPreviewMode(TerrainPreviewMode mode)
		{
			if (previewMode == mode)
				return;
			previewMode = mode;
			ApplyPreviewModeButtonStyle();
			Regenerate();
		}

		private void ApplyPreviewModeButtonStyle()
		{
			Color active = new(0.32f, 0.55f, 0.78f, 1f);
			Color inactive = new(0.22f, 0.22f, 0.22f, 1f);
			previewHeightmapButton.style.backgroundColor = previewMode == TerrainPreviewMode.Heightmap ? active : inactive;
			previewBiomeButton.style.backgroundColor = previewMode == TerrainPreviewMode.Biome ? active : inactive;
			previewMesh3DButton.style.backgroundColor = previewMode == TerrainPreviewMode.Mesh3D ? active : inactive;
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
			if (Labels.TryGetValue(key, out var pair))
				return lang == TerrainEditorLang.Korean ? pair.ko : pair.en;
			return key;
		}

		private void HandleChanged()
		{
			Regenerate();
			onParameterChanged();
		}

		private void Regenerate()
		{
			if (parameters == null)
			{
				statusLabel.text = T("empty");
				return;
			}

			// heightmap PNG 캐시 보장 (main thread). 텍스쳐 변경 시 즉시 반영.
			parameters.EnsureHeightmapCache();

			if (previewMode == TerrainPreviewMode.Mesh3D)
			{
				if (renderMesh3D == null)
				{
					statusLabel.text = T("mesh3dUnavailable");
					return;
				}

				Mesh mesh = TerrainGenerator.GenerateChunkMesh(parameters, 0, 0, mesh3dSize);
				Texture rendered = renderMesh3D(mesh, mesh3dRotation, mesh3dZoom, previewPixelWidth, previewPixelHeight);
				if (rendered is RenderTexture rt)
					previewImage.style.backgroundImage = Background.FromRenderTexture(rt);
				else if (rendered is Texture2D tex2D)
					previewImage.style.backgroundImage = new StyleBackground(tex2D);
				RefreshStatusLabel();
				return;
			}

			Texture2D texture = previewMode == TerrainPreviewMode.Biome
				? TerrainGenerator.GenerateBiomeTexture(parameters, previewPixelWidth, previewPixelHeight)
				: TerrainGenerator.GenerateHeightmapTexture(parameters, previewPixelWidth, previewPixelHeight);
			previewImage.style.backgroundImage = new StyleBackground(texture);
			RefreshStatusLabel();
		}

		private void RefreshStatusLabel()
		{
			if (statusLabel == null || parameters == null)
				return;
			statusLabel.text =
				$"{T("statusSeed")} {parameters.Seed} | " +
				$"{T("statusOct")} {parameters.Octaves} | " +
				$"{T("statusFreq")} {parameters.Frequency:F4} | " +
				$"{T("statusAmp")} {parameters.Amplitude:F1} | " +
				$"{T("statusPers")} {parameters.Persistence:F2} | " +
				$"{T("statusLac")} {parameters.Lacunarity:F2}";
		}

		private void OnPreviewGeometryChanged(GeometryChangedEvent evt)
		{
			int newWidth = Mathf.Clamp((int)evt.newRect.width, PREVIEW_MIN_SIZE, PREVIEW_MAX_SIZE);
			int newHeight = Mathf.Clamp((int)evt.newRect.height, PREVIEW_MIN_SIZE, PREVIEW_MAX_SIZE);
			if (newWidth == previewPixelWidth && newHeight == previewPixelHeight)
				return;
			previewPixelWidth = newWidth;
			previewPixelHeight = newHeight;
			Regenerate();
		}

		private void OnPreviewPointerDown(PointerDownEvent evt)
		{
			if (previewMode != TerrainPreviewMode.Mesh3D)
				return;
			isDragging = true;
			lastPointerPosition = evt.position;
			previewImage.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPreviewPointerMove(PointerMoveEvent evt)
		{
			if (isDragging == false)
				return;
			if (previewMode != TerrainPreviewMode.Mesh3D)
				return;

			Vector2 cur = (Vector2)evt.position;
			Vector2 delta = cur - lastPointerPosition;
			lastPointerPosition = cur;

			mesh3dRotation.y += delta.x * 0.4f;
			mesh3dRotation.x = Mathf.Clamp(mesh3dRotation.x + delta.y * 0.4f, -85f, 85f);

			Regenerate();
			evt.StopPropagation();
		}

		private void OnPreviewPointerUp(PointerUpEvent evt)
		{
			if (isDragging == false)
				return;
			isDragging = false;
			if (previewImage.HasPointerCapture(evt.pointerId))
				previewImage.ReleasePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPreviewWheel(WheelEvent evt)
		{
			if (previewMode != TerrainPreviewMode.Mesh3D)
				return;
			float zoomDelta = -evt.delta.y * 0.1f;
			mesh3dZoom = Mathf.Clamp(mesh3dZoom + zoomDelta, 0.2f, 4f);
			Regenerate();
			evt.StopPropagation();
		}

		private void RandomizeSeed()
		{
			parameters.SetSeed(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
			SyncFromParameters();
			HandleChanged();
		}

		private void ResetToDefault()
		{
			parameters.ResetToDefault();
			SyncFromParameters();
			HandleChanged();
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
