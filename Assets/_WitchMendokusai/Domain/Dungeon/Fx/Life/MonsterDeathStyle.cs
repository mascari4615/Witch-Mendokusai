using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 사망 연출 한 종류. 본체는 즉시 꺼지고, 사망 순간의 모습을 복사한 잔상이 이 값대로 녹음
	/// - 곡선의 가로축은 전부 진행도 0 ~ 1 (Seconds 동안)
	/// - 쓰는 쪽: MonsterLifeFx 의 사망 스타일 칸, 또는 MonsterDeathGhost.Play
	/// - 미리보기 (fx 명령, 우클릭 Preview) 는 PreviewSprite 로 재생
	/// </summary>
	[CreateAssetMenu(fileName = "Death_0_New", menuName = "WM/CombatFx/Monster Death Style")]
	public class MonsterDeathStyle : CombatFx
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

		[Header("미리보기")]
		[SerializeField, Tooltip("미리보기에서 녹일 그림")] private Sprite previewSprite;
		[SerializeField, Min(0.01f), Tooltip("미리보기 그림 크기 배율")] private float previewSize = 1f;
		[SerializeField, Range(0f, 2f), Tooltip("미리보기의 빌보드 방식. 0 카메라 회전 복사, 1 카메라 위치 기준, 2 카메라 회전 기준")]
		private float previewBillboardMode = 0f;

		/// <summary> 미리보기. 플레이어 앞 Radius 거리에 PreviewSprite 를 세우고 녹임 </summary>
		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			MonsterDeathGhostPose pose = new()
			{
				Sprite = previewSprite,
				Color = Color.white,
				Position = context.Position + context.Direction.normalized * context.Radius,
				Rotation = Camera.main.transform.rotation,
				Scale = Vector3.one * previewSize,
				BillboardMode = previewBillboardMode,
			};

			MonsterLifeFxDriver driver = MonsterLifeFxDriver.Instance;
			int ghostId = driver.AddGhost(pose, this, context.Seconds);
			return () =>
			{
				if (driver != null)
					driver.StopGhost(ghostId);
			};
		}
	}
}
