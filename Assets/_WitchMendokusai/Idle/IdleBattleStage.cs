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
	///   판정은 전부 코어에 있으므로 이 무대를 통째로 갈아도 게임은 같다.
	///
	/// ★ 배우는 전부 <b>절차 생성 도형</b>이다 (사용자 결정 2026-08-23: 3D 치비 도형).
	///   인형 = 캡슐 몸 + 큰 머리(치비 비율). 적 = <b>변 수 = 등급</b> 규칙 그대로의 N각기둥.
	/// </summary>
	public sealed class IdleBattleStage : MonoBehaviour
	{
		[Header("자리 — 쿼터뷰 배치")]
		[SerializeField] private Vector3[] dollSpots =
		{
			new Vector3(-2.6f, 0f, -1.2f),
			new Vector3(-3.2f, 0f, 0.1f),
			new Vector3(-2.5f, 0f, 1.4f),
		};

		[SerializeField] private Vector3 enemySpot = new Vector3(2.6f, 0.62f, 0f);

		[Header("장단 — 눈으로 셀 수 있는 데까지만")]
		[Tooltip("이보다 빠른 타격은 사람 눈엔 «계속»이라 안 센다.")]
		[SerializeField] private float fastestVisibleBeats = 8f;

		[Tooltip("달려드는 몸짓이 오가는 시간 (초).")]
		[SerializeField] private float lungeSeconds = 0.22f;

		[Tooltip("쏘는 알갱이가 날아가는 시간 (초).")]
		[SerializeField] private float boltSeconds = 0.18f;

		[Tooltip("적이 죽을 때 부풀었다 꺼지는 시간 (초).")]
		[SerializeField] private float popSeconds = 0.3f;

		[Header("색 — 세계관 전이라 도형 팔레트")]
		[SerializeField] private Color groundColor = new Color(0.62f, 0.73f, 0.55f);
		[SerializeField] private Color coverColor = new Color(0.52f, 0.58f, 0.64f);
		[SerializeField] private Color enemyColor = new Color(0.34f, 0.30f, 0.42f);
		[SerializeField] private Color boltColor = new Color(1f, 0.83f, 0.29f);

		[SerializeField] private Color[] dollColors =
		{
			new Color(0.91f, 0.84f, 0.75f),
			new Color(0.75f, 0.81f, 0.91f),
			new Color(0.84f, 0.75f, 0.91f),
		};

		private readonly List<Transform> bolts = new List<Transform>();
		private readonly List<float> boltAges = new List<float>();
		private readonly List<Vector3> boltFrom = new List<Vector3>();

		private Transform[] dolls;
		private float[] lungeLeft;
		private Transform enemy;
		private MeshFilter enemyMesh;
		private Material enemyMaterial;
		private int enemySides = -1;
		private Material groundMaterial;
		private Color groundRest;

		private float beat;
		private int turn;
		private long lastKills = -1L;
		private float popLeft;
		private float supplyGlowLeft;

		private bool built;

		/// <summary>
		/// 무대를 세운다 — 화면(<see cref="IdleBattleScreen"/>)이 깨어날 때 한 번 부른다.
		/// 멱등이다: 두 번 불러도 배우가 두 벌 생기지 않는다.
		/// </summary>
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
			ground.transform.localScale = new Vector3(1.4f, 1f, 1.1f);
			groundMaterial = Paint(ground, groundColor);
			groundRest = groundColor;

			dolls = new Transform[dollSpots.Length];
			lungeLeft = new float[dollSpots.Length];

			for (int slot = 0; slot < dollSpots.Length; slot++)
			{
				dolls[slot] = BuildDoll(slot);

				GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
				cover.name = "Cover" + slot;
				cover.transform.SetParent(transform, false);
				cover.transform.localPosition = dollSpots[slot] + new Vector3(1.1f, 0.25f, 0f);
				cover.transform.localScale = new Vector3(0.35f, 0.5f, 0.8f);
				Paint(cover, coverColor);
			}

			GameObject foe = new GameObject("Enemy");
			foe.transform.SetParent(transform, false);
			foe.transform.localPosition = enemySpot;
			enemyMesh = foe.AddComponent<MeshFilter>();
			MeshRenderer renderer = foe.AddComponent<MeshRenderer>();
			enemyMaterial = MakeMaterial(enemyColor);
			renderer.sharedMaterial = enemyMaterial;
			enemy = foe.transform;
		}

		/// <summary>치비 인형 하나 — 캡슐 몸에 큰 머리. 비율이 곧 «치비»다.</summary>
		private Transform BuildDoll(int slot)
		{
			GameObject doll = new GameObject("Doll" + slot);
			doll.transform.SetParent(transform, false);
			doll.transform.localPosition = dollSpots[slot];
			doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);

			GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			body.transform.SetParent(doll.transform, false);
			body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
			body.transform.localScale = new Vector3(0.42f, 0.35f, 0.42f);
			Paint(body, dollColors[slot % dollColors.Length]);

			GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			head.transform.SetParent(doll.transform, false);
			head.transform.localPosition = new Vector3(0f, 0.95f, 0f);
			head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
			Paint(head, dollColors[slot % dollColors.Length]);

			return doll.transform;
		}

		/// <summary>
		/// 한 프레임 그린다 — 장단은 코어의 실제 공격속도에서 온다.
		/// 화면이 제 장단을 지어내면 빠르기를 올려도 빨라진 게 안 보인다.
		/// </summary>
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

			DressEnemy(snapshot);

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

			AdvanceBodies(delta);
		}

		/// <summary>적의 변 수·크기 — 등급이 그대로 생김새다. 체력이 줄면 조금 움츠러든다.</summary>
		private void DressEnemy(IdleSnapshot snapshot)
		{
			int sides = Mathf.Max(3, snapshot.MaxTierNow + 2);

			if (sides != enemySides)
			{
				enemySides = sides;
				enemyMesh.sharedMesh = NgonPrism(sides, 0.9f, 1.1f);
			}

			float alive = 0.82f + 0.18f * (float)snapshot.TargetHealthRatio;
			float pop = popLeft > 0f ? 1f + 0.35f * (popLeft / popSeconds) : 1f;
			enemy.localScale = new Vector3(alive * pop, pop, alive * pop);
		}

		/// <summary>한 대 — 차례가 된 인형이 달려들고, 알갱이가 날아간다.</summary>
		private void Strike(IdleSnapshot snapshot)
		{
			int who = NextFighter(snapshot);
			lungeLeft[who] = lungeSeconds;
			SpawnBolt(dolls[who].position + new Vector3(0.3f, 0.9f, 0f));
		}

		/// <summary>서 있는 얼굴이 있으면 그중에서만 돌린다 — 빈 자리가 유령처럼 때리지 않게.</summary>
		private int NextFighter(IdleSnapshot snapshot)
		{
			bool anyoneStanding = false;

			for (int slot = 0; slot < snapshot.Party.Length; slot++)
			{
				if (snapshot.Party[slot] >= 0)
				{
					anyoneStanding = true;
					break;
				}
			}

			for (int tried = 0; tried < dolls.Length; tried++)
			{
				int slot = turn % dolls.Length;
				turn++;

				if (anyoneStanding == false || (slot < snapshot.Party.Length && snapshot.Party[slot] >= 0))
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

		/// <summary>몸짓들을 굴린다 — 달려들기·알갱이·죽음 부풀기·보급 반짝임.</summary>
		private void AdvanceBodies(float delta)
		{
			for (int slot = 0; slot < dolls.Length; slot++)
			{
				if (lungeLeft[slot] > 0f)
				{
					lungeLeft[slot] -= delta;
				}

				// 갔다가 돌아온다 — 반환점이 절반이다.
				float swing = lungeLeft[slot] > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - lungeLeft[slot] / lungeSeconds) * Mathf.PI)
					: 0f;
				dolls[slot].localPosition = dollSpots[slot] + new Vector3(swing * 0.5f, 0f, 0f);
			}

			for (int at = bolts.Count - 1; at >= 0; at--)
			{
				boltAges[at] += delta;
				float gone = Mathf.Clamp01(boltAges[at] / boltSeconds);
				Vector3 target = enemy.position + new Vector3(0f, 0.1f, 0f);
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

		/// <summary>일제 사격 — 셋이 다 달려들고 알갱이가 쏟아진다 (카드의 몸짓).</summary>
		public void OnVolley()
		{
			if (built == false)
			{
				return;
			}

			for (int slot = 0; slot < dolls.Length; slot++)
			{
				lungeLeft[slot] = lungeSeconds;
				SpawnBolt(dolls[slot].position + new Vector3(0.3f, 0.9f, 0f));
				SpawnBolt(dolls[slot].position + new Vector3(0.1f, 1.1f, 0.1f));
			}

			popLeft = popSeconds;
		}

		/// <summary>긴급 보급 — 땅이 잠시 금빛으로 (걸렸다는 게 몸으로 보이게).</summary>
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

		/// <summary>URP 프로젝트다 — URP Lit 를 먼저 찾고, 없으면(시험 판 등) 아무 표준으로.</summary>
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

		/// <summary>
		/// N각기둥 — <b>변의 수 = 등급</b> 규칙의 3D 형태. 위·아래 뚜껑 + 옆면.
		/// </summary>
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

			// 뚜껑 둘 — 부채꼴.
			for (int lid = 0; lid < 2; lid++)
			{
				float y = lid == 0 ? half : -half;
				int center = vertices.Count;
				vertices.Add(new Vector3(0f, y, 0f));

				int first = vertices.Count;
				for (int at = 0; at < sides; at++)
				{
					vertices.Add(ring[at] + new Vector3(0f, y, 0f));
				}

				for (int at = 0; at < sides; at++)
				{
					int next = (at + 1) % sides;
					if (lid == 0)
					{
						triangles.Add(center); triangles.Add(first + next); triangles.Add(first + at);
					}
					else
					{
						triangles.Add(center); triangles.Add(first + at); triangles.Add(first + next);
					}
				}
			}

			// 옆면 — 모서리가 살게 변마다 정점을 따로 쓴다.
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
