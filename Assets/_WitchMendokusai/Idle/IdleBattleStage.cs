using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 쿼터뷰 자동전투 무대 (V2, concept-v2 — 블아 문법).
	///
	/// ★ 이 파일에 게임 규칙이 한 줄도 없다 — 사진(<see cref="IdleSnapshot"/>)을 받아
	///   3D 로 그리고, 방금 일어난 일(처치·카드)을 몸짓으로 옮길 뿐이다.
	///
	/// ★ <b>나아가는 판</b>이다 (사용자 방향 2026-08-23: 「맵이 길어서 나아가는 식」).
	///   부대·카메라는 그대로 두고 <b>세계(worldRoot)가 왼쪽으로 흐른다</b> — 잡을 때마다
	///   한 걸음, 다음 적이 앞자리로 온다. 길가의 바위·풀은 지나가면 앞으로 재활용된다.
	///
	/// ★ <b>웨이브 문법</b> (사용자 방향, 블아·울티마) — 한 구역 = 잡몹 줄 + <b>마지막 하나는 보스</b>.
	///   코어의 「구역당 처치 수」가 그대로 웨이브다: 앞으로 올 적들이 줄 서 보이고,
	///   구역의 막내(<see cref="IdleTuning.KillsPerStage"/>번째)가 크고 어둡게 선다.
	///
	/// ★ 배우는 전부 <b>절차 생성 도형</b>이다 — 인형 = 캡슐+큰 머리(치비), 적 = 변 수=등급 N각기둥.
	/// </summary>
	public sealed class IdleBattleStage : MonoBehaviour
	{
		[Header("자리 — 쿼터뷰 배치. 0번 = 나(항상), 1~3번 = 가챠로 뽑아 앉힌 자리")]
		[SerializeField] private Vector3[] dollSpots =
		{
			new Vector3(-2.1f, 0f, 0.1f),
			new Vector3(-3.0f, 0f, -1.2f),
			new Vector3(-3.4f, 0f, 0.3f),
			new Vector3(-2.9f, 0f, 1.5f),
		};

		[Tooltip("맨 앞 적이 서는 자리.")]
		[SerializeField] private Vector3 frontSpot = new Vector3(2.4f, 0.62f, 0f);

		[Header("행군 — 잡을 때마다 한 걸음")]
		[Tooltip("적과 적 사이 거리 (m) — 한 처치가 이만큼 나아간다.")]
		[SerializeField] private float enemySpacing = 3.2f;

		[Tooltip("한 걸음을 따라잡는 속도 — 클수록 휙휙 나아간다.")]
		[SerializeField] private float marchCatchUp = 4.5f;

		[Tooltip("한 번에 줄 서 보이는 적 수 (맨 앞 포함).")]
		[SerializeField] private int enemiesVisible = 4;

		[Header("장단 — 눈으로 셀 수 있는 데까지만")]
		[SerializeField] private float fastestVisibleBeats = 8f;
		[SerializeField] private float lungeSeconds = 0.22f;
		[SerializeField] private float boltSeconds = 0.18f;
		[SerializeField] private float popSeconds = 0.3f;

		[Header("보스 — 구역의 막내")]
		[Tooltip("보스의 덩치 배수.")]
		[SerializeField] private float bossScale = 1.7f;

		[Header("색 — 세계관 전이라 도형 팔레트")]
		[SerializeField] private Color groundColor = new Color(0.62f, 0.73f, 0.55f);
		[SerializeField] private Color coverColor = new Color(0.52f, 0.58f, 0.64f);
		[SerializeField] private Color enemyColor = new Color(0.55f, 0.50f, 0.63f);
		[SerializeField] private Color bossColor = new Color(0.30f, 0.26f, 0.38f);
		[SerializeField] private Color sceneryColor = new Color(0.50f, 0.60f, 0.46f);
		[SerializeField] private Color boltColor = new Color(1f, 0.83f, 0.29f);

		[Tooltip("0번 = 나의 색. 영웅 자리는 등급색으로 덧입는다.")]
		[SerializeField] private Color myColor = new Color(0.93f, 0.89f, 0.82f);

		[Header("영웅 등급색 — 일반·레어·에픽·레전드")]
		[SerializeField] private Color[] gradeColors =
		{
			new Color(0.68f, 0.72f, 0.80f),
			new Color(0.46f, 0.80f, 0.72f),
			new Color(0.72f, 0.58f, 0.92f),
			new Color(0.95f, 0.72f, 0.36f),
		};

		private sealed class Foe
		{
			public Transform Piece;
			public MeshFilter Mesh;
			public Material Skin;
			public long Index;
			public int Sides;
			public bool Boss;
		}

		private readonly List<Foe> foes = new List<Foe>();
		private readonly List<Transform> scenery = new List<Transform>();
		private readonly List<Transform> bolts = new List<Transform>();
		private readonly List<float> boltAges = new List<float>();
		private readonly List<Vector3> boltFrom = new List<Vector3>();

		private Transform worldRoot;
		private Transform[] dolls;
		private Material[] dollSkins;
		private float[] lungeLeft;
		private Material groundMaterial;
		private Color groundRest;

		private float beat;
		private int turn;
		private long lastKills = -1L;
		private float popLeft;
		private float supplyGlowLeft;
		private float marchOffset;

		private bool built;

		/// <summary>무대를 세운다 — 멱등. 화면이 깨어날 때 한 번 부른다.</summary>
		public void Build()
		{
			if (built)
			{
				return;
			}

			built = true;

			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(transform, false);
			// 땅은 넉넉히 — 모서리가 화면에 들면 「세계」가 아니라 「판때기」로 보인다.
			ground.transform.localScale = new Vector3(6f, 1f, 4f);
			groundMaterial = Paint(ground, groundColor);
			groundRest = groundColor;

			// 흐르는 세계 — 적 줄과 길가 소품이 여기 담긴다.
			GameObject world = new GameObject("World");
			world.transform.SetParent(transform, false);
			worldRoot = world.transform;

			dolls = new Transform[dollSpots.Length];
			dollSkins = new Material[dollSpots.Length];
			lungeLeft = new float[dollSpots.Length];

			for (int slot = 0; slot < dollSpots.Length; slot++)
			{
				dolls[slot] = BuildDoll(slot);

				// ★ 처음부터 서 있는 것은 <b>나 하나</b>다 (사용자 방향 2026-08-23) —
				//   영웅은 가챠로 뽑아 자리에 앉혀야 무대에 선다.
				dolls[slot].gameObject.SetActive(slot == 0);

				GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
				cover.name = "Cover" + slot;
				cover.transform.SetParent(transform, false);
				cover.transform.localPosition = dollSpots[slot] + new Vector3(1.4f, 0.22f, 0f);
				cover.transform.localScale = new Vector3(0.3f, 0.44f, 0.7f);
				Paint(cover, coverColor);
			}

			// 길가 소품 — 지나가면 앞으로 돌려 쓴다 (나아가는 게 땅으로 보이는 장치).
			for (int at = 0; at < 14; at++)
			{
				GameObject prop = GameObject.CreatePrimitive(at % 3 == 0
					? PrimitiveType.Cylinder : PrimitiveType.Cube);
				prop.name = "Scenery" + at;
				prop.transform.SetParent(worldRoot, false);

				// 무작위 없이 흩는다 — 결정적이어야 같은 판이 같은 길로 보인다.
				float side = at % 2 == 0 ? 1f : -1f;
				float size = 0.2f + 0.12f * (at % 4);
				prop.transform.localPosition = new Vector3(
					at * 3.1f - 6f,
					size * 0.5f,
					side * (3.4f + 0.9f * (at % 3)));
				prop.transform.localScale = new Vector3(size, size, size);
				prop.transform.localRotation = Quaternion.Euler(0f, at * 37f, 0f);
				Paint(prop, sceneryColor);
				scenery.Add(prop.transform);
			}
		}

		private Transform BuildDoll(int slot)
		{
			GameObject doll = new GameObject("Doll" + slot);
			doll.transform.SetParent(transform, false);
			doll.transform.localPosition = dollSpots[slot];
			doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);

			// 몸과 머리가 한 껍질을 나눠 입는다 — 등급색을 갈아입힐 때 한 번에.
			Material skin = MakeMaterial(slot == 0 ? myColor : gradeColors[0]);
			dollSkins[slot] = skin;

			GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			body.transform.SetParent(doll.transform, false);
			body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
			body.transform.localScale = new Vector3(0.42f, 0.35f, 0.42f);
			body.GetComponent<MeshRenderer>().sharedMaterial = skin;

			GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			head.transform.SetParent(doll.transform, false);
			head.transform.localPosition = new Vector3(0f, 0.95f, 0f);
			head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
			head.GetComponent<MeshRenderer>().sharedMaterial = skin;

			return doll.transform;
		}

		/// <summary>
		/// 파티를 무대에 맞춘다 — 앉힌 영웅만 서고, 등급색을 입는다.
		/// 판정은 사진(<see cref="IdleSnapshot.Party"/>)이 전부다.
		/// </summary>
		private void DressDolls(IdleSnapshot snapshot)
		{
			for (int seat = 0; seat < snapshot.Party.Length && seat + 1 < dolls.Length; seat++)
			{
				int id = snapshot.Party[seat];
				bool seated = id >= 0;

				if (dolls[seat + 1].gameObject.activeSelf != seated)
				{
					dolls[seat + 1].gameObject.SetActive(seated);
				}

				if (seated == false)
				{
					continue;
				}

				foreach (IdleHeroView hero in snapshot.Heroes)
				{
					if (hero.Id == id)
					{
						int grade = (int)hero.Grade;
						dollSkins[seat + 1].color = gradeColors[Mathf.Clamp(grade, 0, gradeColors.Length - 1)];
						break;
					}
				}
			}
		}

		/// <summary>한 프레임 그린다 — 장단은 코어의 실제 공격속도에서 온다.</summary>
		public void Render(IdleSnapshot snapshot, float delta)
		{
			if (built == false)
			{
				return;
			}

			if (lastKills < 0L)
			{
				lastKills = snapshot.Kills;
			}

			DressDolls(snapshot);
			DressFoes(snapshot);

			float beatsPerSecond = Mathf.Min(fastestVisibleBeats, (float)snapshot.AttacksPerSecond);
			beat += delta * beatsPerSecond;

			if (beat >= 1f)
			{
				beat -= 1f;
				if (beat > 1f)
				{
					beat = 0f;
				}

				Strike(snapshot);
			}

			if (snapshot.Kills > lastKills)
			{
				lastKills = snapshot.Kills;
				popLeft = popSeconds;
			}

			March(snapshot, delta);
			AdvanceBodies(delta);
		}

		/// <summary>
		/// 적 줄을 사진에 맞춘다 — 처치 번호(<see cref="IdleSnapshot.Kills"/>)가 곧 줄 번호다.
		/// 잡힌 적은 창을 벗어나고, 새 적이 줄 끝에 선다.
		/// </summary>
		private void DressFoes(IdleSnapshot snapshot)
		{
			long first = snapshot.Kills;

			// 잡힌 것(줄 앞을 벗어난 것)을 거둔다.
			for (int at = foes.Count - 1; at >= 0; at--)
			{
				if (foes[at].Index < first)
				{
					Destroy(foes[at].Piece.gameObject);
					foes.RemoveAt(at);
				}
			}

			// 줄을 채운다 — 맨 앞부터 enemiesVisible 마리.
			for (long index = first; index < first + enemiesVisible; index++)
			{
				if (Has(index) == false)
				{
					foes.Add(MakeFoe(index));
				}
			}

			foreach (Foe foe in foes)
			{
				// 이 적이 구역의 몇째인가 — 막내면 보스.
				long aheadOfNow = foe.Index - first;
				int placeInStage = (int)((snapshot.KillsInStage + aheadOfNow) % snapshot.KillsPerStage);
				bool boss = snapshot.KillsPerStage > 0 && placeInStage == snapshot.KillsPerStage - 1;

				int sides = Mathf.Max(3, snapshot.MaxTierNow + 2);

				if (foe.Sides != sides)
				{
					foe.Sides = sides;
					foe.Mesh.sharedMesh = NgonPrism(sides, 0.75f, 1.05f);
				}

				if (foe.Boss != boss)
				{
					foe.Boss = boss;
					foe.Skin.color = boss ? bossColor : enemyColor;
				}

				// 세계 좌표: 처치 순번대로 한 칸씩 뒤에 — 세계가 흐르면 앞자리로 온다.
				foe.Piece.localPosition = new Vector3(foe.Index * enemySpacing, frontSpot.y, frontSpot.z);

				bool atFront = foe.Index == first;
				float health = atFront ? 0.82f + 0.18f * (float)snapshot.TargetHealthRatio : 1f;
				float pop = atFront && popLeft > 0f ? 1f + 0.35f * (popLeft / popSeconds) : 1f;
				float bulk = boss ? bossScale : 1f;
				float waiting = atFront ? 1f : 0.82f;
				foe.Piece.localScale = new Vector3(health * pop * bulk * waiting, pop * bulk * waiting, health * pop * bulk * waiting);
			}
		}

		private bool Has(long index)
		{
			foreach (Foe foe in foes)
			{
				if (foe.Index == index)
				{
					return true;
				}
			}

			return false;
		}

		private Foe MakeFoe(long index)
		{
			GameObject piece = new GameObject("Foe" + index);
			piece.transform.SetParent(worldRoot, false);

			Foe foe = new Foe();
			foe.Piece = piece.transform;
			foe.Mesh = piece.AddComponent<MeshFilter>();
			MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
			foe.Skin = MakeMaterial(enemyColor);
			renderer.sharedMaterial = foe.Skin;
			foe.Index = index;
			foe.Sides = -1;
			return foe;
		}

		/// <summary>
		/// 세계가 흐른다 — 맨 앞 적이 앞자리에 오도록 따라잡는다. 소품은 지나가면 재활용.
		/// </summary>
		private void March(IdleSnapshot snapshot, float delta)
		{
			float wanted = frontSpot.x - snapshot.Kills * enemySpacing;
			marchOffset = Mathf.Lerp(marchOffset, wanted, Mathf.Min(1f, delta * marchCatchUp));
			worldRoot.localPosition = new Vector3(marchOffset, 0f, 0f);

			float span = scenery.Count * 3.1f;
			foreach (Transform prop in scenery)
			{
				// 카메라 뒤로 멀어진 소품은 길 앞으로 — 끝없는 길.
				while (prop.localPosition.x + marchOffset < -12f)
				{
					prop.localPosition += new Vector3(span, 0f, 0f);
				}
			}
		}

		private void Strike(IdleSnapshot snapshot)
		{
			int who = NextFighter(snapshot);
			lungeLeft[who] = lungeSeconds;
			SpawnBolt(dolls[who].position + new Vector3(0.3f, 0.9f, 0f));
		}

		/// <summary>서 있는 인형 중에서 차례를 돌린다 — 나(0번)는 늘 서 있다.</summary>
		private int NextFighter(IdleSnapshot snapshot)
		{
			for (int tried = 0; tried < dolls.Length; tried++)
			{
				int slot = turn % dolls.Length;
				turn++;

				if (slot == 0 || (slot - 1 < snapshot.Party.Length && snapshot.Party[slot - 1] >= 0))
				{
					return slot;
				}
			}

			return 0;
		}

		private void SpawnBolt(Vector3 from)
		{
			GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			bolt.name = "Bolt";
			bolt.transform.SetParent(transform, false);
			bolt.transform.position = from;
			bolt.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
			Paint(bolt, boltColor);

			bolts.Add(bolt.transform);
			boltAges.Add(0f);
			boltFrom.Add(from);
		}

		private void AdvanceBodies(float delta)
		{
			// 행군 중인가 — 따라잡을 거리가 남아 있으면 걷는 몸짓.
			bool marching = Mathf.Abs(worldRoot.localPosition.x
				- (frontSpot.x - lastKills * enemySpacing)) > 0.05f;

			for (int slot = 0; slot < dolls.Length; slot++)
			{
				if (lungeLeft[slot] > 0f)
				{
					lungeLeft[slot] -= delta;
				}

				float swing = lungeLeft[slot] > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - lungeLeft[slot] / lungeSeconds) * Mathf.PI)
					: 0f;

				// 걷기 — 위아래로 총총. 자리는 그대로, 세계가 흐른다.
				float bob = marching
					? Mathf.Abs(Mathf.Sin(Time.time * 7f + slot * 1.3f)) * 0.12f
					: 0f;

				dolls[slot].localPosition = dollSpots[slot] + new Vector3(swing * 0.5f, bob, 0f);
			}

			for (int at = bolts.Count - 1; at >= 0; at--)
			{
				boltAges[at] += delta;
				float gone = Mathf.Clamp01(boltAges[at] / boltSeconds);
				Vector3 target = transform.TransformPoint(new Vector3(frontSpot.x, frontSpot.y + 0.1f, frontSpot.z));
				bolts[at].position = Vector3.Lerp(boltFrom[at], target, gone);

				if (gone >= 1f)
				{
					Destroy(bolts[at].gameObject);
					bolts.RemoveAt(at);
					boltAges.RemoveAt(at);
					boltFrom.RemoveAt(at);
				}
			}

			if (popLeft > 0f)
			{
				popLeft -= delta;
			}

			if (supplyGlowLeft > 0f)
			{
				supplyGlowLeft -= delta;
				float glow = Mathf.Clamp01(supplyGlowLeft);
				groundMaterial.color = Color.Lerp(groundRest, boltColor, glow * 0.35f);
			}
		}

		/// <summary>일제 사격 — 셋이 다 달려들고 알갱이가 쏟아진다.</summary>
		public void OnVolley()
		{
			if (built == false)
			{
				return;
			}

			for (int slot = 0; slot < dolls.Length; slot++)
			{
				if (dolls[slot].gameObject.activeSelf == false)
				{
					continue;
				}

				lungeLeft[slot] = lungeSeconds;
				SpawnBolt(dolls[slot].position + new Vector3(0.3f, 0.9f, 0f));
				SpawnBolt(dolls[slot].position + new Vector3(0.1f, 1.1f, 0.1f));
			}

			popLeft = popSeconds;
		}

		/// <summary>긴급 보급 — 땅이 잠시 금빛으로.</summary>
		public void OnSupply(float seconds)
		{
			supplyGlowLeft = seconds;
		}

		/// <summary>손 응원 — 다음 대가 바로 나가게 장단을 당긴다.</summary>
		public void OnTap()
		{
			beat = 1f;
		}

		private static Material Paint(GameObject piece, Color color)
		{
			Material made = MakeMaterial(color);
			piece.GetComponent<MeshRenderer>().sharedMaterial = made;
			return made;
		}

		private static Material MakeMaterial(Color color)
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null)
			{
				shader = Shader.Find("Standard");
			}

			Material made = new Material(shader);
			made.color = color;
			return made;
		}

		/// <summary>N각기둥 — 변의 수 = 등급 규칙의 3D 형태.</summary>
		private static Mesh NgonPrism(int sides, float radius, float height)
		{
			Mesh mesh = new Mesh();
			mesh.name = "Ngon" + sides;

			Vector3[] ring = new Vector3[sides];
			for (int at = 0; at < sides; at++)
			{
				float angle = at * Mathf.PI * 2f / sides;
				ring[at] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			}

			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();
			float half = height * 0.5f;

			for (int lid = 0; lid < 2; lid++)
			{
				float y = lid == 0 ? half : -half;
				int center = vertices.Count;
				vertices.Add(new Vector3(0f, y, 0f));

				int start = vertices.Count;
				for (int at = 0; at < sides; at++)
				{
					vertices.Add(ring[at] + new Vector3(0f, y, 0f));
				}

				for (int at = 0; at < sides; at++)
				{
					int next = (at + 1) % sides;
					if (lid == 0)
					{
						triangles.Add(center); triangles.Add(start + next); triangles.Add(start + at);
					}
					else
					{
						triangles.Add(center); triangles.Add(start + at); triangles.Add(start + next);
					}
				}
			}

			for (int at = 0; at < sides; at++)
			{
				int next = (at + 1) % sides;
				int start = vertices.Count;
				vertices.Add(ring[at] + new Vector3(0f, half, 0f));
				vertices.Add(ring[next] + new Vector3(0f, half, 0f));
				vertices.Add(ring[next] + new Vector3(0f, -half, 0f));
				vertices.Add(ring[at] + new Vector3(0f, -half, 0f));

				triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
				triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
