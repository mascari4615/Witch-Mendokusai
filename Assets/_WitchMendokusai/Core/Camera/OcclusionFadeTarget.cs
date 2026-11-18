using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 물체 단위 가림 페이드 대상. 나무처럼 한 덩어리로 비워야 하는 물체의 뿌리에 붙임.
	/// 카메라와 캐릭터 사이 선분에 걸리거나 카메라에 가까우면 CameraOcclusionCutout 이 자식 렌더러 전체를 점무늬로 비움.
	/// 재질은 _WM_OCCLUDABLE 이 켜진 WM/Occludable 셰이더여야 함 (셰이더 값 _WM_ObjectFade).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class OcclusionFadeTarget : MonoBehaviour
	{
		private static readonly int OBJECT_FADE_ID = Shader.PropertyToID("_WM_ObjectFade");
		private static readonly List<OcclusionFadeTarget> ACTIVE_TARGETS = new();
		private static MaterialPropertyBlock propertyBlock;

		public static IReadOnlyList<OcclusionFadeTarget> ActiveTargets => ACTIVE_TARGETS;

		private Renderer[] renderers;

		/// <summary>지금 비운 정도 (0 그대로, 1 다 비움)</summary>
		public float Fade { get; private set; }

		private void OnEnable()
		{
			renderers = GetComponentsInChildren<Renderer>(false);
			ACTIVE_TARGETS.Add(this);
		}

		private void OnDisable()
		{
			ACTIVE_TARGETS.Remove(this);
			SetFade(0f);
		}

		/// <summary>선분 또는 카메라 근처와 겹치는지. 렌더러 경계 상자 기준</summary>
		public bool Overlaps(Ray ray, float length, float padding, Vector3 cameraPosition, float nearDistance)
		{
			foreach (Renderer targetRenderer in renderers)
			{
				Bounds bounds = targetRenderer.bounds;
				bounds.Expand(padding * 2f);
				if (bounds.SqrDistance(cameraPosition) <= nearDistance * nearDistance)
					return true;
				if (bounds.IntersectRay(ray, out float distance) && distance <= length)
					return true;
			}
			return false;
		}

		// 0 이면 속성 묶음을 떼어 SRP batcher 묶음으로 되돌림
		public void SetFade(float fade)
		{
			if (Mathf.Approximately(fade, Fade))
				return;
			Fade = fade;
			propertyBlock ??= new MaterialPropertyBlock();
			foreach (Renderer targetRenderer in renderers)
			{
				if (fade <= 0f)
				{
					targetRenderer.SetPropertyBlock(null);
					continue;
				}
				propertyBlock.Clear();
				propertyBlock.SetFloat(OBJECT_FADE_ID, fade);
				targetRenderer.SetPropertyBlock(propertyBlock);
			}
		}
	}
}
