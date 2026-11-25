using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 알리사 빛의 검의 그림. 검기는 앞으로 나는 초승달과 뒤따르는 빛줄, 최상 단계는 끝에서 별 모양으로 터지고, 날개 폭발은 깃이 한 번 펼쳐짐
	// - 판정 없음. SwordWaveEmitter 가 검기를 낼 때 같은 자리, 방향, 길이로 부름
	// - 결은 변신 연출과 같음 (FormEnergy. 캐릭터 PPU 와 같은 텍셀, 네 단 색). 게임 시간
	// - 검기 판정 오브젝트 (0.2초) 보다 그림이 길어서 따로 둠. 만든 물체는 이 오브젝트의 자식 (풀과 같이 씬을 넘어 살아남음), 다시 쓰고 파괴될 때 지움
	// - 그림은 LateUpdate 에서만 그림. PlayWave 와 PlayWings 는 예약만 해서 부른 쪽 (판정) 을 끊지 않음
	public sealed class SwordWaveFx : MonoBehaviour
	{
		private static readonly int CORE_COLOR_ID = Shader.PropertyToID("_CoreColor");
		private static readonly int BODY_COLOR_ID = Shader.PropertyToID("_BodyColor");
		private static readonly int TIP_COLOR_ID = Shader.PropertyToID("_TipColor");
		private static readonly int TEXELS_ID = Shader.PropertyToID("_Texels");
		private static readonly int SHAPE_ID = Shader.PropertyToID("_Shape");
		private static readonly int FLASH_ID = Shader.PropertyToID("_Flash");
		private static readonly int DISSOLVE_ID = Shader.PropertyToID("_Dissolve");
		private static readonly int DISSOLVE_FROM_TIP_ID = Shader.PropertyToID("_DissolveFromTip");
		private const float FEATHER_SHAPE = 1f;
		private const float GLOW_SHAPE = 2f;
		private const int QUEUE = 3012;
		private const int BEHIND_BODY_QUEUE = 2962;

		[SerializeField] private Shader energyShader;

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color coreColor = new(1.15f, 1.12f, 1.02f);
		[SerializeField, ColorUsage(false, true)] private Color bodyColor = new(0.98f, 0.86f, 0.52f);
		[SerializeField, ColorUsage(false, true)] private Color tipColor = new(0.62f, 0.46f, 0.2f);
		[SerializeField, Min(8f)] private float texelsPerUnit = 32f;

		[Header("검기")]
		[SerializeField, Min(0.1f)] private float crescentRadius = 0.64f;
		[SerializeField, Range(20f, 300f)] private float crescentDegrees = 150f;
		[SerializeField, Range(0.05f, 1f), Tooltip("가운데 두께 (반지름 1 당)")] private float crescentWidth = 0.42f;
		[SerializeField, Min(0f)] private float height = 0.7f;
		[SerializeField, Min(0f), Tooltip("나오는 자리 (인물 앞)")] private float startDistance = 0.4f;
		[SerializeField, Min(0.02f), Tooltip("끝까지 나는 길이 (초)")] private float flySeconds = 0.17f;
		[SerializeField, Min(0.02f)] private float fadeSeconds = 0.16f;
		[SerializeField, Min(0.1f), Tooltip("뒤따르는 빛줄의 길이")] private float trailLength = 2.4f;
		[SerializeField, Min(0.01f)] private float trailWidth = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("나는 동안 커지는 정도")] private float grow = 0.15f;
		[SerializeField, Min(0.01f), Tooltip("나온 번쩍임이 절반이 되는 길이")] private float flashHalfLife = 0.06f;

		[Header("끝 폭발")]
		[SerializeField, Min(0f)] private float burstSize = 2f;
		[SerializeField, Min(1)] private int burstRays = 8;
		[SerializeField, Range(0.02f, 1f)] private float rayWidth = 0.14f;
		[SerializeField, Min(0.02f)] private float burstSeconds = 0.26f;

		[Header("날개")]
		[SerializeField, Min(1)] private int feathersPerSide = 7;
		[SerializeField, Min(0.1f)] private float featherLength = 2f;
		[SerializeField, Tooltip("아래 깃과 위 깃의 각도 (도)")] private Vector2 featherAngles = new(-18f, 62f);
		[SerializeField, Min(0f)] private float wingHeight = 0.75f;
		[SerializeField, Range(0f, 1f), Tooltip("날개 판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;
		[SerializeField, Min(0.02f)] private float wingHold = 0.45f;
		[SerializeField, Min(0.02f)] private float wingBreak = 0.4f;

		private sealed class Wave
		{
			public Transform Crescent;
			public Transform Trail;
			public Material Material;
			public Vector3 Origin;
			public Vector3 Direction;
			public float Length;
			public float Width;
			public float StartTime;
			public bool BurstAtEnd;
			public bool Alive;
		}

		private sealed class Burst
		{
			public Transform[] Rays;
			public Material Material;
			public float StartTime;
			public bool Alive;
		}

		private readonly List<Wave> waves = new();
		private readonly List<Burst> bursts = new();
		private readonly List<UnityEngine.Object> owned = new();
		private readonly List<Transform> feathers = new();
		private Transform root;
		private Transform wingOwner;
		private Camera viewCamera;
		private bool wingsShown;
		private Material wingMaterial;
		private Mesh crescentMesh;
		private Mesh sheathMesh;
		private Mesh rayMesh;
		private float wingStart = float.NegativeInfinity;

		// 검기 하나. length 는 월드 길이, width 는 배율
		public void PlayWave(Vector3 origin, Vector3 direction, float length, float width, bool burstAtEnd)
		{
			Prepare();
			Wave wave = waves.Find(candidate => candidate.Alive == false);
			if (wave == null)
			{
				wave = new Wave { Material = Energy(QUEUE) };
				wave.Crescent = UltimateFxStage.CreateMeshObject("SwordCrescent", root, crescentMesh, wave.Material).transform;
				wave.Trail = UltimateFxStage.CreateMeshObject("SwordTrail", root, sheathMesh, wave.Material).transform;
				wave.Material.SetVector(TEXELS_ID, new Vector4(Mathf.Round(crescentRadius * 3f * texelsPerUnit), Mathf.Round(crescentRadius * crescentWidth * texelsPerUnit), 0f, 0f));
				waves.Add(wave);
			}
			wave.Origin = origin + Vector3.up * height;
			wave.Direction = direction.normalized;
			wave.Length = length;
			wave.Width = width;
			wave.BurstAtEnd = burstAtEnd;
			wave.StartTime = Time.time;
			wave.Alive = true;
		}

		public void PlayWings(Transform owner)
		{
			Prepare();
			wingOwner = owner;
			wingStart = Time.time;
			if (feathers.Count > 0)
				return;
			wingMaterial = Energy(BEHIND_BODY_QUEUE);
			wingMaterial.SetFloat(SHAPE_ID, FEATHER_SHAPE);
			wingMaterial.SetVector(TEXELS_ID, new Vector4(Mathf.Round(featherLength * texelsPerUnit), 10f, 0f, 0f));
			Mesh feather = Own(FormMeshes.Feather(8, 0.3f, 0.25f, 0.15f, 0.2f));
			for (int i = 0; i < feathersPerSide * 2; i++)
				feathers.Add(UltimateFxStage.CreateMeshObject("SwordFeather", root, feather, wingMaterial).transform);
		}

		private void OnDisable()
		{
			foreach (Wave wave in waves)
				End(wave);
			foreach (Burst burst in bursts)
				End(burst);
			wingStart = float.NegativeInfinity;
			foreach (Transform feather in feathers)
				feather.gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			foreach (UnityEngine.Object target in owned)
				Destroy(target);
		}

		private void LateUpdate()
		{
			viewCamera = Camera.main;
			// 씬이 바뀌는 사이에는 카메라가 없음. 그 프레임은 그리지 않음
			if (viewCamera == null)
				return;
			foreach (Wave wave in waves)
			{
				if (wave.Alive)
					UpdateWave(wave);
			}
			foreach (Burst burst in bursts)
			{
				if (burst.Alive)
					UpdateBurst(burst);
			}
			if (wingsShown || Time.time - wingStart < wingHold + wingBreak)
				UpdateWings();
		}

		private void UpdateWave(Wave wave)
		{
			float age = Time.time - wave.StartTime;
			float fly = Mathf.Clamp01(age / flySeconds);
			float fade = Mathf.Clamp01((age - flySeconds) / fadeSeconds);
			if (fade >= 1f)
			{
				End(wave);
				return;
			}
			if (fly >= 1f && wave.BurstAtEnd)
			{
				wave.BurstAtEnd = false;
				PlayBurst(wave.Origin + wave.Direction * wave.Length);
			}
			float distance = Mathf.Lerp(startDistance, wave.Length, 1f - (1f - fly) * (1f - fly));
			float size = crescentRadius * wave.Width * (1f + grow * fly);
			Vector3 head = wave.Origin + wave.Direction * distance;
			// 초승달의 앞날이 head 에 오게
			wave.Crescent.SetPositionAndRotation(head - wave.Direction * size, Quaternion.LookRotation(wave.Direction, Vector3.up));
			wave.Crescent.localScale = new Vector3(size, 1f, size);
			float trail = Mathf.Min(trailLength, distance) * (1f - fade);
			wave.Trail.SetPositionAndRotation(head - wave.Direction * (size * 0.2f), Quaternion.FromToRotation(Vector3.up, -wave.Direction));
			wave.Trail.localScale = new Vector3(trailWidth * wave.Width, Mathf.Max(0.01f, trail), trailWidth * wave.Width);
			wave.Material.SetFloat(DISSOLVE_ID, fade);
			wave.Material.SetFloat(FLASH_ID, Mathf.Pow(0.5f, age / flashHalfLife) * 0.8f);
			wave.Crescent.gameObject.SetActive(true);
			wave.Trail.gameObject.SetActive(true);
		}

		private void PlayBurst(Vector3 position)
		{
			Burst burst = bursts.Find(candidate => candidate.Alive == false);
			if (burst == null)
			{
				burst = new Burst { Material = Energy(QUEUE + 1), Rays = new Transform[burstRays] };
				burst.Material.SetFloat(SHAPE_ID, GLOW_SHAPE);
				burst.Material.SetVector(TEXELS_ID, new Vector4(Mathf.Round(burstSize * texelsPerUnit), 6f, 0f, 0f));
				for (int i = 0; i < burstRays; i++)
					burst.Rays[i] = UltimateFxStage.CreateMeshObject("SwordRay", root, rayMesh, burst.Material).transform;
				bursts.Add(burst);
			}
			Vector3 facing = -viewCamera.transform.forward;
			for (int i = 0; i < burst.Rays.Length; i++)
			{
				Vector3 direction = Quaternion.AngleAxis(360f * i / burst.Rays.Length, facing) * viewCamera.transform.up;
				burst.Rays[i].SetPositionAndRotation(position + facing * 0.1f, Quaternion.LookRotation(-facing, direction));
				burst.Rays[i].gameObject.SetActive(true);
			}
			burst.StartTime = Time.time;
			burst.Alive = true;
			UpdateBurst(burst);
		}

		private void UpdateBurst(Burst burst)
		{
			float progress = (Time.time - burst.StartTime) / burstSeconds;
			if (progress >= 1f)
			{
				End(burst);
				return;
			}
			burst.Material.SetFloat(DISSOLVE_ID, Mathf.Clamp01((progress - 0.35f) / 0.65f));
			burst.Material.SetFloat(FLASH_ID, 1f - progress);
			for (int i = 0; i < burst.Rays.Length; i++)
			{
				float length = burstSize * (i % 2 == 0 ? 1f : 0.5f) * (0.8f + 0.3f * progress);
				burst.Rays[i].localScale = new Vector3(burstSize * rayWidth, length, 1f);
			}
		}

		private void UpdateWings()
		{
			float age = Time.time - wingStart;
			bool visible = age < wingHold + wingBreak && wingOwner != null;
			wingsShown = visible;
			if (visible)
			{
				wingMaterial.SetFloat(DISSOLVE_ID, Mathf.Clamp01((age - wingHold) / wingBreak));
				wingMaterial.SetFloat(FLASH_ID, Mathf.Pow(0.5f, age / flashHalfLife));
			}
			Transform cameraTransform = viewCamera.transform;
			Vector3 right = cameraTransform.right;
			Vector3 up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
			Vector3 away = Vector3.Cross(right, up).normalized;
			for (int i = 0; i < feathers.Count; i++)
			{
				feathers[i].gameObject.SetActive(visible);
				if (visible == false)
					continue;
				float side = i < feathersPerSide ? 1f : -1f;
				float along = feathersPerSide <= 1 ? 0.5f : i % feathersPerSide / (feathersPerSide - 1f);
				float angle = Mathf.Lerp(featherAngles.x, featherAngles.y, along);
				Vector3 outward = Quaternion.AngleAxis(angle * side, -away) * (right * side);
				Vector3 featherUp = Vector3.Cross(away, outward) * side;
				feathers[i].SetPositionAndRotation(wingOwner.position + Vector3.up * wingHeight + away * 0.2f, Quaternion.LookRotation(Vector3.Cross(outward, featherUp), featherUp));
				feathers[i].localScale = Vector3.one * (featherLength * Mathf.Lerp(0.65f, 1f, along));
			}
		}

		private static void End(Wave wave)
		{
			wave.Alive = false;
			wave.Crescent.gameObject.SetActive(false);
			wave.Trail.gameObject.SetActive(false);
		}

		private static void End(Burst burst)
		{
			burst.Alive = false;
			foreach (Transform ray in burst.Rays)
				ray.gameObject.SetActive(false);
		}

		private void Prepare()
		{
			if (root != null)
				return;
			if (energyShader == null)
				throw new InvalidOperationException($"{name}: 검기 셰이더 미연결");
			root = new GameObject(nameof(SwordWaveFx)).transform;
			root.SetParent(transform, false);
			crescentMesh = Own(FormMeshes.Crescent(28, crescentDegrees, crescentWidth));
			sheathMesh = Own(FormMeshes.Sheath(2, 10, 1f));
			rayMesh = Own(FormMeshes.Sheath(1, 8, 1f));
		}

		private Material Energy(int queue)
		{
			Material material = Own(new Material(energyShader) { name = nameof(SwordWaveFx), renderQueue = queue });
			material.SetColor(CORE_COLOR_ID, coreColor);
			material.SetColor(BODY_COLOR_ID, bodyColor);
			material.SetColor(TIP_COLOR_ID, tipColor);
			material.SetFloat(DISSOLVE_FROM_TIP_ID, 0f);
			return material;
		}

		private T Own<T>(T target) where T : UnityEngine.Object
		{
			owned.Add(target);
			return target;
		}
	}
}
