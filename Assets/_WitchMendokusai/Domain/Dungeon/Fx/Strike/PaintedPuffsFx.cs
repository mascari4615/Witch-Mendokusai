using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 그린 플립북 덩이. 하나면 터짐, 여럿에 travelRatio 가 있으면 시전자에서 뿜어 나가는 숨, alongPath 면 길을 따라 차례로 놓이는 먼지
	// isFlat 이면 바닥에 누워 Direction 을 향하고 크기가 Radius 의 배 (휘두른 자국, 울림)
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
		[SerializeField, Tooltip("예고 물체로 쓸 때: 때리는 순간보다 이만큼 먼저 나감 (초). 0 이면 곧바로"), Min(0f)] private float leadSeconds;
		[SerializeField, Tooltip("덩이들이 나오는 데 걸리는 초"), Min(0f)] private float emitSeconds;
		[SerializeField, Tooltip("반 변 (m) 처음과 끝")] private Vector2 halfSize = new(1.4f, 1.8f);
		[SerializeField, Tooltip("바닥에서 높이 (m)")] private float lift = 0.7f;
		[SerializeField, Tooltip("뿜는 높이 (m). 입 높이"), Min(0f)] private float originLift = 1.2f;
		[SerializeField, Tooltip("Length 의 이만큼까지 감. 0 이면 제자리"), Range(0f, 1.2f)] private float travelRatio;
		[SerializeField, Tooltip("옆으로 흩어짐 (m)"), Min(0f)] private float spread;

		[SerializeField, Tooltip("길 (Direction, Length) 위에 고르게 놓고 emitSeconds (0 이면 Seconds) 동안 차례로")] private bool alongPath;
		[SerializeField, Tooltip("바닥에 눕힘. halfSize 는 Radius 의 배")] private bool isFlat;
		[SerializeField, Tooltip("눕힌 그림을 Direction 에서 돌리는 각도 (도)")] private float flatTurn;
		[SerializeField, Tooltip("바닥을 파묻히지 않게 지형에 안 가려짐")] private bool onTop;

		[SerializeField, Tooltip("안이 빈 고리 (InnerRadius) 를 때릴 때 고리 위 덩이 수"), Min(1)] private int ringPuffCount = 12;
		[SerializeField, Tooltip("고리 위 덩이 크기. 고리 폭의 배"), Min(0.1f)] private float ringPuffScale = 0.75f;

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
			FxQuadBatch puffBatch = new(name, root.transform, set.Shader, sheet, set.Noise, PUFF_QUEUE, onTop);
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
			if (leadSeconds > 0f)
				yield return new WaitForSeconds(Mathf.Max(0f, context.Seconds - leadSeconds));
			Begin(context);
			Puff[] puffs = MakePuffs(context);
			float end = Mathf.Max(EmitSeconds(context) + seconds, ringRatio > 0f ? ringSeconds : 0f);
			for (float time = 0f; time < end; time += Time.deltaTime)
			{
				Camera view = Camera.main;
				if (view != null)
					DrawPuffs(puffBatch, puffs, time, context, view.transform);
				if (ringBatch != null)
					DrawRing(ringBatch, context, time);
				yield return null;
			}
		}

		private float EmitSeconds(CombatFxContext context) => alongPath && emitSeconds <= 0f ? context.Seconds : emitSeconds;

		private void DrawPuffs(FxQuadBatch batch, Puff[] puffs, float time, CombatFxContext context, Transform view)
		{
			Vector3 right = view.right;
			Vector3 up = view.up;
			float unit = 1f;
			if (isFlat)
			{
				Vector3 direction = context.Direction.sqrMagnitude > 0f ? context.Direction.normalized : Vector3.forward;
				up = Quaternion.Euler(0f, flatTurn, 0f) * direction;
				right = Vector3.Cross(Vector3.up, up);
				unit = context.Radius;
			}
			int span = frames.y - frames.x + 1;
			batch.Begin();
			foreach (Puff puff in puffs)
			{
				float progress = (time - puff.Start) / seconds;
				if (progress < 0f || progress >= 1f)
					continue;
				float eased = 1f - (1f - progress) * (1f - progress);
				float half = Mathf.Lerp(halfSize.x, halfSize.y, eased) * puff.Scale * unit;
				Rect cell = FxQuadBatch.Cell(frames.x + Mathf.Min(span - 1, (int)(progress * span)), columns, rows);
				FxQuadLook look = FxQuadLook.Of(color, cover, Mathf.Clamp01((progress - 0.75f) * 3f), puff.Seed);
				batch.Add(Vector3.Lerp(puff.From, puff.To, eased), right * half, up * half, cell, look);
			}
			batch.Commit();
		}

		private void DrawRing(FxQuadBatch batch, CombatFxContext context, float time)
		{
			batch.Begin();
			float progress = time / ringSeconds;
			if (progress < 1f)
			{
				float start = context.InnerRadius;
				float half = Mathf.Lerp(start, context.Radius * ringRatio * 1.15f, 1f - (1f - progress) * (1f - progress));
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

		private Puff[] MakeRingPuffs(CombatFxContext context)
		{
			Puff[] puffs = new Puff[ringPuffCount];
			float middle = (context.InnerRadius + context.Radius) * 0.5f;
			float size = (context.Radius - context.InnerRadius) * 0.5f * ringPuffScale / halfSize.x;
			float startAngle = Random.value * 360f;
			for (int i = 0; i < puffs.Length; i++)
			{
				Vector3 spot = context.Position + Quaternion.Euler(0f, startAngle + 360f / puffs.Length * i, 0f) * Vector3.forward * middle + Vector3.up * lift;
				puffs[i] = new Puff { From = spot, To = spot, Seed = Random.value * 10f, Scale = size };
			}
			return puffs;
		}

		private Puff[] MakePuffs(CombatFxContext context)
		{
			if (context.InnerRadius > 0f)
				return MakeRingPuffs(context);
			Puff[] puffs = new Puff[count];
			Vector3 direction = context.Direction.normalized;
			bool isStream = travelRatio > 0f;
			for (int i = 0; i < count; i++)
			{
				Vector2 side = Random.insideUnitCircle * spread;
				Vector3 to = context.Position + Vector3.up * lift + new Vector3(side.x, 0f, side.y);
				if (alongPath)
					to += direction * (context.Length * i / Mathf.Max(1, count - 1));
				else if (isStream)
					to += direction * (context.Length * travelRatio * Random.Range(0.55f, 1f));
				puffs[i] = new Puff
				{
					Start = count > 1 ? EmitSeconds(context) * i / (count - 1) : 0f,
					From = alongPath ? to : context.Position + Vector3.up * (isStream ? originLift : lift) + new Vector3(side.x, 0f, side.y) * (isStream ? 0.2f : 1f),
					To = to,
					Seed = Random.value * 10f,
					Scale = count > 1 ? Random.Range(0.75f, 1.1f) : 1f,
				};
			}
			return puffs;
		}
	}
}
