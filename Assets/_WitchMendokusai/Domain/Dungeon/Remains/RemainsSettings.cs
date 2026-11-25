using UnityEngine;
using UnityEngine.VFX;

namespace WitchMendokusai
{
	// 싸움 뒤 바닥에 남는 것 (잔해, 자국, 액체) 의 공용 수치. Resources 에 하나
	[CreateAssetMenu(fileName = nameof(RemainsSettings), menuName = "WM/Remains/Settings")]
	public class RemainsSettings : ScriptableObject
	{
		[Header("잔해")]
		[Min(1), Tooltip("동시에 날 수 있는 조각 최대 수")] public int MaxFlying = 128;
		[Min(1), Tooltip("바닥에 누워 남는 조각 최대 수. 넘으면 오래된 것부터 옅어짐")] public int MaxResting = 256;
		[Min(0f), Tooltip("이 속도 아래로 바닥에 닿으면 눕힘")] public float RestSpeed = .6f;
		[Min(.05f), Tooltip("누울 때 바닥에 붙는 시간 초")] public float SettleSeconds = .12f;
		[Min(.1f), Tooltip("유지 시간이 끝난 뒤 작아지며 사라지는 시간 초")] public float VanishSeconds = 1.2f;
		[Min(.1f), Tooltip("너무 오래 날면 강제로 눕힘")] public float MaxFlightSeconds = 3f;
		[Min(0), Tooltip("판 중 프레임마다 미리 만드는 조각 개체 수 (몰살 프레임의 생성 비용을 나눔)")] public int PrewarmPerFrame = 8;

		[Header("몬스터 조각")]
		[Tooltip("프로필에 재질이 없을 때 쓰는 URP Lit 재질")] public Material PieceMaterial;
		[Min(1), Tooltip("초당 조각을 남기는 처치 수 상한. 넘은 처치는 얼룩만")] public int KillPieceBudgetPerSecond = 20;
		[Min(1), Tooltip("몸 색 뽑기 캐시 상한 (그림 수)")] public int ColorSampleCacheLimit = 64;
		[Min(1), Tooltip("프레임당 새로 뽑는 몸 색 수")] public int ColorSampleRequestsPerFrame = 4;
		[Min(1f), Tooltip("치명타 처치 때 조각 수 배율")] public float CritPieceScale = 2f;
		[Min(1f), Tooltip("치명타 처치 때 조각 속도 배율")] public float CritSpeedScale = 1.5f;
		[Range(0f, 1f), Tooltip("몸에서 뽑은 조각 색을 어둡게 하는 배율. 경험치 보석 같은 줍는 것과 헷갈리지 않게")] public float BodyColorShade = .55f;
		[Tooltip("몸 색을 못 뽑았을 때 조각 색")] public Color FallbackPieceColor = new(.5f, .5f, .45f, 1f);

		[Header("밀림")]
		[Min(0f), Tooltip("주인공 발밑에서 조각을 미는 반경")] public float PlayerPushRadius = .7f;
		[Min(0f), Tooltip("주인공이 밀 때 옆으로 튀는 속도")] public float PlayerPushSpeed = 3.2f;
		[Min(0f), Tooltip("주인공이 밀 때 위로 튀는 속도")] public float PlayerPushLift = 1.6f;
		[Min(0f), Tooltip("주인공이 이 속도 아래로 움직이면 밀지 않음 (가만히 서 있을 때 떨림 방지)")] public float PlayerPushMinSpeed = .5f;

		[Min(0f), Tooltip("폭발이 잔해를 날리는 속도 (중심 기준, 멀수록 약해짐)")] public float BlastPushSpeed = 7f;
		[Min(0f), Tooltip("폭발이 잔해를 띄우는 속도")] public float BlastPushLift = 4f;

