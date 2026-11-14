using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 무대 소품 배치와 화면 설정. TitleStageBuilder 입력
	[CreateAssetMenu(fileName = nameof(TitleStageRecipe), menuName = "WM/Title/TitleStageRecipe")]
	public class TitleStageRecipe : ScriptableObject
	{
		[Header("Seed")]
		public int Seed = 4615;

		[Header("Materials")]
		public Material GroundSource;
		[Tooltip("풀 무늬에 곱하는 색. 노란 기 도는 밝은 초록")]
		public Color GroundColor = new(1f, 1.18f, 0.52f);
		public Material GrassSource;
		public Material GrassLit;
		public Color GrassColor = new(0.5f, 0.7f, 0.2f);
		public Material LeafSource;
		[Tooltip("잎 무늬에 곱하는 색. 원본 잎이 어두워 1 넘게 올림")]
		public Color LeafColor = new(1.05f, 1.45f, 0.62f);
		[Range(0f, 1f)] public float LeafCutoff = 0.4f;

		[Header("Big tree")]
		public GameObject TreePrefab;
		public Vector2 BigTreePosition = new(7f, 3f);
		public float BigTreeScale = 3.2f;

		[Header("Character")]
		public GameObject CharacterModel;
		public RuntimeAnimatorController CharacterAnimator;
		[Tooltip("큰 나무 기준 위치")]
		public Vector2 CharacterOffset = new(-4.5f, -5f);
		public float CharacterScale = 1.5f;
		public float CharacterYaw = 200f;

		[Header("Forest")]
		public int ForestCount = 34;
		public Vector2 ForestRadius = new(55f, 150f);
		public Vector2 ForestScale = new(1.1f, 2.4f);
		[Tooltip("카메라 쪽 (음의 z) 으로 이 각도 범위는 비움")]
		public float ForestFrontGapDegrees = 70f;

		[Header("Shrub")]
		public GameObject ShrubPrefab;
		public int ShrubCount = 26;
		[Tooltip("이 z 보다 카메라 쪽에는 덤불을 두지 않음")]
		public float ShrubFrontLimit = -25f;
		public float ShrubRadius = 60f;
		public Vector2 ShrubScale = new(0.8f, 1.6f);

		[Header("Grass")]
		public GameObject GrassPrefab;
		public int GrassCount = 650;
		public float GrassRadius = 70f;
		public Vector2 GrassScale = new(0.9f, 1.8f);

		[Header("Gravel")]
		public GameObject GravelPrefab;
		public int GravelCount = 26;
		public float GravelRadius = 45f;

		[Header("Flowers")]
		[Tooltip("들꽃 색. 비중은 TitleFlowerField 에서")]
		public Color[] FlowerColors = { Color.white, new(1f, 0.62f, 0.72f), new(1f, 0.9f, 0.45f) };

		[Header("Sky")]
		public SkyPresetSO SkyPreset;
		public Material SkyboxMaterial;
		[Range(0f, 24f)] public float Hour = 11.5f;
		public Color SkyZenith = new(0.22f, 0.52f, 0.92f);
		public Color SkyHorizon = new(0.78f, 0.89f, 0.98f);
		public Color CloudColor = Color.white;
		[Range(0f, 1f)] public float CloudCoverage = 0.55f;
		public float CloudScale = 2.2f;
		public float CloudSoftness = 0.25f;
		public float CloudHeight = 0.08f;
		[Tooltip("먼 산이 하늘색으로 흐려지는 정도")]
		public float FogDensityScale = 0.35f;
		public float PostSaturation = 8f;
		public float PostExposure = 0.2f;

		[Header("Camera")]
		[Tooltip("y 는 지면 위 높이")]
		public Vector3 CameraPosition = new(-8f, 1.2f, -92f);
		public Vector3 CameraEuler = new(-12f, 6f, 0f);
		public float CameraFieldOfView = 42f;

		[Header("Logo")]
		public Font LogoFontSource;
		public Font SubtitleFontSource;
		[Tooltip("한글 부제와 같은 꼴. 콜론과 느낌표 포함")]
		public string LogoText = "WITCH: MENDOKUSAI !";
		public string SubtitleText = "마녀: 귀찮아 !";
		[Tooltip("화면 위에서 로고 윗변까지")]
		public float LogoTop = 220f;
		public float LogoFontSize = 96f;
		[Tooltip("TMP 글자 간격 단위 (em 의 1/100)")]
		public float LogoCharacterSpacing = 30f;
		public float RuleWidth = 520f;
		public float RuleThickness = 2f;
		public float RuleGap = 18f;
		public float SubtitleFontSize = 32f;
		public float SubtitleCharacterSpacing = 25f;
		public Color TextColor = Color.white;
		[Tooltip("글자 뒤 흐린 그림자. 밝은 하늘 위 가독성")]
		public Color TextShadowColor = new(0.12f, 0.27f, 0.47f, 0.45f);
		[Tooltip("TMP 밑그림 값")]
		public float TextShadowOffsetY = -0.3f;
		public float TextShadowSoftness = 0.8f;
		[Tooltip("가운데 정렬 글자 줄의 폭")]
		public float TextRowWidth = 1920f;
		[Tooltip("글자 크기에 곱해 줄 높이")]
		public float LineHeightScale = 1.3f;

		[Header("Press any key")]
		public string PressText = "PRESS ANY KEY";
		public float PressBottom = 140f;
		public float PressFontSize = 24f;
		public float PressCharacterSpacing = 33f;

		[Header("Menu")]
		[Tooltip("화면 아래에서 메뉴 아랫변까지. 안내 문구 자리 근처")]
		public float MenuBottom = 96f;
		public Vector2 MenuCellSize = new(420f, 60f);
		public float MenuSpacing = 6f;
		[Tooltip("디버그 빌드의 멀티 버튼까지 넣은 최대 줄 수")]
		public int MenuMaxRows = 4;
		public float MenuFontSize = 34f;
		public float MenuCharacterSpacing = 12f;

		[Header("Footer")]
		public Vector2 FooterMargin = new(48f, 32f);
		public Vector2 FooterSize = new(600f, 40f);
		public float FooterFontSize = 18f;
		public float FooterCharacterSpacing = 18f;
		public Color FooterColor = new(1f, 1f, 1f, 0.85f);
	}
}
