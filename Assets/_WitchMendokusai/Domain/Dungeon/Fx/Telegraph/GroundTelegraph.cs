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
		private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
		private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
		private static readonly int FrontWidthId = Shader.PropertyToID("_FrontWidth");
		private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
		private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
		private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
		private static readonly int ProgressId = Shader.PropertyToID("_Progress");
		private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
		private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
		private static readonly int FlashId = Shader.PropertyToID("_Flash");
		private static readonly int FlashStrengthId = Shader.PropertyToID("_FlashStrength");
		private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
		private static readonly int PlateSizeId = Shader.PropertyToID("_PlateSize");
		private static readonly int ShapeHalfSizeId = Shader.PropertyToID("_ShapeHalfSize");
		private static readonly int InnerRingInsetId = Shader.PropertyToID("_InnerRingInset");
		private static readonly int InnerRingWidthId = Shader.PropertyToID("_InnerRingWidth");
		private static readonly int FrontGlowWidthId = Shader.PropertyToID("_FrontGlowWidth");
		private static readonly int HatchScaleId = Shader.PropertyToID("_HatchScale");
		private static readonly int HatchStrengthId = Shader.PropertyToID("_HatchStrength");
		private static readonly int HatchSpeedId = Shader.PropertyToID("_HatchSpeed");
		private static readonly int TickCountId = Shader.PropertyToID("_TickCount");
		private static readonly int TickLengthId = Shader.PropertyToID("_TickLength");
		private static readonly int TickGapId = Shader.PropertyToID("_TickGap");
		private static readonly int TickSpeedId = Shader.PropertyToID("_TickSpeed");
		private static readonly int AppearId = Shader.PropertyToID("_Appear");
		private static readonly int ShockId = Shader.PropertyToID("_Shock");
		private static readonly int ShockDistanceId = Shader.PropertyToID("_ShockDistance");
		private static readonly int ShockWidthId = Shader.PropertyToID("_ShockWidth");
		private static readonly int AddStrengthId = Shader.PropertyToID("_AddStrength");

		private static Mesh plateMesh;
		private static MaterialPropertyBlock propertyBlock;
		private static readonly Action NoStop = delegate { };
		// 재생 중인 판. 씬이 바뀌어 실행기와 함께 파괴된 판은 세지 않음 (null 로 걸러냄)
		private static readonly System.Collections.Generic.List<GameObject> activePlates = new();

		[Header("그리기")]
		[SerializeField, Tooltip("GroundTelegraph 셰이더를 쓰는 재질. 색과 굵기는 이 에셋의 값이 덮어씀")] private Material material;
		[SerializeField, Tooltip("바닥에서 띄우는 높이. 바닥과 겹쳐 깜빡이면 올림")] private float liftHeight = 0.03f;

		[Header("색")]
		[SerializeField, Tooltip("아직 안 찬 안쪽 색. 알파가 옅을수록 차오른 면과 구분됨")] private Color baseColor = new(0.89f, 0.35f, 0.3f, 0.2f);
		[SerializeField, Tooltip("차오른 면 색")] private Color fillColor = new(0.93f, 0.36f, 0.3f, 0.5f);
		[SerializeField, Tooltip("차오르는 맨 앞 선 색")] private Color frontColor = new(1f, 0.86f, 0.8f, 0.9f);
		[SerializeField, Tooltip("가장자리 선과 바깥 빛 색")] private Color edgeColor = new(0.89f, 0.25f, 0.22f, 1f);
		[SerializeField, Tooltip("끝나는 순간 번쩍이는 색")] private Color flashColor = new(1f, 0.97f, 0.92f, 1f);

		[Header("선")]
		[SerializeField, Tooltip("가장자리 선 굵기 (월드 단위)")] private float edgeWidth = 0.08f;
		[SerializeField, Tooltip("차오르는 맨 앞 선 굵기의 절반 (월드 단위)")] private float frontWidth = 0.04f;
		[SerializeField, Tooltip("가장자리 바깥으로 번지는 빛의 폭 (월드 단위)")] private float glowWidth = 0.25f;
		[SerializeField, Range(0f, 1f), Tooltip("바깥 빛 세기")] private float glowStrength = 0.35f;
		[SerializeField, Range(0f, 1f), Tooltip("가장자리 맥동 깊이. 0 이면 안 뜀")] private float pulseAmount = 0.3f;
		[SerializeField, Tooltip("가장자리 맥동 빠르기 (초당 라디안)")] private float pulseSpeed = 9f;

		[Header("안쪽 무늬와 바깥 장식")]
		[SerializeField, Tooltip("가장자리 안쪽 가는 고리까지의 거리 (월드 단위)")] private float innerRingInset = 0.16f;
		[SerializeField, Tooltip("안쪽 가는 고리 굵기 (월드 단위)")] private float innerRingWidth = 0.02f;
		[SerializeField, Tooltip("차오르는 앞선 뒤로 번지는 빛의 폭 (월드 단위)")] private float frontGlowWidth = 0.45f;
		[SerializeField, Tooltip("빗금 촘촘함 (월드 단위당 줄 수)")] private float hatchScale = 2.4f;
		[SerializeField, Range(0f, 1f), Tooltip("빗금 진하기. 0 이면 없음")] private float hatchStrength = 0.22f;
		[SerializeField, Tooltip("빗금이 흐르는 빠르기")] private float hatchSpeed = 0.35f;
		[SerializeField, Tooltip("바깥 눈금 수. 원은 둘레 전체의 개수, 띠는 월드 단위당 개수")] private float tickCount = 36f;
		[SerializeField, Tooltip("바깥 눈금 길이 (월드 단위). 0 이면 없음")] private float tickLength = 0.12f;
		[SerializeField, Tooltip("가장자리에서 눈금까지의 틈 (월드 단위)")] private float tickGap = 0.07f;
		[SerializeField, Tooltip("눈금이 둘레를 도는 빠르기")] private float tickSpeed = 0.06f;
		[SerializeField, Range(0f, 2f), Tooltip("밝은 선이 빛나는 세기 (더하기)")] private float addStrength = 0.7f;

		[Header("등장과 끝")]
		[SerializeField, Tooltip("나타날 때 시작 크기 배율. 1 이면 크기 변화 없음")] private float appearStartScale = 0.82f;
		[SerializeField, Tooltip("끝 충격파가 가장자리에서 퍼지는 거리 (월드 단위)")] private float shockDistance = 0.9f;
		[SerializeField, Tooltip("끝 충격파 굵기 (월드 단위)")] private float shockWidth = 0.12f;

		[Header("동시 재생")]
		[SerializeField, Min(1), Tooltip("동시에 띄울 수 있는 판 수. 넘으면 새 예고는 생략")] private int maxConcurrentPlates = 24;

		[Header("소리")]
		[SerializeField, Tooltip("끄면 이 예고는 시작음과 끝음이 없음")] private bool playSfx = true;

		[Header("시간")]
		[SerializeField, Tooltip("Seconds 를 안 넘겼을 때 차오르는 시간 (초)")] private float defaultSeconds = 1f;
		[SerializeField, Tooltip("차오름 곡선. 가로 0~1 시간 비율, 세로 0~1 진행도")] private AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
		[SerializeField, Tooltip("나타날 때 가장자리가 한 바퀴 그려지는 시간 (초)")] private float appearSeconds = 0.16f;
		[SerializeField, Tooltip("끝 번쩍임과 충격파가 사라지는 데 걸리는 시간 (초)")] private float flashSeconds = 0.28f;
		[SerializeField, Range(0f, 1f), Tooltip("끝 번쩍임 세기. 1 이면 안쪽이 번쩍임 색으로 완전히 덮임")] private float flashStrength = 0.85f;

		/// <summary> 셰이더의 _Shape 값. 0 원, 1 띠 </summary>
		protected abstract float ShapeValue { get; }

		/// <summary> 띠 모서리 둥글림 (월드 단위). 원은 안 씀 </summary>
		protected virtual float CornerRadius => 0f;

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
			// 판은 바깥 빛, 눈금, 충격파가 다 들어갈 만큼 모양보다 큼
			float margin = Mathf.Max(glowWidth, tickGap + tickLength, shockDistance + shockWidth) + edgeWidth;
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
			propertyBlock.SetColor(FlashColorId, flashColor);
			propertyBlock.SetFloat(EdgeWidthId, edgeWidth);
			propertyBlock.SetFloat(FrontWidthId, frontWidth);
			propertyBlock.SetFloat(GlowWidthId, glowWidth);
			propertyBlock.SetFloat(GlowStrengthId, glowStrength);
			propertyBlock.SetFloat(CornerRadiusId, CornerRadius);
			propertyBlock.SetFloat(ProgressId, Mathf.Clamp01(progress));
			propertyBlock.SetFloat(PulseAmountId, pulseAmount);
			propertyBlock.SetFloat(PulseSpeedId, pulseSpeed);
			propertyBlock.SetFloat(FlashId, flash);
			propertyBlock.SetFloat(FlashStrengthId, flashStrength);
			propertyBlock.SetFloat(AlphaId, alpha);
			propertyBlock.SetVector(PlateSizeId, plateSize);
			propertyBlock.SetVector(ShapeHalfSizeId, halfSize);
			propertyBlock.SetFloat(InnerRingInsetId, innerRingInset);
			propertyBlock.SetFloat(InnerRingWidthId, innerRingWidth);
			propertyBlock.SetFloat(FrontGlowWidthId, frontGlowWidth);
			propertyBlock.SetFloat(HatchScaleId, hatchScale);
			propertyBlock.SetFloat(HatchStrengthId, hatchStrength);
			propertyBlock.SetFloat(HatchSpeedId, hatchSpeed);
			propertyBlock.SetFloat(TickCountId, tickCount);
			propertyBlock.SetFloat(TickLengthId, tickLength);
			propertyBlock.SetFloat(TickGapId, tickGap);
			propertyBlock.SetFloat(TickSpeedId, tickSpeed);
			propertyBlock.SetFloat(AppearId, appear);
			propertyBlock.SetFloat(ShockId, shock);
			propertyBlock.SetFloat(ShockDistanceId, shockDistance);
			propertyBlock.SetFloat(ShockWidthId, shockWidth);
			propertyBlock.SetFloat(AddStrengthId, addStrength);
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