		[Header("자국 (바닥 캔버스)")]
		[Min(8f), Tooltip("카메라 주변을 덮는 캔버스 한 변, 미터")] public float CanvasSize = 64f;
		[Tooltip("캔버스 해상도 (한 변 화소)")] public int CanvasResolution = 1024;
		[Min(1), Tooltip("프레임당 찍는 도장 최대 수. 넘으면 다음 프레임")] public int MaxStampsPerFrame = 64;
		[Tooltip("자국 도장 텍스처 (흰 둥근 얼룩, 알파가 모양)")] public Texture2D StampTexture;
		[Range(0f, 1f), Tooltip("캔버스 전체 진하기")] public float CanvasOpacity = .85f;
		[Range(1f, 4f), Tooltip("도장 알파를 키우는 배율 (가장자리 옅은 부분까지 보이게)")] public float CanvasAlphaGain = 1.8f;

		[Header("액체 (바닥 웅덩이)")]
		[Min(.1f), Tooltip("액체 칸 한 변, 미터")] public float GoopCellSize = .5f;
		[Range(0f, 1f), Tooltip("쏟은 웅덩이 가장자리 양 (가운데 대비)")] public float GoopEdgeAmount = .5f;
		[Min(16), Tooltip("동시에 있을 수 있는 액체 칸 최대 수")] public int MaxGoopCells = 4096;
		[Min(.05f), Tooltip("밟은 유닛에게 피해와 상태를 주는 간격 초")] public float GoopEffectInterval = .25f;
		[Min(.02f), Tooltip("웅덩이 그림을 다시 쓰는 간격 초")] public float GoopRedrawInterval = .08f;
		[Min(.1f), Tooltip("한 칸이 타는 시간 초")] public float FireBurnSeconds = 3f;
		[Min(0f), Tooltip("불이 이웃 칸으로 번지는 데 걸리는 초")] public float FireSpreadSeconds = .18f;
		[Min(0), Tooltip("불타는 칸 위 몬스터 초당 피해")] public int FireMonsterDamagePerSecond = 12;
		[Min(0), Tooltip("불타는 칸 위 주인공 초당 피해")] public int FirePlayerDamagePerSecond = 2;
		[Min(.05f), Tooltip("감전이 남는 시간 초")] public float ShockSeconds = .7f;
		[Min(0f), Tooltip("감전이 끝난 칸이 다시 감전될 때까지 쉬는 초")] public float ShockCooldownSeconds = 1.2f;
		[Min(0), Tooltip("감전된 웅덩이 위 몬스터가 한 번 받는 피해")] public int ShockMonsterDamage = 15;
		[Min(0), Tooltip("감전된 웅덩이 위 주인공이 한 번 받는 피해")] public int ShockPlayerDamage = 2;
		[Min(.1f), Tooltip("느려짐이 남는 시간 초 (웅덩이를 벗어난 뒤)")] public float GoopSlowLingerSeconds = .4f;
		[ColorUsage(true, true), Tooltip("타는 액체 바닥의 잉걸 빛 (빛남 포함). 불 모양은 불 그래프가 맡아 바닥은 낮게")] public Color FireColor = new(.32f, .07f, .015f, 1f);
		[ColorUsage(true, true), Tooltip("감전 색 (빛남 포함)")] public Color ShockColor = new(.8f, 1.6f, 3f, 1f);
		[Tooltip("다 탄 자리에 남는 그을음 색")] public Color ScorchColor = new(.06f, .045f, .035f, .85f);
		[Min(0f), Tooltip("다 탄 칸 하나가 남기는 그을음 반지름, 미터")] public float ScorchRadius = .75f;
		[Min(8f), Tooltip("액체 그림이 덮는 한 변, 미터. 자국 캔버스보다 작아도 됨")] public float GoopViewSize = 48f;

