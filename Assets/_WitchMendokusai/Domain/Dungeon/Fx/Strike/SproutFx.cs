using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 땅에서 솟거나 하늘에서 떨어져 꽂히는 물체 하나. 예고 시작에 재생하고 Seconds 가 때리는 순간
	// Hold 가 있으면 그 수명대로 남고, 아픈 동안 달아올라 맥동하고 식으면 어두워짐. 금이 가면 금 간 그림으로 떨다 사라짐
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
		[SerializeField, Min(0f)] private float trembleSpeed = 70f;
		[SerializeField, Tooltip("예고 동안 흙더미 크기 비율"), Range(0f, 1f)] private float budMoundRatio = 0.7f;

		[Header("떨어짐 (높이가 있으면 싹 대신)")]
		[SerializeField, Tooltip("떨어지기 시작하는 높이 (m). 0 이면 땅에서 솟음"), Min(0f)] private float fallHeight;
		[SerializeField, Min(0.02f)] private float fallSeconds = 0.32f;
		[SerializeField, Tooltip("떨어질 때 그림. 비우면 꽂힌 그림")] private Sprite fallSprite;
		[SerializeField, Tooltip("떨어질 때 세로로 늘어나는 비율"), Range(0f, 0.8f)] private float fallStretch = 0.15f;

		[Header("솟음과 남음")]
		[SerializeField, Tooltip("솟기 시작을 때리는 순간보다 이만큼 앞당김 (초). 피해가 그림보다 먼저 오지 않게"), Min(0f)] private float leadSeconds = 0.05f;
		[SerializeField, Min(0.01f)] private float riseSeconds = 0.09f;
		[SerializeField, Tooltip("솟을 때 세로로 더 늘어나는 비율"), Range(0f, 0.8f)] private float overshoot = 0.28f;
		[SerializeField, Tooltip("솟은 뒤 서 있는 초"), Min(0f)] private float holdSeconds = 0.8f;
		[SerializeField, Min(0.01f)] private float sinkSeconds = 0.28f;
		[SerializeField, Tooltip("때리는 순간 달아오르는 색")] private Color strikeTint = new(1f, 0.82f, 0.55f, 1f);
		[SerializeField, Tooltip("남아서 아픈 동안 맥동하는 색")] private Color hotTint = Color.white;
		[SerializeField, Tooltip("식어서 안 아픈 색")] private Color coolTint = Color.white;
		[SerializeField, Min(0f)] private float hotPulseSpeed = 7f;
		[SerializeField, Tooltip("맥동하며 옅어지는 폭"), Range(0f, 1f)] private float hotPulse = 0.7f;
		[SerializeField, Min(0.01f)] private float strikeTintSeconds = 0.25f;
		[SerializeField, Tooltip("솟은 뒤 찰랑임이 잦아드는 빠르기"), Min(0f)] private float wobbleDamping = 11f;
		[SerializeField, Min(0f)] private float wobbleSpeed = 30f;
		[SerializeField, Tooltip("금 간 그림. 비우면 그대로")] private Sprite crackedSprite;

		[Header("흙")]
		[SerializeField, Tooltip("밑동을 덮는 흙더미. 비우면 없음")] private Sprite moundSprite;
		[SerializeField, Min(0.05f)] private float moundScale = 1.1f;
		[SerializeField, Tooltip("흙더미를 카메라 쪽으로 당기는 거리 (m)"), Min(0f)] private float moundDepth = 0.15f;
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
			Sprite settled = body.sprite;
			body.flipX = Random.value < 0.5f;
			float scale = Random.Range(scaleRange.x, scaleRange.y);
			float height = body.sprite.bounds.size.y * scale;
			Transform root = body.transform;

			SpriteRenderer mound = null;
			if (moundSprite != null)
			{
				mound = FxSpritePool.Rent(runner, moundSprite, material);
				rented.Add(mound);
			}

			float budSeconds = Mathf.Max(0f, context.Seconds - leadSeconds);
			for (float time = 0f; time < budSeconds; time += Time.deltaTime)
			{
				float progress = time / budSeconds;
				if (fallHeight > 0f)
				{
					Fall(body, ground, scale, budSeconds - time);
					SetMound(mound, ground, 0f);
					yield return null;
					continue;
				}
				float rise = Mathf.Min(1f, progress * 4f);
				float shake = Mathf.Sin(time * trembleSpeed) * budTremble * progress;
				Set(root, ground, shake, scale, 1f, height * (budRatio * rise - 1f));
				SetMound(mound, ground, scale * rise * budMoundRatio);
				yield return null;
			}

			onBurst();
			body.sprite = settled;
			body.enabled = true;
			Burst(ground);
			Chunk[] chunks = ThrowChunks(runner, ground, rented);
			FxHold hold = context.Hold;
			float sinkTime = 0f;
			for (float time = 0f; time < chunkSeconds || sinkTime < sinkSeconds; time += Time.deltaTime)
			{
				bool isOver = hold != null ? Time.time >= hold.EndTime : time >= riseSeconds + holdSeconds;
				if (hold != null && hold.IsGone)
					sinkTime = sinkSeconds;
				else if (isOver)
					sinkTime += Time.deltaTime;
				float up = fallHeight > 0f ? 1f : Mathf.Clamp01(time / riseSeconds);
				float settle = Mathf.Max(0f, time - riseSeconds);
				// 늘어났다가 찰랑이며 제 키로
				float stretch = 1f + overshoot * up * Mathf.Exp(-settle * wobbleDamping) * Mathf.Cos(settle * wobbleSpeed);
				float sink = Mathf.Clamp01(sinkTime / sinkSeconds);
				bool isCracked = hold != null && hold.IsCracked;
				body.sprite = isCracked && crackedSprite != null ? crackedSprite : settled;
				Set(root, ground, isCracked ? Mathf.Sin(Time.time * trembleSpeed) * budTremble : 0f, scale, stretch, height * (up - 1f) - height * 1.05f * sink * sink);
				body.color = Color.Lerp(strikeTint, RestTint(hold), Mathf.Clamp01(settle / strikeTintSeconds));
				body.enabled = sink < 1f;
				SetMound(mound, ground, scale * (1f - sink));
				if (chunks.Length > 0 && time >= chunkSeconds)
				{
					ReturnChunks(runner, chunks, rented);
					chunks = Array.Empty<Chunk>();
				}
				MoveChunks(chunks, ground, time);
				yield return null;
			}
		}

		private Color RestTint(FxHold hold)
		{
			if (hold == null)
				return Color.white;
			if (hold.IsHot(Time.time))
				return Color.Lerp(Color.white, hotTint, 1f - hotPulse * (0.5f - 0.5f * Mathf.Sin(Time.time * hotPulseSpeed)));
			return coolTint;
		}

		// left 는 꽂히기까지 남은 초
		private void Fall(SpriteRenderer body, Vector3 ground, float scale, float left)
		{
			body.enabled = left <= fallSeconds;
			if (fallSprite != null)
				body.sprite = fallSprite;
			float remaining = Mathf.Clamp01(left / fallSeconds);
			// 점점 빨라지며 떨어짐
			float fallen = 1f - remaining;
			Set(body.transform, ground + Vector3.up * (fallHeight * (1f - fallen * fallen)), 0f, scale, 1f + fallStretch, 0f);
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

		private void SetMound(SpriteRenderer mound, Vector3 ground, float size)
		{
			if (mound == null)
				return;
			Camera view = Camera.main;
			Vector3 toward = view == null ? Vector3.zero : (view.transform.position - ground).normalized * moundDepth;
			mound.transform.position = ground + toward + Vector3.up * (0.1f * size);
			mound.transform.localScale = Vector3.one * (moundScale * size);
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

		// 물체는 오래 남아도 흙덩이는 바로 돌려줌
		private static void ReturnChunks(CombatFxRunner runner, Chunk[] chunks, List<SpriteRenderer> rented)
		{
			foreach (Chunk chunk in chunks)
			{
				rented.Remove(chunk.Body);
				FxSpritePool.Return(runner, chunk.Body);
			}
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
