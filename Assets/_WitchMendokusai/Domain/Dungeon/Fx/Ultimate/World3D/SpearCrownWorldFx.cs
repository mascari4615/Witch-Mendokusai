using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 창 왕관 3D 의 공통 부품. 약한 판 (SpearCrownLightUltimateFx) 과 강한 판 (SpearCrownHeavyUltimateFx) 이 같이 씀
	/// - 결정 가시 메시 (코드 생성, 면마다 밝기와 발광이 다른 각진 기둥) 와 재질, 솟음, 결정 가루, 깨짐 (조각 메시 입자), 뿌리 바위, 균열 데칼
	/// - 쿼터뷰에서 분홍 판으로 읽히지 않게: 바탕은 짙게, 면마다 발광 차, 프레넬 테두리, 심 점광이 안쪽 면을 비춤 (설계서 알려진 위험)
	/// - 설계: memo wm/design/ult3d-all-20261120/02_spear-crown.md
	/// </summary>
	public abstract class SpearCrownWorldFx : WorldUltimateFx
	{
		protected sealed class CrystalPiece
		{
			public Transform Transform;
			public Vector3 Buried;
			public Vector3 Raised;
			public float Height;
			public float Width;
		}

		[Header("결정")]
		[SerializeField] private Color crystalColor = new(0.55f, 0.16f, 0.38f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color crystalEmission = new(0.85f, 0.16f, 0.48f);
		[SerializeField, ColorUsage(false, true)] private Color crystalRim = new(1.5f, 0.75f, 1.3f);
		[SerializeField, Range(1f, 8f)] private float crystalRimPower = 2.2f;
		[SerializeField, Range(0f, 1f)] private float crystalSpecular = 0.9f;
		[SerializeField, Range(1f, 128f)] private float crystalSmoothness = 40f;
		[SerializeField, Min(4)] private int crystalSides = 6;
		[SerializeField, Min(1)] private int crystalVariants = 4;
		[SerializeField] private int crystalSeed = 23;
		[SerializeField, Tooltip("바깥으로 기우는 각도 범위 (도)")] private Vector2 crystalTilt = new(8f, 26f);
		[SerializeField, Range(0f, 0.5f), Tooltip("솟을 때 넘쳤다 줄어드는 정도")] private float riseOvershoot = 0.14f;
		[SerializeField, Min(0.05f), Tooltip("조각 하나의 최대 크기. 거대 결정 조각이 통나무처럼 커지지 않게")] private float shardMaxSize = 0.9f;

		[Header("빛과 입자 색")]
		[SerializeField] private Color coreLightColor = new(1f, 0.45f, 0.78f, 1f);
		[SerializeField, ColorUsage(true, true)] private Color sparkleColor = new(2.6f, 1.1f, 2f, 1f);
		[SerializeField, ColorUsage(true, true)] private Color sparkleWhite = new(2.4f, 2.2f, 2.4f, 1f);
		[SerializeField] private Color dustColor = new(0.62f, 0.55f, 0.5f, 0.7f);
		[SerializeField] private Color rockColor = new(0.38f, 0.31f, 0.26f, 1f);
		[SerializeField] private Color scorchColor = new(0.1f, 0.05f, 0.08f, 0.85f);
		[SerializeField] private Color crackLineColor = new(1f, 0.45f, 0.85f, 1f);
		[SerializeField] private int crackSeed = 57;

		protected Color CoreLightColor => coreLightColor;
		protected Color SparkleColor => sparkleColor;
		protected Color DustColor => dustColor;

		protected Mesh[] CrystalMeshes(UltimateFxRun run)
		{
			Mesh[] meshes = new Mesh[Mathf.Max(1, crystalVariants)];
			for (int i = 0; i < meshes.Length; i++)
				meshes[i] = Keep(run, UltimateMeshFactory.Crystal(crystalSeed + i * 31, crystalSides + (i % 2)));
			return meshes;
		}

		protected Material CrystalMaterial(UltimateFxRun run) => CrystalMaterial(run, 1f);

		/// <summary> emissionScale: 거대 결정처럼 큰 면은 발광을 낮춰야 빛 번짐에 묻혀 평평한 띠로 읽히지 않음 </summary>
		protected Material CrystalMaterial(UltimateFxRun run, float emissionScale)
		{
			Material material = Glow(run, crystalColor, crystalEmission * emissionScale, crystalRim, true);
			material.SetFloat(RIM_POWER_ID, crystalRimPower);
			material.SetFloat(SPECULAR_ID, crystalSpecular);
			material.SetFloat(SMOOTHNESS_ID, crystalSmoothness);
			material.SetColor(DISSOLVE_EDGE_ID, sparkleColor);
			return material;
		}

		/// <summary> 땅 속에 숨긴 결정 하나를 만듦. 솟는 시각은 Rise 로 </summary>
		protected CrystalPiece PlaceCrystal(UltimateFxRun run, Mesh mesh, Material material, Vector3 position, Vector3 outward, float height, float width, bool castShadows)
		{
			GameObject crystal = UltimateFxStage.CreateMeshObject("Crystal", run.Root, mesh, material);
			Solid(crystal, castShadows);
			Vector3 tiltAxis = Vector3.Cross(Vector3.up, outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward);
			crystal.transform.rotation = Quaternion.AngleAxis(Random.Range(crystalTilt.x, crystalTilt.y), tiltAxis) * Quaternion.Euler(0f, Random.value * 360f, 0f);
			crystal.transform.localScale = new Vector3(width, height, width);
			position.y = UltimateFxStage.GroundHeight(position);
			CrystalPiece piece = new()
			{
				Transform = crystal.transform,
				Raised = position,
				Buried = position - crystal.transform.up * height * 1.05f,
				Height = height,
				Width = width,
			};
			crystal.transform.position = piece.Buried;
			crystal.SetActive(false);
			return piece;
		}

		/// <summary> start 에 seconds 동안 넘쳤다 줄어들며 솟음 </summary>
		protected void Rise(UltimateFxRun run, CrystalPiece piece, float start, float seconds)
		{
			run.At(start, () => piece.Transform.gameObject.SetActive(true));
			run.Track(start, seconds, progress => piece.Transform.position = Vector3.LerpUnclamped(piece.Buried, piece.Raised, OverShoot(progress, riseOvershoot)));
		}

		/// <summary> 결정이 조각 메시로 깨짐. 조각은 바닥에 튀어 남았다 줄어듦, 결정은 빠르게 녹음 </summary>
		protected void Shatter(UltimateFxRun run, Material material, Mesh shardMesh, IReadOnlyList<CrystalPiece> pieces, float start, int shardsPerUnitHeight, float speed, float linger, float dissolveSeconds)
		{
			int total = 0;
			foreach (CrystalPiece piece in pieces)
				total += Mathf.Max(2, Mathf.RoundToInt(piece.Height * shardsPerUnitHeight));
			Material shardMaterial = CrystalMaterial(run);
			ParticleSystem shards = MeshParticles(run, "CrystalShards", shardMesh, shardMaterial, total + 8, 2.2f, true, 0.3f, 0.5f, true);
			UltimateFxStage.SetLifetimeLook(shards, null, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
			ParticleSystem sparkle = Motes(run, "ShatterSparkle", total * 2 + 8, 0.4f, 0.8f);
			run.At(start, () =>
			{
				foreach (CrystalPiece piece in pieces)
				{
					int count = Mathf.Max(2, Mathf.RoundToInt(piece.Height * shardsPerUnitHeight));
					for (int i = 0; i < count; i++)
					{
						Vector3 along = piece.Transform.position + piece.Transform.up * piece.Height * Random.Range(0.1f, 0.9f);
						Vector3 away = (along - piece.Raised);
						away.y = 0f;
						away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere;
						Vector3 velocity = (away * Random.Range(0.4f, 1f) + Random.insideUnitSphere * 0.6f) * speed + Vector3.up * speed * Random.Range(0.3f, 0.8f);
						float size = Mathf.Min(shardMaxSize, piece.Width * Random.Range(0.35f, 0.7f)) * Random.Range(0.3f, 1f);
						Color shard = Color.white;
						shard.a = Random.Range(0.4f, 1f);
						EmitMesh(shards, along, velocity, size, linger * Random.Range(0.85f, 1.1f), shard, Random.insideUnitSphere * 180f, Random.insideUnitSphere * 600f);
						UltimateFxStage.Emit(sparkle, along, velocity * 0.5f, Random.Range(0.08f, 0.2f), Random.Range(0.5f, 1f), Random.value < 0.5f ? sparkleColor : sparkleWhite);
					}
				}
			});
			run.Track(start, dissolveSeconds, progress =>
			{
				material.SetFloat(DISSOLVE_DIRECTION_ID, 1f);
				material.SetFloat(DISSOLVE_ID, UltimateFxRun.EaseOut(progress));
			}, () =>
			{
				foreach (CrystalPiece piece in pieces)
					run.Destroy(piece.Transform.gameObject);
			});
		}

		/// <summary> 반짝이는 결정 가루가 솟아오름 </summary>
		protected void Sparkle(UltimateFxRun run, Vector3 origin, int count, float radius, float speed)
		{
			ParticleSystem sparkle = Motes(run, "CrystalSparkle", count + 8, -0.1f, 1f);
			for (int i = 0; i < count; i++)
			{
				Vector3 offset = UltimateFxStage.RandomRingPoint(0f, radius);
				Vector3 velocity = offset.normalized * Random.Range(0.4f, 1.4f) * speed + Vector3.up * Random.Range(1.5f, 4.5f) * speed;
				UltimateFxStage.Emit(sparkle, origin + offset + Vector3.up * Random.Range(0.1f, 0.6f), velocity, Random.Range(0.06f, 0.18f), Random.Range(0.7f, 1.5f), Random.value < 0.6f ? sparkleColor : sparkleWhite);
			}
		}

		/// <summary> 결정 뿌리에서 바위가 바깥으로 기울며 들림. 실제 메시라 그림자와 광원 음영이 생김 </summary>
		protected void RootRocks(UltimateFxRun run, Vector3 center, int count, Vector2 radius, Vector2 scale, float start, float riseSeconds, float sinkStart, float sinkSeconds)
		{
			if (count <= 0)
				return;
			Material material = Glow(run, Color.white, Color.black, Color.black, true);
			Mesh[] meshes = { Keep(run, UltimateMeshFactory.Rock(crystalSeed + 3, rockColor, 0.45f)), Keep(run, UltimateMeshFactory.Rock(crystalSeed + 9, rockColor * 0.85f, 0.55f)) };
			Transform[] rocks = new Transform[count];
			Vector3[] raised = new Vector3[count];
			Vector3[] buried = new Vector3[count];
			for (int i = 0; i < count; i++)
			{
				float angle = (i + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 position = center + direction * Random.Range(radius.x, radius.y);
				position.y = UltimateFxStage.GroundHeight(position);
				float size = Random.Range(scale.x, scale.y);
				GameObject rock = UltimateFxStage.CreateMeshObject("RootRock", run.Root, meshes[i % meshes.Length], material);
				Solid(rock, true);
				rock.transform.rotation = Quaternion.AngleAxis(Random.Range(20f, 45f), Vector3.Cross(Vector3.up, direction)) * Quaternion.Euler(0f, Random.value * 360f, 0f);
				rock.transform.localScale = new Vector3(size, size * Random.Range(0.6f, 0.9f), size);
				raised[i] = position + Vector3.up * size * 0.12f;
				buried[i] = position - Vector3.up * size * 0.6f;
				rock.transform.position = buried[i];
				rock.SetActive(false);
				rocks[i] = rock.transform;
			}
			run.At(start, () =>
			{
				foreach (Transform rock in rocks)
					rock.gameObject.SetActive(true);
			});
			run.Track(start, riseSeconds, progress =>
			{
				for (int i = 0; i < rocks.Length; i++)
					rocks[i].position = Vector3.LerpUnclamped(buried[i], raised[i], OverShoot(progress, 0.15f));
			});
			run.Track(sinkStart, sinkSeconds, progress =>
			{
				for (int i = 0; i < rocks.Length; i++)
					rocks[i].position = Vector3.Lerp(raised[i], buried[i], UltimateFxRun.EaseIn(progress));
			});
		}

		/// <summary> 균열과 그을림 그림. 연출 한 번에 한 장 만들어 여러 데칼이 같이 씀 </summary>
		protected Texture2D CrackTexture(UltimateFxRun run, int seedOffset, float coreRadius) => ScorchTexture(run, crackSeed + seedOffset, scorchColor, crackLineColor, coreRadius);

		/// <summary> 결정 뿌리의 균열과 그을림 </summary>
		protected static void Crack(UltimateFxRun run, Texture2D texture, Vector3 position, float size, float appear, float lingerEnd, float fadeOut) => GroundDecal(run, texture, position, size, appear, 0.08f, lingerEnd, fadeOut, 0);

		protected void Puff(UltimateFxRun run, Vector3 origin, int count, float radius) => DustRing(run, origin, count, radius, dustColor, 1f);
	}
}
