using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>Idle 전투 스냅샷과 표시 계층을 조율한다.</summary>
	[ExecuteAlways]
	public sealed class IdleBattleStage : MonoBehaviour
	{
		[SerializeField] private GameObject dollPrefab;
		[SerializeField] private GameObject foePrefab;
		[SerializeField] private GameObject bossPrefab;
		[SerializeField] private GameObject groundPrefab;
		[SerializeField] private float partyAnchorX = -2.5f;
		[SerializeField] private float followCatchUp = 4f;
		[SerializeField] private float snapJump = 3f;
		[SerializeField] private float lungeSeconds = 0.22f;
		[SerializeField] private float boltSeconds = 0.18f;
		[SerializeField] private float popSeconds = 0.3f;
		[SerializeField] private float positionCatchUp = 14f;
		[SerializeField] private float foeSpinDegrees = 12f;
		[SerializeField] private float foeBobHeight = 0.025f;
		[SerializeField] private float shakeSeconds = 0.08f;
		[SerializeField] private float shakeDistance = 0.025f;
		[SerializeField] private float bossScale = 1.35f;
		[SerializeField] private float foeHeight = 0.62f;

		[Header("적 도형 (visual.md 2). 구역이 깊을수록 면이 는다")]
		[Tooltip("적 반지름 (m)")]
		[SerializeField] private float foeRadius = 0.62f;

		[Tooltip("몇 구역마다 도형이 한 계단 오르나. 정사면체, 정육면체, 정팔면체, 정12면체, 정20면체, 세분 구, 잔 세분 구")]
		[SerializeField] private int shapeStagesPerStep = 5;

		[Tooltip("배경 소품 수")]
		[SerializeField] private int sceneryCount = 14;

		[Header("보스 (visual.md 4). 코어와 껍질 조각")]
		[Tooltip("껍질 조각 수")]
		[SerializeField] private int bossShardCount = 6;

		[Tooltip("껍질이 코어에서 떨어진 거리. 반지름 배수")]
		[SerializeField] private float bossShellRadius = 1.15f;

		[Tooltip("체력이 빌수록 이만큼 더 벌어진다")]
		[SerializeField] private float bossShellSpread = 0.6f;

		[Tooltip("껍질 회전 속도. 코어와 반대라 음수")]
		[SerializeField] private float bossShellSpinDegrees = -26f;

		[Tooltip("이 구역부터 보스 코어에 뿔을 세운다 (별 만들기)")]
		[SerializeField] private int bossSpikeFromStage = 21;

		[Header("색 (visual.md 6). 구역이 깊을수록 짙어진다")]
		[Tooltip("이 구역에서 채도가 최대")]
		[SerializeField] private int colorDepthStage = 40;

		[Tooltip("깊이가 채도에 더하는 몫")]
		[SerializeField] private float depthSaturation = 0.35f;

		[Tooltip("깊이가 밝기에서 빼는 몫")]
		[SerializeField] private float depthDarken = 0.18f;

		[Tooltip("보스 발광 세기. 0 이면 안 빛난다")]
		[SerializeField] private float bossGlow = 0.55f;

		[Tooltip("근접이 뜨는 높이 배수. 낮게")]
		[SerializeField] private float meleeHeightShare = 0.85f;

		[Tooltip("원거리가 뜨는 높이 배수. 높게")]
		[SerializeField] private float rangedHeightShare = 1.3f;
		[SerializeField] private Color groundColor = new Color(0.62f, 0.73f, 0.55f);
		[SerializeField] private Color enemyColor = new Color(0.55f, 0.50f, 0.63f);
		[SerializeField] private Color rangedEnemyColor = new Color(0.66f, 0.54f, 0.50f);
		[SerializeField] private Color bossColor = new Color(0.30f, 0.26f, 0.38f);
		[SerializeField] private Color sceneryColor = new Color(0.50f, 0.60f, 0.46f);
		[SerializeField] private Color boltColor = new Color(1f, 0.83f, 0.29f);
		[SerializeField] private Color myColor = new Color(0.93f, 0.89f, 0.82f);
		[SerializeField] private Color barBackColor = new Color(0.12f, 0.13f, 0.16f, 1f);
		[SerializeField] private Color allyBarColor = new Color(0.36f, 0.78f, 0.44f);
		[SerializeField] private Color reviveBarColor = new Color(0.62f, 0.66f, 0.74f);
		[SerializeField] private Color enemyBarColor = new Color(0.91f, 0.36f, 0.36f);
		[SerializeField] private Color[] gradeColors = { new Color(0.68f, 0.72f, 0.80f), new Color(0.46f, 0.80f, 0.72f), new Color(0.72f, 0.58f, 0.92f), new Color(0.95f, 0.72f, 0.36f) };
		[SerializeField] private float numberSeconds = 0.8f;
		[SerializeField] private float numberRise = 1.2f;
		[SerializeField] private float numberSize = 0.12f;
		[SerializeField] private Color numberColor = Color.white;
		[SerializeField] private Color hurtColor = new Color(1f, 0.45f, 0.4f);

		private readonly List<Transform> scenery = new List<Transform>();
		private readonly List<MeshFilter> sceneryMeshes = new List<MeshFilter>();
		private IdleGeometry.Shape sceneryShape = (IdleGeometry.Shape)(-1);
		private Transform holder;
		private Transform worldRoot;
		private Material groundMaterial;
		private Color groundRest;
		private IdleBattleEntityPresenter entities;
		private IdleBattleFx fx;
		private float scroll;
		private bool scrollReady;
		private float supplyGlowLeft;
		private bool built;

		public void Build()
		{
			if (built) { return; }
			built = true;
			ClearPreview();
			GameObject root = new GameObject("Preview");
			root.hideFlags = HideFlags.DontSave;
			root.transform.SetParent(transform, false);
			holder = root.transform;
			BuildGround();
			GameObject world = new GameObject("World");
			world.transform.SetParent(holder, false);
			worldRoot = world.transform;
			entities = new IdleBattleEntityPresenter(worldRoot, CreateEntitySettings());
			fx = new IdleBattleFx(holder, new IdleBattleFx.Settings
			{
				BoltSeconds = boltSeconds,
				ShakeSeconds = shakeSeconds,
				ShakeDistance = shakeDistance,
				NumberSeconds = numberSeconds,
				NumberRise = numberRise,
				NumberSize = numberSize,
				BoltColor = boltColor,
				NumberColor = numberColor,
				HurtColor = hurtColor,
			});
			BuildScenery();
		}

		public void Render(IdleSnapshot snapshot, float delta)
		{
			if (built == false) { return; }
			ReshapeScenery(IdleGeometry.ShapeOfStage(snapshot.Stage, shapeStagesPerStep));
			Follow(snapshot, delta);
			entities.Render(snapshot, delta);
			fx.Consume(snapshot.Hits, entities);
			fx.Advance(delta, entities);
			AdvanceSupply(delta);
		}

		public void OnVolley() { }

		public bool TryPickFoe(Vector2 panelPosition, out long foeIndex)
		{
			if (entities == null)
			{
				foeIndex = -1L;
				return false;
			}

			return entities.TryPickFoe(panelPosition, out foeIndex);
		}

		public void OnSupply(float seconds) { supplyGlowLeft = seconds; }
		public void OnTap() { }

		private IdleBattleEntityPresenter.Settings CreateEntitySettings()
		{
			return new IdleBattleEntityPresenter.Settings
			{
				DollPrefab = dollPrefab,
				FoePrefab = foePrefab,
				BossPrefab = bossPrefab,
				LungeSeconds = lungeSeconds,
				PopSeconds = popSeconds,
				PositionCatchUp = positionCatchUp,
				FoeSpinDegrees = foeSpinDegrees,
				FoeBobHeight = foeBobHeight,
				BossScale = bossScale,
				FoeHeight = foeHeight,
				FoeRadius = foeRadius,
				ShapeStagesPerStep = shapeStagesPerStep,
				BossShardCount = bossShardCount,
				BossShellRadius = bossShellRadius,
				BossShellSpread = bossShellSpread,
				BossShellSpinDegrees = bossShellSpinDegrees,
				BossSpikeFromStage = bossSpikeFromStage,
				ColorDepthStage = colorDepthStage,
				DepthSaturation = depthSaturation,
				DepthDarken = depthDarken,
				BossGlow = bossGlow,
				MeleeHeightShare = meleeHeightShare,
				RangedHeightShare = rangedHeightShare,
				MyColor = myColor,
				EnemyColor = enemyColor,
				RangedEnemyColor = rangedEnemyColor,
				BossColor = bossColor,
				BarBackColor = barBackColor,
				AllyBarColor = allyBarColor,
				ReviveBarColor = reviveBarColor,
				EnemyBarColor = enemyBarColor,
				GradeColors = gradeColors,
			};
		}

		private void BuildGround()
		{
			if (groundPrefab != null)
			{
				GameObject made = Instantiate(groundPrefab, holder, false);
				made.name = "Ground";
				MeshRenderer floor = made.GetComponentInChildren<MeshRenderer>();
				groundMaterial = floor != null ? floor.sharedMaterial : IdleBattleVisualFactory.MakeMaterial(groundColor);
				groundRest = groundMaterial.color;
				return;
			}
			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(holder, false);
			ground.transform.localScale = new Vector3(6f, 1f, 4f);
			groundMaterial = IdleBattleVisualFactory.Paint(ground, groundColor);
			groundRest = groundColor;
		}

		/// <summary>
		/// 배경 소품. 적과 같은 기하 언어를 쓰되 저채도 (visual.md 6)
		///
		/// ★ 구역이 바뀌면 소품 도형도 따라감. 세계가 달라진 것이 배경에서 먼저 읽힘
		/// </summary>
		private void BuildScenery()
		{
			if (groundPrefab != null)
			{
				return;
			}

			for (int at = 0; at < sceneryCount; at++)
			{
				GameObject prop = new GameObject("Scenery" + at);
				prop.transform.SetParent(worldRoot, false);

				MeshFilter mesh = prop.AddComponent<MeshFilter>();
				MeshRenderer renderer = prop.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = IdleBattleVisualFactory.MakeMaterial(sceneryColor);

				float side = at % 2 == 0 ? 1f : -1f;
				float size = 0.2f + 0.12f * (at % 4);
				prop.transform.localPosition = new Vector3(at * 3.1f - 6f, size * 0.5f, side * (3.4f + 0.9f * (at % 3)));
				prop.transform.localScale = new Vector3(size, size, size);
				prop.transform.localRotation = Quaternion.Euler(at * 23f, at * 37f, at * 13f);

				sceneryMeshes.Add(mesh);
				scenery.Add(prop.transform);
			}

			ReshapeScenery(IdleGeometry.Shape.Tetrahedron);
		}

		/// <summary>소품 도형을 구역에 맞춤. 같은 도형이면 그대로</summary>
		private void ReshapeScenery(IdleGeometry.Shape shape)
		{
			if (sceneryShape == shape && sceneryMeshes.Count > 0 && sceneryMeshes[0].sharedMesh != null)
			{
				return;
			}

			sceneryShape = shape;
			Mesh made = IdleGeometry.Build(shape, 1f);

			foreach (MeshFilter mesh in sceneryMeshes)
			{
				mesh.sharedMesh = made;
			}
		}

		private void Follow(IdleSnapshot snapshot, float delta)
		{
			float sum = 0f;
			int count = 0;
			for (int seat = 0; seat < snapshot.Fighters.Length && seat < snapshot.Seats.Length; seat++)
			{
				if (snapshot.Seats[seat].Taken)
				{
					sum += (float)snapshot.Fighters[seat].X;
					count++;
				}
			}
			float wanted = partyAnchorX - (count > 0 ? sum / count : 0f);
			if (scrollReady == false || Mathf.Abs(wanted - scroll) > snapJump)
			{
				scroll = wanted;
				scrollReady = true;
			}
			else { scroll = Mathf.Lerp(scroll, wanted, IdleBattleMotion.CatchUp(followCatchUp, delta)); }
			worldRoot.localPosition = new Vector3(scroll, 0f, 0f);
			float span = scenery.Count * 3.1f;
			foreach (Transform prop in scenery)
			{
				while (prop.localPosition.x + scroll < -12f) { prop.localPosition += new Vector3(span, 0f, 0f); }
				while (prop.localPosition.x + scroll > span - 12f) { prop.localPosition -= new Vector3(span, 0f, 0f); }
			}
		}

		private void AdvanceSupply(float delta)
		{
			if (supplyGlowLeft <= 0f) { return; }
			supplyGlowLeft -= delta;
			groundMaterial.color = Color.Lerp(groundRest, boltColor, Mathf.Clamp01(supplyGlowLeft) * 0.35f);
		}

		private void ClearPreview()
		{
			for (int at = transform.childCount - 1; at >= 0; at--)
			{
				Transform child = transform.GetChild(at);
				if (child.name == "Preview") { Kill(child.gameObject); }
			}
		}

		private void OnDisable()
		{
			if (holder != null) { Kill(holder.gameObject); }
			holder = null;
			worldRoot = null;
			entities = null;
			fx = null;
			scenery.Clear();
			sceneryMeshes.Clear();
			sceneryShape = (IdleGeometry.Shape)(-1);
			scrollReady = false;
			built = false;
		}

		private static void Kill(GameObject piece)
		{
			if (Application.isPlaying) { Destroy(piece); }
			else { DestroyImmediate(piece); }
		}
	}
}
