using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 땅에서 솟는 물체 하나. 예고 시작에 재생하고 Seconds 가 때리는 순간
	// 싹 (끝만 나와 떨림) -> 솟음 -> 굳어 남음 -> 들어감. 예고, 타격, 남는 것이 한 물체의 성장 단계
	[CreateAssetMenu(fileName = "Sprout_0", menuName = "WM/CombatFx/" + nameof(SproutFx))]
	public class SproutFx : CombatFx
	{
		[Header("그림")]
		[SerializeField] private Sprite[] sprites;
		[SerializeField, Tooltip("카메라를 보는 재질")] private Material material;
		[SerializeField, Tooltip("크기 배율 최소, 최대")] private Vector2 scaleRange = new(0.9f, 1.1f);
		[SerializeField, Tooltip("자리 흔들림 (m)"), Min(0f)] private float jitter = 0.12f;

		[Header("싹 (예고)")]
		[SerializeField, Tooltip("예고 동안 나와 있는 높이 비율"), Range(0f, 0.6f)] private float budRatio = 0.16f;
		[SerializeField, Tooltip("예고 동안 떨리는 폭 (m)"), Min(0f)] private float budTremble = 0.05f;

		[Header("솟음과 남음")]
		[SerializeField, Min(0.01f)] private float riseSeconds = 0.09f;
		[SerializeField, Tooltip("솟을 때 세로로 더 늘어나는 비율"), Range(0f, 0.8f)] private float overshoot = 0.28f;
		[SerializeField, Tooltip("솟은 뒤 서 있는 초"), Min(0f)] private float holdSeconds = 0.8f;
		[SerializeField, Min(0.01f)] private float sinkSeconds = 0.28f;
		[SerializeField, Tooltip("때리는 순간 달아오르는 색")] private Color strikeTint = new(1f, 0.82f, 0.55f, 1f);

		[Header("흙")]
		[SerializeField] private Sprite[] chunkSprites;
		[SerializeField, Min(0)] private int chunkCount = 5;
		[SerializeField, Min(0f)] private float chunkSpeed = 4f;
		[SerializeField, Min(0.05f)] private float chunkSeconds = 0.55f;
		[SerializeField, Min(0.05f)] private float chunkScale = 0.5f;
		[SerializeField, Tooltip("바닥에 남는 자국 반지름 (m). 0 이면 없음"), Min(0f)] private float stainRadius = 0.6f;
		[SerializeField] private Color stainColor = new(0.23f, 0.14f, 0.08f, 0.6f);

		[Header("순간")]
		[SerializeField, Tooltip("던전 안 흔들림, 조각, 고리")] private FeelMoment moment = new();
		[SerializeField] private CombatSfxEvent sound = CombatSfxEvent.WeaponSwing;

		private struct Chunk
		{
			public SpriteRenderer Body;
			public Vector3 Velocity;
			public float Scale;
		}

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			List<SpriteRenderer> rented = new();
			bool hasBurst = false;
			Action stop = runner.Run(Routine(runner, context, rented, () => hasBurst = true), () =>
			{
				foreach (SpriteRenderer body in rented)
					FxSpritePool.Return(runner, body);
			});
			// 솟은 뒤에는 공격이 끊겨도 스스로 들어감
			return () =>
			{
				if (hasBurst == false)
					stop();
			};
		}

		private IEnumerator Routine(CombatFxRunner runner, CombatFxContext context, List<SpriteRenderer> rented, Action onBurst)
		{
			Vector2 offset = Random.insideUnitCircle * jitter;
			Vector3 ground = context.Position + new Vector3(offset.x, 0f, offset.y);
			SpriteRenderer body = FxSpritePool.Rent(runner, sprites[Random.Range(0, sprites.Length)], material);
			rented.Add(body);
			body.flipX = Random.value < 0.5f;
			float scale = Random.Range(scaleRange.x, scaleRange.y);
			float height = body.sprite.bounds.size.y * scale;
			Transform root = body.transform;

			float budSeconds = Mathf.Max(0f, context.Seconds);
			for (float time = 0f; time < budSeconds; time += Time.deltaTime)
			{
				float progress = time / budSeconds;
				// 쑥 나왔다가 때리기 직전 움츠림
				float rise = Mathf.Min(1f, progress * 5f) - Mathf.Clamp01((progress - 0.85f) / 0.15f) * 0.6f;
				float shake = Mathf.Sin(time * 70f) * budTremble * progress;
				Set(root, ground, shake, scale, 1f, height * (budRatio * rise - 1f));
				yield return null;
			}

			onBurst();
			Burst(ground);
			Chunk[] chunks = ThrowChunks(runner, ground, rented);
			float total = riseSeconds + holdSeconds + sinkSeconds;
			float end = Mathf.Max(total, chunkSeconds);
			for (float time = 0f; time < end; time += Time.deltaTime)
			{
				float up = Mathf.Clamp01(time / riseSeconds);
				float settle = Mathf.Max(0f, time - riseSeconds);
				// 늘어났다가 찰랑이며 제 키로
				float stretch = 1f + overshoot * up * Mathf.Exp(-settle * 11f) * Mathf.Cos(settle * 30f);
				float sink = Mathf.Clamp01((time - riseSeconds - holdSeconds) / sinkSeconds);
				Set(root, ground, 0f, scale, stretch, height * (up - 1f) - height * 1.05f * sink * sink);
				body.color = Color.Lerp(strikeTint, Color.white, Mathf.Clamp01(settle / 0.25f));
				body.enabled = time < total;
				MoveChunks(chunks, ground, time);
				yield return null;
			}
		}

		// 카메라를 보게 누운 판의 세로축을 따라 밀어 넣음. 월드 위아래로 옮기면 나오는 점이 자리에서 밀림
		private static void Set(Transform root, Vector3 ground, float shake, float scale, float stretch, float lift)
		{
			Camera view = Camera.main;
			Vector3 right = Vector3.right;
			Vector3 up = Vector3.up;
			if (view != null)
			{
				Vector3 forward = (ground - view.transform.position).normalized;
				right = Vector3.Cross(Vector3.up, forward).normalized;
				up = Vector3.Cross(forward, right);
			}
			root.position = ground + up * lift + right * shake;
			root.localScale = new Vector3(scale / Mathf.Sqrt(stretch), scale * stretch, scale);
		}

		private void Burst(Vector3 ground)
		{
			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.OnCustomMoment(moment, ground);
			if (RemainsWorld.Current != null)
			{
				RemainsWorld.Current.Blast(ground, 1f, 3f, 2f, false);
				if (stainRadius > 0f)
					RemainsWorld.Current.Stain(ground, stainRadius, stainColor);
			}
			CombatSfx.Play(sound, ground);
		}

		private Chunk[] ThrowChunks(CombatFxRunner runner, Vector3 ground, List<SpriteRenderer> rented)
		{
			int count = chunkSprites.Length == 0 ? 0 : chunkCount;
			Chunk[] chunks = new Chunk[count];
			for (int i = 0; i < count; i++)
			{
				SpriteRenderer body = FxSpritePool.Rent(runner, chunkSprites[Random.Range(0, chunkSprites.Length)], material);
				rented.Add(body);
				Vector2 side = Random.insideUnitCircle.normalized * Random.Range(0.35f, 1f);
				body.transform.position = ground + new Vector3(side.x, 0.4f, side.y) * 0.25f;
				chunks[i] = new Chunk { Body = body, Velocity = new Vector3(side.x * 0.6f, Random.Range(0.8f, 1.3f), side.y * 0.6f) * chunkSpeed, Scale = chunkScale * Random.Range(0.6f, 1.2f) };
			}
			return chunks;
		}

		private void MoveChunks(Chunk[] chunks, Vector3 ground, float time)
		{
			float progress = Mathf.Clamp01(time / chunkSeconds);
			foreach (Chunk chunk in chunks)
			{
				chunk.Body.enabled = progress < 1f;
				Vector3 position = chunk.Body.transform.position + chunk.Velocity * Time.deltaTime + Physics.gravity * (2.2f * time * Time.deltaTime);
				position.y = Mathf.Max(position.y, ground.y + 0.1f);
				chunk.Body.transform.position = position;
				chunk.Body.transform.localScale = Vector3.one * (chunk.Scale * (1f - progress * progress));
			}
		}
	}
}
