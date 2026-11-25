using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 그린 플립북 덩이. 하나면 터짐, 여럿에 travelRatio 가 있으면 시전자에서 뿜어 나가는 숨
	[CreateAssetMenu(fileName = "Puffs_0", menuName = "WM/CombatFx/" + nameof(PaintedPuffsFx))]
	public class PaintedPuffsFx : CombatFx
	{
		private const int PUFF_QUEUE = 3050;
		private const int RING_QUEUE = 2990;

		[Header("그림")]
		[SerializeField, Tooltip("셰이더, 잡음, 바닥 고리를 빌리는 묶음")] private PaintedFxSet set;
		[SerializeField] private Texture2D sheet;
		[SerializeField, Min(1)] private int columns = 4;
		[SerializeField, Min(1)] private int rows = 4;
		[SerializeField, Tooltip("쓰는 칸 (처음, 끝)")] private Vector2Int frames = new(0, 15);
		[SerializeField, ColorUsage(true, true)] private Color color = Color.white;
		[SerializeField, Tooltip("0 더하기, 1 덮기"), Range(0f, 1f)] private float cover = 0.85f;

		[Header("덩이")]
		[SerializeField, Min(1)] private int count = 1;
		[SerializeField, Tooltip("덩이 하나의 초"), Min(0.05f)] private float seconds = 0.7f;
		[SerializeField, Tooltip("덩이들이 나오는 데 걸리는 초"), Min(0f)] private float emitSeconds;
		[SerializeField, Tooltip("반 변 (m) 처음과 끝")] private Vector2 halfSize = new(1.4f, 1.8f);
		[SerializeField, Tooltip("바닥에서 높이 (m)")] private float lift = 0.7f;
		[SerializeField, Tooltip("뿜는 높이 (m). 입 높이"), Min(0f)] private float originLift = 1.2f;
		[SerializeField, Tooltip("Length 의 이만큼까지 감. 0 이면 제자리"), Range(0f, 1.2f)] private float travelRatio;
		[SerializeField, Tooltip("옆으로 흩어짐 (m)"), Min(0f)] private float spread;

		[Header("바닥 고리")]
		[SerializeField, Tooltip("고리 반지름 (Radius 배). 0 이면 없음"), Min(0f)] private float ringRatio = 1f;
		[SerializeField, Min(0.05f)] private float ringSeconds = 0.35f;
		[SerializeField, ColorUsage(true, true)] private Color ringColor = new(1f, 0.8f, 1.4f, 0.8f);

		[Header("순간")]
		[SerializeField] private FeelMoment moment = new();
		[SerializeField] private CombatSfxEvent sound = CombatSfxEvent.WeaponSplash;
		[SerializeField, Tooltip("바닥 얼룩 반지름 (m). 0 이면 없음"), Min(0f)] private float stainRadius;
		[SerializeField] private Color stainColor = new(0.45f, 0.25f, 0.55f, 0.45f);

		private struct Puff
		{
			public float Start;
			public Vector3 From;
			public Vector3 To;
			public float Seed;
			public float Scale;
		}

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			GameObject root = new(name);
			root.transform.SetParent(runner.transform, false);
			FxQuadBatch puffBatch = new(name, root.transform, set.Shader, sheet, set.Noise, PUFF_QUEUE);
			FxQuadBatch ringBatch = ringRatio > 0f ? new("Ring", root.transform, set.Shader, set.Shock, set.Noise, RING_QUEUE) : null;
			return runner.Run(Routine(context, puffBatch, ringBatch), () =>
			{
				puffBatch.Destroy();
				ringBatch?.Destroy();
				if (root != null)
					Destroy(root);
			});
		}

		private IEnumerator Routine(CombatFxContext context, FxQuadBatch puffBatch, FxQuadBatch ringBatch)
		{
			Begin(context);
			Puff[] puffs = MakePuffs(context);
			float end = Mathf.Max(emitSeconds + seconds, ringRatio > 0f ? ringSeconds : 0f);
			for (float time = 0f; time < end; time += Time.deltaTime)
			{
				Camera view = Camera.main;
				if (view != null)
					DrawPuffs(puffBatch, puffs, time, view.transform);
				if (ringBatch != null)
					DrawRing(ringBatch, context, time);
				yield return null;
			}
		}

		private void DrawPuffs(FxQuadBatch batch, Puff[] puffs, float time, Transform view)
		{
			int span = frames.y - frames.x + 1;
			batch.Begin();
			foreach (Puff puff in puffs)
			{
				float progress = (time - puff.Start) / seconds;
				if (progress < 0f || progress >= 1f)
					continue;
				float eased = 1f - (1f - progress) * (1f - progress);
				float half = Mathf.Lerp(halfSize.x, halfSize.y, eased) * puff.Scale;
				Rect cell = FxQuadBatch.Cell(frames.x + Mathf.Min(span - 1, (int)(progress * span)), columns, rows);
				FxQuadLook look = FxQuadLook.Of(color, cover, Mathf.Clamp01((progress - 0.75f) * 3f), puff.Seed);
				batch.Add(Vector3.Lerp(puff.From, puff.To, eased), view.right * half, view.up * half, cell, look);
			}
			batch.Commit();
		}

		private void DrawRing(FxQuadBatch batch, CombatFxContext context, float time)
		{
			batch.Begin();
			float progress = time / ringSeconds;
			if (progress < 1f)
			{
				float half = context.Radius * ringRatio * (1f - (1f - progress) * (1f - progress)) * 1.15f;
				Color faded = ringColor;
				faded.a *= 1f - progress;
				batch.Add(context.Position + Vector3.up * 0.06f, Vector3.right * half, Vector3.forward * half, FxQuadLook.Of(faded, 0f, progress, 0.3f));
			}
			batch.Commit();
		}

		private void Begin(CombatFxContext context)
		{
			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.OnCustomMoment(moment, context.Position);
			CombatSfx.Play(sound, context.Position);
			if (RemainsWorld.Current == null)
				return;
			RemainsWorld.Current.Blast(context.Position, Mathf.Max(1f, context.Radius), 4f, 2f, false);
			if (stainRadius > 0f)
				RemainsWorld.Current.Stain(context.Position, stainRadius, stainColor);
		}

		private Puff[] MakePuffs(CombatFxContext context)
		{
			Puff[] puffs = new Puff[count];
			Vector3 direction = context.Direction.normalized;
			bool isStream = travelRatio > 0f;
			for (int i = 0; i < count; i++)
			{
				Vector2 side = Random.insideUnitCircle * spread;
				Vector3 to = context.Position + Vector3.up * lift + new Vector3(side.x, 0f, side.y);
				if (isStream)
					to += direction * (context.Length * travelRatio * Random.Range(0.55f, 1f));
				puffs[i] = new Puff
				{
					Start = count > 1 ? emitSeconds * i / (count - 1) : 0f,
					From = context.Position + Vector3.up * (isStream ? originLift : lift) + new Vector3(side.x, 0f, side.y) * (isStream ? 0.2f : 1f),
					To = to,
					Seed = Random.value * 10f,
					Scale = count > 1 ? Random.Range(0.75f, 1.1f) : 1f,
				};
			}
			return puffs;
		}
	}
}
