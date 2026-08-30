using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 3D 무대. 코어 사진(<see cref="IdleSnapshot"/>)의 전장 위치를 그대로 그린다 (combat.md 8).
	///
	/// ★ 위치는 시뮬 것. 인형은 <c>Fighters</c>, 적은 <c>Foes</c>.
	///   무대가 자리를 지어내지 않는다 (전에는 무대가 연출 자리를 지어내 코어와 무관했다. 사용자 2026-08-30)
	/// ★ 타격은 <c>Hits</c> 만. 볼트와 피해 숫자의 유일한 출처.
	///   그래서 숫자와 체력이 일치
	/// ★ 시뮬 x 는 오른쪽이 적, y 는 옆. Unity 로는 (x, 0, y). 세계 스크롤은 부대 가운데 기준
	/// </summary>
	[ExecuteAlways]
	public sealed class IdleBattleStage : MonoBehaviour
	{
		[Header("손으로 만든 조각. 비우면 코드 도형 (사용자 2026-08-30: 고퀄리티는 사람 손길)")]
		[Tooltip("인형 prefab. 자식 이름 Body/Head 의 MeshRenderer 에 등급색. 없으면 캡슐 + 구")]
		[SerializeField] private GameObject dollPrefab;

		[Tooltip("잡몹 prefab. 없으면 N각기둥")]
		[SerializeField] private GameObject foePrefab;

		[Tooltip("보스 prefab. 없으면 잡몹 prefab 또는 N각기둥 (bossScale 배)")]
		[SerializeField] private GameObject bossPrefab;

		[Tooltip("바닥과 소품 prefab. 없으면 Plane + 상자, 기둥")]
		[SerializeField] private GameObject groundPrefab;

		[Header("시야. 부대 가운데가 화면 어디에 서나")]
		[Tooltip("부대 가운데의 무대 x. 왼쪽에 둬야 오른쪽에서 오는 적이 보인다")]
		[SerializeField] private float partyAnchorX = -2.5f;

		[Tooltip("세계가 부대를 따라잡는 속도 (1/s)")]
		[SerializeField] private float followCatchUp = 4f;

		[Tooltip("한 프레임에 이만큼 넘게 튀면 (웨이브 재원점) 끌지 않고 붙인다")]
		[SerializeField] private float snapJump = 3f;

		[Header("장단. 눈으로 셀 수 있는 데까지만")]
		[SerializeField] private float lungeSeconds = 0.22f;
		[SerializeField] private float boltSeconds = 0.18f;
		[SerializeField] private float popSeconds = 0.3f;
		[SerializeField] private float positionCatchUp = 14f;
		[SerializeField] private float foeSpinDegrees = 42f;
		[SerializeField] private float foeBobHeight = 0.08f;

		[Header("적")]
		[SerializeField] private float bossScale = 1.9f;
		[SerializeField] private float foeHeight = 0.62f;

		[Header("색")]
		[SerializeField] private Color groundColor = new Color(0.62f, 0.73f, 0.55f);
		[SerializeField] private Color enemyColor = new Color(0.55f, 0.50f, 0.63f);
		[SerializeField] private Color rangedEnemyColor = new Color(0.66f, 0.54f, 0.50f);
		[SerializeField] private Color bossColor = new Color(0.30f, 0.26f, 0.38f);
		[SerializeField] private Color sceneryColor = new Color(0.50f, 0.60f, 0.46f);
		[SerializeField] private Color boltColor = new Color(1f, 0.83f, 0.29f);
		[SerializeField] private Color myColor = new Color(0.93f, 0.89f, 0.82f);

		[Header("체력바. 머리 위")]
		[SerializeField] private Color barBackColor = new Color(0.12f, 0.13f, 0.16f, 1f);
		[SerializeField] private Color allyBarColor = new Color(0.36f, 0.78f, 0.44f);
		[SerializeField] private Color reviveBarColor = new Color(0.62f, 0.66f, 0.74f);
		[SerializeField] private Color enemyBarColor = new Color(0.91f, 0.36f, 0.36f);

		[Header("영웅 등급색. 일반, 레어, 에픽, 레전드")]
		[SerializeField] private Color[] gradeColors =
		{
			new Color(0.68f, 0.72f, 0.80f),
			new Color(0.46f, 0.80f, 0.72f),
			new Color(0.72f, 0.58f, 0.92f),
			new Color(0.95f, 0.72f, 0.36f),
		};

		[Header("피해 숫자")]
		[SerializeField] private float numberSeconds = 0.8f;
		[SerializeField] private float numberRise = 1.2f;
		[SerializeField] private float numberSize = 0.12f;
		[SerializeField] private Color numberColor = new Color(1f, 1f, 1f);
		[SerializeField] private Color hurtColor = new Color(1f, 0.45f, 0.4f);

		private sealed class Foe
		{
			public Transform Piece;
			public MeshFilter Mesh;
			public Material Skin;
			public IdleHealthBar Bar;
			public long Index;
			public int Sides;
			public bool Boss;
			public IdleFoeKind Kind;
			public float PopLeft;
		}

		private readonly List<Foe> foes = new List<Foe>();
		private readonly List<Transform> scenery = new List<Transform>();
		private readonly List<Transform> bolts = new List<Transform>();
		private readonly List<float> boltAges = new List<float>();
		private readonly List<Vector3> boltFrom = new List<Vector3>();
		private readonly List<long> boltTarget = new List<long>();

		private readonly List<TextMesh> numbers = new List<TextMesh>();
		private readonly List<float> numberAges = new List<float>();

		// 한 프레임의 타격을 대상별로 합쳐 숫자 하나로 (초당 수십 타면 낱개 숫자는 판독 불가)
		private readonly Dictionary<long, double> dealtByFoe = new Dictionary<long, double>();
		private readonly Dictionary<int, double> takenBySeat = new Dictionary<int, double>();

		// 이번 프레임에 사라진 적의 마지막 머리 위치. 막타 숫자가 설 자리
		private readonly Dictionary<long, Vector3> goneHeads = new Dictionary<long, Vector3>();

		private Transform holder;
		private Transform worldRoot;
		private float clock;
		private Transform[] dolls;
		private Material[] dollSkins;
		private IdleHealthBar[] dollBars;
		private float[] lungeLeft;
		private Material groundMaterial;
		private Color groundRest;

		private float scroll;
		private bool scrollReady;
		private float supplyGlowLeft;
		private bool built;

		/// <summary>무대 세우기. 멱등</summary>
		public void Build()
		{
			if (built)
			{
				return;
			}

			built = true;

			// 도메인 리로드 뒤 남은 미리보기 잔재부터 정리 (씬에는 안 저장되지만 메모리에는 잔존)
			for (int at = transform.childCount - 1; at >= 0; at--)
			{
				Transform child = transform.GetChild(at);
				if (child.name == "Preview")
				{
					Kill(child.gameObject);
				}
			}

			GameObject root = new GameObject("Preview");
			root.hideFlags = HideFlags.DontSave;
			root.transform.SetParent(transform, false);
			holder = root.transform;

			if (groundPrefab != null)
			{
				GameObject made = Instantiate(groundPrefab, holder, false);
				made.name = "Ground";
				MeshRenderer floor = made.GetComponentInChildren<MeshRenderer>();
				groundMaterial = floor != null ? floor.sharedMaterial : MakeMaterial(groundColor);
				groundRest = groundMaterial.color;
			}
			else
			{
				GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
				ground.name = "Ground";
				ground.transform.SetParent(holder, false);
				ground.transform.localScale = new Vector3(6f, 1f, 4f);
				groundMaterial = Paint(ground, groundColor);
				groundRest = groundColor;
			}

			GameObject world = new GameObject("World");
			world.transform.SetParent(holder, false);
			worldRoot = world.transform;

			int seats = IdleSquad.SEAT_COUNT;
			dolls = new Transform[seats];
			dollSkins = new Material[seats];
			dollBars = new IdleHealthBar[seats];
			lungeLeft = new float[seats];

			for (int seat = 0; seat < seats; seat++)
			{
				dolls[seat] = BuildDoll(seat);
				dolls[seat].gameObject.SetActive(false);
			}

			for (int at = 0; at < (groundPrefab != null ? 0 : 14); at++)
			{
				GameObject prop = GameObject.CreatePrimitive(at % 3 == 0
					? PrimitiveType.Cylinder : PrimitiveType.Cube);
				prop.name = "Scenery" + at;
				prop.transform.SetParent(worldRoot, false);

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

		private Transform BuildDoll(int seat)
		{
			GameObject doll = new GameObject("Doll" + seat);
			doll.transform.SetParent(worldRoot, false);
			doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);

			Material skin = MakeMaterial(seat == 0 ? myColor : gradeColors[0]);
			dollSkins[seat] = skin;

			if (dollPrefab != null)
			{
				GameObject made = Instantiate(dollPrefab, doll.transform, false);
				made.name = "Model";
				foreach (MeshRenderer part in made.GetComponentsInChildren<MeshRenderer>())
				{
					if (part.name == "Body" || part.name == "Head")
					{
						part.sharedMaterial = skin;
					}
				}

				dollBars[seat] = IdleHealthBar.Attach(doll.transform, 1.45f, 0.9f, 0.11f,
					barBackColor, allyBarColor);
				return doll.transform;
			}

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

			dollBars[seat] = IdleHealthBar.Attach(doll.transform, 1.45f, 0.9f, 0.11f,
				barBackColor, allyBarColor);

			return doll.transform;
		}

		/// <summary>한 프레임. 위치는 사진, 연출은 여기</summary>
		public void Render(IdleSnapshot snapshot, float delta)
		{
			if (built == false)
			{
				return;
			}

			Follow(snapshot, delta);
			DressDolls(snapshot);
			DressFoes(snapshot, delta);
			ShowHits(snapshot);
			AdvanceBodies(snapshot, delta);
		}

		/// <summary>세계가 부대 가운데를 따라 흐른다. 웨이브 재원점의 큰 점프는 끌지 않고 붙인다</summary>
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

			float center = count > 0 ? sum / count : 0f;
			float wanted = partyAnchorX - center;

			if (scrollReady == false || Mathf.Abs(wanted - scroll) > snapJump)
			{
				scroll = wanted;
				scrollReady = true;
			}
			else
			{
				scroll = Mathf.Lerp(scroll, wanted, Mathf.Min(1f, delta * followCatchUp));
			}

			worldRoot.localPosition = new Vector3(scroll, 0f, 0f);

			float span = scenery.Count * 3.1f;
			foreach (Transform prop in scenery)
			{
				while (prop.localPosition.x + scroll < -12f)
				{
					prop.localPosition += new Vector3(span, 0f, 0f);
				}

				while (prop.localPosition.x + scroll > span - 12f)
				{
					prop.localPosition -= new Vector3(span, 0f, 0f);
				}
			}
		}

		/// <summary>인형을 사진에. 자리 위치, 등급색, 체력바, 쓰러짐</summary>
		private void DressDolls(IdleSnapshot snapshot)
		{
			for (int seat = 0; seat < dolls.Length && seat < snapshot.Seats.Length; seat++)
			{
				IdleSeatView view = snapshot.Seats[seat];

				if (dolls[seat].gameObject.activeSelf != view.Taken)
				{
					dolls[seat].gameObject.SetActive(view.Taken);
				}

				if (view.Taken == false)
				{
					continue;
				}

				if (view.HeroId >= 0)
				{
					int grade = (int)view.Grade;
					dollSkins[seat].color = gradeColors[Mathf.Clamp(grade, 0, gradeColors.Length - 1)];
				}

				if (view.Standing)
				{
					dollBars[seat].SetFillColor(allyBarColor);
					dollBars[seat].SetRatio((float)view.HealthRatio);
					dolls[seat].localRotation = Quaternion.LookRotation(Vector3.right);
					dolls[seat].localScale = Vector3.one;
				}
				else
				{
					dollBars[seat].SetFillColor(reviveBarColor);
					dollBars[seat].SetRatio((float)view.ReviveRatio);
					dolls[seat].localRotation = Quaternion.Euler(0f, 0f, -78f);
					dolls[seat].localScale = new Vector3(1f, 0.75f, 1f);
				}
			}
		}

		/// <summary>적을 사진에. 번호로 잇고, 없어진 번호는 지운다</summary>
		private void DressFoes(IdleSnapshot snapshot, float delta)
		{
			IdleFoeView[] views = snapshot.Foes;

			goneHeads.Clear();

			for (int at = foes.Count - 1; at >= 0; at--)
			{
				if (IndexOfView(views, foes[at].Index) < 0)
				{
					goneHeads[foes[at].Index] = foes[at].Piece.position + new Vector3(0f, 0.7f, 0f);
					Kill(foes[at].Piece.gameObject);
					foes.RemoveAt(at);
				}
			}

			for (int at = 0; at < views.Length; at++)
			{
				IdleFoeView view = views[at];
				Foe foe = Find(view.Index);
				if (foe == null)
				{
					foe = MakeFoe(view.Index, view.Boss);
					foes.Add(foe);
				}

				int sides = Mathf.Max(3, snapshot.MaxTierNow + 2);
				if (foe.Mesh != null && foe.Sides != sides)
				{
					foe.Sides = sides;
					foe.Mesh.sharedMesh = NgonPrism(sides, 0.62f, 0.95f);
				}

				if (foe.Boss != view.Boss || foe.Kind != view.Kind)
				{
					foe.Boss = view.Boss;
					foe.Kind = view.Kind;
					foe.Skin.color = view.Boss ? bossColor
						: (view.Kind == IdleFoeKind.Ranged ? rangedEnemyColor : enemyColor);
				}

				Vector3 wanted = new Vector3((float)view.X, foeHeight, (float)view.Y);
				foe.Piece.localPosition = Vector3.Lerp(foe.Piece.localPosition, wanted, CatchUp(delta));

				float health = 0.82f + 0.18f * (float)view.HealthRatio;
				float pop = foe.PopLeft > 0f ? 1f + 0.35f * (foe.PopLeft / popSeconds) : 1f;
				float bulk = view.Boss ? bossScale : 1f;
				foe.Piece.localScale = new Vector3(health * pop * bulk, pop * bulk, health * pop * bulk);

				// 보스는 화면 상단 큰 바가 따로 있어 머리 위 바는 잡몹만
				foe.Bar.SetVisible(view.Boss == false);
				if (view.Boss == false)
				{
					foe.Bar.SetRatio((float)view.HealthRatio);
				}
			}
		}

		private static int IndexOfView(IdleFoeView[] views, long index)
		{
			for (int at = 0; at < views.Length; at++)
			{
				if (views[at].Index == index)
				{
					return at;
				}
			}

			return -1;
		}

		private Foe Find(long index)
		{
			foreach (Foe foe in foes)
			{
				if (foe.Index == index)
				{
					return foe;
				}
			}

			return null;
		}

		private Foe MakeFoe(long index, bool boss)
		{
			GameObject piece = new GameObject("Foe" + index);
			piece.transform.SetParent(worldRoot, false);

			Foe foe = new Foe();
			foe.Piece = piece.transform;
			foe.Skin = MakeMaterial(enemyColor);
			foe.Index = index;
			foe.Sides = -1;

			GameObject source = boss && bossPrefab != null ? bossPrefab : foePrefab;
			if (source != null)
			{
				GameObject made = Instantiate(source, piece.transform, false);
				made.name = "Model";
				MeshRenderer part = made.GetComponentInChildren<MeshRenderer>();
				if (part != null)
				{
					part.sharedMaterial = foe.Skin;
				}

				foe.Sides = int.MaxValue;
			}
			else
			{
				foe.Mesh = piece.AddComponent<MeshFilter>();
				MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = foe.Skin;
			}

			foe.Kind = IdleFoeKind.Melee;
			foe.Bar = IdleHealthBar.Attach(piece.transform, 0.95f, 0.8f, 0.1f,
				barBackColor, enemyBarColor);
			return foe;
		}

		/// <summary>이번 프레임의 타격. 볼트는 낱개, 숫자는 대상별 합</summary>
		private void ShowHits(IdleSnapshot snapshot)
		{
			IdleHit[] hits = snapshot.Hits;
			if (hits == null || hits.Length == 0)
			{
				return;
			}

			dealtByFoe.Clear();
			takenBySeat.Clear();

			for (int at = 0; at < hits.Length; at++)
			{
				IdleHit hit = hits[at];

				if (hit.ByFoe)
				{
					takenBySeat.TryGetValue(hit.Seat, out double taken);
					takenBySeat[hit.Seat] = taken + hit.Damage;
					Foe shooter = Find(hit.FoeIndex);
					if (shooter != null)
					{
						shooter.PopLeft = popSeconds * 0.5f;
					}
					continue;
				}

				dealtByFoe.TryGetValue(hit.FoeIndex, out double dealt);
				dealtByFoe[hit.FoeIndex] = dealt + hit.Damage;

				if (hit.Seat >= 0 && hit.Seat < dolls.Length)
				{
					lungeLeft[hit.Seat] = lungeSeconds;
					// 볼트는 한 프레임에 자리당 하나면 충분 (초당 수십 발은 점으로 뭉침)
					if (at == 0 || hits[at - 1].Seat != hit.Seat || hits[at - 1].ByFoe)
					{
						SpawnBolt(dolls[hit.Seat].position + new Vector3(0.3f, 0.9f, 0f), hit.FoeIndex);
					}
				}
			}

			foreach (KeyValuePair<long, double> pair in dealtByFoe)
			{
				Foe foe = Find(pair.Key);
				Vector3 head;
				if (foe != null)
				{
					head = foe.Piece.position + new Vector3(0f, 0.7f, 0f);
				}
				else if (goneHeads.TryGetValue(pair.Key, out Vector3 gone))
				{
					head = gone;
				}
				else
				{
					continue;
				}

				SpawnNumber(head, Numerics.BigNumberText.Format(pair.Value), numberColor);
			}

			foreach (KeyValuePair<int, double> pair in takenBySeat)
			{
				if (pair.Key < 0 || pair.Key >= dolls.Length)
				{
					continue;
				}

				SpawnNumber(dolls[pair.Key].position + new Vector3(0f, 1.3f, 0f),
					"-" + Numerics.BigNumberText.Format(pair.Value), hurtColor);
			}
		}

		private void SpawnBolt(Vector3 from, long target)
		{
			GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			bolt.name = "Bolt";
			bolt.transform.SetParent(holder, false);
			bolt.transform.position = from;
			bolt.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
			Paint(bolt, boltColor);

			bolts.Add(bolt.transform);
			boltAges.Add(0f);
			boltFrom.Add(from);
			boltTarget.Add(target);
		}

		/// <summary>피해 숫자 하나. 내장 글꼴 TextMesh. 카메라를 본다</summary>
		private void SpawnNumber(Vector3 at, string what, Color color)
		{
			GameObject sign = new GameObject("Damage");
			sign.transform.SetParent(holder, false);
			sign.transform.position = at;

			TextMesh text = sign.AddComponent<TextMesh>();
			text.text = what;
			text.fontSize = 48;
			text.characterSize = numberSize;
			text.anchor = TextAnchor.MiddleCenter;
			text.alignment = TextAlignment.Center;
			text.color = color;
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			sign.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;

			numbers.Add(text);
			numberAges.Add(0f);
		}

		private void AdvanceNumbers(float delta)
		{
			Camera eye = Camera.main;

			for (int at = numbers.Count - 1; at >= 0; at--)
			{
				numberAges[at] += delta;
				float life = Mathf.Clamp01(numberAges[at] / numberSeconds);
				TextMesh text = numbers[at];

				if (life >= 1f)
				{
					Kill(text.gameObject);
					numbers.RemoveAt(at);
					numberAges.RemoveAt(at);
					continue;
				}

				text.transform.position += new Vector3(0f, numberRise * delta, 0f);
				Color tone = text.color;
				text.color = new Color(tone.r, tone.g, tone.b, 1f - life * life);

				if (eye != null)
				{
					text.transform.rotation = Quaternion.LookRotation(text.transform.position - eye.transform.position);
				}
			}
		}

		private void AdvanceBodies(IdleSnapshot snapshot, float delta)
		{
			clock += delta;

			for (int seat = 0; seat < dolls.Length; seat++)
			{
				if (lungeLeft[seat] > 0f)
				{
					lungeLeft[seat] -= delta;
				}

				float swing = lungeLeft[seat] > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - lungeLeft[seat] / lungeSeconds) * Mathf.PI)
					: 0f;

				bool walking = seat < snapshot.Fighters.Length && snapshot.Fighters[seat].Moving;
				float bob = walking ? Mathf.Abs(Mathf.Sin(clock * 7f + seat * 1.3f)) * 0.1f : 0f;

				float x = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].X : 0f;
				float y = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].Y : 0f;
				Vector3 wanted = new Vector3(x + swing * 0.3f, bob, y);
				dolls[seat].localPosition = Vector3.Lerp(dolls[seat].localPosition, wanted, CatchUp(delta));
			}

			for (int index = 0; index < foes.Count; index++)
			{
				Foe foe = foes[index];
				foe.Piece.Rotate(Vector3.up, foeSpinDegrees * delta, Space.Self);
				Vector3 position = foe.Piece.localPosition;
				position.y += Mathf.Sin(clock * 2.4f + index * 1.7f) * foeBobHeight;
				foe.Piece.localPosition = position;
			}

			for (int at = bolts.Count - 1; at >= 0; at--)
			{
				boltAges[at] += delta;
				float gone = Mathf.Clamp01(boltAges[at] / boltSeconds);
				Foe target = Find(boltTarget[at]);
				Vector3 to = target != null
					? target.Piece.position + new Vector3(0f, 0.1f, 0f)
					: boltFrom[at] + new Vector3(3f, 0f, 0f);
				bolts[at].position = Vector3.Lerp(boltFrom[at], to, gone);

				if (gone >= 1f)
				{
					Kill(bolts[at].gameObject);
					bolts.RemoveAt(at);
					boltAges.RemoveAt(at);
					boltFrom.RemoveAt(at);
					boltTarget.RemoveAt(at);
				}
			}

			foreach (Foe foe in foes)
			{
				if (foe.PopLeft > 0f)
				{
					foe.PopLeft -= delta;
				}
			}

			AdvanceNumbers(delta);

			if (supplyGlowLeft > 0f)
			{
				supplyGlowLeft -= delta;
				float glow = Mathf.Clamp01(supplyGlowLeft);
				groundMaterial.color = Color.Lerp(groundRest, boltColor, glow * 0.35f);
			}
		}

		private float CatchUp(float delta)
		{
			return 1f - Mathf.Exp(-positionCatchUp * delta);
		}

		/// <summary>일제 사격. 서 있는 모두가 달려든다. 볼트와 숫자는 사진의 타격이 따로 낸다</summary>
		public void OnVolley()
		{
			if (built == false)
			{
				return;
			}

			for (int seat = 0; seat < dolls.Length; seat++)
			{
				if (dolls[seat].gameObject.activeSelf)
				{
					lungeLeft[seat] = lungeSeconds;
				}
			}
		}

		public bool TryPickFoe(Vector2 panelPosition, out long foeIndex)
		{
			foeIndex = -1L;
			Camera eye = Camera.main;
			if (eye == null)
			{
				return false;
			}

			Vector2 screen = new Vector2(panelPosition.x, Screen.height - panelPosition.y);
			float best = 54f;
			foreach (Foe foe in foes)
			{
				Vector3 point = eye.WorldToScreenPoint(foe.Piece.position);
				if (point.z <= 0f)
				{
					continue;
				}

				float distance = Vector2.Distance(screen, new Vector2(point.x, point.y));
				if (distance < best)
				{
					best = distance;
					foeIndex = foe.Index;
				}
			}

			return foeIndex >= 0L;
		}

		/// <summary>긴급 보급. 땅이 잠시 금빛</summary>
		public void OnSupply(float seconds)
		{
			supplyGlowLeft = seconds;
		}

		/// <summary>손 응원. 연출은 사진의 타격이 낸다</summary>
		public void OnTap()
		{
		}

		/// <summary>지우기. 에디트 모드는 즉시, 플레이는 프레임 끝</summary>
		private static void Kill(GameObject piece)
		{
			if (Application.isPlaying)
			{
				Destroy(piece);
			}
			else
			{
				DestroyImmediate(piece);
			}
		}

		/// <summary>끄면 미리보기 잔재를 지운다. 다시 켜면 새로 짓는다</summary>
		private void OnDisable()
		{
			if (holder != null)
			{
				Kill(holder.gameObject);
			}

			holder = null;
			worldRoot = null;
			foes.Clear();
			scenery.Clear();
			bolts.Clear();
			boltAges.Clear();
			boltFrom.Clear();
			boltTarget.Clear();
			numbers.Clear();
			numberAges.Clear();
			scrollReady = false;
			built = false;
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
			made.hideFlags = HideFlags.DontSave;
			made.color = color;

			if (made.HasProperty("_BaseColor"))
			{
				made.SetColor("_BaseColor", color);
			}

			return made;
		}

		/// <summary>N각기둥. 변의 수는 등급 규칙의 3D 형태</summary>
		private static Mesh NgonPrism(int sides, float radius, float height)
		{
			Mesh mesh = new Mesh();
			mesh.name = "Polyhedron" + sides;
			sides = Mathf.Clamp(sides, 4, 12);
			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();
			vertices.Add(new Vector3(0f, height * 0.5f, 0f));
			vertices.Add(new Vector3(0f, -height * 0.5f, 0f));
			for (int at = 0; at < sides; at++)
			{
				float angle = at * Mathf.PI * 2f / sides;
				vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
			}
			for (int at = 0; at < sides; at++)
			{
				int next = 2 + (at + 1) % sides;
				int current = 2 + at;
				triangles.Add(0); triangles.Add(current); triangles.Add(next);
				triangles.Add(1); triangles.Add(next); triangles.Add(current);
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
