using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 등장 연출 한 종류. 몸이 작게 시작해 살짝 넘쳤다가 제 크기로 돌아오고, 발밑에 짧은 먼지 고리
	/// - 곡선의 가로축은 전부 진행도 0 ~ 1
	/// - 몸 크기는 MonsterLifeFx 가 지정한 트랜스폼에만 곱함 (피격 부풀림, 정예 배율이 쓰는 트랜스폼과 다른 것)
	/// - 미리보기 (fx 명령, 우클릭 Preview) 는 PreviewSprite 로 재생
	/// </summary>
	[CreateAssetMenu(fileName = "Spawn_0_New", menuName = "WM/CombatFx/Monster Spawn Style")]
	public class MonsterSpawnStyle : CombatFx, IGroundPuffStyle
	{
		private const string PREVIEW_KEY = "MonsterSpawnPreview";

		[field: Header("몸 튕김")]
		[field: SerializeField, Min(0f), Tooltip("튕김이 끝날 때까지 걸리는 시간 (초). 0 이면 튕김 없음")]
		public float PopSeconds { get; private set; } = 0.26f;
		[SerializeField, Tooltip("가로 배율. 끝 값은 1")]
		private AnimationCurve widthCurve = new(new Keyframe(0f, 0.6f), new Keyframe(0.45f, 1.1f), new Keyframe(1f, 1f));
		[SerializeField, Tooltip("세로 배율. 끝 값은 1")]
		private AnimationCurve heightCurve = new(new Keyframe(0f, 0.6f), new Keyframe(0.45f, 1.1f), new Keyframe(1f, 1f));

		[field: Header("발밑 먼지 고리")]
		[field: SerializeField, Tooltip("고리를 그릴 재질 (WM/GroundPuff 셰이더). 비우면 고리 없음")]
		public Material PuffMaterial { get; private set; }
		[field: SerializeField, Min(0.01f), Tooltip("고리가 사라질 때까지 걸리는 시간 (초)")]
		public float PuffSeconds { get; private set; } = 0.32f;
		[field: SerializeField, Min(1), Tooltip("동시에 떠 있을 수 있는 고리 수. 넘으면 새 고리는 생략")]
		public int MaxPuffs { get; private set; } = 32;
		[field: SerializeField, Min(0f), Tooltip("고리의 최대 반지름 (월드 단위)")]
		public float PuffRadius { get; private set; } = 0.9f;
		[field: SerializeField, Tooltip("반지름 비율. 1 이면 PuffRadius")]
		public AnimationCurve PuffRadiusCurve { get; private set; } = AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f);
		[field: SerializeField, Tooltip("가운데 빈 정도. 0 꽉 찬 원판, 1 에 가까울수록 가는 고리")]
		public AnimationCurve PuffHollowCurve { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);
		[field: SerializeField, Tooltip("투명도 비율")]
		public AnimationCurve PuffAlphaCurve { get; private set; } = AnimationCurve.Linear(0f, 1f, 1f, 0f);
		[field: SerializeField, Tooltip("고리 색. 알파는 최대 진하기")]
		public Color PuffColor { get; private set; } = new(1f, 0.98f, 0.92f, 0.7f);
		[field: SerializeField, Range(0.01f, 1f), Tooltip("고리 가장자리의 부드러움")]
		public float PuffSoftness { get; private set; } = 0.25f;
		[field: SerializeField, Tooltip("바닥에서 띄우는 높이. 바닥과 겹쳐 깜빡이지 않게")]
		public float PuffGroundOffset { get; private set; } = 0.03f;

		[Header("미리보기")]
		[SerializeField, Tooltip("미리보기에서 튕길 그림")] private Sprite previewSprite;
		[SerializeField, Min(0.01f), Tooltip("미리보기 그림 크기 배율")] private float previewSize = 1f;
		[SerializeField, Min(0f), Tooltip("튕김이 끝난 뒤 미리보기 그림이 남는 시간 (초)")] private float previewHoldSeconds = 0.6f;

		/// <summary> 진행도 (0 ~ 1) 의 몸 크기 배율. 기준 크기에 곱해 씀 </summary>
		public Vector3 EvaluatePop(float progress)
		{
			float width = widthCurve.Evaluate(progress);
			return new Vector3(width, heightCurve.Evaluate(progress), width);
		}

		/// <summary> 발밑 고리만 재생. 상한을 넘었거나 재질이 비었으면 생략 </summary>
		public void PlayPuff(Vector3 groundPosition, float sizeMultiplier)
		{
			if (PuffMaterial != null)
				MonsterLifeFxDriver.Instance.AddPuff(groundPosition, this, sizeMultiplier);
		}

		/// <summary> 미리보기. 플레이어 앞 Radius 거리에 PreviewSprite 를 세우고 튕김 </summary>
		// 미리보기 전용. 플레이어 뒤에 가려지지 않게 카메라 기준 오른쪽에 놓음 (실제 몬스터는 MonsterLifeFx 경로)
		private static Vector3 PreviewSide()
		{
			Vector3 side = Camera.main.transform.right;
			side.y = 0f;
			return side.normalized;
		}

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			Vector3 position = context.Position + PreviewSide() * context.Radius;
			MonsterLifeFxDriver driver = MonsterLifeFxDriver.Instance;
			int puffId = PuffMaterial != null ? driver.AddPuff(position, this, previewSize) : MonsterLifeFxDriver.NONE;

			GameObject body = runner.Rent(PREVIEW_KEY, CreatePreviewBody);
			body.GetComponent<SpriteRenderer>().sprite = previewSprite;
			body.transform.SetPositionAndRotation(position, Camera.main.transform.rotation);

			return runner.Run(PreviewRoutine(body.transform), () =>
			{
				runner.Return(PREVIEW_KEY, body);
				if (driver != null)
					driver.StopPuff(puffId);
			});
		}

		private static GameObject CreatePreviewBody()
		{
			GameObject body = new(PREVIEW_KEY);
			body.AddComponent<SpriteRenderer>();
			return body;
		}

		private IEnumerator PreviewRoutine(Transform body)
		{
			Vector3 baseScale = Vector3.one * previewSize;
			float elapsed = 0f;
			while (elapsed < PopSeconds)
			{
				body.localScale = Vector3.Scale(baseScale, EvaluatePop(elapsed / PopSeconds));
				yield return null;
				elapsed += Time.deltaTime;
			}
			body.localScale = baseScale;
			yield return new WaitForSeconds(previewHoldSeconds);
		}
	}
}
