using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 카메라 주변 바닥을 덮는 렌더 텍스처 한 장. 처치 얼룩, 누운 조각 자국, 그을음을 도장으로 찍고 유지 시간에 걸쳐 옅어짐
	/// - 개체 수 0. 도장은 프레임마다 모아서 한 번 그림
	/// - 중심은 화소 격자에 맞춰 옮기고, 옮긴 만큼 텍스처를 밀어 바깥은 지움
	/// - 보이는 것은 바닥 위 얇은 판 하나 (셰이더 WM/Remains/GroundStain). 판 없이 다른 셰이더가 읽는 용도 (풀 눌림) 로도 씀
	/// </summary>
	public sealed class GroundStainCanvas
	{
		private struct Stamp
		{
			public Vector3 Position;
			public float Radius;
			public float Rotation;
			public Color Color;
		}

		private static readonly int SCROLL_ID = Shader.PropertyToID("_Scroll");
		private static readonly int FADE_ID = Shader.PropertyToID("_Fade");
		private static readonly int CANVAS_ID = Shader.PropertyToID("_StainCanvas");
		private static readonly int OPACITY_ID = Shader.PropertyToID("_Opacity");
		private static readonly int ALPHA_GAIN_ID = Shader.PropertyToID("_AlphaGain");
		private static readonly int STAMP_TEX_ID = Shader.PropertyToID("_StampTex");
		private const int PASS_SCROLL = 0;
		private const int PASS_FADE = 1;
		private const int PASS_STAMP = 2;

		private readonly List<Stamp> pending = new();
		private readonly Material utility;
		private readonly Material display;
		private readonly Transform plane;
		// 판 없는 캔버스 (풀 눌림) 는 이 값이 null
		private RenderTexture canvas;
		private RenderTexture scratch;
		private Vector2 centerTexel;
		private bool centerValid;
		private float texelSize;
		private float fadeCarry;

		// 지금 캔버스와 덮는 범위 (xz 시작점, 한 변). 다른 셰이더에 넘길 때
		public Texture Texture => canvas;
		public Vector4 WorldRect { get; private set; }

		private readonly int resolutionOverride;
		private readonly RenderTextureFormat format;
		private Mesh planeMesh;

		// resolution 이 0 이면 설정의 CanvasResolution. 판 없는 캔버스 (풀 눌림) 는 낮은 해상도와 한 채널로
		public GroundStainCanvas(Transform parent, Shader utilityShader, Shader displayShader, int resolution = 0, RenderTextureFormat format = RenderTextureFormat.ARGB32)
		{
			resolutionOverride = resolution;
			this.format = format;
			utility = new Material(utilityShader) { hideFlags = HideFlags.HideAndDontSave };
			if (displayShader == null)
				return;
			display = new Material(displayShader) { hideFlags = HideFlags.HideAndDontSave };
			GameObject planeObject = new("GroundStain");
			planeObject.transform.SetParent(parent, false);
			MeshFilter filter = planeObject.AddComponent<MeshFilter>();
			planeMesh = BuildPlane();
			filter.sharedMesh = planeMesh;
			MeshRenderer renderer = planeObject.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = display;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			plane = planeObject.transform;
		}

		// 가로세로 1 짜리 바닥 판. 크기는 transform 으로
		internal static Mesh BuildPlane()
		{
			Mesh mesh = new() { name = "GroundStainPlane" };
			mesh.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f) };
			mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
			mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
			mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
			mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, .1f, 1f));
			return mesh;
		}

		private int Resolution(RemainsSettings settings) => resolutionOverride > 0 ? resolutionOverride : settings.CanvasResolution;

		private void EnsureTextures(RemainsSettings settings)
		{
			int resolution = Resolution(settings);
			if (canvas != null && canvas.width == resolution)
				return;
			Release();
			canvas = NewTexture(resolution, format);
			scratch = NewTexture(resolution, format);
			ClearTexture(canvas);
			centerValid = false;
		}

		private static RenderTexture NewTexture(int resolution, RenderTextureFormat format)
		{
			RenderTexture texture = new(resolution, resolution, 0, format)
			{
				name = "GroundStainCanvas",
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				useMipMap = false,
			};
			texture.Create();
			return texture;
		}

		private static void ClearTexture(RenderTexture texture)
		{
			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = texture;
			GL.Clear(false, true, Color.clear);
			RenderTexture.active = previous;
		}

		public void Add(Vector3 position, float radius, Color color)
		{
			pending.Add(new Stamp { Position = position, Radius = radius, Rotation = Random.Range(0f, 360f), Color = color });
		}

		public void Tick(RemainsSettings settings, float lingerSeconds, Vector3 focus, float groundHeight, float deltaTime)
		{
			EnsureTextures(settings);
			texelSize = settings.CanvasSize / Resolution(settings);
			Recenter(settings, focus, groundHeight);
			Fade(lingerSeconds, deltaTime);
			DrawStamps(settings);
			Vector2 origin = centerTexel * texelSize - Vector2.one * settings.CanvasSize * .5f;
			WorldRect = new Vector4(origin.x, origin.y, settings.CanvasSize, settings.CanvasSize);
			if (display == null)
				return;
			display.SetTexture(CANVAS_ID, canvas);
			display.SetFloat(OPACITY_ID, settings.CanvasOpacity);
			display.SetFloat(ALPHA_GAIN_ID, settings.CanvasAlphaGain);
		}

		private void Recenter(RemainsSettings settings, Vector3 focus, float groundHeight)
		{
			Vector2 nextTexel = new(Mathf.Round(focus.x / texelSize), Mathf.Round(focus.z / texelSize));
			if (centerValid && nextTexel != centerTexel)
			{
				// 중심이 오른쪽으로 가면 내용은 왼쪽으로 밀림
				Vector2 shift = (nextTexel - centerTexel) / Resolution(settings);
				utility.SetVector(SCROLL_ID, new Vector4(shift.x, shift.y, 0f, 0f));
				Graphics.Blit(canvas, scratch, utility, PASS_SCROLL);
				(canvas, scratch) = (scratch, canvas);
			}
			centerTexel = nextTexel;
			centerValid = true;
			if (plane == null)
				return;
			plane.SetPositionAndRotation(new Vector3(centerTexel.x * texelSize, groundHeight, centerTexel.y * texelSize), Quaternion.identity);
			plane.localScale = new Vector3(settings.CanvasSize, 1f, settings.CanvasSize);
		}

		// 알파를 시간에 비례해 빼서 유지 시간이면 0. 8비트라 한 번에 한 칸 이상 뺄 때만 그림
		private void Fade(float lingerSeconds, float deltaTime)
		{
			if (float.IsPositiveInfinity(lingerSeconds))
				return;
			fadeCarry += deltaTime / lingerSeconds;
			const float ONE_STEP = 1f / 255f;
			if (fadeCarry < ONE_STEP)
				return;
			utility.SetFloat(FADE_ID, fadeCarry);
			fadeCarry = 0f;
			Graphics.Blit(canvas, scratch, utility, PASS_FADE);
			(canvas, scratch) = (scratch, canvas);
		}

		private void DrawStamps(RemainsSettings settings)
		{
			if (pending.Count == 0)
				return;
			if (settings.StampTexture == null)
				throw new System.InvalidOperationException($"{nameof(RemainsSettings)}.{nameof(RemainsSettings.StampTexture)} 미연결");
			int count = Mathf.Min(pending.Count, settings.MaxStampsPerFrame);
			utility.SetTexture(STAMP_TEX_ID, settings.StampTexture);
			RenderTexture previous = RenderTexture.active;
			Graphics.SetRenderTarget(canvas);
			GL.PushMatrix();
			GL.LoadOrtho();
			utility.SetPass(PASS_STAMP);
			GL.Begin(GL.QUADS);
			Vector2 origin = centerTexel * texelSize - Vector2.one * settings.CanvasSize * .5f;
			for (int i = 0; i < count; i++)
				EmitQuad(pending[i], origin, settings.CanvasSize);
			GL.End();
			GL.PopMatrix();
			RenderTexture.active = previous;
			pending.RemoveRange(0, count);
		}

		private static void EmitQuad(Stamp stamp, Vector2 origin, float canvasSize)
		{
			Vector2 center = (new Vector2(stamp.Position.x, stamp.Position.z) - origin) / canvasSize;
			float half = stamp.Radius / canvasSize;
			float radians = stamp.Rotation * Mathf.Deg2Rad;
			Vector2 axisX = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * half;
			Vector2 axisY = new Vector2(-axisX.y, axisX.x);
			GL.Color(stamp.Color);
			GL.TexCoord2(0f, 0f);
			GL.Vertex3(center.x - axisX.x - axisY.x, center.y - axisX.y - axisY.y, 0f);
			GL.TexCoord2(0f, 1f);
			GL.Vertex3(center.x - axisX.x + axisY.x, center.y - axisX.y + axisY.y, 0f);
			GL.TexCoord2(1f, 1f);
			GL.Vertex3(center.x + axisX.x + axisY.x, center.y + axisX.y + axisY.y, 0f);
			GL.TexCoord2(1f, 0f);
			GL.Vertex3(center.x + axisX.x - axisY.x, center.y + axisX.y - axisY.y, 0f);
		}

		public void Clear()
		{
			pending.Clear();
			if (canvas != null)
				ClearTexture(canvas);
			fadeCarry = 0f;
		}

		public void SetVisible(bool visible)
		{
			if (plane != null && plane.gameObject.activeSelf != visible)
				plane.gameObject.SetActive(visible);
		}

		private void Release()
		{
			if (canvas != null)
				Object.Destroy(canvas);
			if (scratch != null)
				Object.Destroy(scratch);
			canvas = null;
			scratch = null;
		}

		public void Dispose()
		{
			Release();
			Object.Destroy(utility);
			if (display != null)
				Object.Destroy(display);
			if (planeMesh != null)
				Object.Destroy(planeMesh);
			if (plane != null)
				Object.Destroy(plane.gameObject);
		}
	}
}
