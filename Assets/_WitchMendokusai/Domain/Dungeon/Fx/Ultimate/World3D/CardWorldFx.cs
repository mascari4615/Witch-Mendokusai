using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 카드 3D 의 공통 부품. 약한 판 (CardLightUltimateFx) 과 강한 판 (CardHeavyUltimateFx) 이 같이 씀
	/// - 카드 메시 (코드 생성, 두께와 둥근 모서리, 앞면 마름모 무늬, 뒷면 금 격자) 와 재질, 꽂힘, 금 불꽃, 종이 조각, 균열 데칼
	/// - 앞면 재질은 faceMaterial 에셋으로 교체 가능 (비우면 코드 무늬). 카드 앞면 그림 에셋이 생기면 여기만 바꿈
	/// - 카드 면은 반사를 넓고 강하게: 돌 때 해와 연출 광원을 반사해 번쩍임 (실제 반사 하이라이트)
	/// - 설계: memo wm/design/ult3d-all-20261120/03_card.md
	/// </summary>
	public abstract class CardWorldFx : WorldUltimateFx
	{
		[Header("카드")]
		[SerializeField] private Color faceTone = new(0.95f, 0.92f, 0.84f, 1f);
		[SerializeField] private Color pipTone = new(0.85f, 0.12f, 0.2f, 1f);
		[SerializeField] private Color backTone = new(0.32f, 0.08f, 0.3f, 1f);
		[SerializeField] private Color goldTone = new(1f, 0.78f, 0.36f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color goldGlow = new(1.3f, 0.85f, 0.3f);
		[SerializeField, ColorUsage(false, true)] private Color cardRim = new(0.6f, 0.45f, 0.75f);
		[SerializeField, Range(0f, 1f)] private float cardSpecular = 1f;
		[SerializeField, Range(1f, 128f)] private float cardSmoothness = 24f;
		[SerializeField, Tooltip("앞면 재질 교체 자리. 비우면 코드 무늬 (꼭짓점 색). 하위 메시 구분이 없어 카드 전체에 씀")] private Material faceMaterial;
		[SerializeField, Min(0.2f), Tooltip("카드 한 장 배율 (기본 높이 0.88)")] private float cardScale = 1.15f;

		[Header("반응 색")]
		[SerializeField, ColorUsage(true, true)] private Color sparkColor = new(2.6f, 1.8f, 0.7f, 1f);
		[SerializeField, ColorUsage(true, true)] private Color sparkWhite = new(2.4f, 2.3f, 2.1f, 1f);
		[SerializeField] private Color lightColor = new(1f, 0.82f, 0.5f, 1f);
		[SerializeField] private Color dustColor = new(0.6f, 0.55f, 0.5f, 0.65f);
		[SerializeField] private Color scorchColor = new(0.08f, 0.05f, 0.06f, 0.8f);
		[SerializeField] private Color crackLineColor = new(0.95f, 0.7f, 0.35f, 1f);
		[SerializeField] private int crackSeed = 71;

		protected float CardScale => cardScale;
		protected Color GoldGlow => goldGlow;
		protected Color LightColor => lightColor;
		protected Color SparkColor => sparkColor;
		protected Color DustColor => dustColor;

		protected Mesh CardMesh(UltimateFxRun run) => Keep(run, UltimateMeshFactory.Card(faceTone, pipTone, backTone, goldTone));

		protected Material CardMaterial(UltimateFxRun run)
		{
			if (faceMaterial != null)
			{
				Material replaced = new(faceMaterial) { name = "UltimateFx_CardFace" };
				run.OnRelease(() => Object.Destroy(replaced));
				return replaced;
			}
			Material material = Glow(run, Color.white, goldGlow, cardRim, true);
			material.SetFloat(SPECULAR_ID, cardSpecular);
			material.SetFloat(SMOOTHNESS_ID, cardSmoothness);
			material.SetFloat(RIM_POWER_ID, 3f);
			material.SetColor(DISSOLVE_EDGE_ID, sparkColor);
			// 카드 메시는 가운데 원점 (세로 -0.44~0.44). 녹는 높이 범위를 맞춤
			material.SetVector(DISSOLVE_RANGE_ID, new Vector4(-0.45f, 0.45f, 0f, 0f));
			return material;
		}

		/// <summary> 카드 한 장 물체 (그림자 투사) </summary>
		protected Transform CardObject(UltimateFxRun run, Mesh mesh, Material material, float scale, bool castShadows)
		{
			GameObject card = UltimateFxStage.CreateMeshObject("Card", run.Root, mesh, material);
			Solid(card, castShadows);
			card.transform.localScale = Vector3.one * scale * cardScale;
			return card.transform;
		}

		/// <summary> 땅에 비스듬히 꽂힌 자세. 카드 아래 끝이 땅속 sink 만큼 </summary>
		protected static Quaternion StuckRotation(Vector3 travel, float pitch, float roll)
		{
			Vector3 flat = travel;
			flat.y = 0f;
			Quaternion facing = Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward);
			return facing * Quaternion.Euler(pitch, 0f, roll);
		}

		/// <summary> 금색 불꽃과 흰 점이 튐 </summary>
		protected void GoldSparks(UltimateFxRun run, Vector3 origin, int count, float speed)
		{
			ParticleSystem sparks = Motes(run, "CardSparks", count + 8, 0.9f, 0f);
			for (int i = 0; i < count; i++)
			{
				Vector3 velocity = (Random.onUnitSphere + Vector3.up * 1.2f).normalized * Random.Range(0.4f, 1f) * speed;
				UltimateFxStage.Emit(sparks, origin, velocity, Random.Range(0.06f, 0.14f), Random.Range(0.3f, 0.7f), Random.value < 0.7f ? sparkColor : sparkWhite);
			}
		}

		/// <summary> 작은 카드 조각 (같은 카드 메시 축소, 메시 입자). 회전하며 날아 바닥에 멈춤 </summary>
		protected ParticleSystem CardBits(UltimateFxRun run, Mesh mesh, Material material, int maxParticles, float gravity, bool collide)
		{
			ParticleSystem bits = MeshParticles(run, "CardBits", mesh, material, maxParticles + 8, gravity, collide, 0.05f, 0.9f, false);
			ParticleSystem.LimitVelocityOverLifetimeModule drag = bits.limitVelocityOverLifetime;
			drag.enabled = true;
			drag.drag = 1.2f;
			return bits;
		}

		/// <summary> 균열과 그을림 그림. 연출 한 번에 한 장 만들어 여러 데칼이 같이 씀 </summary>
		protected Texture2D CrackTexture(UltimateFxRun run, int seedOffset) => ScorchTexture(run, crackSeed + seedOffset, scorchColor, crackLineColor, 0.5f);

		protected static void Crack(UltimateFxRun run, Texture2D texture, Vector3 position, float size, float appear, float lingerEnd, float fadeOut) => GroundDecal(run, texture, position, size, appear, 0.06f, lingerEnd, fadeOut, 0);

		protected void Puff(UltimateFxRun run, Vector3 origin, int count, float radius) => DustRing(run, origin, count, radius, dustColor, 1f);
	}
}
