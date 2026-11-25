using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// 형태 카드 그림. 무기 메시를 멀리 떨어진 자리에 놓고 작은 텍스처에 찍음 (점 필터로 키워 캐릭터 그림과 같은 결)
	// 시간이 멈춘 카드 창에서도 돌게 실제 시간으로 흔들림
	public sealed class FormCardStage : IDisposable
	{
		// 미리보기 전용 층 (전체 메뉴 코어, 도감과 같은 층). 자리로 떼어 놓음
		private const int PREVIEW_LAYER = 30;
		private static readonly Vector3 FAR_AWAY = new(10000f, -10000f, 0f);
		private const int SPOTS = 64;
		private const float SPOT_GAP = 20f;
		private static int created;

		private readonly FormCardLook look;
		private readonly GameObject root;
		private readonly Camera camera;
		private readonly Mesh mesh;
		private readonly Material material;

		public RenderTexture Texture { get; }

		public FormCardStage(FormCardLook look, float aspect)
		{
			this.look = look;
			root = new GameObject(nameof(FormCardStage));
			// 카드마다 겹치지 않는 자리
			root.transform.position = FAR_AWAY + Vector3.right * (created++ % SPOTS * SPOT_GAP);
			Texture = new RenderTexture(look.ArtWidth, Mathf.RoundToInt(look.ArtWidth / aspect), 24) { filterMode = FilterMode.Point, name = nameof(FormCardStage) };
			camera = new GameObject("Camera").AddComponent<Camera>();
			camera.transform.SetParent(root.transform, false);
			camera.transform.localPosition = new Vector3(0f, 0f, -4f);
			camera.orthographic = true;
			camera.orthographicSize = 1f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 8f;
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = Color.clear;
			camera.targetTexture = Texture;
			camera.cullingMask = 1 << PREVIEW_LAYER;
			UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
			cameraData.renderType = CameraRenderType.Base;
			cameraData.renderPostProcessing = false;
			cameraData.volumeLayerMask = 0;
			cameraData.requiresColorOption = CameraOverrideOption.Off;
			cameraData.requiresDepthOption = CameraOverrideOption.Off;
			cameraData.renderShadows = false;

			bool wand = look.Weapon == HeldWeapon.Shape.Wand;
			mesh = wand ? FormMeshes.Wand() : FormMeshes.Bar(0.16f);
			material = new Material(look.SolidShader) { name = nameof(FormCardStage) };
			material.SetColor("_EmissionColor", look.WeaponGlow);
			material.SetColor("_EdgeColor", look.WeaponGlow * 1.3f);
			float texels = look.ArtWidth / (2f * aspect) * look.WeaponSize;
			material.SetVector("_TexelDensity", wand ? Vector3.one * texels : new Vector3(5f, texels, 5f));
			material.SetFloat("_Outline", wand ? FormMeshes.WAND_OUTLINE : 0f);
			Transform pivot = new GameObject("Pivot").transform;
			pivot.SetParent(root.transform, false);
			for (int i = 0; i < look.WeaponCount; i++)
				Place(UltimateFxStage.CreateMeshObject("Weapon", pivot, mesh, material).transform, i);
			root.AddComponent<FormCardSway>().Set(pivot, look.SwayDegrees);
		}

		// 하나면 기울여 가운데, 여럿이면 아래 가운데에서 부채로
		private void Place(Transform weapon, int index)
		{
			bool wand = look.Weapon == HeldWeapon.Shape.Wand;
			weapon.gameObject.layer = PREVIEW_LAYER;
			weapon.localScale = Vector3.one * look.WeaponSize;
			if (look.WeaponCount == 1)
			{
				weapon.localRotation = Quaternion.Euler(0f, 0f, wand ? look.WeaponTilt : look.WeaponTilt - 60f);
				weapon.localPosition = weapon.localRotation * (wand ? new Vector3(-0.3f, -0.02f, 0f) : new Vector3(0f, -0.5f, 0f)) * look.WeaponSize;
				return;
			}
			float along = index / (look.WeaponCount - 1f);
			float fan = 90f + Mathf.Lerp(look.FanDegrees, -look.FanDegrees, along) * 0.5f;
			Vector3 outward = Quaternion.Euler(0f, 0f, fan) * Vector3.right;
			weapon.localRotation = Quaternion.Euler(0f, 0f, wand ? fan : fan - 90f);
			weapon.localPosition = new Vector3(0f, -0.95f, index * 0.12f) + outward * (index % 2 == 0 ? 0.75f : 0.5f);
		}

		public void SetVisible(bool visible)
		{
			if (root != null)
				root.SetActive(visible);
		}

		public void Dispose()
		{
			UnityEngine.Object.Destroy(mesh);
			UnityEngine.Object.Destroy(material);
			// 씬이 내려갈 때는 무대가 먼저 파괴됐을 수 있음
			if (root != null)
			{
				camera.targetTexture = null;
				UnityEngine.Object.Destroy(root);
			}
			Texture.Release();
			UnityEngine.Object.Destroy(Texture);
		}
	}

	// 형태 카드 그림의 흔들림 (실제 시간)
	public sealed class FormCardSway : MonoBehaviour
	{
		private Transform pivot;
		private float degrees;

		public void Set(Transform pivotTransform, float swayDegrees)
		{
			pivot = pivotTransform;
			degrees = swayDegrees;
		}

		private void Update() => pivot.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * 1.3f) * degrees * 2f, Mathf.Sin(Time.unscaledTime * 0.9f) * degrees * 0.4f);
	}
}
