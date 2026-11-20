using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 상태이상 하나의 머리 위 3D 궤도. 기절처럼 별 여러 개가 고리를 따라 머리 위를 돎
	// - 월드 공간 메시 (UnitStatusMeshes). 화면 위 2D 오버레이가 아님. 불투명이라 앞 몬스터 그림 뒤로 가려짐
	// - 궤도 면은 땅과 평행에서 OrbitTilt 만큼 카메라 쪽으로 세움. 위에서 내려다보는 카메라에서도 원이 납작하게 안 눌림
	// - 별 수가 SO 에서 늘면 그때 렌더러를 더 만듦. 줄면 남는 것은 숨김
	internal sealed class UnitStatusOrbit3D
	{
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

		private readonly Transform parent;
		private readonly List<MeshRenderer> stars = new();
		private readonly List<MeshFilter> starFilters = new();
		private MeshRenderer ring;
		private MeshFilter ringFilter;
		private readonly MaterialPropertyBlock block = new();

		public UnitStatusOrbit3D(Transform parent)
		{
			this.parent = parent;
		}

		public void Hide()
		{
			foreach (MeshRenderer star in stars)
				star.enabled = false;
			if (ring != null)
				ring.enabled = false;
		}

		// top: 그림 위 끝, toCamera: 몸에서 카메라 쪽 방향
		public void Draw(UnitStatusStyle style, UnitStatusConfig config, Vector3 top, float bodyWidth, Vector3 toCamera, float time, float seed)
		{
			if (style.OrbitStars <= 0 || config.OrbitMaterial == null)
			{
				Hide();
				return;
			}

			EnsureStars(style.OrbitStars, config.OrbitMaterial);
			EnsureRing(config.OrbitMaterial);

			// 궤도 면: 땅과 평행한 면을 카메라 쪽 수평 방향 축으로 OrbitTilt 만큼 세움
			Vector3 flatToCamera = Vector3.ProjectOnPlane(toCamera, Vector3.up);
			if (flatToCamera.sqrMagnitude < 0.0001f)
				flatToCamera = Vector3.back;
			flatToCamera.Normalize();
			Vector3 side = Vector3.Cross(Vector3.up, flatToCamera).normalized;
			// side 축 양의 회전이 면의 위 방향을 카메라 쪽으로 기울임
			Quaternion tilt = Quaternion.AngleAxis(style.OrbitTilt, side);
			Vector3 planeUp = tilt * Vector3.up;
			Vector3 planeForward = tilt * flatToCamera;

			float radius = bodyWidth * style.OrbitRadius;
			float bob = Mathf.Sin((time + seed) * style.OrbitBobRate * Mathf.PI * 2f) * style.OrbitBob;
			Vector3 center = top + Vector3.up * (style.OrbitHeight + bob);

			Mesh starMesh = UnitStatusMeshes.Star(config.OrbitStarPoints, config.OrbitStarInnerRatio, config.OrbitStarDepth);
			block.SetColor(COLOR_ID, style.OrbitStarColor);
			for (int i = 0; i < stars.Count; i++)
			{
				MeshRenderer star = stars[i];
				if (i >= style.OrbitStars)
				{
					star.enabled = false;
					continue;
				}
				float angle = ((time + seed) * style.OrbitSpinRate + (float)i / style.OrbitStars) * Mathf.PI * 2f;
				Vector3 position = center + (side * Mathf.Cos(angle) + planeForward * Mathf.Sin(angle)) * radius;
				// 별 앞면이 카메라를 보고 제자리에서 돎
				Quaternion facing = Quaternion.LookRotation(-toCamera, Vector3.up);
				Quaternion spin = Quaternion.AngleAxis((time + seed + i * 0.37f) * style.OrbitStarSpinRate * 360f, Vector3.up);
				starFilters[i].sharedMesh = starMesh;
				star.transform.SetPositionAndRotation(position, facing * spin);
				star.transform.localScale = Vector3.one * style.OrbitStarSize;
				star.SetPropertyBlock(block);
				star.enabled = true;
			}

			if (style.OrbitRingWidth <= 0f)
			{
				ring.enabled = false;
				return;
			}
			ringFilter.sharedMesh = UnitStatusMeshes.Ring(config.OrbitRingSegments, style.OrbitRingWidth);
			block.SetColor(COLOR_ID, style.OrbitRingColor);
			ring.transform.SetPositionAndRotation(center, Quaternion.LookRotation(planeForward, planeUp));
			ring.transform.localScale = Vector3.one * (radius * 2f);
			ring.SetPropertyBlock(block);
			ring.enabled = true;
		}

		private void EnsureStars(int count, Material material)
		{
			while (stars.Count < count)
			{
				MeshRenderer star = CreateRenderer($"Orbit star {stars.Count}", material, out MeshFilter filter);
				stars.Add(star);
				starFilters.Add(filter);
			}
		}

		private void EnsureRing(Material material)
		{
			if (ring != null)
				return;
			ring = CreateRenderer("Orbit ring", material, out ringFilter);
		}

		private MeshRenderer CreateRenderer(string rendererName, Material material, out MeshFilter filter)
		{
			GameObject child = new(rendererName);
			child.transform.SetParent(parent, false);
			filter = child.AddComponent<MeshFilter>();
			MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = material;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.lightProbeUsage = LightProbeUsage.Off;
			meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			meshRenderer.enabled = false;
			return meshRenderer;
		}
	}
}
