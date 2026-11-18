using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 바닥 예고의 공통 부분. 바닥에 눕힌 사각판 하나에 GroundTelegraph 셰이더로 모양을 그림
	/// - 차오름: Seconds 동안 진행도 0 -> 1 (채움 곡선). 게임 시간 (판정과 맞아야 함)
	/// - 끝: 진행도 1 이 되는 순간 번쩍이고 flashSeconds 동안 사라짐
	/// - Follow 가 있으면 그 자리를 따라가고, Follow 가 꺼지거나 없어지면 바로 치움 (시전자 사망)
	/// - 판은 실행기의 Rent, Return 으로 재사용. 값은 MaterialPropertyBlock (재질 에셋은 하나를 같이 씀)
	/// 모양 (원, 띠) 은 파생 클래스가 정함
	/// </summary>
	public abstract class GroundTelegraph : CombatFx
	{
		private const string PLATE_KEY = "GroundTelegraphPlate";

		private static readonly int ShapeId = Shader.PropertyToID("_Shape");
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
		private static readonly int FrontColorId = Shader.PropertyToID("_FrontColor");
		private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
		private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
		private static readonly int PatternColorId = Shader.PropertyToID("_PatternColor");
		private static readonly int SparkColorId = Shader.PropertyToID("_SparkColor");
		private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
		private static readonly int MembraneCenterId = Shader.PropertyToID("_MembraneCenter");
		private static readonly int FillDepthShadeId = Shader.PropertyToID("_FillDepthShade");
		private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
		private static readonly int CoreWidthId = Shader.PropertyToID("_CoreWidth");
		private static readonly int CoreIntensityId = Shader.PropertyToID("_CoreIntensity");
		private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
		private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
		private static readonly int GlowInnerId = Shader.PropertyToID("_GlowInner");
		private static readonly int GlowOpacityId = Shader.PropertyToID("_GlowOpacity");
		private static readonly int FrontWidthId = Shader.PropertyToID("_FrontWidth");
		private static readonly int FrontGlowWidthId = Shader.PropertyToID("_FrontGlowWidth");
		private static readonly int FrontGlowAddId = Shader.PropertyToID("_FrontGlowAdd");
		private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
		private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
		private static readonly int PatternFilledBoostId = Shader.PropertyToID("_PatternFilledBoost");
		private static readonly int PatternGlowId = Shader.PropertyToID("_PatternGlow");
		private static readonly int SparkSpacingId = Shader.PropertyToID("_SparkSpacing");
		private static readonly int SparkSizeId = Shader.PropertyToID("_SparkSize");
		private static readonly int SparkJitterId = Shader.PropertyToID("_SparkJitter");
		private static readonly int SparkSpeedId = Shader.PropertyToID("_SparkSpeed");
		private static readonly int SparkTwinkleSpeedId = Shader.PropertyToID("_SparkTwinkleSpeed");
		private static readonly int SparkStrengthId = Shader.PropertyToID("_SparkStrength");
		private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
		private static readonly int ProgressId = Shader.PropertyToID("_Progress");
		private static readonly int AppearId = Shader.PropertyToID("_Appear");
		private static readonly int FlashId = Shader.PropertyToID("_Flash");
		private static readonly int FlashStrengthId = Shader.PropertyToID("_FlashStrength");
		private static readonly int FlashEdgeBoostId = Shader.PropertyToID("_FlashEdgeBoost");
		private static readonly int ShockId = Shader.PropertyToID("_Shock");
		private static readonly int ShockDistanceId = Shader.PropertyToID("_ShockDistance");
		private static readonly int ShockWidthId = Shader.PropertyToID("_ShockWidth");
		private static readonly int AddStrengthId = Shader.PropertyToID("_AddStrength");
		private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
		private static readonly int PlateSizeId = Shader.PropertyToID("_PlateSize");
		private static readonly int ShapeHalfSizeId = Shader.PropertyToID("_ShapeHalfSize");

		private static Mesh plateMesh;
		private static MaterialPropertyBlock propertyBlock;
		private static readonly Action NoStop = delegate { };
		// 재생 중인 판. 씬이 바뀌어 실행기와 함께 파괴된 판은 세지 않음 (null 로 걸러냄)
		private static readonly System.Collections.Generic.List<GameObject> activePlates = new();

		[Header("그리기")]
		[SerializeField, Tooltip("GroundTelegraph 셰이더를 쓰는 재질. 색과 굵기는 이 에셋의 값이 덮어씀")] private Material material;
		[SerializeField, Tooltip("바닥에서 띄우는 높이. 바닥과 겹쳐 깜빡이면 올림")] private float liftHeight = 0.03f;

		[Header("색")]
		[SerializeField, Tooltip("안쪽 얇은 막 색. 알파는 0.08~0.15 정도로 거의 투명하게")] private Color baseColor = new(1f, 0.35f, 0.22f, 0.1f);
		[SerializeField, Tooltip("차오른 막 색. 얇은 막보다 조금 짙게")] private Color fillColor = new(1f, 0.32f, 0.18f, 0.22f);
		[SerializeField, Tooltip("차오르는 맨 앞 선 색")] private Color frontColor = new(1f, 0.85f, 0.62f, 1f);
		[SerializeField, Tooltip("가장자리 색 띠와 번짐 색. 알파는 색 띠의 진하기")] private Color edgeColor = new(1f, 0.38f, 0.2f, 0.85f);
		[SerializeField, Tooltip("가장자리 가운데 심 색. 거의 흰색에 가깝게")] private Color coreColor = new(1f, 0.93f, 0.82f, 1f);
		[SerializeField, Tooltip("안쪽 무늬 색 (원은 마법진, 띠는 화살 무늬). 알파를 테두리보다 확실히 낮게")] private Color patternColor = new(1f, 0.55f, 0.35f, 0.22f);
		[SerializeField, Tooltip("가장자리 위 반짝이는 점 색")] private Color sparkColor = new(1f, 0.85f, 0.6f, 1f);
		[SerializeField, Tooltip("발동 순간 번쩍임과 충격파 색")] private Color flashColor = new(1f, 0.9f, 0.75f, 1f);

		[Header("막")]
		[SerializeField, Range(0f, 1f), Tooltip("얇은 막의 가운데 진하기 비율. 가장자리 쪽이 1")] private float membraneCenter = 0.55f;
		[SerializeField, Range(0f, 1f), Tooltip("차오른 막의 안쪽 진하기 비율. 앞선 쪽이 1")] private float fillDepthShade = 0.65f;

		[Header("가장자리")]
		[SerializeField, Tooltip("가장자리 색 띠 굵기 (월드 단위)")] private float edgeWidth = 0.06f;
		[SerializeField, Tooltip("가장자리 가운데 밝은 심 굵기 (월드 단위)")] private float coreWidth = 0.022f;
		[SerializeField, Tooltip("심과 앞선의 빛 세기. 1 넘으면 HDR 카메라에서 번짐")] private float coreIntensity = 1.6f;
		[SerializeField, Tooltip("가장자리 번짐 폭 (월드 단위)")] private float glowWidth = 0.3f;
		[SerializeField, Tooltip("번짐 세기 (더하기)")] private float glowStrength = 0.8f;
		[SerializeField, Range(0f, 1f), Tooltip("안쪽으로 번지는 비율. 바깥이 1")] private float glowInner = 0.35f;
		[SerializeField, Range(0f, 1f), Tooltip("번짐이 밝은 바닥 위에서도 보이게 깔리는 덮기 진하기")] private float glowOpacity = 0.2f;
		[SerializeField, Range(0f, 1f), Tooltip("가장자리 맥동 깊이. 0 이면 안 뜀")] private float pulseAmount = 0.2f;
		[SerializeField, Tooltip("가장자리 맥동 빠르기 (초당 라디안)")] private float pulseSpeed = 7f;

		[Header("차오르는 앞선")]
		[SerializeField, Tooltip("앞선 굵기의 절반 (월드 단위)")] private float frontWidth = 0.018f;
		[SerializeField, Tooltip("앞선 뒤로 번지는 빛의 폭 (월드 단위)")] private float frontGlowWidth = 0.5f;
		[SerializeField, Tooltip("앞선 뒤 빛의 더하기 세기")] private float frontGlowAdd = 0.35f;

		[Header("무늬 공통")]
		[SerializeField, Tooltip("차오른 곳에서 무늬가 짙어지는 배율")] private float patternFilledBoost = 1.6f;
		[SerializeField, Tooltip("무늬의 더하기 빛 세기")] private float patternGlow = 0.25f;

		[Header("반짝이는 점")]
		[SerializeField, Tooltip("점 사이 간격 (월드 단위). 원은 둘레에 맞게 조금 조정됨")] private float sparkSpacing = 0.55f;
		[SerializeField, Tooltip("점 크기 (월드 단위)")] private float sparkSize = 0.035f;
		[SerializeField, Tooltip("점이 가장자리에서 벗어나는 폭 (월드 단위)")] private float sparkJitter = 0.04f;
		[SerializeField, Tooltip("점이 가장자리를 따라 흐르는 빠르기 (초당 칸)")] private float sparkSpeed = 0.6f;
		[SerializeField, Tooltip("깜빡임 빠르기 (초당 라디안)")] private float sparkTwinkleSpeed = 5f;
		[SerializeField, Tooltip("점 밝기. 0 이면 없음")] private float sparkStrength = 1.4f;

		[Header("빛")]
		[SerializeField, Tooltip("빛 성분 전체 배율 (더하기)")] private float addStrength = 1f;

		[Header("등장과 끝")]
		[SerializeField, Tooltip("나타날 때 시작 크기 배율. 1 이면 크기 변화 없음")] private float appearStartScale = 0.82f;
		[SerializeField, Tooltip("발동 충격파가 가장자리에서 퍼지는 거리 (월드 단위)")] private float shockDistance = 0.9f;
		[SerializeField, Tooltip("발동 충격파 굵기의 절반 (월드 단위)")] private float shockWidth = 0.06f;
		[SerializeField, Tooltip("발동 순간 가장자리가 밝아지는 배율")] private float flashEdgeBoost = 2.5f;

		[Header("동시 재생")]
		[SerializeField, Min(1), Tooltip("동시에 띄울 수 있는 판 수. 넘으면 새 예고는 생략")] private int maxConcurrentPlates = 24;

		[Header("소리")]
		[SerializeField, Tooltip("끄면 이 예고는 시작음과 끝음이 없음")] private bool playSfx = true;

		[Header("시간")]
		[SerializeField, Tooltip("Seconds 를 안 넘겼을 때 차오르는 시간 (초)")] private float defaultSeconds = 1f;
		[SerializeField, Tooltip("차오름 곡선. 가로 0~1 시간 비율, 세로 0~1 진행도")] private AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
		[SerializeField, Tooltip("나타날 때 가장자리가 한 바퀴 그려지는 시간 (초)")] private float appearSeconds = 0.16f;
		[SerializeField, Tooltip("끝 번쩍임과 충격파가 사라지는 데 걸리는 시간 (초)")] private float flashSeconds = 0.28f;
		[SerializeField, Range(0f, 1f), Tooltip("발동 순간 안쪽 번쩍임 세기. 1 이면 안쪽이 번쩍임 색으로 완전히 덮임")] private float flashStrength = 0.3f;

		/// <summary> 셰이더의 _Shape 값. 0 원, 1 띠 </summary>
		protected abstract float ShapeValue { get; }

		/// <summary> 띠 모서리 둥글림 (월드 단위). 원은 안 씀 </summary>
		protected virtual float CornerRadius => 0f;

		/// <summary> 모양 전용 셰이더 값 (원의 마법진, 띠의 화살 무늬). 매 프레임 공통 값 뒤에 부름 </summary>
		protected abstract void ApplyShapeProperties(MaterialPropertyBlock block);

		/// <summary> 모양의 반크기. x 는 가로 (원은 반지름), y 는 향하는 쪽 </summary>
		protected abstract Vector2 GetShapeHalfSize(CombatFxContext context);

		/// <summary> 기준 자리에서 판 중심까지 (direction 은 수평 단위 벡터) </summary>
		protected abstract Vector3 GetCenterOffset(CombatFxContext context, Vector3 direction);

		/// <summary> 판이 향하는 쪽. 수평 단위 벡터 </summary>
		protected abstract Vector3 ResolveDirection(CombatFxContext context);

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			// 방향 오류는 판을 꺼내기 전에 터지게 먼저 풂
			Vector3 direction = ResolveDirection(context);
			activePlates.RemoveAll(activePlate => activePlate == null);
			if (activePlates.Count >= maxConcurrentPlates)
				return NoStop;
			GameObject plate = runner.Rent(PLATE_KEY, CreatePlate);
			activePlates.Add(plate);
			return runner.Run(Animate(plate, context, direction), () =>
			{
				activePlates.Remove(plate);
				runner.Return(PLATE_KEY, plate);
			});
		}

		private IEnumerator Animate(GameObject plate, CombatFxContext context, Vector3 direction)
		{
			MeshRenderer plateRenderer = plate.GetComponent<MeshRenderer>();
			plateRenderer.sharedMaterial = material;

			bool hasFollow = context.Follow != null;
			float groundHeight = context.Position.y + liftHeight;
			Vector2 halfSize = GetShapeHalfSize(context);
			Vector3 centerOffset = GetCenterOffset(context, direction);
			// 판은 번짐, 반짝이는 점, 충격파와 그 빛이 다 들어갈 만큼 모양보다 큼
			float margin = Mathf.Max(glowWidth, sparkJitter + sparkSize * 3f, shockDistance + shockWidth * 4f) + edgeWidth;
			Vector2 plateSize = (halfSize + new Vector2(margin, margin)) * 2f;

			plate.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			Vector3 fullScale = new(plateSize.x, 1f, plateSize.y);

			float seconds = context.Seconds > 0f ? context.Seconds : defaultSeconds;
			float elapsed = 0f;
			if (playSfx)
				CombatSfx.Play(CombatSfxEvent.TelegraphCharge, GetSoundPosition(context, hasFollow));
			while (elapsed < seconds)
			{
				if (hasFollow && IsAlive(context.Follow) == false)
					yield break;

				PlacePlate(plate, context, hasFollow, groundHeight, centerOffset);
				float appear = appearSeconds > 0f ? Mathf.Clamp01(elapsed / appearSeconds) : 1f;
				// 등장: 가장자리가 그려지면서 작은 크기에서 제 크기로 (끝이 느려지는 곡선)
				float appearEased = 1f - (1f - appear) * (1f - appear);
				plate.transform.localScale = Vector3.Scale(fullScale, new Vector3(Mathf.Lerp(appearStartScale, 1f, appearEased), 1f, Mathf.Lerp(appearStartScale, 1f, appearEased)));
				ApplyProperties(plateRenderer, halfSize, plateSize, fillCurve.Evaluate(elapsed / seconds), 0f, 0f, appearEased, Mathf.Clamp01(appear * 2f));
				yield return null;
				elapsed += Time.deltaTime;
			}

			// 끝: 가득 찬 채로 번쩍이고 사라짐
			if (playSfx)
				CombatSfx.Play(CombatSfxEvent.TelegraphFire, GetSoundPosition(context, hasFollow));
			elapsed = 0f;
			while (elapsed < flashSeconds)
			{
				if (hasFollow && IsAlive(context.Follow) == false)
					yield break;

				PlacePlate(plate, context, hasFollow, groundHeight, centerOffset);
				float fade = elapsed / flashSeconds;
				plate.transform.localScale = fullScale;
				// 충격파는 처음에 빠르게 퍼지고 느려짐
				float shock = 1f - (1f - fade) * (1f - fade);
				ApplyProperties(plateRenderer, halfSize, plateSize, 1f, (1f - fade) * (1f - fade), shock, 1f, 1f - fade * fade);
				yield return null;
				elapsed += Time.deltaTime;
			}
		}

		private static Vector3 GetSoundPosition(CombatFxContext context, bool hasFollow) => hasFollow ? context.Follow.position : context.Position;

		private static bool IsAlive(Transform follow) => follow != null && follow.gameObject.activeInHierarchy;

		private static void PlacePlate(GameObject plate, CombatFxContext context, bool hasFollow, float groundHeight, Vector3 centerOffset)
		{
			Vector3 origin = hasFollow ? context.Follow.position : context.Position;
			origin.y = groundHeight;
			plate.transform.position = origin + centerOffset;
		}

		private void ApplyProperties(MeshRenderer plateRenderer, Vector2 halfSize, Vector2 plateSize, float progress, float flash, float shock, float appear, float alpha)
		{
			propertyBlock ??= new MaterialPropertyBlock();
			propertyBlock.SetFloat(ShapeId, ShapeValue);
			propertyBlock.SetColor(BaseColorId, baseColor);
			propertyBlock.SetColor(FillColorId, fillColor);
			propertyBlock.SetColor(FrontColorId, frontColor);
			propertyBlock.SetColor(EdgeColorId, edgeColor);
			propertyBlock.SetColor(CoreColorId, coreColor);
			propertyBlock.SetColor(PatternColorId, patternColor);
			propertyBlock.SetColor(SparkColorId, sparkColor);
			propertyBlock.SetColor(FlashColorId, flashColor);
			propertyBlock.SetFloat(MembraneCenterId, membraneCenter);
			propertyBlock.SetFloat(FillDepthShadeId, fillDepthShade);
			propertyBlock.SetFloat(EdgeWidthId, edgeWidth);
			propertyBlock.SetFloat(CoreWidthId, coreWidth);
			propertyBlock.SetFloat(CoreIntensityId, coreIntensity);
			propertyBlock.SetFloat(GlowWidthId, glowWidth);
			propertyBlock.SetFloat(GlowStrengthId, glowStrength);
			propertyBlock.SetFloat(GlowInnerId, glowInner);
			propertyBlock.SetFloat(GlowOpacityId, glowOpacity);
			propertyBlock.SetFloat(FrontWidthId, frontWidth);
			propertyBlock.SetFloat(FrontGlowWidthId, frontGlowWidth);
			propertyBlock.SetFloat(FrontGlowAddId, frontGlowAdd);
			propertyBlock.SetFloat(PulseAmountId, pulseAmount);
			propertyBlock.SetFloat(PulseSpeedId, pulseSpeed);
			propertyBlock.SetFloat(PatternFilledBoostId, patternFilledBoost);
			propertyBlock.SetFloat(PatternGlowId, patternGlow);
			propertyBlock.SetFloat(SparkSpacingId, sparkSpacing);
			propertyBlock.SetFloat(SparkSizeId, sparkSize);
			propertyBlock.SetFloat(SparkJitterId, sparkJitter);
			propertyBlock.SetFloat(SparkSpeedId, sparkSpeed);
			propertyBlock.SetFloat(SparkTwinkleSpeedId, sparkTwinkleSpeed);
			propertyBlock.SetFloat(SparkStrengthId, sparkStrength);
			propertyBlock.SetFloat(CornerRadiusId, CornerRadius);
			propertyBlock.SetFloat(ProgressId, Mathf.Clamp01(progress));
			propertyBlock.SetFloat(AppearId, appear);
			propertyBlock.SetFloat(FlashId, flash);
			propertyBlock.SetFloat(FlashStrengthId, flashStrength);
			propertyBlock.SetFloat(FlashEdgeBoostId, flashEdgeBoost);
			propertyBlock.SetFloat(ShockId, shock);
			propertyBlock.SetFloat(ShockDistanceId, shockDistance);
			propertyBlock.SetFloat(ShockWidthId, shockWidth);
			propertyBlock.SetFloat(AddStrengthId, addStrength);
			propertyBlock.SetFloat(AlphaId, alpha);
			propertyBlock.SetVector(PlateSizeId, plateSize);
			propertyBlock.SetVector(ShapeHalfSizeId, halfSize);
			ApplyShapeProperties(propertyBlock);
			plateRenderer.SetPropertyBlock(propertyBlock);
		}

		private static GameObject CreatePlate()
		{
			GameObject plate = new(PLATE_KEY);
			plate.AddComponent<MeshFilter>().sharedMesh = GetPlateMesh();
			MeshRenderer plateRenderer = plate.AddComponent<MeshRenderer>();
			plateRenderer.shadowCastingMode = ShadowCastingMode.Off;
			plateRenderer.receiveShadows = false;
			plateRenderer.lightProbeUsage = LightProbeUsage.Off;
			plateRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			return plate;
		}

		/// <summary> 바닥 (XZ) 에 누운 한 변 1 의 사각판. UV 의 v 가 +Z (향하는 쪽) </summary>
		private static Mesh GetPlateMesh()
		{
			if (plateMesh != null)
				return plateMesh;

			plateMesh = new Mesh
			{
				name = PLATE_KEY,
				hideFlags = HideFlags.HideAndDontSave,
				vertices = new Vector3[]
				{
					new(-0.5f, 0f, -0.5f),
					new(-0.5f, 0f, 0.5f),
					new(0.5f, 0f, 0.5f),
					new(0.5f, 0f, -0.5f),
				},
				uv = new Vector2[]
				{
					new(0f, 0f),
					new(0f, 1f),
					new(1f, 1f),
					new(1f, 0f),
				},
				triangles = new int[] { 0, 1, 2, 0, 2, 3 },
			};
			plateMesh.RecalculateBounds();
			return plateMesh;
		}
	}
}
