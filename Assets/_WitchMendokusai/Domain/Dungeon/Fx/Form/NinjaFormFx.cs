using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 알리사 닌자 확정 변신. 구름 연기가 덮음 -> 앞 연기만 찢겨 실루엣 -> 한 프레임에 붓결로 터지고 걷힘
	/// - 그림은 캐릭터와 같은 밀도 (월드 1 에 32칸) 로 구운 판. 칸 크기를 늘리지 않고 프레임 번호로 크기를 바꿈
	/// - 시전자를 따라다님. 정지, 줌, 흔들림은 화면 박자표 (FeelUltimateFx) 몫. 자리와 시각의 근거 memo wm/design/alisa-ninja-refs-20261125
	/// </summary>
	[CreateAssetMenu(fileName = "FormFx_Ninja", menuName = "WM/CombatFx/" + nameof(NinjaFormFx))]
	public sealed class NinjaFormFx : WorldUltimateFx
	{
		// 앞 연기 (Veil) 는 실루엣이 보일 때 먼저 찢김
		private enum PuffLayer
		{
			Back = 0,
			Front = 1,
			Veil = 2,
		}

		[Serializable]
		private struct Puff
		{
			[Tooltip("화면 오른쪽, 위 (월드)")] public Vector2 Offset;
			[Min(0f)] public float Delay;
			[Range(0, 8), Tooltip("다 자란 칸. 작으면 작은 구름")] public int PeakFrame;
			public PuffLayer Layer;
		}

		private const int SIDE = 4;
		private const int LAST_FRAME = SIDE * SIDE - 1;
		private const int TEAR_FRAME = 9;
		private const int FRONT_TEAR_FRAME = 11;
		private const int BRUSH_FIRST_FRAME = 3;
		private const int WISP_ROWS = 4;
		// 인물 그림 (큐 3000, 순서 0~2) 뒤. 앞은 순서로
		private const int INK_QUEUE = 2950;
		private const int BACK_QUEUE = 2962;
		private const int WISP_QUEUE = 2970;
		private const int FRONT_QUEUE = 3050;
		// 덮는 동안의 뒤 구름. 다른 유닛과 몸 위, 실루엣 아래
		private const int COVER_ORDER = 5;
		private const int OUTLINE_ORDER = 9;
		private const int FILL_ORDER = 10;
		private const int FRONT_ORDER = 20;
		private const int BRUSH_ORDER = 21;
		// 그림자 같은 바닥 그림 (음수) 과 사망 표시 (100) 는 실루엣에서 뺌
		private const int MAX_BODY_ORDER = 50;
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly Vector2[] OUTLINE_STEPS = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

		[Header("그림")]
		[SerializeField, Tooltip("셰이더와 잡음을 빌리는 묶음")] private PaintedFxSet art;
		[SerializeField, Tooltip("4x4, 칸 하나가 월드 2")] private Texture2D smokeSheet;
		[SerializeField, Tooltip("4x4, 칸 하나가 월드 4")] private Texture2D brushSheet;
		[SerializeField] private Texture2D inkTexture;
		[SerializeField, Tooltip("가로 줄기 네 줄. 머리가 오른쪽")] private Texture2D wispTexture;
		[SerializeField] private Shader silhouetteShader;
		[SerializeField, Min(8f), Tooltip("월드 1 당 칸. 캐릭터 그림의 PPU 와 같게")] private float texelsPerUnit = 32f;
		[SerializeField, Min(1f), Tooltip("그림이 바뀌는 횟수 (초당)")] private float poseSteps = 20f;

		[Header("시간 (연출 초)")]
		[SerializeField, Min(0.05f), Tooltip("구름 하나가 다 자라는 길이")] private float growSeconds = 0.16f;
		[SerializeField, Min(0.1f), Tooltip("앞 연기가 찢기기 시작. 여기서 실루엣")] private float revealTime = 0.28f;
		[SerializeField, Min(0.02f)] private float revealSeconds = 0.16f;
		[SerializeField, Min(0.2f), Tooltip("터짐. 화면 박자표의 타격 시각과 맞춤")] private float impactTime = 0.72f;
		[SerializeField, Min(0.1f), Tooltip("터진 뒤 구름이 흩어지는 길이")] private float scatterSeconds = 0.36f;
		[SerializeField, Min(0.1f)] private float wispSeconds = 0.5f;
		[SerializeField, Min(0.1f), Tooltip("바닥 먹이 남는 길이와 사라지는 길이")] private Vector2 inkSeconds = new(0.42f, 0.43f);
		[SerializeField, Range(0.01f, 1f), Tooltip("덮음과 실루엣 동안 게임 시간 배율")] private float coverTimeScale = 0.3f;

		[Header("구름")]
		[SerializeField] private Puff[] puffs =
		{
			new() { Offset = new Vector2(-0.95f, 0.55f), Delay = 0.02f, PeakFrame = 7 },
			new() { Offset = new Vector2(0.95f, 0.6f), Delay = 0.04f, PeakFrame = 8 },
			new() { Offset = new Vector2(0f, 1.25f), Delay = 0.05f, PeakFrame = 8 },
			new() { Offset = new Vector2(-0.6f, 1.05f), Delay = 0.03f, PeakFrame = 7 },
			new() { Offset = new Vector2(0.6f, 1.1f), Delay = 0.06f, PeakFrame = 7 },
			new() { Offset = new Vector2(0f, 0.6f), Delay = 0f, PeakFrame = 8 },
			new() { Offset = new Vector2(-0.1f, 0.45f), Delay = 0f, PeakFrame = 8, Layer = PuffLayer.Veil },
			new() { Offset = new Vector2(-0.35f, 0.85f), Delay = 0.05f, PeakFrame = 6, Layer = PuffLayer.Veil },
			new() { Offset = new Vector2(0.4f, 0.8f), Delay = 0.07f, PeakFrame = 6, Layer = PuffLayer.Veil },
			new() { Offset = new Vector2(0.25f, 0.2f), Delay = 0.04f, PeakFrame = 5, Layer = PuffLayer.Veil },
			new() { Offset = new Vector2(-1.1f, 0.12f), Delay = 0.03f, PeakFrame = 5, Layer = PuffLayer.Front },
			new() { Offset = new Vector2(1.1f, 0.1f), Delay = 0.05f, PeakFrame = 5, Layer = PuffLayer.Front },
			new() { Offset = new Vector2(-0.62f, -0.12f), Delay = 0.06f, PeakFrame = 3, Layer = PuffLayer.Front },
			new() { Offset = new Vector2(0.66f, -0.12f), Delay = 0.07f, PeakFrame = 3, Layer = PuffLayer.Front },
		};
		[SerializeField, Tooltip("터진 뒤 자리 배율 (처음, 더해지는 양)")] private Vector2 scatterSpread = new(1.25f, 1f);
		[SerializeField, Range(0f, 1f), Tooltip("판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;

		[Header("실루엣")]
		[SerializeField] private Color fillColor = new(0.024f, 0.02f, 0.055f, 1f);
		[SerializeField] private Color outlineColor = new(0.69f, 0.73f, 1f, 1f);
		[SerializeField, Min(0f), Tooltip("터지는 순간 색이 뒤집힌 채 남는 길이")] private float invertSeconds = 0.1f;

		[Header("터짐")]
		[SerializeField, Min(0f)] private float brushHeight = 0.55f;
		[SerializeField, Tooltip("연기 줄기가 나가는 각도 (도, 0 화면 오른쪽, 90 화면 위)")] private float[] wispAngles = { 14f, 40f, 140f, 166f, 197f, 343f };
		[SerializeField, Tooltip("연기 줄기 꼬리 자리 (처음, 더해지는 양)")] private Vector2 wispReach = new(0.2f, 1f);
		[SerializeField, Min(0)] private int dropCount = 46;
		[SerializeField] private Color dropColor = new(0.04f, 0.035f, 0.09f, 1f);
		[SerializeField] private Color dropLight = new(0.63f, 0.67f, 0.96f, 1f);

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.45f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.5f;
		[SerializeField] private Color lightColor = new(0.5f, 0.56f, 1f, 1f);
		[SerializeField, Min(0f)] private float impactLight = 6f;
		[SerializeField, Min(1f)] private float lightRange = 8f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 0.5f;

		/// <summary> 터짐 시각 (연출 초). 화면 박자표의 타격 시각과 맞춤 </summary>
		public float ImpactSeconds => impactTime;
		public override float DurationSeconds => impactTime + inkSeconds.x + inkSeconds.y;

		private sealed class Layers
		{
			public FxQuadBatch Ink;
			public FxQuadBatch Cover;
			public FxQuadBatch Back;
			public FxQuadBatch Wisp;
			public FxQuadBatch Front;
			public FxQuadBatch Brush;

			public IEnumerable<FxQuadBatch> All => new[] { Ink, Cover, Back, Wisp, Front, Brush };
		}

		private sealed class Silhouette
		{
			public SpriteRenderer Source;
			public SpriteRenderer Fill;
			public readonly List<SpriteRenderer> Outlines = new();
		}

		private readonly struct Pose
		{
			public readonly Vector3 Foot;
			public readonly Vector3 Right;
			public readonly Vector3 Up;
			public readonly Vector3 View;
			public readonly Vector3 GroundForward;

			public Pose(UltimateFxRun run, float faceCamera)
			{
				Transform cameraTransform = run.MainCamera.transform;
				Foot = run.Caster;
				Right = cameraTransform.right;
				Up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
				View = cameraTransform.forward;
				GroundForward = run.ScreenForward;
			}
		}

		protected override void ComposeWorld(Stage stage)
		{
			if (art == null || smokeSheet == null || brushSheet == null || inkTexture == null || wispTexture == null || silhouetteShader == null)
				throw new InvalidOperationException($"{name}: 변신 그림이나 셰이더 미연결");
			UltimateFxRun run = stage.Run;
			run.At(0f, () => run.HitStop(impactTime, coverTimeScale));
			ComposeSmoke(run);
			ComposeSilhouette(run);
			ComposeDrops(run);
			ComposeLight(stage);
		}

		/// <summary> 움직임 시계. 터짐 시각에 맞춰 초당 poseSteps 번만 바뀜 </summary>
		private float Stepped(float now) => impactTime + Mathf.Floor((now - impactTime) * poseSteps + 0.001f) / poseSteps;

		// ---- 구름, 붓결, 바닥 먹, 연기 줄기. 매 프레임 쿼드를 다시 채움 ----

		private void ComposeSmoke(UltimateFxRun run)
		{
			// 쿼드는 월드 자리라 뿌리를 원점에 둠
			Transform root = new GameObject("NinjaFormQuads").transform;
			root.SetParent(run.Root, false);
			root.position = Vector3.zero;
			FxQuadBatch Batch(string label, Texture2D texture, int queue, bool onTop, int order) => new($"NinjaForm{label}", root, art.Shader, texture, art.Noise, queue, onTop, order);
			Layers layers = new()
			{
				Ink = Batch("Ink", inkTexture, INK_QUEUE, false, 0),
				// 아래쪽 구름이 바닥에 잘리지 않게 지형 위에
				Cover = Batch("Cover", smokeSheet, FRONT_QUEUE, true, COVER_ORDER),
				Back = Batch("Back", smokeSheet, BACK_QUEUE, true, 0),
				Wisp = Batch("Wisp", wispTexture, WISP_QUEUE, true, 0),
				Front = Batch("Front", smokeSheet, FRONT_QUEUE, true, FRONT_ORDER),
				Brush = Batch("Brush", brushSheet, FRONT_QUEUE, true, BRUSH_ORDER),
			};
			run.OnRelease(() =>
			{
				foreach (FxQuadBatch batch in layers.All)
					batch.Destroy();
			});
			run.Track(0f, DurationSeconds, _ =>
			{
				foreach (FxQuadBatch batch in layers.All)
					batch.Begin();
				if (run.MainCamera != null)
					Draw(layers, new Pose(run, faceCamera), Stepped(run.Now));
				foreach (FxQuadBatch batch in layers.All)
					batch.Commit();
			}, () =>
			{
				foreach (FxQuadBatch batch in layers.All)
					batch.Clear();
			});
		}

		private void Draw(Layers layers, in Pose pose, float now)
		{
			float age = now - impactTime;
			foreach (Puff puff in puffs)
				DrawPuff(layers, pose, puff, now, age);
			if (age < 0f)
				return;
			DrawInk(layers.Ink, pose, age);
			DrawWisps(layers.Wisp, pose, age);
			int frame = BRUSH_FIRST_FRAME + Mathf.FloorToInt(age * poseSteps + 0.001f);
			if (frame <= LAST_FRAME)
				AddCell(layers.Brush, pose, pose.Foot + pose.Up * brushHeight, brushSheet, frame, false);
		}

		private void DrawPuff(Layers layers, in Pose pose, in Puff puff, float now, float age)
		{
			int frame = PuffFrame(puff, now, age);
			if (frame < 0 || frame > LAST_FRAME)
				return;
			float spread = age < 0f ? 1f : scatterSpread.x + scatterSpread.y * UltimateFxRun.EaseOut(age / scatterSeconds);
			Vector3 center = pose.Foot + pose.Right * (puff.Offset.x * spread) + pose.Up * (puff.Offset.y * (1f + (spread - 1f) * 0.5f));
			FxQuadBatch back = age < invertSeconds ? layers.Cover : layers.Back;
			AddCell(puff.Layer == PuffLayer.Back ? back : layers.Front, pose, center, smokeSheet, frame, puff.Offset.x > 0f);
		}

		// 안 그릴 때 -1
		private int PuffFrame(in Puff puff, float now, float age)
		{
			if (now < puff.Delay)
				return -1;
			if (age >= 0f)
			{
				if (puff.Layer == PuffLayer.Veil)
					return -1;
				return (puff.Layer == PuffLayer.Back ? TEAR_FRAME : FRONT_TEAR_FRAME) + Mathf.FloorToInt(age / scatterSeconds * (LAST_FRAME - TEAR_FRAME + 1));
			}
			if (puff.Layer == PuffLayer.Veil && now > revealTime)
				return Mathf.Max(puff.PeakFrame, TEAR_FRAME) + Mathf.FloorToInt((now - revealTime) / revealSeconds * (LAST_FRAME - TEAR_FRAME));
			float grow = UltimateFxRun.Span(now, puff.Delay, puff.Delay + growSeconds);
			if (grow < 1f)
				return Mathf.FloorToInt(grow * puff.PeakFrame);
			// 다 자란 뒤에는 두 칸을 오가며 꿈틀거림
			return puff.PeakFrame - Mathf.FloorToInt(now * 7.5f + Mathf.Abs(puff.Offset.x) * 3f) % 2;
		}

		private void AddCell(FxQuadBatch batch, in Pose pose, Vector3 center, Texture2D sheet, int frame, bool flip)
		{
			float half = sheet.width / (float)SIDE / texelsPerUnit * 0.5f;
			batch.Add(center, pose.Right * (flip ? -half : half), pose.Up * half, FxQuadBatch.Cell(frame, SIDE, SIDE), FxQuadLook.Of(Color.white, 1f));
		}

		private void DrawInk(FxQuadBatch batch, in Pose pose, float age)
		{
			float open = 0.65f + 0.35f * UltimateFxRun.EaseOut(UltimateFxRun.Span(age, -1f / poseSteps, 0.16f));
			float left = Faded(UltimateFxRun.Span(age, inkSeconds.x, inkSeconds.x + inkSeconds.y), 4);
			float half = inkTexture.width / texelsPerUnit * 0.5f * open;
			batch.Add(pose.Foot + Vector3.up * 0.05f, pose.Right * half, pose.GroundForward * half, FxQuadLook.Of(new Color(1f, 1f, 1f, left), 1f));
		}

		private void DrawWisps(FxQuadBatch batch, in Pose pose, float age)
		{
			if (age >= wispSeconds)
				return;
			Color color = new(1f, 1f, 1f, Faded(UltimateFxRun.Span(age, wispSeconds * 0.4f, wispSeconds), 3));
			float length = wispTexture.width / texelsPerUnit;
			float width = wispTexture.height / (float)WISP_ROWS / texelsPerUnit;
			float tail = wispReach.x + wispReach.y * UltimateFxRun.EaseOut(age / wispSeconds) - length * 0.5f;
			for (int i = 0; i < wispAngles.Length; i++)
			{
				float angle = wispAngles[i] * Mathf.Deg2Rad;
				Vector3 outward = pose.Right * Mathf.Cos(angle) + pose.GroundForward * Mathf.Sin(angle);
				Vector3 from = pose.Foot + Vector3.up * 0.2f + outward * Mathf.Max(0f, tail);
				Rect row = new(0f, 1f - (i % WISP_ROWS + 1) / (float)WISP_ROWS, 1f, 1f / WISP_ROWS);
				batch.AddStrip(from, from + outward * length, width, pose.View, row, FxQuadLook.Of(color, 1f));
			}
		}

		// 몇 단으로 끊어 흐려짐 (도트 결)
		private static float Faded(float progress, int steps) => 1f - Mathf.Floor(progress * steps) / steps;

		// ---- 실루엣: 몸 그림을 한 색으로 덧그림. 몸의 색은 안 건드림 (피격 번쩍임, 상태이상 색과 안 섞이게) ----

		private void ComposeSilhouette(UltimateFxRun run)
		{
			if (run.Follow == null || run.Follow.TryGetComponent(out UnitObject unit) == false)
				return;
			Material fill = Flat(run);
			Material outline = Flat(run);
			List<Silhouette> bodies = new();
			foreach (SpriteRenderer source in unit.MeshParent.GetComponentsInChildren<SpriteRenderer>(false))
			{
				if (source.enabled == false || source.sortingOrder < 0 || source.sortingOrder > MAX_BODY_ORDER)
					continue;
				Silhouette body = new() { Source = source, Fill = Copy(run, source, fill, FILL_ORDER) };
				for (int i = 0; i < OUTLINE_STEPS.Length; i++)
					body.Outlines.Add(Copy(run, source, outline, OUTLINE_ORDER));
				bodies.Add(body);
			}
			float start = growSeconds;
			run.Track(start, impactTime + invertSeconds - start, _ =>
			{
				bool inverted = run.Now >= impactTime;
				fill.SetColor(COLOR_ID, inverted ? outlineColor : fillColor);
				outline.SetColor(COLOR_ID, inverted ? fillColor : outlineColor);
				foreach (Silhouette body in bodies)
					Follow(body, run.MainCamera);
			}, () =>
			{
				foreach (Silhouette body in bodies)
				{
					body.Fill.enabled = false;
					foreach (SpriteRenderer line in body.Outlines)
						line.enabled = false;
				}
			});
		}

		private void Follow(Silhouette body, Camera view)
		{
			bool visible = body.Source != null && body.Source.enabled && body.Source.gameObject.activeInHierarchy && view != null;
			Match(body.Fill, body.Source, visible, Vector3.zero);
			for (int i = 0; i < OUTLINE_STEPS.Length; i++)
			{
				Vector3 step = visible ? (view.transform.right * OUTLINE_STEPS[i].x + view.transform.up * OUTLINE_STEPS[i].y) / texelsPerUnit : Vector3.zero;
				Match(body.Outlines[i], body.Source, visible, step);
			}
		}

		private static void Match(SpriteRenderer copy, SpriteRenderer source, bool visible, Vector3 offset)
		{
			copy.enabled = visible;
			if (visible == false)
				return;
			copy.sprite = source.sprite;
			copy.flipX = source.flipX;
			copy.flipY = source.flipY;
			copy.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation);
			copy.transform.localScale = source.transform.lossyScale;
		}

		private static SpriteRenderer Copy(UltimateFxRun run, SpriteRenderer source, Material material, int order)
		{
			GameObject copyObject = new("NinjaFormSilhouette");
			copyObject.transform.SetParent(run.Root, false);
			SpriteRenderer copy = copyObject.AddComponent<SpriteRenderer>();
			copy.sharedMaterial = material;
			copy.sortingLayerID = source.sortingLayerID;
			copy.sortingOrder = order;
			copy.enabled = false;
			return copy;
		}

		private Material Flat(UltimateFxRun run)
		{
			Material material = new(silhouetteShader) { name = "NinjaFormSilhouette" };
			run.OnRelease(() => UnityEngine.Object.Destroy(material));
			return material;
		}

		// ---- 먹 방울과 빛 ----

		private void ComposeDrops(UltimateFxRun run)
		{
			ParticleSystem drops = run.Particles("NinjaFormDrops", run.Alpha(UltimateFxTextures.White, Color.white), dropCount + 4, 1f);
			run.At(impactTime, () =>
			{
				for (int i = 0; i < dropCount; i++)
				{
					Vector3 outward = UltimateFxStage.RandomRingPoint(0.5f, 0.8f);
					int texels = Random.Range(1, 4);
					UltimateFxStage.Emit(drops, run.Caster + outward + Vector3.up * Random.Range(0.3f, 0.8f), outward.normalized * Random.Range(1.2f, 3.6f) + Vector3.up * Random.Range(1.2f, 3.2f), texels / texelsPerUnit, Random.Range(0.4f, 0.6f), texels > 1 ? dropColor : dropLight);
				}
			});
		}

		private void ComposeLight(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Light lamp = WorldLight(stage, "NinjaFormLight", LightType.Point, lightColor, lightRange);
			run.Track(0f, DurationSeconds, _ =>
			{
				lamp.transform.position = run.Caster + Vector3.up * 1.2f;
				lamp.intensity = impactLight * Spike(run.Now, impactTime, 0.02f, 0.12f);
			}, () => lamp.enabled = false);
			float Dim(float now) => Plateau(now, 0f, 0.25f, impactTime + 0.28f, restoreSeconds);
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Dim(now));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Dim(now));
			stage.Vignette = now => 0.28f * Dim(now);
			stage.Filter = now => Color.Lerp(Color.white, new Color(0.86f, 0.88f, 1f), Dim(now));
		}
	}
}