		[Header("풀 (눕기와 눌림)")]
		[Min(0f), Tooltip("풀이 옆으로 눕는 세기, 미터")] public float GrassBendStrength = .35f;
		[Min(.05f), Tooltip("풀 키, 미터 (이 높이에서 가장 많이 누움)")] public float GrassHeight = .5f;
		[Range(0f, 1f), Tooltip("싸운 자리에서 키가 줄어드는 비율")] public float GrassFlattenAmount = .7f;
		[Min(0f), Tooltip("주인공 주변 풀이 눕는 반경")] public float GrassPlayerBendRadius = 1f;
		[Min(0f), Tooltip("몬스터 주변 풀이 눕는 반경")] public float GrassMonsterBendRadius = .7f;
		[Min(0f), Tooltip("풀을 눕히는 몬스터를 찾는 주인공 주변 거리")] public float GrassBenderRange = 12f;
		[Range(1, GrassInteraction.MAX_BENDERS), Tooltip("풀을 눕히는 것 최대 수 (주인공 포함)")] public int GrassMaxBenders = GrassInteraction.MAX_BENDERS;
		[Tooltip("풀 눌림 캔버스 해상도 (한 변 화소). 자국 캔버스와 같은 넓이를 덮음")] public int GrassFlattenResolution = 256;
		[Min(1f), Tooltip("눌린 풀이 다시 서는 시간 초")] public float GrassRegrowSeconds = 20f;
		[Min(0f), Tooltip("처치 자리 풀 눌림 반지름")] public float GrassKillFlattenRadius = .9f;
		[Min(0f), Tooltip("폭발 반경 대비 풀 눌림 반지름 비율")] public float GrassBlastFlattenScale = .8f;
		[Tooltip("풀 재질. 이 셰이더로 바꿔 둠 (WM/Remains/InteractiveGrass)")] public Shader InteractiveGrassShader;

		[Header("발밑 먼지")]
		[Min(0f), Tooltip("걸을 때 발밑 먼지 간격 초. 자산 생성기가 주인공 먼지 부품에 써 넣음 (0 이면 걸을 때 없음)")] public float WalkDustInterval = .3f;

		[Header("불 (VFX 그래프)")]
		[Tooltip("불 하나. 폭 1 짜리 그래프 (몸통, 연기, 불티). 모양과 장 속도와 연기 수는 그래프에서")] public VisualEffect FirePrefab;
		[Range(1, 32), Tooltip("동시에 서는 불 최대 수. 넘으면 주인공에게 가까운 자리부터")] public int MaxFires = 16;
		[Min(1), Tooltip("구운 불 무늬의 장 수 (그래프 플립북 칸 수와 같게. 시작 장을 고르는 범위)")] public int FireFrameCount = 36;
		[Min(.1f), Tooltip("불 판 크기 / 타는 자리 폭. 무늬 안에서 밑동이 차지하는 폭의 역수에 조금 넓힘")] public float FireSizePerWidth = 1.7f;
		[Range(0f, .5f), Tooltip("불마다 크기가 다른 정도")] public float FireSizeJitter = .1f;
		[Min(1f), Tooltip("덩이의 긴 쪽이 짧은 쪽의 이만큼을 넘으면 반으로 나눔")] public float FireSplitAspect = 1.7f;
		[Range(0f, 1f), Tooltip("덩이의 긴 쪽과 짧은 쪽 네모를 타는 칸이 이만큼 못 채우면 (휜 줄, 고리) 반으로 나눔")] public float FireMinFill = .5f;
		[Min(.1f), Tooltip("나눈 불 하나의 최소 폭, 미터")] public float FireMinWidth = 1f;
		[Min(.5f), Tooltip("불 하나의 최대 폭, 미터. 넘으면 나눔")] public float FireMaxWidth = 4.5f;
		[Min(.02f), Tooltip("불 자리를 다시 구하는 간격 초")] public float FireRebuildInterval = .1f;
		[Min(0f), Tooltip("자리가 옮겨져도 같은 불로 잇는 거리 (폭 대비)")] public float FireMatchReach = .75f;
		[Range(0f, .5f), Tooltip("화면 앞쪽으로 내려 세우는 거리 (폭 대비). 앞쪽 기름이 비어 보이지 않게")] public float FireFrontShift = .14f;
		[Range(0f, .6f), Tooltip("카메라 쪽으로 당기는 거리 (판 크기 대비). 밑동이 땅에 잘리지 않을 만큼만")] public float FireDepthPull = .16f;
		[Min(.1f), Tooltip("불이 자리와 크기를 따라가는 빠르기 (초당)")] public float FireFollowRate = 7f;
		[Range(.05f, 1f), Tooltip("막 붙은 불의 크기 비율")] public float FireStartScale = .35f;
		[Min(.01f), Tooltip("다 커지는 시간 초")] public float FireIgniteSeconds = .35f;
		[Min(.01f), Tooltip("몸통이 사라지는 시간 초")] public float FireExtinguishSeconds = .5f;
		[Min(0f), Tooltip("몸통이 사라진 뒤 남은 연기를 기다리는 시간 초. 그래프의 연기 수명보다 길게")] public float FireLingerSeconds = 2.5f;
		[ColorUsage(false, true), Tooltip("불 색 배율 (빛남 포함). 무늬에 구운 색에 곱함")] public Color FireTint = new(2f, 2f, 2f, 1f);
		[Range(0f, 1f), Tooltip("주인공이 안에 설 때 불의 진하기")] public float FireOpacityWithPlayer = .65f;
		[Range(0f, 1f), Tooltip("몬스터가 안에 설 때 불의 진하기")] public float FireOpacityWithMonster = .8f;

