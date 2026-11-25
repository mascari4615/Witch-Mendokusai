using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 줄기, 총구 섬광, 맞은 자리. 줄기는 총구에서 탄 머리까지 이어지고 꼬리가 뒤따라 줄어듦
	// 탄이 꺼지면 (맞음이나 수명 끝) 마지막 자리에서 터짐
	public sealed partial class VolleyArsenal
	{
		[Header("줄기")]
		[SerializeField, Min(0.02f)] private float beamWidth = 0.62f;
		[SerializeField, Min(0.02f), Tooltip("줄기 안을 나는 빛 무기의 길이")] private float boltLength = 1.3f;
		[SerializeField, Min(0f), Tooltip("꼬리가 총구에 머무는 길이")] private float tailHold = 0.3f;
		[SerializeField, Min(1f), Tooltip("꼬리가 머리를 쫓는 빠르기")] private float tailSpeed = 25f;
		[SerializeField, Min(0.02f), Tooltip("총구에서 나간 줄기가 실제 탄의 길로 모이는 길이")] private float convergeSeconds = 0.75f;
		[SerializeField, Min(0.01f)] private float boltWidth = 0.2f;
		[SerializeField, Min(0.02f), Tooltip("탄이 꺼진 뒤 줄기가 부서지는 길이")] private float beamFade = 0.22f;
		[SerializeField, Min(0f), Tooltip("탄 중심에서 그림 높이 (탄 충돌 높이와 같게)")] private float shotHeight = 0.8f;
		[SerializeField, Min(0f), Tooltip("한 프레임에 이보다 멀리 뛰면 다른 탄으로 다시 쓰인 것으로 봄")] private float jumpDistance = 3f;
		[SerializeField, Min(0f), Tooltip("끊긴 프레임에서 허용하는 빠르기 (월드/초)")] private float jumpSpeed = 45f;

		[Header("섬광")]
		[SerializeField, Min(0f)] private float muzzleSize = 1.05f;
		[SerializeField, Min(0f)] private float impactSize = 1.5f;
		[SerializeField, Min(0.02f)] private float flashSeconds = 0.22f;
		[SerializeField, Min(1)] private int flashRays = 9;
		[SerializeField, Min(0)] private int sparksPerFlash = 9;
		[SerializeField, Range(0.05f, 1f), Tooltip("섬광 줄기의 폭 (길이 1 당)")] private float rayWidth = 0.13f;
		[SerializeField, Range(0f, 1f), Tooltip("짧은 줄기의 길이 비율")] private float shortRay = 0.35f;

		[Header("빛")]
		[SerializeField, Min(0f)] private float lightIntensity = 4f;
		[SerializeField, Min(0.1f)] private float lightRange = 7f;

		private sealed class Tracer
		{
			public Transform Beam;
			public Transform Bolt;
			public Material BeamMaterial;
			public Transform Shot;
			public Vector3 Head;
			public Vector3 Tail;
			// 총구와 탄 시작 자리의 차이. 시간이 지나며 0 이 되어 그림이 탄의 길로 모임
			public Vector3 Offset;
			public Vector3 Shown;
			public float StartTime;
			public float EndTime;
			public bool Alive;
		}

		private sealed class Burst
		{
			public Transform[] Rays;
			public float[] Lengths;
			public Material Material;
			public float StartTime;
			public float Size;
			public bool Alive;
		}

		private readonly List<Tracer> tracers = new();
		private readonly List<Burst> bursts = new();
		private Mesh beamMesh;
		private Mesh boltMesh;
		private Mesh rayMesh;
		private Material boltMaterial;
		private ParticleSystem sparks;
		private Light flashLight;
		private float lightLevel;

		private void PrepareShots()
		{
			beamMesh = Own(FormMeshes.Sheath(2, 12, 1f));
			boltMesh = Own(FormMeshes.Bar(1f));
			rayMesh = Own(FormMeshes.Sheath(1, 8, 1f));
			boltMaterial = Solid();
			boltMaterial.SetVector(DENSITY_ID, new Vector4(5f, boltLength * texelsPerUnit, 5f, 0f));
			boltMaterial.SetVector(DISSOLVE_RANGE_ID, new Vector4(0f, 1f, 0f, 0f));
			boltMaterial.SetFloat(HEAT_ID, 0.55f);
			Material sparkMaterial = Energy(FRONT_QUEUE + 2);
			sparkMaterial.SetVector(TEXELS_ID, Vector4.zero);
			sparks = UltimateFxStage.CreateParticles("VolleySparks", root, sparkMaterial, 400, 0.35f, true);
			UltimateFxStage.SetLifetimeLook(sparks, null, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
			flashLight = new GameObject("VolleyLight").AddComponent<Light>();
			flashLight.transform.SetParent(root, false);
			flashLight.type = LightType.Point;
			flashLight.color = lightColor;
			flashLight.range = lightRange;
			flashLight.enabled = false;
		}

		private void StartTracer(Vector3 muzzle, Transform shot)
		{
			// 풀이 같은 탄을 바로 다시 냈으면 옛 줄기는 거기서 끝
			foreach (Tracer old in tracers)
			{
				if (old.Alive && old.Shot == shot)
					EndTracer(old);
			}
			Tracer tracer = tracers.Find(candidate => candidate.Alive == false);
			if (tracer == null)
			{
				tracer = new Tracer { BeamMaterial = Energy(FRONT_QUEUE) };
				tracer.BeamMaterial.SetFloat(SHAPE_ID, GLOW_SHAPE);
				tracer.BeamMaterial.SetFloat(DISSOLVE_FROM_TIP_ID, 0f);
				tracer.Beam = UltimateFxStage.CreateMeshObject("VolleyBeam", root, beamMesh, tracer.BeamMaterial).transform;
				tracer.Bolt = UltimateFxStage.CreateMeshObject("VolleyBolt", root, boltMesh, boltMaterial).transform;
				tracers.Add(tracer);
			}
			tracer.Shot = shot;
			tracer.Head = shot.position + Vector3.up * shotHeight;
			tracer.Tail = muzzle;
			tracer.Offset = muzzle - tracer.Head;
			tracer.Shown = muzzle;
			tracer.StartTime = Time.time;
			tracer.EndTime = float.PositiveInfinity;
			tracer.Alive = true;
		}

		private void UpdateShots()
		{
			foreach (Tracer tracer in tracers)
			{
				if (tracer.Alive)
					UpdateTracer(tracer);
			}
			foreach (Burst burst in bursts)
			{
				if (burst.Alive)
					UpdateBurst(burst);
			}
			lightLevel *= Mathf.Pow(0.5f, Time.deltaTime / 0.08f);
			flashLight.enabled = lightLevel > 0.02f;
			flashLight.intensity = lightIntensity * lightLevel;
			flashLight.transform.position = owner.position + Vector3.up * 1.4f + Aim() * 0.8f;
		}

		// 줄기 끝을 실제 맞은 자리에 맞추고 터뜨림
		private void EndTracer(Tracer tracer)
		{
			tracer.Shot = null;
			tracer.EndTime = Time.time;
			tracer.Shown = tracer.Head;
			Flash(tracer.Head, Vector3.up, impactSize);
		}

		private void UpdateTracer(Tracer tracer)
		{
			if (tracer.EndTime == float.PositiveInfinity)
			{
				Vector3 now = tracer.Shot == null ? tracer.Head : tracer.Shot.position + Vector3.up * shotHeight;
				bool gone = tracer.Shot == null || tracer.Shot.gameObject.activeInHierarchy == false || (now - tracer.Head).magnitude > Mathf.Max(jumpDistance, jumpSpeed * Time.deltaTime);
				if (gone)
				{
					EndTracer(tracer);
				}
				else
				{
					tracer.Head = now;
					tracer.Shown = now + tracer.Offset * (1f - Mathf.SmoothStep(0f, 1f, (Time.time - tracer.StartTime) / convergeSeconds));
				}
			}
			if (Time.time - tracer.StartTime > tailHold)
				tracer.Tail = Vector3.MoveTowards(tracer.Tail, tracer.Shown, tailSpeed * Time.deltaTime);
			Vector3 span = tracer.Shown - tracer.Tail;
			float length = span.magnitude;
			float faded = Mathf.Clamp01((Time.time - tracer.EndTime) / beamFade);
			bool ended = tracer.EndTime != float.PositiveInfinity;
			if (faded >= 1f || (ended && length < 0.05f))
			{
				tracer.Alive = false;
				ShowTracer(tracer, false);
				return;
			}
			if (length < 0.05f)
			{
				ShowTracer(tracer, false);
				return;
			}
			ShowTracer(tracer, true);
			Vector3 direction = span / length;
			Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);
			// 나온 순간 굵고 금방 제 굵기로
			float birth = Mathf.Pow(0.5f, (Time.time - tracer.StartTime) / 0.05f);
			float width = beamWidth * (1f + birth) * (1f - 0.5f * faded);
			// 메시는 뿌리가 뾰족하고 끝으로 가늘어짐. 굵은 쪽이 머리에 오게 뒤집음
			tracer.Beam.SetPositionAndRotation(tracer.Shown, Quaternion.FromToRotation(Vector3.up, -direction));
			tracer.Beam.localScale = new Vector3(width, length, width);
			tracer.BeamMaterial.SetVector(TEXELS_ID, new Vector4(Mathf.Max(4f, Mathf.Round(length * texelsPerUnit)), Mathf.Max(4f, Mathf.Round(beamWidth * texelsPerUnit)), 0f, 0f));
			tracer.BeamMaterial.SetFloat(DISSOLVE_ID, faded);
			tracer.BeamMaterial.SetFloat(FLASH_ID, birth * 0.5f);
			float boltSize = Mathf.Min(boltLength, length);
			tracer.Bolt.SetPositionAndRotation(tracer.Shown - direction * boltSize, rotation * Quaternion.Euler(0f, 45f, 0f));
			tracer.Bolt.localScale = new Vector3(boltWidth, boltSize, boltWidth);
			tracer.Bolt.gameObject.SetActive(ended == false);
		}

		// 별 모양 섬광. 앞 (forward) 이 위면 맞은 자리 (위로 퍼짐), 아니면 총구 (앞으로 퍼짐)
		private void Flash(Vector3 position, Vector3 forward, float size)
		{
			Burst burst = bursts.Find(candidate => candidate.Alive == false);
			if (burst == null)
			{
				burst = new Burst { Material = Energy(FRONT_QUEUE + 1), Rays = new Transform[flashRays], Lengths = new float[flashRays] };
				burst.Material.SetFloat(SHAPE_ID, GLOW_SHAPE);
				burst.Material.SetFloat(DISSOLVE_FROM_TIP_ID, 0f);
				burst.Material.SetVector(TEXELS_ID, new Vector4(24f, 6f, 0f, 0f));
				for (int i = 0; i < flashRays; i++)
					burst.Rays[i] = UltimateFxStage.CreateMeshObject("VolleyRay", root, rayMesh, burst.Material).transform;
				bursts.Add(burst);
			}
			burst.StartTime = Time.time;
			burst.Size = size;
			burst.Alive = true;
			Vector3 facing = -CameraForward();
			Vector3 axis = Vector3.ProjectOnPlane(forward, facing).normalized;
			if (axis.sqrMagnitude < 0.01f)
				axis = viewCamera.transform.up;
			for (int i = 0; i < burst.Rays.Length; i++)
			{
				float angle = 360f * i / burst.Rays.Length + Random.Range(-18f, 18f);
				Vector3 direction = Quaternion.AngleAxis(angle, facing) * axis;
				// 앞쪽 줄기가 길고 뒤쪽이 짧음
				burst.Lengths[i] = Mathf.Lerp(0.4f, 1f, Vector3.Dot(direction, axis) * 0.5f + 0.5f) * Random.Range(0.8f, 1.1f) * (i % 2 == 0 ? 1f : shortRay);
				burst.Rays[i].SetPositionAndRotation(position + facing * 0.1f, Quaternion.LookRotation(-facing, direction));
				burst.Rays[i].gameObject.SetActive(true);
			}
			UpdateBurst(burst);
			for (int i = 0; i < sparksPerFlash; i++)
			{
				Vector3 velocity = (forward * 0.9f + Random.onUnitSphere).normalized * Random.Range(2.5f, 7f);
				UltimateFxStage.Emit(sparks, position, velocity, Random.Range(0.05f, 0.1f), Random.Range(0.18f, 0.35f), Color.white);
			}
		}

		private void UpdateBurst(Burst burst)
		{
			float progress = (Time.time - burst.StartTime) / flashSeconds;
			if (progress >= 1f)
			{
				burst.Alive = false;
				foreach (Transform ray in burst.Rays)
					ray.gameObject.SetActive(false);
				return;
			}
			burst.Material.SetFloat(DISSOLVE_ID, Mathf.Clamp01((progress - 0.35f) / 0.65f));
			burst.Material.SetFloat(FLASH_ID, 1f - progress);
			burst.Material.SetFloat(INTENSITY_ID, Mathf.Lerp(1.5f, 1f, progress));
			for (int i = 0; i < burst.Rays.Length; i++)
				burst.Rays[i].localScale = new Vector3(burst.Size * rayWidth, burst.Size * burst.Lengths[i] * (0.85f + 0.25f * progress), 1f);
		}

		private static void ShowTracer(Tracer tracer, bool visible)
		{
			tracer.Beam.gameObject.SetActive(visible);
			tracer.Bolt.gameObject.SetActive(visible);
		}

		private void StopShots()
		{
			foreach (Tracer tracer in tracers)
			{
				tracer.Alive = false;
				ShowTracer(tracer, false);
			}
			foreach (Burst burst in bursts)
			{
				burst.Alive = false;
				foreach (Transform ray in burst.Rays)
					ray.gameObject.SetActive(false);
			}
			if (flashLight != null)
				flashLight.enabled = false;
			lightLevel = 0f;
		}
	}
}
