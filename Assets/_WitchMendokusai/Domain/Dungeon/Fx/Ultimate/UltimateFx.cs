using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 스킬 (궁극기) 연출의 공통 바탕. 연출마다 파생 클래스 하나 (주제 물체, 실루엣, 배경 장악, 시간 구조가 다름) 와 프리셋 에셋 하나
	/// - 파생 클래스는 Compose 에서 진행기 (UltimateFxRun) 의 시간선에 효과를 올림. 값은 파생 클래스의 직렬화 필드
	/// - 여기 값: 셰이더 연결, 섬광 밝기 상한 (인물이 묻히지 않게), 시연용 임시 풀과 나무, 풀 반응
	/// - 수치 근거는 memo wm/design/fx-ceiling-20261119 와 dc-fx-analysis-20261119 (참고 영상 프레임 실측)
	/// - 재생: 개발 창 fx ultimate [이름 일부], 프리셋은 메뉴 WM/CombatFx/Build Ultimate Presets
	/// </summary>
	public abstract class UltimateFx : CombatFx
	{
		[Serializable]
		public struct FoliageKind
		{
			public GameObject Prefab;
			[Min(0)] public int Count;
			[Range(0f, 1f)] public float Flexibility;
			public Vector2 ScaleRange;
			[Min(0f), Tooltip("중심에서 이만큼 안쪽에는 놓지 않음")] public float MinDistance;
		}

		[Serializable]
		public struct FoliageResponse
		{
			[Min(0f), Tooltip("풀과 나무 최대 기울기 (도)")] public float Bend;
			[Min(0.1f)] public float DecaySeconds;
			[Min(0.5f)] public float Hertz;
			[Min(0), Tooltip("충격파 앞에서 뜯겨 날아가는 풀잎 수")] public int BladeCount;
			[Min(0f)] public float BladeSpeed;
			public Color BladeColor;
		}

		[Header("개발 창")]
		[SerializeField, Tooltip("fx ultimate 뒤에 쓰는 정확한 이름. 이름 일부 검색보다 먼저 맞춤 (예: ult3d, ult3d_heavy)")] private string devKey = string.Empty;

		[Header("연결")]
		[SerializeField] private Shader spriteShader;
		[SerializeField] private Shader shockwaveShader;
		[SerializeField] private Shader gradeShader;

		[Header("섬광 상한")]
		[SerializeField, Range(0f, 1f), Tooltip("인물 위까지 덮는 화면 섬광의 최대 알파. 높으면 인물이 흰색에 묻힘")] private float overlayFlashCap = 0.32f;
		[SerializeField, Range(0f, 1f), Tooltip("배경 (인물과 효과 제외) 이 섬광 색으로 가는 최대 비율")] private float backgroundFlashCap = 0.85f;

		[Header("환경")]
		[SerializeField, Tooltip("시연용 임시 풀과 나무를 깔고 끝나면 치움 (진열장 용)")] private bool spawnTemporaryFoliage = true;
		[SerializeField, Min(1f), Tooltip("임시 풀과 나무를 까는 반지름")] private float foliageAreaRadius = 14f;
		[SerializeField] private FoliageKind[] foliage = Array.Empty<FoliageKind>();
		[SerializeField] private FoliageResponse foliageResponse = new() { Bend = 78f, DecaySeconds = 1.2f, Hertz = 1.7f, BladeCount = 70, BladeSpeed = 11f, BladeColor = new Color(0.45f, 0.7f, 0.3f, 1f) };

		[Header("진행")]
		[SerializeField, Min(0f), Tooltip("마지막 효과 뒤 입자가 사라질 때까지 붙드는 시간")] private float tailSeconds = 1.5f;
		[SerializeField, Min(100), Tooltip("화면 층 정렬 순서")] private int screenSortingOrder = 2400;

		public string DevKey => devKey;
		public Shader SpriteShader => spriteShader;
		public Shader ShockwaveShader => shockwaveShader;
		public Shader GradeShader => gradeShader;
		public float OverlayFlashCap => overlayFlashCap;
		public float BackgroundFlashCap => backgroundFlashCap;
		public bool SpawnTemporaryFoliage => spawnTemporaryFoliage;
		public float FoliageAreaRadius => foliageAreaRadius;
		public FoliageKind[] Foliage => foliage;
		public FoliageResponse Response => foliageResponse;
		public float TailSeconds => tailSeconds;
		public int ScreenSortingOrder => screenSortingOrder;

		/// <summary> 처음부터 마지막 효과가 끝날 때까지 초 (입자 꼬리 제외) </summary>
		public abstract float DurationSeconds { get; }

		/// <summary> 시간선에 효과를 올림. 진행기가 준비를 마친 뒤 한 번 부름 </summary>
		public abstract void Compose(UltimateFxRun run);

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			UltimateFxRun run = new(this, context);
			return runner.Run(run.Routine(), run.Dispose);
		}
	}
}
