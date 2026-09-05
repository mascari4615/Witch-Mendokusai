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
		Slope = 1,
		Biome = 2,
		Mesh3D = 3,
	}

	/// <summary>
	/// 지형 에디터 View. EditorWindow / 런타임 UIDocument 양쪽에서 호스팅.
	/// Host는 onParameterChanged 콜백으로 SO 변경 처리(EditorUtility.SetDirty 등) 담당.
	/// 라벨 한글/영어 토글 — EditorPrefs(Editor) / PlayerPrefs(Runtime)로 영속.
	/// </summary>
	public partial class TerrainEditorView : VisualElement
	{
		public const string ROOT_CLASS = "wm-terrain-editor";
		public const string PREVIEW_CLASS = "wm-terrain-editor__preview";
		public const string SIDEBAR_CLASS = "wm-terrain-editor__sidebar";
		public const string BUTTON_ROW_CLASS = "wm-terrain-editor__button-row";

		private const int PREVIEW_MIN_SIZE = 64;
		private const int PREVIEW_MAX_SIZE = 1024;
		private const string LANG_PREF_KEY = "WM.TerrainEditor.Lang";

		private readonly TerrainParameters parameters;
		private readonly Action onParameterChanged;
		private readonly Func<Mesh, Vector2, float, int, int, Texture> renderMesh3D;
		// Mesh3D preview 영역 한 변 길이 (m). 큰 값일수록 biome 분포가 넓게 보임. UInt32 인덱스 사용 — 안전.
		private int mesh3dSize = 128;

		private int previewPixelWidth = 256;
		private int previewPixelHeight = 256;

		private TerrainEditorLang lang;
		private TerrainPreviewMode previewMode = TerrainPreviewMode.Heightmap;

		private VisualElement sidebar;
		private VisualElement previewArea;
		private VisualElement previewImage;
		private Label statusLabel;
		// splitter / window resize 같은 빈번한 GeometryChangedEvent 시 Regenerate 폭주 방지 — 150ms debounce.
		private IVisualElementScheduledItem geometryRegenerateSchedule;

		/// <summary>
		/// 외부 layout (예: TerrainEditorWindow 의 3-pane 구조) 가 sidebar 를 가져가 쓸 수 있게 노출.
		/// `RemoveFromHierarchy()` 로 자기 view 에서 떼고 다른 곳 child 로 add 가능. 이벤트 핸들러는 view instance
		/// 메서드라 view 인스턴스가 GC 안 되는 동안 정상 동작.
		/// </summary>
		public VisualElement SidebarPane => sidebar;
		public VisualElement PreviewPane => previewArea;

		private Label titleLabel;
		private Label previewHeader;
		private Label heightHeader;
		private Label biomeHeader;
		private Button regenerateButton;
		private Button randomSeedButton;
		private Button resetButton;
		private Button langToggleButton;
		private Button previewHeightmapButton;
		private Button previewSlopeButton;
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

			sidebar = new VisualElement();
			sidebar.AddToClassList(SIDEBAR_CLASS);
			sidebar.style.width = 280;
			sidebar.style.paddingLeft = 8;
			sidebar.style.paddingRight = 8;
			sidebar.style.paddingTop = 8;
			sidebar.style.paddingBottom = 8;
			Add(sidebar);

			BuildSidebar(sidebar);

			previewArea = new VisualElement();
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
#pragma warning disable CS0618 // unityBackgroundScaleMode deprecated; background-* 대체 API 마이그 미검증
			previewImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore CS0618
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

		private void HandleChanged()
		{
			Regenerate();
			onParameterChanged();
		}

		/// <summary>외부 호출용 (TerrainEditorWindow 의 graph dirty polling 등) — preview 강제 갱신.</summary>
		public void Refresh()
		{
			Regenerate();
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

			Texture2D texture;
			if (previewMode == TerrainPreviewMode.Slope)
				texture = TerrainGenerator.GenerateSlopeTexture(parameters, previewPixelWidth, previewPixelHeight);
			else if (previewMode == TerrainPreviewMode.Biome)
				texture = TerrainGenerator.GenerateBiomeTexture(parameters, previewPixelWidth, previewPixelHeight);
			else
				texture = TerrainGenerator.GenerateHeightmapTexture(parameters, previewPixelWidth, previewPixelHeight);
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
	}
}
