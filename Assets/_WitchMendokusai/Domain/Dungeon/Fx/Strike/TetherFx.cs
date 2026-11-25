using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 몸에서 줄에 매달려 날아갔다 되감겨 오는 물체. Seconds 가 끝에 닿는 순간, Hold.EndTime 이 돌아온 순간
	[CreateAssetMenu(fileName = "Tether_0", menuName = "WM/CombatFx/" + nameof(TetherFx))]
	public class TetherFx : CombatFx
	{
		private const int SHADOW_QUEUE = 2991;
		private const int CORD_QUEUE = 3040;
		private const int HEAD_QUEUE = 3041;

		[Header("그림")]
		[SerializeField, Tooltip("셰이더와 잡음을 빌리는 묶음")] private PaintedFxSet set;
		[SerializeField, Tooltip("나갈 때. 앞이 오른쪽")] private Texture2D headOut;
		[SerializeField, Tooltip("끝에 닿은 뒤와 돌아올 때. 앞이 오른쪽")] private Texture2D headBack;
		[SerializeField, Tooltip("줄 한 마디. 가로로 이어짐")] private Texture2D link;
		[SerializeField, Tooltip("머리 그림의 1m 당 칸. 몸과 칸 크기가 같게"), Min(1f)] private float headPixelsPerUnit = 23f;
		[SerializeField, Tooltip("줄 한 마디 길이와 폭 (m)")] private Vector2 linkSize = new(0.7f, 0.35f);

		[SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.35f);

		[Header("움직임")]
		[SerializeField, Tooltip("몸에서 나가는 높이 (m)"), Min(0f)] private float launchHeight = 1.2f;
		[SerializeField, Tooltip("몸 가운데에서 앞으로 이만큼 나간 데서 줄이 나옴 (m)"), Min(0f)] private float launchForward = 1.1f;
		[SerializeField, Tooltip("끝 높이 (m)"), Min(0f)] private float endHeight = 0.6f;
		[SerializeField, Tooltip("나가는 초. 때리는 순간 이만큼 전에 떠남"), Min(0.02f)] private float outSeconds = 0.14f;
		[SerializeField, Tooltip("되감기는 초"), Min(0.02f)] private float backSeconds = 0.22f;
		[SerializeField, Tooltip("되감기 전 당겨지며 떨리는 폭 (m)"), Min(0f)] private float tugTremble = 0.12f;
		[SerializeField, Min(0f)] private float tugSpeed = 70f;
		[SerializeField, Tooltip("닿는 순간을 때리는 순간보다 이만큼 앞당김 (초). 피해가 그림보다 먼저 오지 않게"), Min(0f)] private float leadSeconds = 0.05f;
		[SerializeField, Tooltip("수명 (Hold) 없이 재생할 때 (미리보기) 나가 있는 초"), Min(0.1f)] private float previewHoldSeconds = 0.8f;
		[SerializeField, Tooltip("늘어졌을 때 줄이 휘는 폭 (m)"), Min(0f)] private float slack = 0.35f;

		[Header("순간")]
		[SerializeField] private FeelMoment landMoment = new();
		[SerializeField] private FeelMoment backMoment = new();
		[SerializeField, Tooltip("끝에 닿는 순간과 되감기 시작에. 비우면 없음")] private CombatFx dust;
		[SerializeField, Min(0.1f)] private float dustRadius = 1.2f;
		[SerializeField, Tooltip("바닥 얼룩 반지름 (m). 0 이면 없음"), Min(0f)] private float stainRadius = 0.8f;
		[SerializeField] private Color stainColor = new(0.23f, 0.14f, 0.08f, 0.6f);

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			GameObject root = new(name);
			root.transform.SetParent(runner.transform, false);
			FxQuadBatch shadow = new("Shadow", root.transform, set.Shader, headBack, set.Noise, SHADOW_QUEUE);
			FxQuadBatch cord = new("Cord", root.transform, set.Shader, link, set.Noise, CORD_QUEUE);
			FxQuadBatch outward = new("Out", root.transform, set.Shader, headOut, set.Noise, HEAD_QUEUE);
			FxQuadBatch back = new("Back", root.transform, set.Shader, headBack, set.Noise, HEAD_QUEUE);
			FxQuadBatch[] batches = { shadow, cord, outward, back };
			return runner.Run(Routine(context, batches), () =>
			{
				foreach (FxQuadBatch batch in batches)
					batch.Destroy();
				if (root != null)
					Destroy(root);
			});
		}

		private IEnumerator Routine(CombatFxContext context, FxQuadBatch[] batches)
		{
			float flyTime = Time.time + context.Seconds - outSeconds - leadSeconds;
			while (Time.time + Time.deltaTime < flyTime)
				yield return null;
			Vector3 direction = context.Direction.normalized;
			Vector3 start = context.Origin;
			Vector3 end = start + direction * (context.Length - HeadLength(headOut) * 0.5f) + Vector3.up * endHeight;
			CombatSfx.Play(CombatSfxEvent.WeaponThrow, start);
			for (float time = 0f; time < outSeconds; time += Time.deltaTime)
			{
				Draw(context, batches, Vector3.Lerp(Root(context, start), end, time / outSeconds), direction, true, 0f);
				yield return null;
			}
			Land(end, landMoment);
			float landTime = Time.time;
			float backStart = (context.Hold != null ? context.Hold.EndTime : landTime + previewHoldSeconds) - backSeconds;
			while (Time.time < backStart)
			{
				// 되감기 직전일수록 팽팽하고 크게 떨림
				float tension = Mathf.Clamp01(1f - (backStart - Time.time) / Mathf.Max(0.05f, backStart - landTime));
				Vector3 shake = Vector3.Cross(Vector3.up, direction) * (Mathf.Sin(Time.time * tugSpeed) * tugTremble * tension * tension);
				Draw(context, batches, end + shake, direction, false, slack * (1f - tension));
				yield return null;
			}
			Land(end, backMoment);
			for (float time = 0f; time < backSeconds; time += Time.deltaTime)
			{
				float progress = time / backSeconds;
				Draw(context, batches, Vector3.Lerp(end, Root(context, start), progress * progress), direction, false, 0f);
				yield return null;
			}
		}

		private float HeadLength(Texture2D texture) => texture.width / headPixelsPerUnit;

		// 시전자가 사라져도 놓인 자리에서 마저 움직임
		private Vector3 Root(CombatFxContext context, Vector3 start) => (context.Follow != null ? context.Follow.position : start) + context.Direction.normalized * launchForward + Vector3.up * launchHeight;

		private void Land(Vector3 position, FeelMoment moment)
		{
			Vector3 ground = position - Vector3.up * endHeight;
			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.OnCustomMoment(moment, ground);
			CombatSfx.Play(CombatSfxEvent.WeaponSwing, ground);
			if (dust != null)
				dust.Play(new CombatFxContext { Position = ground, Origin = ground, Radius = dustRadius });
			if (RemainsWorld.Current == null)
				return;
			RemainsWorld.Current.Blast(ground, dustRadius, 4f, 2f, false);
			if (stainRadius > 0f)
				RemainsWorld.Current.Stain(ground, stainRadius, stainColor);
		}

		private void Draw(CombatFxContext context, FxQuadBatch[] batches, Vector3 head, Vector3 direction, bool isOut, float sag)
		{
			foreach (FxQuadBatch batch in batches)
				batch.Begin();
			Camera view = Camera.main;
			if (view != null)
			{
				Vector3 forward = view.transform.forward;
				FxQuadLook plain = FxQuadLook.Of(Color.white, 1f);
				Texture2D texture = isOut ? headOut : headBack;
				float headLength = HeadLength(texture);
				Vector3 half = direction * (headLength * 0.5f);
				Vector3 ground = new(head.x, context.Position.y + 0.06f, head.z);
				batches[0].Add(ground, half, Vector3.Cross(direction, Vector3.up) * (headLength * 0.3f), FxQuadLook.Of(shadowColor, 1f));
				// 머리 폭은 그림 비율대로
				float width = headLength * texture.height / texture.width;
				batches[isOut ? 2 : 3].AddStrip(head - half, head + half, width, forward, new Rect(0f, 0f, 1f, 1f), plain);
				DrawCord(batches[1], Root(context, context.Origin), head - half * 0.9f, sag, forward, plain);
			}
			foreach (FxQuadBatch batch in batches)
				batch.Commit();
		}

		private void DrawCord(FxQuadBatch batch, Vector3 from, Vector3 to, float sag, Vector3 forward, in FxQuadLook look)
		{
			int count = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / linkSize.x));
			Vector3 last = from;
			for (int i = 1; i <= count; i++)
			{
				float at = (float)i / count;
				Vector3 next = Vector3.Lerp(from, to, at) + Vector3.down * (Mathf.Sin(at * Mathf.PI) * sag);
				batch.AddStrip(last, next, linkSize.y, forward, new Rect(0f, 0f, 1f, 1f), look);
				last = next;
			}
		}
	}
}
