using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 한 번의 진행기. 파생 연출 (UltimateFx.Compose) 이 시간선 (트랙) 에 효과를 올리면 매 프레임 평가
	/// - 시간은 실제 시간 (녹화 중에는 고정 간격). 히트스톱 중에도 화면 연출은 계속 감, 입자는 게임 시간이라 같이 멈춤
	/// - 이 파일: 진행, 트랙, 재질과 판 생성, 화면 섬광, 카메라. 여러 연출이 같이 쓰는 월드 부품은 UltimateFxRun.Parts.cs
	/// - 끝나면 만든 물체, 재질, 화면 층, 배경 장악 판, 카메라 설정을 전부 되돌림
	/// </summary>
	public sealed partial class UltimateFxRun
	{
		private sealed class TimelineEntry
		{
			public float Start;
			public float Duration;
			public Action<float> Apply;
			public Action End;
			public bool Finished;
			public bool Shown;
		}

		private readonly UltimateFx fx;
		private readonly CombatFxContext context;
		private readonly List<TimelineEntry> tracks = new();
		private readonly List<TimelineEntry> added = new();
		private readonly List<Material> materials = new();
		private readonly List<Action> releases = new();

		private GameObject root;
		private bool disposed;
		private float realTime;
		private float realDelta;

		public UltimateFxRun(UltimateFx fx, CombatFxContext context)
		{
			this.fx = fx;
			this.context = context;
		}

		public UltimateScreenLayer Layer { get; private set; }
		public UltimateGradeLayer Grade { get; private set; }
		public Camera MainCamera { get; private set; }
		public Transform Root => root.transform;
		/// <summary> 연출 중심 (바닥 높이) </summary>
		public Vector3 Center { get; private set; }
		/// <summary> 시전자 발 위치. 따라갈 대상이 없으면 중심 </summary>
		public Vector3 Caster => context.Follow != null ? context.Follow.position : Center;
		/// <summary> 화면 오른쪽을 바닥에 눕힌 방향 </summary>
		public Vector3 ScreenRight { get; private set; }
		/// <summary> 화면 위쪽 (카메라가 보는 쪽) 을 바닥에 눕힌 방향 </summary>
		public Vector3 ScreenForward { get; private set; }
		/// <summary> 시작부터 흐른 실제 시간 (초) </summary>
		public float Now => realTime;
		public float Delta => realDelta;
		public UltimateFx Settings => fx;

		public IEnumerator Routine()
		{
			Setup();
			fx.Compose(this);
			float end = fx.DurationSeconds + fx.TailSeconds;
			while (realTime < end || (tracks.Count > 0 && realTime < end * 2f))
			{
				// 프레임 녹화 중에는 고정 간격 (DevFrameRecorder), 아니면 실제 경과
				realDelta = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
				realTime += realDelta;
				EvaluateTracks();
				Grade.Follow();
				yield return null;
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			foreach (Action release in releases)
				release();
			Layer?.Dispose();
			Grade?.Dispose();
			if (root != null)
				UnityEngine.Object.Destroy(root);
			foreach (Material material in materials)
				UnityEngine.Object.Destroy(material);
		}

		private void Setup()
		{
			MainCamera = Camera.main;
			Vector3 position = context.Position;
			position.y = UltimateFxStage.GroundHeight(position);
			Center = position;
			root = new GameObject("UltimateFxRun");
			root.transform.position = Center;
			Layer = new UltimateScreenLayer(fx.SpriteShader, fx.ScreenSortingOrder);
			Grade = new UltimateGradeLayer(fx.GradeShader, MainCamera, UltimateFxStage.CenterQuad);
			Vector3 right = MainCamera != null ? MainCamera.transform.right : Vector3.right;
			Vector3 forward = MainCamera != null ? MainCamera.transform.forward : Vector3.forward;
			right.y = 0f;
			forward.y = 0f;
			ScreenRight = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
			ScreenForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
			if (fx.SpawnTemporaryFoliage)
				SpawnFoliage();
		}

		// ---- 트랙 ----

		/// <summary> start 초부터 duration 초 동안 매 프레임 apply(0~1). 끝나면 end </summary>
		public void Track(float start, float duration, Action<float> apply, Action end = null)
			=> added.Add(new TimelineEntry { Start = start, Duration = duration, Apply = apply, End = end });

		/// <summary> time 초에 한 번 </summary>
		public void At(float time, Action action) => Track(time, 0f, _ => action());

		/// <summary> 진행이 끝날 때 (도중 중단 포함) 부를 정리 </summary>
		public void OnRelease(Action release) => releases.Add(release);

		// 올린 순서대로 평가. 같은 시각에 만드는 트랙 (At) 과 쓰는 트랙 (Track) 이 있으면 먼저 올린 쪽이 먼저 돎
		private void EvaluateTracks()
		{
			MergeAdded();
			int count = tracks.Count;
			for (int i = 0; i < count; i++)
			{
				TimelineEntry track = tracks[i];
				if (realTime < track.Start)
					continue;
				float progress = track.Duration <= 0f ? 1f : Mathf.Min(1f, (realTime - track.Start) / track.Duration);
				// 한 장짜리 트랙이 프레임 사이에 끼어 그려지지 않는 일을 막음: 길이가 있으면 적어도 한 프레임은 끝내지 않고 보여 줌
				if (track.Duration > 0f && track.Shown == false && progress >= 1f)
				{
					track.Shown = true;
					track.Apply?.Invoke(0.5f);
					continue;
				}
				track.Shown = true;
				track.Apply?.Invoke(progress);
				if (progress >= 1f)
				{
					track.End?.Invoke();
					track.Finished = true;
				}
			}
			tracks.RemoveAll(entry => entry.Finished);
			MergeAdded();
		}

		private void MergeAdded()
		{
			if (added.Count == 0)
				return;
			tracks.AddRange(added);
			added.Clear();
		}

		// ---- 곡선 ----
		public static float EaseOut(float progress) => 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);

		public static float EaseIn(float progress) => Mathf.Pow(Mathf.Clamp01(progress), 3f);

		public static float EaseInOut(float progress) => Mathf.SmoothStep(0f, 1f, progress);

		/// <summary> 0 에서 attack 비율까지 올라가고 끝까지 내려오는 종 모양 </summary>
		public static float Bell(float progress, float attack)
		{
			if (progress < attack)
				return attack <= 0f ? 1f : progress / attack;
			return 1f - Mathf.Clamp01((progress - attack) / Mathf.Max(0.0001f, 1f - attack));
		}

		/// <summary> 구간 [from, to] 안 위치 0~1 </summary>
		public static float Span(float value, float from, float to) => Mathf.Clamp01((value - from) / Mathf.Max(0.0001f, to - from));

		// ---- 재질과 판 ----
		public Material Additive(Texture texture, Color color) => NewMaterial(texture, true, color);

		public Material Alpha(Texture texture, Color color) => NewMaterial(texture, false, color);

		private Material NewMaterial(Texture texture, bool additive, Color color)
		{
			Material material = UltimateFxStage.CreateMaterial(fx.SpriteShader, texture, additive, color);
			materials.Add(material);
			return material;
		}

		public Material Shockwave()
		{
			Material material = new(fx.ShockwaveShader) { name = "UltimateFx_Shockwave" };
			materials.Add(material);
			return material;
		}

		/// <summary> 바닥에 눕힌 판. order 가 클수록 나중에 그림. 인물 (큐 3000) 보다 먼저 그려 인물이 위에 옴 </summary>
		public GameObject Plate(string name, Material material, Vector3 position, float size, int order)
		{
			GameObject plate = UltimateFxStage.CreateMeshObject(name, root.transform, UltimateFxStage.GroundQuad, material);
			material.renderQueue = 2940 + order;
			plate.transform.position = position;
			plate.transform.localScale = new Vector3(size, 1f, size);
			return plate;
		}

		/// <summary>
		/// 카메라를 향한 판. upright 면 발 중심 원점으로 세워 좌우만 돌림 (기둥, 검), 아니면 가운데 원점으로 카메라 면과 나란히 (달, 카드, 블랙홀)
		/// - overWorld 면 깊이 검사 없이 배경과 바닥 위에, 인물 (큐 3000) 보다는 먼저 그림 (하늘의 큰 물체가 인물을 가리지 않게)
		/// </summary>
		public GameObject Billboard(string name, Material material, Vector3 position, float width, float height, bool upright, bool overWorld, int order)
		{
			Mesh mesh = upright ? UltimateFxStage.StandingQuad : UltimateFxStage.CenterQuad;
			GameObject board = UltimateFxStage.CreateMeshObject(name, root.transform, mesh, material, order);
			if (overWorld)
				material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
			material.renderQueue = overWorld ? 2960 + order : 3000 + order;
			board.transform.position = position;
			board.transform.localScale = new Vector3(width, height, 1f);
			UltimateBillboard billboard = board.AddComponent<UltimateBillboard>();
			billboard.Upright = upright;
			return board;
		}

		public ParticleSystem Particles(string name, Material material, int maxParticles, float gravity, bool stretched = false)
			=> UltimateFxStage.CreateParticles(name, root.transform, material, maxParticles, gravity, stretched);

		public Light PointLight(string name, Color color, float range)
		{
			GameObject lightObject = new(name);
			lightObject.transform.SetParent(root.transform, false);
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = color;
			light.range = range;
			light.intensity = 0f;
			light.shadows = LightShadows.None;
			return light;
		}

		public void Destroy(UnityEngine.Object target)
		{
			if (target != null)
				UnityEngine.Object.Destroy(target);
		}

		// ---- 화면 ----

		/// <summary> 섬광. 배경은 상한까지 섬광 색으로, 인물 위 화면 층은 더 낮은 상한으로 (인물이 묻히지 않게) </summary>
		public void Flash(float amount, Color color)
		{
			float clamped = Mathf.Clamp01(amount);
			Grade.SetFlashColor(color);
			Grade.Flash = clamped * fx.BackgroundFlashCap;
			Layer.SetFlash(clamped * fx.OverlayFlashCap, color);
		}

		// ---- 카메라 ----

		/// <summary> 카메라 거리 배율과 시야각 변화. 돌려받은 함수를 부르면 복귀 시작, 진행이 끝나면 자동 복귀 </summary>
		public Action Zoom(float distanceScale, float fieldOfView, float inSeconds, float holdSeconds, float outSeconds)
		{
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager) == false)
				return delegate { };
			Action release = cameraManager.PunchZoom(distanceScale, fieldOfView, inSeconds, holdSeconds, outSeconds, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
			bool released = false;
			Action once = () =>
			{
				if (released)
					return;
				released = true;
				release?.Invoke();
			};
			releases.Add(once);
			return once;
		}

		public void Shake(float strength)
		{
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
				cameraManager.GenerateCameraImpulse(strength);
		}

		/// <summary> 줄어드는 연속 흔들림 </summary>
		public void ShakeSeries(float start, int count, float interval, float strength, float decay)
		{
			float amplitude = strength;
			for (int i = 0; i < count; i++)
			{
				float current = amplitude;
				At(start + i * interval, () => Shake(current));
				amplitude *= decay;
			}
		}

		public void HitStop(float seconds, float scale)
		{
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.HitStop(seconds, scale);
		}

		/// <summary> 월드 위치의 화면 좌표 (0~1). 카메라가 없으면 가운데 </summary>
		public Vector2 Viewport(Vector3 position)
		{
			if (MainCamera == null)
				return new Vector2(0.5f, 0.5f);
			Vector3 viewport = MainCamera.WorldToViewportPoint(position);
			return new Vector2(viewport.x, viewport.y);
		}
	}
}