		[Header("액체 입자 (감전 불똥)")]
		[Tooltip("가산 파티클 재질. 감전 불똥")] public Material GoopParticleMaterial;
		[Min(16)] public int MaxGoopParticles = 600;
		[Min(1), Tooltip("프레임당 새로 내는 불똥 상한")] public int MaxGoopParticlesPerFrame = 12;
		[Min(0f), Tooltip("감전된 칸 하나가 초당 내는 불똥 수")] public float SparksPerCellPerSecond = 10f;
		[Tooltip("불똥 색")] public Color SparkColor = new(.75f, .9f, 1f, 1f);
		[Min(0f), Tooltip("불똥 속도")] public float SparkSpeed = 3.5f;
		[Min(0f), Tooltip("불똥이 위로 튀는 비율 (속도 대비 최대)")] public float SparkLift = .5f;
		[Min(.01f)] public float SparkSize = .09f;
		[Min(.02f)] public float SparkLife = .24f;
		[Min(0f), Tooltip("불똥 늘이기 (속도 배율)")] public float SparkStretch = .06f;

		[Header("셰이더 (빌드에 포함되게 참조로)")]
		public Shader StainUtilityShader;
		public Shader GroundStainShader;
		public Shader GoopShader;

		[Header("유지 시간")]
		[Tooltip("설정 창의 흔적 유지 선택지, 초. 0 은 끔, 음수는 판 내내")] public float[] LingerChoices = { 0f, 10f, 30f, 60f, -1f };
		[Min(0), Tooltip("처음 고른 선택지 번호")] public int DefaultLingerChoice = 2;

		private static RemainsSettings loaded;

		// Resources 의 하나. 공용 ResourceManager 대신 이 기능 폴더에 둠 (BreakableSettings 와 같은 꼴)
		public static RemainsSettings Load()
		{
			if (loaded == null)
				loaded = Resources.Load<RemainsSettings>(nameof(RemainsSettings));
			if (loaded == null)
				throw new System.InvalidOperationException($"Resources/{nameof(RemainsSettings)} 없음");
			return loaded;
		}

		// 선택지 번호를 초로. 판 내내는 무한
		public float LingerSecondsAt(int choice)
		{
			float seconds = LingerChoices[Mathf.Clamp(choice, 0, LingerChoices.Length - 1)];
			return seconds < 0f ? float.PositiveInfinity : seconds;
		}
	}
}
