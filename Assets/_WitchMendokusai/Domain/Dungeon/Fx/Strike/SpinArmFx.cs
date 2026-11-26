using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 몸을 축으로 바닥 위에서 도는 물체 (시곗바늘). Turn 이 각도, Seconds 가 전체 길이. 자라 나오고 -> 돌고 -> 거둬짐
	// 도는 빠르기만큼 뒤로 잔상이 남음
	[CreateAssetMenu(fileName = "SpinArm_0", menuName = "WM/CombatFx/" + nameof(SpinArmFx))]
	public class SpinArmFx : CombatFx
	{
		// 몸 그림 (3000) 보다 먼저 그려 몸과 주인공이 바늘 위에
		private const int SHADOW_QUEUE = 2991;
		private const int TRAIL_QUEUE = 2992;
		private const int ARM_QUEUE = 2993;

		[SerializeField, Tooltip("셰이더와 잡음을 빌리는 묶음")] private PaintedFxSet set;
		[SerializeField, Tooltip("축이 왼쪽, 끝이 오른쪽인 그림")] private Texture2D texture;
		[SerializeField, ColorUsage(true, true)] private Color color = Color.white;
		[SerializeField, ColorUsage(true, true), Tooltip("예고와 방향을 바꾸려 멈춘 동안 달아오르는 색")] private Color stillColor = new(2.2f, 1.5f, 0.8f, 1f);
		[SerializeField, Tooltip("그림 폭 (판정 폭의 배)"), Min(0.1f)] private float widthRatio = 1.6f;
		[SerializeField, Tooltip("몸에서 이만큼 떨어진 데서 자라 나옴 (m). 몸 그림을 안 가리게. 판정과 별개"), Min(0f)] private float startOffset = 1.2f;
		[SerializeField, Tooltip("바닥에서 높이 (m)"), Min(0f)] private float height = 0.45f;
		[SerializeField, Tooltip("자라 나오는 초"), Min(0.01f)] private float growSeconds = 0.5f;
		[SerializeField, Tooltip("거둬지는 초"), Min(0.01f)] private float foldSeconds = 0.25f;
		[SerializeField, Tooltip("돌기 전 들썩이는 각도"), Min(0f)] private float tremble = 1.5f;
		[SerializeField, Min(0f)] private float trembleSpeed = 45f;
		[SerializeField, Min(0)] private int trailCount = 7;
		[SerializeField, Tooltip("잔상 하나가 뒤처지는 초"), Min(0.005f)] private float trailStep = 0.035f;
		[SerializeField, ColorUsage(true, true)] private Color trailColor = new(1.4f, 0.8f, 0.25f, 0.3f);
		[SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.4f);

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			GameObject root = new(name);
			root.transform.SetParent(runner.transform, false);
			FxQuadBatch shadow = new("Shadow", root.transform, set.Shader, texture, set.Noise, SHADOW_QUEUE);
			FxQuadBatch trail = new("Trail", root.transform, set.Shader, texture, set.Noise, TRAIL_QUEUE);
			FxQuadBatch arm = new("Arm", root.transform, set.Shader, texture, set.Noise, ARM_QUEUE);
			return runner.Run(Routine(context, shadow, trail, arm), () =>
			{
				shadow.Destroy();
				trail.Destroy();
				arm.Destroy();
				if (root != null)
					Destroy(root);
			});
		}

		private IEnumerator Routine(CombatFxContext context, FxQuadBatch shadow, FxQuadBatch trail, FxQuadBatch arm)
		{
			// 몸 없이 미리보기로는 돌 수 없음
			if (context.Follow == null || context.Turn == null)
				yield break;
			for (float time = 0f; time < context.Seconds; time += Time.deltaTime)
			{
				if (context.Follow == null || context.Follow.gameObject.activeInHierarchy == false)
					yield break;
				float grown = Mathf.Min(Mathf.Clamp01(time / growSeconds), Mathf.Clamp01((context.Seconds - time) / foldSeconds));
				grown = 1f - (1f - grown) * (1f - grown);
				float turned = context.Turn(time);
				// 아직 안 돌면 들썩임
				bool isStill = Mathf.Approximately(turned, context.Turn(time + trailStep));
				float angle = isStill ? turned + Mathf.Sin(time * trembleSpeed) * tremble : turned;
				shadow.Begin();
				trail.Begin();
				arm.Begin();
				Add(shadow, context, angle, grown, 0.05f, FxQuadLook.Of(shadowColor, 1f));
				for (int i = trailCount; i >= 1; i--)
				{
					Color faded = trailColor;
					faded.a *= 1f - (float)i / (trailCount + 1);
					float past = context.Turn(Mathf.Max(0f, time - trailStep * i));
					if (Mathf.Approximately(past, turned) == false)
						Add(trail, context, past, grown, height, FxQuadLook.Of(faded));
				}
				Add(arm, context, angle, grown, height, FxQuadLook.Of(isStill && grown >= 1f ? Color.Lerp(color, stillColor, 0.5f + 0.5f * Mathf.Sin(time * trembleSpeed * 0.4f)) : color, 1f));
				shadow.Commit();
				trail.Commit();
				arm.Commit();
				yield return null;
			}
		}

		private void Add(FxQuadBatch batch, CombatFxContext context, float angle, float grown, float lift, in FxQuadLook look)
		{
			Vector3 direction = Quaternion.Euler(0f, angle, 0f) * context.Direction;
			Vector3 side = Vector3.Cross(Vector3.up, direction);
			float inner = Mathf.Max(startOffset, context.InnerRadius);
			float length = (context.Length - inner) * grown;
			Vector3 center = context.Follow.position + direction * (inner + length * 0.5f) + Vector3.up * lift;
			batch.Add(center, direction * (length * 0.5f), -side * (context.Width * widthRatio * 0.5f), look);
		}
	}
}
