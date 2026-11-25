using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 바닥 예고의 공통 부분. 바닥에 눕힌 사각판 하나에 GroundTelegraph 셰이더로 모양을 그림
	/// - 차오름: Seconds 동안 진행도 0 -> 1 (채움 곡선). 게임 시간 (판정과 맞아야 함)
	/// - 끝: 진행도 1 이 되는 순간 번쩍이고 겉모습의 FlashSeconds 동안 사라짐
	/// - Follow 가 있으면 그 자리를 따라가고, Follow 가 꺼지거나 없어지면 바로 치움 (시전자 사망)
	/// - 판은 실행기의 Rent, Return 으로 재사용. 값은 MaterialPropertyBlock (재질 에셋은 하나를 같이 씀)
	/// 모양 (원, 띠) 은 파생 클래스, 색과 굵기와 채움 방식은 겉모습 에셋 (TelegraphStyle) 이 정함
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

		/// <summary> 판 상한에 걸려 안 뜬 재생의 중단 함수인가 </summary>
		public static bool IsSkipped(Action stop) => stop == NoStop;
		// 재생 중인 판. 씬이 바뀌어 실행기와 함께 파괴된 판은 세지 않음 (null 로 걸러냄)
		private static readonly System.Collections.Generic.List<GameObject> activePlates = new();

		[Header("겉모습")]
		[SerializeField, Tooltip("색, 굵기, 채움 방식, 끝 번쩍임 한 벌. 원과 띠가 같은 겉모습 에셋을 같이 씀")] private TelegraphStyle style;

		[Header("놓기")]
		[SerializeField, Tooltip("바닥에서 띄우는 높이. 바닥과 겹쳐 깜빡이면 올림")] private float liftHeight = 0.03f;
		[SerializeField, Min(1), Tooltip("동시에 띄울 수 있는 판 수. 넘으면 새 예고는 생략")] private int maxConcurrentPlates = 24;
		[SerializeField, Tooltip("Seconds 를 안 넘겼을 때 차오르는 시간 (초)")] private float defaultSeconds = 1f;

		public TelegraphStyle Style => style;

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
			plateRenderer.sharedMaterial = style.Material;

			bool hasFollow = context.Follow != null;
			float groundHeight = context.Position.y + liftHeight;
			Vector2 halfSize = GetShapeHalfSize(context);
			Vector3 centerOffset = GetCenterOffset(context, direction);
			// 판은 번짐, 반짝이는 점, 충격파와 그 빛이 다 들어갈 만큼 모양보다 큼
			float margin = Mathf.Max(style.GlowWidth, style.SparkJitter + style.SparkSize * 3f, style.ShockDistance + style.ShockWidth * 4f) + style.EdgeWidth;
			Vector2 plateSize = (halfSize + new Vector2(margin, margin)) * 2f;

			plate.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			Vector3 fullScale = new(plateSize.x, 1f, plateSize.y);

			float seconds = context.Seconds > 0f ? context.Seconds : defaultSeconds;
			float elapsed = 0f;
			if (style.PlaySfx)
				CombatSfx.Play(CombatSfxEvent.TelegraphCharge, GetSoundPosition(context, hasFollow));
			while (elapsed < seconds)
			{
				if (hasFollow && IsAlive(context.Follow) == false)
					yield break;

				PlacePlate(plate, context, hasFollow, groundHeight, centerOffset);
				float appear = style.AppearSeconds > 0f ? Mathf.Clamp01(elapsed / style.AppearSeconds) : 1f;
				// 등장: 가장자리가 그려지면서 작은 크기에서 제 크기로 (끝이 느려지는 곡선)
				float appearEased = 1f - (1f - appear) * (1f - appear);
				plate.transform.localScale = Vector3.Scale(fullScale, new Vector3(Mathf.Lerp(style.AppearStartScale, 1f, appearEased), 1f, Mathf.Lerp(style.AppearStartScale, 1f, appearEased)));
				ApplyProperties(plateRenderer, halfSize, plateSize, style.FillCurve.Evaluate(elapsed / seconds), 0f, 0f, appearEased, Mathf.Clamp01(appear * 2f));
				yield return null;
				elapsed += Time.deltaTime;
			}

			// 끝: 가득 찬 채로 번쩍이고 사라짐
			if (style.PlaySfx)
				CombatSfx.Play(CombatSfxEvent.TelegraphFire, GetSoundPosition(context, hasFollow));
			elapsed = 0f;
			while (elapsed < style.FlashSeconds)
			{
				if (hasFollow && IsAlive(context.Follow) == false)
					yield break;

				PlacePlate(plate, context, hasFollow, groundHeight, centerOffset);
				float fade = elapsed / style.FlashSeconds;
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
			propertyBlock.SetColor(BaseColorId, style.BaseColor);
			propertyBlock.SetColor(FillColorId, style.FillColor);
			propertyBlock.SetColor(FrontColorId, style.FrontColor);
			propertyBlock.SetColor(EdgeColorId, style.EdgeColor);
			propertyBlock.SetColor(CoreColorId, style.CoreColor);
			propertyBlock.SetColor(PatternColorId, style.PatternColor);
			propertyBlock.SetColor(SparkColorId, style.SparkColor);
			propertyBlock.SetColor(FlashColorId, style.FlashColor);
			propertyBlock.SetFloat(MembraneCenterId, style.MembraneCenter);
			propertyBlock.SetFloat(FillDepthShadeId, style.FillDepthShade);
			propertyBlock.SetFloat(EdgeWidthId, style.EdgeWidth);
			propertyBlock.SetFloat(CoreWidthId, style.CoreWidth);
			propertyBlock.SetFloat(CoreIntensityId, style.CoreIntensity);
			propertyBlock.SetFloat(GlowWidthId, style.GlowWidth);
			propertyBlock.SetFloat(GlowStrengthId, style.GlowStrength);
			propertyBlock.SetFloat(GlowInnerId, style.GlowInner);
			propertyBlock.SetFloat(GlowOpacityId, style.GlowOpacity);
			propertyBlock.SetFloat(FrontWidthId, style.FrontWidth);
			propertyBlock.SetFloat(FrontGlowWidthId, style.FrontGlowWidth);
			propertyBlock.SetFloat(FrontGlowAddId, style.FrontGlowAdd);
			propertyBlock.SetFloat(PulseAmountId, style.PulseAmount);
			propertyBlock.SetFloat(PulseSpeedId, style.PulseSpeed);
			propertyBlock.SetFloat(PatternFilledBoostId, style.PatternFilledBoost);
			propertyBlock.SetFloat(PatternGlowId, style.PatternGlow);
			propertyBlock.SetFloat(SparkSpacingId, style.SparkSpacing);
			propertyBlock.SetFloat(SparkSizeId, style.SparkSize);
			propertyBlock.SetFloat(SparkJitterId, style.SparkJitter);
			propertyBlock.SetFloat(SparkSpeedId, style.SparkSpeed);
			propertyBlock.SetFloat(SparkTwinkleSpeedId, style.SparkTwinkleSpeed);
			propertyBlock.SetFloat(SparkStrengthId, style.SparkStrength);
			propertyBlock.SetFloat(CornerRadiusId, CornerRadius);
			propertyBlock.SetFloat(ProgressId, Mathf.Clamp01(progress));
			propertyBlock.SetFloat(AppearId, appear);
			propertyBlock.SetFloat(FlashId, flash);
			propertyBlock.SetFloat(FlashStrengthId, style.FlashStrength);
			propertyBlock.SetFloat(FlashEdgeBoostId, style.FlashEdgeBoost);
			propertyBlock.SetFloat(ShockId, shock);
			propertyBlock.SetFloat(ShockDistanceId, style.ShockDistance);
			propertyBlock.SetFloat(ShockWidthId, style.ShockWidth);
			propertyBlock.SetFloat(AddStrengthId, style.AddStrength);
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
