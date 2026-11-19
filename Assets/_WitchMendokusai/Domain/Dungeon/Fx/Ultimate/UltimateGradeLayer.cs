using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 배경 장악 판 (UltimateGrade 셰이더) 의 C# 쪽. 카메라 앞에 화면 크기 판을 매 프레임 맞춰 둠
	/// - 셰이더가 카메라 Opaque Texture 를 읽으므로 켜는 동안 카메라 설정을 바꾸고, 끝나면 되돌림
	/// - Opaque Texture 는 프로젝트 설정상 절반 해상도라 배경이 흐려짐. 켜는 동안만 원래 해상도로 (파이프라인 에셋의 비공개 칸을 반사로 바꿈, 실패하면 흐린 채로 둠)
	/// - 아무 값도 안 걸린 동안은 판을 끔 (흐림과 비용 없음)
	/// </summary>
	public sealed class UltimateGradeLayer
	{
		private static readonly int DESATURATE = Shader.PropertyToID("_Desaturate");
		private static readonly int TINT_COLOR = Shader.PropertyToID("_TintColor");
		private static readonly int TINT_AMOUNT = Shader.PropertyToID("_TintAmount");
		private static readonly int DARKEN = Shader.PropertyToID("_Darken");
		private static readonly int LINE_ART = Shader.PropertyToID("_LineArt");
		private static readonly int LINE_INK = Shader.PropertyToID("_LineInk");
		private static readonly int LINE_PAPER = Shader.PropertyToID("_LinePaper");
		private static readonly int TWO_TONE = Shader.PropertyToID("_TwoTone");
		private static readonly int TONE_DARK = Shader.PropertyToID("_ToneDark");
		private static readonly int TONE_LIGHT = Shader.PropertyToID("_ToneLight");
		private static readonly int TONE_THRESHOLD = Shader.PropertyToID("_ToneThreshold");
		private static readonly int DITHER = Shader.PropertyToID("_Dither");
		private static readonly int DITHER_DARK = Shader.PropertyToID("_DitherDark");
		private static readonly int DITHER_LIGHT = Shader.PropertyToID("_DitherLight");
		private static readonly int SPACE = Shader.PropertyToID("_Space");
		private static readonly int SPACE_TOP = Shader.PropertyToID("_SpaceTop");
		private static readonly int SPACE_BOTTOM = Shader.PropertyToID("_SpaceBottom");
		private static readonly int BAND_COLOR = Shader.PropertyToID("_BandColor");
		private static readonly int WIPE_ON = Shader.PropertyToID("_WipeOn");
		private static readonly int WIPE_ANGLE = Shader.PropertyToID("_WipeAngle");
		private static readonly int WIPE_POSITION = Shader.PropertyToID("_WipePosition");
		private static readonly int WIPE_EDGE_COLOR = Shader.PropertyToID("_WipeEdgeColor");
		private static readonly int FLASH = Shader.PropertyToID("_Flash");
		private static readonly int FLASH_COLOR = Shader.PropertyToID("_FlashColor");

		private readonly Camera targetCamera;
		private readonly Material material;
		private readonly GameObject quad;
		private readonly MeshRenderer quadRenderer;
		private readonly UniversalAdditionalCameraData cameraData;
		private readonly CameraOverrideOption previousColorOption;
		private readonly FieldInfo downsamplingField;
		private readonly object previousDownsampling;
		private float desaturate;
		private float tint;
		private float darken;
		private float lineArt;
		private float twoTone;
		private float dither;
		private float space;
		private float flash;
		private bool wipe;

		public UltimateGradeLayer(Shader shader, Camera camera, Mesh mesh)
		{
			targetCamera = camera;
			material = new Material(shader) { name = "UltimateFx_Grade" };
			quad = UltimateFxStage.CreateMeshObject("UltimateGrade", camera != null ? camera.transform : null, mesh, material);
			quadRenderer = quad.GetComponent<MeshRenderer>();
			quadRenderer.enabled = false;
			if (camera == null)
				return;
			cameraData = camera.GetUniversalAdditionalCameraData();
			previousColorOption = cameraData.requiresColorOption;
			cameraData.requiresColorOption = CameraOverrideOption.On;
			if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
			{
				downsamplingField = typeof(UniversalRenderPipelineAsset).GetField("m_OpaqueDownsampling", BindingFlags.Instance | BindingFlags.NonPublic);
				if (downsamplingField != null)
				{
					previousDownsampling = downsamplingField.GetValue(pipeline);
					downsamplingField.SetValue(pipeline, Downsampling.None);
				}
			}
		}

		public float Desaturate { set => Set(ref desaturate, DESATURATE, value); }
		public float Darken { set => Set(ref darken, DARKEN, value); }
		public float LineArt { set => Set(ref lineArt, LINE_ART, value); }
		public float TwoTone { set => Set(ref twoTone, TWO_TONE, value); }
		public float Dither { set => Set(ref dither, DITHER, value); }
		public float Space { set => Set(ref space, SPACE, value); }
		public float Flash { set => Set(ref flash, FLASH, value); }

		public void SetTint(float amount, Color color)
		{
			material.SetColor(TINT_COLOR, color);
			Set(ref tint, TINT_AMOUNT, amount);
		}

		public void SetLineColors(Color ink, Color paper)
		{
			material.SetColor(LINE_INK, ink);
			material.SetColor(LINE_PAPER, paper);
		}

		public void SetToneColors(Color dark, Color light, float threshold)
		{
			material.SetColor(TONE_DARK, dark);
			material.SetColor(TONE_LIGHT, light);
			material.SetFloat(TONE_THRESHOLD, threshold);
		}

		public void SetDitherColors(Color dark, Color light)
		{
			material.SetColor(DITHER_DARK, dark);
			material.SetColor(DITHER_LIGHT, light);
		}

		/// <summary> 우주 배경 색. band 의 알파가 은하수 띠 세기 </summary>
		public void SetSpaceColors(Color top, Color bottom, Color band)
		{
			material.SetColor(SPACE_TOP, top);
			material.SetColor(SPACE_BOTTOM, bottom);
			material.SetColor(BAND_COLOR, band);
		}

		public void SetFlashColor(Color color) => material.SetColor(FLASH_COLOR, color);

		/// <summary> 사선 와이프. 켜면 우주 교체가 화면 가운데를 지나는 사선 띠 안에만. position 은 띠 반폭 (화면 높이 1 기준), angle 은 띠 법선 </summary>
		public void SetWipe(bool enabled, float angleRadians, float position, Color edgeColor)
		{
			wipe = enabled;
			material.SetFloat(WIPE_ON, enabled ? 1f : 0f);
			material.SetFloat(WIPE_ANGLE, angleRadians);
			material.SetFloat(WIPE_POSITION, position);
			material.SetColor(WIPE_EDGE_COLOR, edgeColor);
			Refresh();
		}

		/// <summary> 매 프레임 카메라 앞 근평면 바로 뒤에 화면을 꼭 덮는 크기로 맞춤 (줌과 시야각 변화 따라감) </summary>
		public void Follow()
		{
			if (targetCamera == null || quadRenderer.enabled == false)
				return;
			float distance = targetCamera.nearClipPlane * 1.5f;
			float height = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			quad.transform.localPosition = new Vector3(0f, 0f, distance);
			quad.transform.localRotation = Quaternion.identity;
			quad.transform.localScale = new Vector3(height * targetCamera.aspect * 1.02f, height * 1.02f, 1f);
		}

		public void Dispose()
		{
			if (cameraData != null)
				cameraData.requiresColorOption = previousColorOption;
			if (downsamplingField != null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
				downsamplingField.SetValue(pipeline, previousDownsampling);
			if (quad != null)
				Object.Destroy(quad);
			Object.Destroy(material);
		}

		private void Set(ref float field, int property, float value)
		{
			field = Mathf.Clamp01(value);
			material.SetFloat(property, field);
			Refresh();
		}

		private void Refresh()
		{
			bool active = wipe || desaturate > 0.001f || tint > 0.001f || darken > 0.001f || lineArt > 0.001f || twoTone > 0.001f || dither > 0.001f || space > 0.001f || flash > 0.001f;
			if (quadRenderer.enabled != active)
			{
				quadRenderer.enabled = active;
				Follow();
			}
		}
	}
}
