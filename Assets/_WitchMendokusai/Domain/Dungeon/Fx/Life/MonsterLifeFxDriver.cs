using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 등장과 사망 연출의 구동기. 사망 잔상과 발밑 고리를 한곳에서 매 프레임 갱신
	/// - 몬스터마다 코루틴이나 Update 를 두지 않으려고 모음. 초당 수십 마리가 죽어도 갱신은 이 Update 하나
	/// - CombatFxRunner 와 같은 오브젝트에 처음 쓸 때 붙음. 오브젝트는 실행기의 Rent, Return 으로 재사용
	/// - 수는 스타일 에셋의 상한 (MaxGhosts, MaxPuffs) 까지. 넘으면 NONE 을 돌려주고 아무것도 안 만듦
	/// - 게임 시간으로 흐름. 히트스톱과 일시정지 동안 같이 멈춤
	/// </summary>
	public class MonsterLifeFxDriver : MonoBehaviour
	{
		/// <summary> 재생하지 않았을 때 돌려주는 번호 </summary>
		public const int NONE = 0;

		private const string GHOST_KEY = "MonsterDeathGhost";
		private const string PUFF_KEY = "MonsterSpawnPuff";

		private static readonly int TINT_ID = Shader.PropertyToID("_Tint");
		private static readonly int FLIP_ID = Shader.PropertyToID("_Flip");
		private static readonly int DEFORM_ID = Shader.PropertyToID("_Deform");
		private static readonly int DISSOLVE_ID = Shader.PropertyToID("_Dissolve");
		private static readonly int EDGE_COLOR_ID = Shader.PropertyToID("_EdgeColor");
		private static readonly int FLASH_COLOR_ID = Shader.PropertyToID("_FlashColor");
		private static readonly int BOUNDS_ID = Shader.PropertyToID("_BoundsY");
		private static readonly int BILLBOARD_MODE_ID = Shader.PropertyToID("_BillboardMode");
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly int SHAPE_ID = Shader.PropertyToID("_Shape");

		private static MonsterLifeFxDriver instance;

		public static MonsterLifeFxDriver Instance
		{
			get
			{
				if (instance == null)
					instance = CombatFxRunner.Instance.gameObject.AddComponent<MonsterLifeFxDriver>();
				return instance;
			}
		}

		private struct Ghost
		{
			public int Id;
			public GameObject Object;
			public SpriteRenderer Renderer;
			public MonsterDeathStyle Style;
			public float Elapsed;
			public float Seconds;
			public Color Color;
			public Vector4 Flip;
			public Vector4 Bounds;
			public float BillboardMode;
			public float BodyHeight;
		}

		private struct Puff
		{
			public int Id;
			public GameObject Object;
			public MeshRenderer Renderer;
			public MonsterSpawnStyle Style;
			public float Elapsed;
			public float Radius;
		}

		private readonly List<Ghost> ghosts = new();
		private readonly List<Puff> puffs = new();
		private CombatFxRunner runner;
		private MaterialPropertyBlock properties;
		private Func<GameObject> createGhostObject;
		private Func<GameObject> createPuffObject;
		private Mesh puffMesh;
		private int lastId = NONE;

		public int GhostCount => ghosts.Count;
		public int PuffCount => puffs.Count;

		private void Awake()
		{
			runner = GetComponent<CombatFxRunner>();
			properties = new MaterialPropertyBlock();
			createGhostObject = CreateGhostObject;
			createPuffObject = CreatePuffObject;
		}

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
			if (puffMesh != null)
				Destroy(puffMesh);
		}

		private void Update()
		{
			float deltaTime = Time.deltaTime;

			// 뒤에서부터. 끝난 칸은 맨 뒤 것으로 메우고 줄임 (맨 뒤 것은 이미 갱신됨)
			for (int i = ghosts.Count - 1; i >= 0; i--)
			{
				Ghost ghost = ghosts[i];
				ghost.Elapsed += deltaTime;
				if (ghost.Elapsed >= ghost.Seconds)
				{
					ReleaseGhost(i);
					continue;
				}
				ghosts[i] = ghost;
				ApplyGhost(ghost);
			}

			for (int i = puffs.Count - 1; i >= 0; i--)
			{
				Puff puff = puffs[i];
				puff.Elapsed += deltaTime;
				if (puff.Elapsed >= puff.Style.PuffSeconds)
				{
					ReleasePuff(i);
					continue;
				}
				puffs[i] = puff;
				ApplyPuff(puff);
			}
		}

		/// <summary> 사망 잔상 하나 추가. seconds 가 0 이하면 스타일의 시간. 상한을 넘었거나 그림이 없으면 NONE </summary>
		public int AddGhost(in MonsterDeathGhostPose pose, MonsterDeathStyle style, float seconds)
		{
			if (pose.Sprite == null || ghosts.Count >= style.MaxGhosts)
				return NONE;

			GameObject rented = runner.Rent(GHOST_KEY, createGhostObject);
			rented.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
			rented.transform.localScale = pose.Scale;

			SpriteRenderer spriteRenderer = rented.GetComponent<SpriteRenderer>();
			spriteRenderer.sprite = pose.Sprite;
			spriteRenderer.sharedMaterial = style.Material;
			spriteRenderer.sortingLayerID = pose.SortingLayerId;
			spriteRenderer.sortingOrder = pose.SortingOrder;

			Bounds spriteBounds = pose.Sprite.bounds;
			Ghost ghost = new()
			{
				Id = ++lastId,
				Object = rented,
				Renderer = spriteRenderer,
				Style = style,
				Elapsed = 0f,
				Seconds = seconds > 0f ? seconds : style.Seconds,
				Color = pose.Color,
				Flip = new Vector4(pose.FlipX ? -1f : 1f, pose.FlipY ? -1f : 1f, 0f, 0f),
				Bounds = new Vector4(spriteBounds.min.y, spriteBounds.size.y, UnityEngine.Random.Range(0f, style.NoiseSeedRange), 0f),
				BillboardMode = pose.BillboardMode,
				BodyHeight = pose.Scale.y,
			};
			ghosts.Add(ghost);
			ApplyGhost(ghost);
			return ghost.Id;
		}

		/// <summary> 잔상을 도중에 치움. 이미 끝났거나 NONE 이면 아무 일 없음 </summary>
		public void StopGhost(int ghostId)
		{
			if (ghostId == NONE)
				return;
			for (int i = 0; i < ghosts.Count; i++)
			{
				if (ghosts[i].Id == ghostId)
				{
					ReleaseGhost(i);
					return;
				}
			}
		}

		/// <summary> 발밑 고리 하나 추가. 상한을 넘으면 NONE </summary>
		public int AddPuff(Vector3 groundPosition, MonsterSpawnStyle style, float sizeMultiplier)
		{
			if (puffs.Count >= style.MaxPuffs)
				return NONE;

			GameObject rented = runner.Rent(PUFF_KEY, createPuffObject);
			rented.transform.SetPositionAndRotation(groundPosition + Vector3.up * style.PuffGroundOffset, Quaternion.identity);

			MeshRenderer meshRenderer = rented.GetComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = style.PuffMaterial;

			Puff puff = new()
			{
				Id = ++lastId,
				Object = rented,
				Renderer = meshRenderer,
				Style = style,
				Elapsed = 0f,
				Radius = style.PuffRadius * sizeMultiplier,
			};
			puffs.Add(puff);
			ApplyPuff(puff);
			return puff.Id;
		}

		public void StopPuff(int puffId)
		{
			if (puffId == NONE)
				return;
			for (int i = 0; i < puffs.Count; i++)
			{
				if (puffs[i].Id == puffId)
				{
					ReleasePuff(i);
					return;
				}
			}
		}

		private void ApplyGhost(in Ghost ghost)
		{
			MonsterDeathStyle style = ghost.Style;
			float progress = ghost.Elapsed / ghost.Seconds;

			Color tint = ghost.Color;
			tint.a *= style.AlphaCurve.Evaluate(progress);
			Color flash = style.FlashColor;
			flash.a *= style.FlashCurve.Evaluate(progress);
			float rise = style.RiseHeight * ghost.BodyHeight * style.RiseCurve.Evaluate(progress);

			// 스프라이트 렌더러가 넣어 둔 값 (그림 텍스처) 을 지우지 않게 받아서 덧씀
			ghost.Renderer.GetPropertyBlock(properties);
			properties.SetColor(TINT_ID, tint);
			properties.SetVector(FLIP_ID, ghost.Flip);
			properties.SetVector(DEFORM_ID, new Vector4(style.WidthCurve.Evaluate(progress), style.HeightCurve.Evaluate(progress), rise, 0f));
			properties.SetVector(DISSOLVE_ID, new Vector4(style.DissolveCurve.Evaluate(progress), style.EdgeWidth, style.NoiseScale, style.VerticalBias));
			properties.SetColor(EDGE_COLOR_ID, style.EdgeColor);
			properties.SetColor(FLASH_COLOR_ID, flash);
			properties.SetVector(BOUNDS_ID, ghost.Bounds);
			properties.SetFloat(BILLBOARD_MODE_ID, ghost.BillboardMode);
			ghost.Renderer.SetPropertyBlock(properties);
		}

		private void ApplyPuff(in Puff puff)
		{
			MonsterSpawnStyle style = puff.Style;
			float progress = puff.Elapsed / style.PuffSeconds;

			puff.Object.transform.localScale = Vector3.one * (puff.Radius * style.PuffRadiusCurve.Evaluate(progress));

			Color color = style.PuffColor;
			color.a *= style.PuffAlphaCurve.Evaluate(progress);

			properties.Clear();
			properties.SetColor(COLOR_ID, color);
			properties.SetVector(SHAPE_ID, new Vector4(style.PuffHollowCurve.Evaluate(progress), style.PuffSoftness, 0f, 0f));
			puff.Renderer.SetPropertyBlock(properties);
		}

		private void ReleaseGhost(int index)
		{
			runner.Return(GHOST_KEY, ghosts[index].Object);
			int last = ghosts.Count - 1;
			ghosts[index] = ghosts[last];
			ghosts.RemoveAt(last);
		}

		private void ReleasePuff(int index)
		{
			runner.Return(PUFF_KEY, puffs[index].Object);
			int last = puffs.Count - 1;
			puffs[index] = puffs[last];
			puffs.RemoveAt(last);
		}

		private static GameObject CreateGhostObject()
		{
			GameObject ghostObject = new(GHOST_KEY);
			SpriteRenderer spriteRenderer = ghostObject.AddComponent<SpriteRenderer>();
			spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
			spriteRenderer.receiveShadows = false;
			return ghostObject;
		}

		private GameObject CreatePuffObject()
		{
			if (puffMesh == null)
				puffMesh = BuildGroundQuad();

			GameObject puffObject = new(PUFF_KEY);
			puffObject.AddComponent<MeshFilter>().sharedMesh = puffMesh;
			MeshRenderer meshRenderer = puffObject.AddComponent<MeshRenderer>();
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			return puffObject;
		}

		// 바닥에 누운 한 변 2 짜리 판. 크기 배율이 곧 반지름
		private static Mesh BuildGroundQuad()
		{
			Mesh mesh = new()
			{
				name = PUFF_KEY,
				vertices = new Vector3[] { new(-1f, 0f, -1f), new(-1f, 0f, 1f), new(1f, 0f, 1f), new(1f, 0f, -1f) },
				uv = new Vector2[] { new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f) },
				triangles = new int[] { 0, 1, 2, 0, 2, 3 },
			};
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
