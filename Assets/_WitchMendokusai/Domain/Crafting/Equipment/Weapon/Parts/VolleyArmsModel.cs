using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 몸에 붙은 광석 무장 (3D). 두 팔은 회전 총열, 두 어깨는 발사관. 조준을 따라 돎
	// - 판정 없음. VolleyArsenal 이 꺼냄, 쏘는 동안, 거둠을 알리고 총구 자리를 물음
	// - 인물 그림은 카메라를 보는 판이라 어깨와 조준 반대쪽 팔을 카메라 깊이로 밀어 얼굴을 가리지 않음
	// - 빛줄기 (팔), 빛 미사일 (어깨) 은 그림. 피해는 VolleyGauge 의 탄
	// - 만든 물체는 이 오브젝트의 자식 (풀과 같이 씬을 넘어 살아남음), 자리는 월드
	public sealed class VolleyArmsModel : MonoBehaviour
	{
		private static readonly int DISSOLVE_ID = Shader.PropertyToID("_Dissolve");
		private static readonly int TEXELS_ID = Shader.PropertyToID("_Texels");

		private const int ARMS = 2;
		private const int BODY_PARTS = 4;

		[Header("그리기")]
		[SerializeField] private Shader solidShader;
		[SerializeField] private Shader energyShader;
		[SerializeField] private Shader ribbonShader;
		[SerializeField, Min(1f)] private float texelsPerUnit = 32f;
		[SerializeField, ColorUsage(false, true)] private Color oreGlow = new(1.9f, 1.6f, 0.7f);
		[SerializeField, ColorUsage(false, true)] private Color hotColor = new(1.6f, 1.35f, 0.8f);
		[SerializeField] private Color outlineColor = new(0.3f, 0.19f, 0.09f);
		[SerializeField, Min(0f)] private float outline = 0.022f;

		[Header("자리 (월드)")]
		[SerializeField, Tooltip("팔 (화면 옆, 위, 앞)")] private Vector3 armSlot = new(0.6f, 0.3f, 0.05f);
		[SerializeField, Tooltip("어깨 (화면 옆, 위, 앞)")] private Vector3 podSlot = new(0.84f, 1.2f, -0.1f);
		[SerializeField, Min(0f), Tooltip("어깨를 카메라 깊이로 미는 거리. 인물 뒤로")] private float podDepth = 0.95f;
		[SerializeField, Min(0f), Tooltip("조준 반대쪽 팔을 미는 거리")] private float offArmDepth = 0.8f;
		[SerializeField, Tooltip("조준 반대쪽 팔이 옮겨 가는 거리 (조준 쪽, 위)")] private Vector2 offArmReach = new(0.3f, 0.12f);
		[SerializeField, Range(-60f, 60f)] private float podPitch = -20f;
		[SerializeField, Min(0.1f)] private float armScale = 1f;
		[SerializeField, Min(0.1f)] private float podScale = 0.92f;
		[SerializeField, Min(0f)] private float bobHeight = 0.03f;
		[SerializeField, Min(0f)] private float bobSpeed = 5f;

		[Header("움직임")]
		[SerializeField, Min(0.02f)] private float popSeconds = 0.4f;
		[SerializeField, Min(0f), Tooltip("팔과 어깨가 나오는 차이")] private float popStagger = 0.12f;
		[SerializeField, Min(0f), Tooltip("조준을 따라 도는 빠르기 (도/초)")] private float turnSpeed = 540f;
		[SerializeField, Min(0f), Tooltip("총열이 도는 빠르기 (도/초)")] private float spinSpeed = 1500f;
		[SerializeField, Min(0.02f)] private float spinUpSeconds = 0.8f;
		[SerializeField, Min(0f)] private float recoil = 0.06f;
		[SerializeField, Min(0.1f)] private float recoilRate = 40f;
		[SerializeField, Min(0f), Tooltip("막타에 밀림")] private float finalRecoil = 0.3f;
		[SerializeField, Min(0.01f)] private float finalHalfLife = 0.12f;
		[SerializeField, Min(0.02f)] private float vanishSeconds = 0.35f;

		[Header("빛줄기")]
		[SerializeField, Min(0f)] private float beamWidth = 0.85f;
		[SerializeField, Min(0f), Tooltip("밝은 심의 폭")] private float beamCoreWidth = 0.4f;
		[SerializeField, Min(0f)] private float beamLength = 8.5f;
		[SerializeField, Min(0f), Tooltip("조준점을 넘어 더 뻗는 길이")] private float beamOvershoot = 1.2f;
		[SerializeField, ColorUsage(false, true)] private Color beamCore = new(1.55f, 1.4f, 0.9f);
		[SerializeField, ColorUsage(false, true)] private Color beamBody = new(1.35f, 0.92f, 0.24f);
		[SerializeField, ColorUsage(false, true)] private Color beamTip = new(0.8f, 0.42f, 0.08f);
		[SerializeField] private Vector2 beamPulse = new(0.75f, 0.25f);
		[SerializeField, Min(0f)] private float beamPulseRate = 60f;

		[Header("미사일")]
		[SerializeField, Min(0f), Tooltip("초당")] private float missileRate = 5f;
		[SerializeField, Min(0.05f)] private float missileSeconds = 0.7f;
		[SerializeField, Min(0f)] private float missileSpeed = 9f;
		[SerializeField, Tooltip("처음 날아가는 쪽 (옆, 위)")] private Vector2 missileLoft = new(0.5f, 1.1f);
		[SerializeField, Range(0f, 90f)] private float missileFan = 28f;
		[SerializeField] private Vector3 missileScale = new(0.1f, 0.1f, 0.3f);
		[SerializeField, Min(0f)] private float missileTrail = 0.08f;
		[SerializeField] private Color trailHead = new(1f, 0.95f, 0.7f, 1f);
		[SerializeField] private Color trailTail = new(1f, 0.75f, 0.2f, 0f);

		private sealed class Missile
		{
			public Transform Body;
			public TrailRenderer Trail;
			public Vector3 Velocity;
			public Vector3 Target;
			public float StartTime;
			public bool Alive;
		}

		private readonly List<Missile> missiles = new();
		private readonly List<UnityEngine.Object> owned = new();
		private readonly Transform[] mounts = new Transform[BODY_PARTS];
		private readonly Transform[] spins = new Transform[ARMS];
		private readonly Transform[] beams = new Transform[ARMS * 2];
		private Transform root;
		private Material bodyMaterial;
		private Material lightMaterial;
		private Material ribbonMaterial;
		private Mesh shard;
		private Transform owner;
		private Func<Vector3> aimSource;
		private Func<Vector3> pointSource;
		private Vector3 aim = Vector3.forward;
		private float raiseTime;
		private float dismissTime;
		private float finalTime;
		private float missileCarry;
		private int missileSide;
		private bool firing;

		/// <summary> 무장이 보이는 동안. 손에 든 무기 그림을 감춤 </summary>
		public bool IsShown => root != null && root.gameObject.activeSelf && Time.time < dismissTime + vanishSeconds;

		/// <summary> 등 뒤 시점. 무장을 인물 뒤로 밀지 않음 </summary>
		public bool BackView { get; set; }

		/// <summary> 무장이 지금 겨누는 쪽 </summary>
		public Vector3 Aim => aim;

		public void Raise(Transform ownerTransform, Func<Vector3> aimDirection, Func<Vector3> aimPoint)
		{
			Prepare();
			owner = ownerTransform;
			aimSource = aimDirection;
			pointSource = aimPoint;
			aim = aimDirection();
			raiseTime = Time.time;
			dismissTime = float.PositiveInfinity;
			finalTime = float.PositiveInfinity;
			firing = false;
			missileCarry = 0f;
			root.gameObject.SetActive(true);
		}

		public void SetFiring(bool on) => firing = on;

		/// <summary> 막타에 크게 밀리고 linger 뒤 부서짐 </summary>
		public void Dismiss(float finalDelay, float linger)
		{
			firing = false;
			finalTime = Time.time + finalDelay;
			dismissTime = Time.time + linger;
		}

		/// <summary> 총구 자리. 앞의 둘은 팔, 뒤의 둘은 어깨 </summary>
		public Vector3 Muzzle(int part)
		{
			Transform mount = mounts[part];
			float length = part < ARMS ? OreArmsMeshes.BARREL_MUZZLE : OreArmsMeshes.POD_MUZZLE;
			return mount.position + mount.forward * (length * mount.localScale.z);
		}

		private void OnDisable()
		{
			firing = false;
			foreach (Missile missile in missiles)
				Stop(missile);
			if (root != null)
				root.gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			foreach (UnityEngine.Object target in owned)
				Destroy(target);
		}

		private void LateUpdate()
		{
			if (root == null || root.gameObject.activeSelf == false)
				return;
			Camera viewCamera = Camera.main;
			// 씬이 바뀌는 사이에는 카메라나 인물이 없음
			if (owner == null || viewCamera == null)
			{
				OnDisable();
				return;
			}
			// 무기 오브젝트는 베기마다 자리를 옮김. 그림은 월드 자리라 뿌리를 원점에 되돌림
			root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			float gone = Mathf.Clamp01((Time.time - dismissTime) / vanishSeconds);
			bool shown = gone < 1f;
			SetBodyVisible(shown);
			if (shown)
			{
				bodyMaterial.SetFloat(DISSOLVE_ID, gone);
				aim = Vector3.RotateTowards(aim, aimSource(), turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
				Pose(viewCamera);
				DrawBeams();
				LaunchMissiles();
			}
			if (DrawMissiles() == false && shown == false)
				root.gameObject.SetActive(false);
		}

		private void Pose(Camera viewCamera)
		{
			Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
			Quaternion look = Quaternion.LookRotation(aim, Vector3.up);
			float spin = spinSpeed * Mathf.Clamp01((Time.time - raiseTime) / spinUpSeconds);
			float final = Time.time < finalTime ? 0f : Mathf.Pow(0.5f, (Time.time - finalTime) / finalHalfLife);
			for (int i = 0; i < BODY_PARTS; i++)
			{
				bool arm = i < ARMS;
				Vector3 slot = arm ? armSlot : podSlot;
				float side = i % 2 == 0 ? 1f : -1f;
				float shown = Mathf.Clamp01((Time.time - raiseTime - (arm ? 0f : popStagger)) / popSeconds);
				shown = 1f - Mathf.Pow(1f - shown, 3f);
				// 조준 반대쪽 팔은 몸 뒤로
				float off = arm ? Mathf.Clamp01(-side * Vector3.Dot(aim, right)) : 0f;
				float push = BackView ? 0f : arm ? offArmDepth * Mathf.SmoothStep(0f, 1f, off * 2f) : podDepth;
				float kick = (firing ? recoil * Mathf.Abs(Mathf.Sin(Time.time * recoilRate + i)) : 0f) + finalRecoil * final;
				Vector3 seat = owner.position + viewCamera.transform.forward * push
					+ right * (slot.x * side * shown)
					+ Vector3.up * (slot.y * shown + offArmReach.y * off + bobHeight * Mathf.Sin(Time.time * bobSpeed + i))
					+ aim * (slot.z + offArmReach.x * off - kick);
				mounts[i].SetPositionAndRotation(seat, look * Quaternion.Euler(arm ? 0f : podPitch, 0f, 0f));
				mounts[i].localScale = Vector3.one * (shown * (arm ? armScale : podScale));
				if (arm)
					spins[i].Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
			}
		}

		// 조준점까지. 굵은 두 줄이 한 점에 모임
		private void DrawBeams()
		{
			Vector3 target = pointSource();
			for (int i = 0; i < beams.Length; i++)
			{
				beams[i].gameObject.SetActive(firing);
				if (firing == false)
					continue;
				int arm = i % ARMS;
				Vector3 from = Muzzle(arm);
				Vector3 span = target - from;
				span.y = 0f;
				Vector3 direction = span.sqrMagnitude < 0.01f || Vector3.Dot(span, aim) < 0f ? aim : span.normalized;
				float length = Mathf.Clamp(span.magnitude + beamOvershoot, beamOvershoot, beamLength);
				float width = (i < ARMS ? beamWidth : beamCoreWidth) * (beamPulse.x + beamPulse.y * Mathf.Sin(Time.time * beamPulseRate + arm * 1.7f));
				beams[i].SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), direction));
				beams[i].localScale = new Vector3(width, length, width);
			}
		}

		private void LaunchMissiles()
		{
			if (firing == false)
				return;
			missileCarry += missileRate * Time.deltaTime;
			while (missileCarry >= 1f)
			{
				missileCarry -= 1f;
				missileSide = 1 - missileSide;
				Missile missile = missiles.Find(candidate => candidate.Alive == false) ?? CreateMissile();
				missile.Alive = true;
				missile.StartTime = Time.time;
				Vector3 side = Vector3.Cross(Vector3.up, aim) * (missileSide == 0 ? missileLoft.x : -missileLoft.x);
				missile.Velocity = (aim + Vector3.up * missileLoft.y + side).normalized * missileSpeed;
				Vector3 toPoint = pointSource() - owner.position;
				toPoint.y = 0f;
				missile.Target = owner.position + Quaternion.Euler(0f, Random.Range(-0.5f, 0.5f) * missileFan, 0f) * toPoint;
				missile.Body.position = Muzzle(ARMS + missileSide);
				missile.Body.gameObject.SetActive(true);
				missile.Trail.Clear();
			}
		}

		// 하나라도 날고 있으면 true
		private bool DrawMissiles()
		{
			bool flying = false;
			foreach (Missile missile in missiles)
			{
				if (missile.Alive == false)
					continue;
				float progress = (Time.time - missile.StartTime) / missileSeconds;
				if (progress >= 1f)
				{
					Stop(missile);
					continue;
				}
				flying = true;
				Vector3 wanted = (missile.Target - missile.Body.position) / Mathf.Max(0.05f, missileSeconds * (1f - progress));
				missile.Velocity = Vector3.Lerp(missile.Velocity, wanted, progress * progress);
				missile.Body.position += missile.Velocity * Time.deltaTime;
				missile.Body.rotation = Quaternion.LookRotation(missile.Velocity);
			}
			return flying;
		}

		private Missile CreateMissile()
		{
			GameObject body = UltimateFxStage.CreateMeshObject("OreMissile", root, shard, lightMaterial);
			body.transform.localScale = missileScale;
			TrailRenderer trail = body.AddComponent<TrailRenderer>();
			trail.sharedMaterial = ribbonMaterial;
			trail.time = missileSeconds * 0.6f;
			trail.startWidth = missileTrail;
			trail.endWidth = 0f;
			trail.startColor = trailHead;
			trail.endColor = trailTail;
			trail.minVertexDistance = 0.1f;
			trail.alignment = LineAlignment.View;
			trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			Missile missile = new() { Body = body.transform, Trail = trail };
			missiles.Add(missile);
			return missile;
		}

		private static void Stop(Missile missile)
		{
			missile.Alive = false;
			missile.Body.gameObject.SetActive(false);
		}

		private void SetBodyVisible(bool visible)
		{
			foreach (Transform mount in mounts)
				mount.gameObject.SetActive(visible);
			if (visible)
				return;
			foreach (Transform beam in beams)
				beam.gameObject.SetActive(false);
		}

		private void Prepare()
		{
			if (root != null)
				return;
			if (solidShader == null || energyShader == null || ribbonShader == null)
				throw new InvalidOperationException($"{name}: 광석 무장 셰이더 미연결");
			root = new GameObject(nameof(VolleyArmsModel)).transform;
			root.SetParent(transform, false);
			bodyMaterial = Solid(outline, oreGlow, 0.16f);
			lightMaterial = Solid(0f, hotColor * 1.5f, 0f);
			ribbonMaterial = Own(new Material(ribbonShader) { name = "OreRibbon", renderQueue = 3012 });
			ribbonMaterial.SetFloat("_Shape", 2f);
			Mesh barrels = Own(OreArmsMeshes.Barrels());
			Mesh housing = Own(OreArmsMeshes.Housing());
			Mesh pod = Own(OreArmsMeshes.Pod());
			shard = Own(OreArmsMeshes.Shard());
			for (int i = 0; i < BODY_PARTS; i++)
			{
				mounts[i] = new GameObject("OreMount" + i).transform;
				mounts[i].SetParent(root, false);
				if (i >= ARMS)
				{
					UltimateFxStage.CreateMeshObject("OrePod", mounts[i], pod, bodyMaterial);
					continue;
				}
				UltimateFxStage.CreateMeshObject("OreHousing", mounts[i], housing, bodyMaterial);
				spins[i] = UltimateFxStage.CreateMeshObject("OreBarrels", mounts[i], barrels, bodyMaterial).transform;
			}
			Mesh sheath = Own(FormMeshes.Sheath(3, 12, 1f));
			for (int i = 0; i < beams.Length; i++)
			{
				beams[i] = UltimateFxStage.CreateMeshObject("OreBeam", root, sheath, Beam(i < ARMS)).transform;
				beams[i].gameObject.SetActive(false);
			}
		}

		private Material Beam(bool wide)
		{
			Material material = Own(new Material(energyShader) { name = "OreBeam", renderQueue = wide ? 3010 : 3011 });
			material.SetFloat("_Shape", 2f);
			material.SetColor("_CoreColor", beamCore);
			material.SetColor("_BodyColor", beamBody);
			material.SetColor("_TipColor", beamTip);
			material.SetFloat("_Intensity", wide ? 1f : 1.3f);
			material.SetFloat("_Cover", wide ? 0.5f : 0.1f);
			material.SetFloat("_Flow", 6f);
			material.SetFloat("_Steps", 24f);
			material.SetVector(TEXELS_ID, new Vector4(beamLength * texelsPerUnit, (wide ? beamWidth : beamCoreWidth) * texelsPerUnit, 0f, 0f));
			return material;
		}

		private Material Solid(float outlineWidth, Color glow, float grain)
		{
			Material material = Own(new Material(solidShader) { name = "OreSolid" });
			material.SetVector("_TexelDensity", Vector3.one * texelsPerUnit);
			material.SetColor("_EmissionColor", glow);
			material.SetColor("_EdgeColor", hotColor);
			material.SetColor("_OutlineColor", outlineColor);
			material.SetFloat("_Outline", outlineWidth);
			material.SetFloat("_NoiseAmount", grain);
			return material;
		}

		private T Own<T>(T target) where T : UnityEngine.Object
		{
			owned.Add(target);
			return target;
		}
	}
}
