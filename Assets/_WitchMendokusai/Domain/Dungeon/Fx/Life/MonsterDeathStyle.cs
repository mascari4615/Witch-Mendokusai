using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 사망 연출 한 종류. 본체는 즉시 꺼지고, 사망 순간의 모습을 복사한 잔상이 이 값대로 녹음
	/// - 곡선의 가로축은 전부 진행도 0 ~ 1 (Seconds 동안)
	/// - 처치 순간 파편 (공유 ParticleSystem) 과 발밑 고리 (WM/GroundPuff) 를 같이 냄. 재질 칸을 비우면 그것만 없음
	/// - 쓰는 쪽: MonsterLifeFx 의 사망 스타일 칸, 또는 MonsterDeathGhost.Play 와 PlayBurst
	/// - 미리보기 (fx 명령, 우클릭 Preview) 는 PreviewSprite 로 재생
	/// </summary>
	[CreateAssetMenu(fileName = "Death_0_New", menuName = "WM/CombatFx/Monster Death Style")]
	public class MonsterDeathStyle : CombatFx, IGroundPuffStyle
	{
		[field: Header("잔상")]
		[field: SerializeField, Tooltip("잔상을 그릴 재질 (WM/SpriteDissolve 셰이더)")]
		public Material Material { get; private set; }
		[field: SerializeField, Min(0.01f), Tooltip("다 녹을 때까지 걸리는 시간 (초)")]
		public float Seconds { get; private set; } = 0.45f;
		[field: SerializeField, Min(1), Tooltip("동시에 떠 있을 수 있는 잔상 수. 넘으면 새 잔상은 생략")]
		public int MaxGhosts { get; private set; } = 48;

		[field: Header("녹는 모양")]
		[field: SerializeField, Tooltip("녹은 정도. 0 멀쩡, 1 전부 사라짐")]
		public AnimationCurve DissolveCurve { get; private set; } = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
		[field: SerializeField, Min(0.1f), Tooltip("무늬 크기. 클수록 잘게 녹음")]
		public float NoiseScale { get; private set; } = 5f;
		[field: SerializeField, Min(0f), Tooltip("잔상마다 무늬가 달라지게 무늬를 미는 범위")]
		public float NoiseSeedRange { get; private set; } = 50f;
		[field: SerializeField, Range(-1f, 1f), Tooltip("세로 치우침. 양수면 위부터, 음수면 아래부터, 0 이면 얼룩으로 녹음")]
		public float VerticalBias { get; private set; } = 0.6f;
		[field: SerializeField, Range(0f, 0.5f), Tooltip("녹는 가장자리의 밝은 띠 폭")]
		public float EdgeWidth { get; private set; } = 0.12f;
		[field: SerializeField, ColorUsage(true, true), Tooltip("가장자리 띠 색. 알파는 세기")]
		public Color EdgeColor { get; private set; } = new(1f, 0.94f, 0.66f, 1f);

		[field: Header("변형")]
		[field: SerializeField, Tooltip("가로 배율. 발밑 기준")]
		public AnimationCurve WidthCurve { get; private set; } = AnimationCurve.Linear(0f, 1f, 1f, 1f);
		[field: SerializeField, Tooltip("세로 배율. 발밑 기준. 1 아래로 내리면 납작하게 찌그러짐")]
		public AnimationCurve HeightCurve { get; private set; } = AnimationCurve.Linear(0f, 1f, 1f, 1f);
		[field: SerializeField, Tooltip("위로 떠오르는 높이 (몸 크기 1 기준 월드 단위)")]
		public float RiseHeight { get; private set; } = 0f;
		[field: SerializeField, Tooltip("떠오르는 정도. 0 제자리, 1 RiseHeight")]
		public AnimationCurve RiseCurve { get; private set; } = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		[field: Header("색")]
		[field: SerializeField, Tooltip("처음에 번쩍이는 색. 알파는 최대 세기")]
		public Color FlashColor { get; private set; } = Color.white;
		[field: SerializeField, Tooltip("번쩍임 세기. 1 이면 FlashColor 로 완전히 덮임")]
		public AnimationCurve FlashCurve { get; private set; } = AnimationCurve.Linear(0f, 1f, 1f, 0f);
		[field: SerializeField, Tooltip("전체 투명도")]
		public AnimationCurve AlphaCurve { get; private set; } = AnimationCurve.Linear(0f, 1f, 1f, 1f);

		[field: Header("처치 파편")]
		[field: SerializeField, Tooltip("파편 재질 (URP Particles/Unlit 가산). 비우면 파편 없음. 같은 재질끼리 ParticleSystem 하나를 나눠 씀")]
		public Material SparkMaterial { get; private set; }
		[field: SerializeField, Range(0, 32), Tooltip("한 번에 튀는 파편 수. 0 이면 파편 없음")]
		public int SparkCount { get; private set; } = 8;
		[field: SerializeField, Min(1), Tooltip("동시에 떠 있을 수 있는 입자 수 (흰 심 포함). 넘으면 새 입자는 생략")]
		public int SparkMaxParticles { get; private set; } = 2048;
		[field: SerializeField, Min(0f), Tooltip("처음 속도 최소 (월드 단위/초)")]
		public float SparkSpeedMin { get; private set; } = 6f;
		[field: SerializeField, Min(0f), Tooltip("처음 속도 최대 (월드 단위/초)")]
		public float SparkSpeedMax { get; private set; } = 10f;
		[field: SerializeField, Min(0f), Tooltip("감속. 초당 속도가 줄어드는 비율. 클수록 빨리 멈춤")]
		public float SparkDrag { get; private set; } = 9f;
		[field: SerializeField, Min(0.01f), Tooltip("수명 최소 (초)")]
		public float SparkLifetimeMin { get; private set; } = 0.25f;
		[field: SerializeField, Min(0.01f), Tooltip("수명 최대 (초)")]
		public float SparkLifetimeMax { get; private set; } = 0.4f;
		[field: SerializeField, Min(0f), Tooltip("굵기 최소 (월드 단위)")]
		public float SparkSizeMin { get; private set; } = 0.1f;
		[field: SerializeField, Min(0f), Tooltip("굵기 최대 (월드 단위)")]
		public float SparkSizeMax { get; private set; } = 0.16f;
		[field: SerializeField, Tooltip("수명 동안 크기 비율. 가로축 수명 0 ~ 1")]
		public AnimationCurve SparkSizeCurve { get; private set; } = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
		[field: SerializeField, Min(0f), Tooltip("속도 방향 늘림. 속도 1 당 길이")]
		public float SparkStretch { get; private set; } = 0.05f;
		[field: SerializeField, Min(0f), Tooltip("굵기 대비 기본 길이")]
		public float SparkLengthScale { get; private set; } = 1.2f;
		[field: SerializeField, Tooltip("위쪽 성분 최소 (수평 1 기준)")]
		public float SparkUpwardMin { get; private set; } = 0.1f;
		[field: SerializeField, Tooltip("위쪽 성분 최대 (수평 1 기준)")]
		public float SparkUpwardMax { get; private set; } = 0.6f;
		[field: SerializeField, Range(0f, 1f), Tooltip("방향 흔들림. 0 고른 간격, 1 한 칸 폭까지 흔들림")]
		public float SparkAngleJitter { get; private set; } = 0.7f;
		[field: SerializeField, Tooltip("파편 기본 색. 알파는 세기")]
		public Color SparkColor { get; private set; } = new(1f, 0.9f, 0.6f, 1f);
		[field: SerializeField, Range(0f, 1f), Tooltip("몸 색 (그림 평균 색과 렌더러 색) 을 섞는 비율. 0 이면 기본 색만")]
		public float SparkBodyColorWeight { get; private set; } = 0.65f;
		[field: SerializeField, Range(0f, 1f), Tooltip("몸 색을 흰색 쪽으로 밝히는 정도")]
		public float SparkBodyColorLift { get; private set; } = 0.35f;
		[field: SerializeField, Tooltip("파편 가운데 심 색")]
		public Color SparkCoreColor { get; private set; } = Color.white;
		[field: SerializeField, Range(0f, 1f), Tooltip("심 굵기 비율. 0 이면 심 없음")]
		public float SparkCoreSizeRatio { get; private set; } = 0.45f;

		[field: Header("처치 고리")]
		[field: SerializeField, Tooltip("발밑 고리 재질 (WM/GroundPuff 셰이더). 비우면 고리 없음")]
		public Material RingMaterial { get; private set; }
		[field: SerializeField, Min(0.01f), Tooltip("고리가 사라질 때까지 걸리는 시간 (초)")]
		public float RingSeconds { get; private set; } = 0.25f;
		[field: SerializeField, Min(1), Tooltip("동시에 떠 있을 수 있는 처치 고리 수. 넘으면 새 고리는 생략")]
		public int MaxRings { get; private set; } = 48;
		[field: SerializeField, Min(0f), Tooltip("고리의 최대 반지름 (월드 단위). 몬스터의 고리 크기 배율이 곱해짐")]
		public float RingRadius { get; private set; } = 1.1f;
		[field: SerializeField, Tooltip("반지름 비율. 1 이면 RingRadius")]
		public AnimationCurve RingRadiusCurve { get; private set; } = new(new Keyframe(0f, 0.2f, 2.6f, 2.6f), new Keyframe(1f, 1f, 0.2f, 0.2f));
		[field: SerializeField, Tooltip("가운데 빈 정도. 1 에 가까울수록 가는 고리")]
		public AnimationCurve RingHollowCurve { get; private set; } = AnimationCurve.Linear(0f, 0.7f, 1f, 0.92f);
		[field: SerializeField, Tooltip("투명도 비율")]
		public AnimationCurve RingAlphaCurve { get; private set; } = AnimationCurve.Linear(0f, 1f, 1f, 0f);
		[field: SerializeField, Tooltip("고리 색. 알파는 최대 진하기")]
		public Color RingColor { get; private set; } = new(1f, 0.96f, 0.82f, 0.85f);
		[field: SerializeField, Range(0.01f, 1f), Tooltip("고리 가장자리의 부드러움")]
		public float RingSoftness { get; private set; } = 0.06f;
		[field: SerializeField, Tooltip("바닥에서 띄우는 높이. 바닥과 겹쳐 깜빡이지 않게")]
		public float RingGroundOffset { get; private set; } = 0.03f;

		[Header("미리보기")]
		[SerializeField, Tooltip("미리보기에서 녹일 그림")] private Sprite previewSprite;
		[SerializeField, Min(0.01f), Tooltip("미리보기 그림 크기 배율")] private float previewSize = 1f;
		[SerializeField, Range(0f, 2f), Tooltip("미리보기의 빌보드 방식. 0 카메라 회전 복사, 1 카메라 위치 기준, 2 카메라 회전 기준")]
		private float previewBillboardMode = 0f;

		Material IGroundPuffStyle.PuffMaterial => RingMaterial;
		float IGroundPuffStyle.PuffSeconds => RingSeconds;
		int IGroundPuffStyle.MaxPuffs => MaxRings;
		float IGroundPuffStyle.PuffRadius => RingRadius;
		AnimationCurve IGroundPuffStyle.PuffRadiusCurve => RingRadiusCurve;
		AnimationCurve IGroundPuffStyle.PuffHollowCurve => RingHollowCurve;
		AnimationCurve IGroundPuffStyle.PuffAlphaCurve => RingAlphaCurve;
		Color IGroundPuffStyle.PuffColor => RingColor;
		float IGroundPuffStyle.PuffSoftness => RingSoftness;
		float IGroundPuffStyle.PuffGroundOffset => RingGroundOffset;

		/// <summary> 처치 순간의 파편과 발밑 고리. 잔상과 별개. 재질이 빈 쪽은 생략. 고리 번호 (없으면 NONE) 를 돌려줌 </summary>
		public int PlayBurst(in MonsterDeathGhostPose pose, Vector3 groundPosition, float sizeMultiplier)
		{
			MonsterLifeFxDriver driver = MonsterLifeFxDriver.Instance;
			if (SparkMaterial != null && SparkCount > 0)
				driver.EmitSparks(pose, this);
			return RingMaterial != null ? driver.AddRing(groundPosition, this, sizeMultiplier) : MonsterLifeFxDriver.NONE;
		}

		/// <summary> 미리보기. 플레이어 앞 Radius 거리에 PreviewSprite 를 세우고 녹임 </summary>
		// 미리보기 전용. 플레이어 뒤에 가려지지 않게 카메라 기준 오른쪽에 놓음 (실제 몬스터는 MonsterLifeFx 경로)
		private static Vector3 PreviewSide()
		{
			Vector3 side = Camera.main.transform.right;
			side.y = 0f;
			return side.normalized;
		}

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			MonsterDeathGhostPose pose = new()
			{
				Sprite = previewSprite,
				Color = Color.white,
				Position = context.Position + PreviewSide() * context.Radius,
				Rotation = Camera.main.transform.rotation,
				Scale = Vector3.one * previewSize,
				BillboardMode = previewBillboardMode,
			};

			MonsterLifeFxDriver driver = MonsterLifeFxDriver.Instance;
			int ghostId = driver.AddGhost(pose, this, context.Seconds);
			int ringId = PlayBurst(pose, pose.Position, previewSize);
			return () =>
			{
				if (driver == null)
					return;
				driver.StopGhost(ghostId);
				driver.StopPuff(ringId);
			};
		}
	}
}
