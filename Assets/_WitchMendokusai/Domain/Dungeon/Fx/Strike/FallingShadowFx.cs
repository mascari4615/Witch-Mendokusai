using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 하늘에서 떨어질 몸의 바닥 그림자. Follow 를 따라다니며 Seconds 동안 짙어지고 커져 Radius 를 채움
	[CreateAssetMenu(fileName = "Shadow_0", menuName = "WM/CombatFx/" + nameof(FallingShadowFx))]
	public class FallingShadowFx : CombatFx
	{
		private const int SHADOW_QUEUE = 2991;

		[SerializeField, Tooltip("셰이더와 잡음을 빌리는 묶음")] private PaintedFxSet set;
		[SerializeField, Tooltip("위에서 본 그림자. 알파만 씀")] private Texture2D texture;
		[SerializeField] private Color color = new(0.12f, 0.06f, 0.08f, 0.75f);
		[SerializeField, Tooltip("처음 크기와 짙기 (끝의 배)"), Range(0f, 1f)] private float startRatio = 0.35f;
		[SerializeField, Tooltip("그림 반 변 (Radius 의 배)"), Min(0.1f)] private float sizeRatio = 1.1f;
		[SerializeField, Tooltip("나타나는 초"), Min(0.01f)] private float appearSeconds = 0.35f;
		[SerializeField, Tooltip("출렁이는 폭 (크기의 비율)"), Range(0f, 0.3f)] private float sway = 0.06f;
		[SerializeField, Min(0f)] private float swaySpeed = 9f;
		[SerializeField, Min(0f)] private float lift = 0.05f;

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			GameObject root = new(name);
			root.transform.SetParent(runner.transform, false);
			FxQuadBatch batch = new(name, root.transform, set.Shader, texture, set.Noise, SHADOW_QUEUE);
			return runner.Run(Routine(context, batch), () =>
			{
				batch.Destroy();
				if (root != null)
					Destroy(root);
			});
		}

		private IEnumerator Routine(CombatFxContext context, FxQuadBatch batch)
		{
			float startTime = Time.time;
			for (float time = 0f; time < context.Seconds; time = Time.time - startTime)
			{
				if (context.Follow != null && context.Follow.gameObject.activeInHierarchy == false)
					yield break;
				float progress = time / context.Seconds;
				Vector3 center = (context.Follow != null ? context.Follow.position : context.Position) + Vector3.up * lift;
				float near = Mathf.Lerp(startRatio, 1f, progress * progress);
				float half = context.Radius * sizeRatio * near;
				float wave = 1f + Mathf.Sin(time * swaySpeed) * sway;
				Color faded = color;
				faded.a *= near * Mathf.Clamp01(time / appearSeconds);
				batch.Begin();
				batch.Add(center, Vector3.right * (half * wave), Vector3.forward * (half / wave), FxQuadLook.Of(faded, 1f));
				batch.Commit();
				yield return null;
			}
		}
	}
}
