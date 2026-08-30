using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 쿼터뷰 자동전투 무대 (V2, concept-v2 — 실조사 반영 2026-08-23).
	///
	/// ★ 이 파일에 게임 규칙이 한 줄도 없다 — 사진(<see cref="IdleSnapshot"/>)을 받아 3D 로 그린다.
	///
	/// ★ <b>웨이브는 무리다</b> (실조사 `refs/blue-archive.md` · `refs/ultima-squad.md`):
	///   자동전투+카드 개입 계열은 한 웨이브에 잡몹 여럿이 <b>동시에</b> 오고 마지막에 보스, 대열 방치 전투 계열도 「몬스터들이 무리지어
	///   등장하고 마지막에 보스」다. 전에는 한 마리씩 줄을 세웠는데 그건 두 원작 어느 쪽도 아니었다.
	///   판정(코어)은 여전히 한 번에 하나씩 — <b>보이는 것만</b> 무리다. 맨 앞이 지금 맞는 놈이고,
	///   뒤의 것들은 「아직 안 온 차례」다.
	///
	/// ★ <b>체력바는 머리 위</b> — 아군·잡몹 모두. 상단 대형 바는 <b>보스에게만</b> (화면 쪽에서 그린다).
	///
	/// ★ 배우는 전부 절차 생성 도형 — 인형 = 캡슐+큰 머리(치비), 적 = 변 수=등급 N각기둥.
	/// </summary>
	public sealed class IdleBattleStage : MonoBehaviour
	{
		[Header("자리 — 0번 = 나(항상), 1~3번 = 가챠로 뽑아 앉힌 자리")]
		[SerializeField] private Vector3[] dollSpots =
		{
			new Vector3(-2.1f, 0f, 0.1f),
			new Vector3(-3.0f, 0f, -1.2f),
			new Vector3(-3.4f, 0f, 0.3f),
			new Vector3(-2.9f, 0f, 1.5f),
		};

		[Header("적 무리 — 한 웨이브가 이만큼 몰려 온다")]
		[Tooltip("한 웨이브의 잡몹 수 (마지막 웨이브는 보스 혼자).")]
		[SerializeField] private int waveSize = 3;

		[Tooltip("무리의 맨 앞이 서는 자리.")]
		[SerializeField] private Vector3 frontSpot = new Vector3(2.4f, 0.62f, 0f);

		[Tooltip("무리 안에서 뒤로 밀리는 간격 (m).")]
		[SerializeField] private float packSpacing = 1.5f;

		[Header("행군 — 한 웨이브를 끝내면 나아간다")]
		[Tooltip("웨이브와 웨이브 사이 거리 (m).")]
		[SerializeField] private float waveSpacing = 7f;

		[SerializeField] private float marchCatchUp = 3.5f;

		[Header("장단 — 눈으로 셀 수 있는 데까지만")]
		[SerializeField] private float lungeSeconds = 0.22f;
		[SerializeField] private float boltSeconds = 0.18f;
		[SerializeField] private float popSeconds = 0.3f;

		[Header("보스")]
		[SerializeField] private float bossScale = 1.9f;

		[Header("색")]
		[SerializeField] private Color groundColor = new Color(0.62f, 0.73f, 0.55f);
		[SerializeField] private Color coverColor = new Color(0.52f, 0.58f, 0.64f);
		[SerializeField] private Color enemyColor = new Color(0.55f, 0.50f, 0.63f);
		[SerializeField] private Color bossColor = new Color(0.30f, 0.26f, 0.38f);
		[SerializeField] private Color sceneryColor = new Color(0.50f, 0.60f, 0.46f);
		[SerializeField] private Color boltColor = new Color(1f, 0.83f, 0.29f);
		[SerializeField] private Color myColor = new Color(0.93f, 0.89f, 0.82f);

		[Header("체력바 — 머리 위")]
		[SerializeField] private Color barBackColor = new Color(0.12f, 0.13f, 0.16f, 1f);
		[SerializeField] private Color allyBarColor = new Color(0.36f, 0.78f, 0.44f);
		[SerializeField] private Color reviveBarColor = new Color(0.62f, 0.66f, 0.74f);
		[SerializeField] private Color enemyBarColor = new Color(0.91f, 0.36f, 0.36f);

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
			public IdleHealthBar Bar;
			public long Index;
			public int Sides;
			public bool Boss;
		}

		private readonly List<Foe> foes = new List<Foe>();
		private readonly List<Transform> scenery = new List<Transform>();
		private readonly List<Transform> bolts = new List<Transform>();
		private readonly List<float> boltAges = new List<float>();
		private readonly List<Vector3> boltFrom = new List<Vector3>();

		// 피해 숫자. 코어 타격마다 적 머리 위, 오르며 소멸. 적 피해는 붉게 아군 머리 위
		private readonly List<TextMesh> numbers = new List<TextMesh>();
		private readonly List<float> numberAges = new List<float>();
		private double hitDamage;
		[SerializeField] private float numberSeconds = 0.8f;
		[SerializeField] private float numberRise = 1.2f;
		[SerializeField] private float numberSize = 0.12f;
		[SerializeField] private Color numberColor = new Color(1f, 1f, 1f);
		[SerializeField] private Color hurtColor = new Color(1f, 0.45f, 0.4f);
		[SerializeField] private float hurtNumberSeconds = 0.5f;
		private long lastHits = -1L;
		private float hurtClock;

		private Transform worldRoot;
		private Transform[] dolls;
		private Material[] dollSkins;
		private IdleHealthBar[] dollBars;
		private float[] lungeLeft;
		private Material groundMaterial;
		private Color groundRest;

		private int turn;
		private long lastKills = -1L;
		private float popLeft;
		private float supplyGlowLeft;
		private float marchOffset;

		private bool built;

		/// <summary>무대를 세운다 — 멱등.</summary>
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
			ground.transform.localScale = new Vector3(6f, 1f, 4f);
			groundMaterial = Paint(ground, groundColor);
			groundRest = groundColor;

			GameObject world = new GameObject("World");
			world.transform.SetParent(transform, false);
			worldRoot = world.transform;

			dolls = new Transform[dollSpots.Length];
			dollSkins = new Material[dollSpots.Length];
			dollBars = new IdleHealthBar[dollSpots.Length];
			lungeLeft = new float[dollSpots.Length];

			for (int slot = 0; slot < dollSpots.Length; slot++)
			{
				dolls[slot] = BuildDoll(slot);
				dolls[slot].gameObject.SetActive(slot == 0);

				// 엄폐물은 없다 (사용자 2026-08-30. 부대와 같이 움직여 어색)
			}

			for (int at = 0; at < 14; at++)
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

		private Transform BuildDoll(int slot)
		{
			GameObject doll = new GameObject("Doll" + slot);
			doll.transform.SetParent(transform, false);
			doll.transform.localPosition = dollSpots[slot];
			doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);

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

			// ★ 머리 위 체력바 — 아군도 잡몹도 여기 단다 (실조사).
			dollBars[slot] = IdleHealthBar.Attach(doll.transform, 1.45f, 0.9f, 0.11f,
				barBackColor, allyBarColor);

			return doll.transform;
		}

		/// <summary>파티를 무대에 맞춘다 — 앉힌 영웅만 서고, 등급색을 입고, 체력바가 붙는다.</summary>
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

				if (seat > 0 && view.HeroId >= 0)
				{
					int grade = (int)view.Grade;
					dollSkins[seat].color = gradeColors[Mathf.Clamp(grade, 0, gradeColors.Length - 1)];
				}

				// 쓰러진 자리는 <b>부활 게이지</b>가 그 자리에 뜬다 (대열 방치 전투 계열: 머리 위 부활 대기 게이지).
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
					// 쓰러진 것이 보이게 — 옆으로 눕고 납작해진다.
					dolls[seat].localRotation = Quaternion.Euler(0f, 0f, -78f);
					dolls[seat].localScale = new Vector3(1f, 0.75f, 1f);
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

			hitDamage = snapshot.Damage.CurrentValue;
			DressDolls(snapshot);
			DressFoes(snapshot);

			// 타격은 코어가 센 만큼만. 연출 박자가 따로 돌면 숫자와 체력이 어긋난다 (사용자 2026-08-30)
			if (lastHits < 0L)
			{
				lastHits = snapshot.HitsOnTarget;
			}

			long hitsNow = snapshot.HitsOnTarget;
			long landed = hitsNow >= lastHits ? hitsNow - lastHits : hitsNow;
			lastHits = hitsNow;

			if (landed > 0L)
			{
				Strike(snapshot);
				SpawnNumber(FoeHead(), Numerics.BigNumberText.Format(hitDamage * landed), numberColor);
			}

			Hurt(snapshot, delta);

			if (snapshot.Kills > lastKills)
			{
				lastKills = snapshot.Kills;
				popLeft = popSeconds;
			}

			March(snapshot, delta);
			AdvanceBodies(delta);
		}

		/// <summary>
		/// 적 <b>무리</b>를 사진에 맞춘다 — 한 웨이브가 통째로 서 있고, 앞에서부터 쓰러진다.
		///
		/// ★ 웨이브 번호·무리 안 자리는 처치 수(<see cref="IdleSnapshot.KillsInStage"/>)에서 나온다.
		///   구역의 <b>마지막 하나</b>는 보스라 혼자 선다 (자동전투+카드 개입 계열·대열 방치 전투 계열 공통).
		/// </summary>
		private void DressFoes(IdleSnapshot snapshot)
		{
			long first = snapshot.Kills;
			int inStage = snapshot.KillsInStage;
			int perStage = Mathf.Max(1, snapshot.KillsPerStage);
			int mobs = perStage - 1; // 마지막 하나는 보스.
			int pack = Mathf.Max(1, waveSize);

			// 지금 무리에 몇이 남았나 — 보스 차례면 하나.
			int standingNow;
			if (inStage >= mobs)
			{
				standingNow = 1;
			}
			else
			{
				int intoWave = inStage % pack;
				int leftInStage = mobs - inStage;
				standingNow = Mathf.Min(pack - intoWave, leftInStage);
			}

			for (int at = foes.Count - 1; at >= 0; at--)
			{
				if (foes[at].Index < first || foes[at].Index >= first + standingNow)
				{
					Destroy(foes[at].Piece.gameObject);
					foes.RemoveAt(at);
				}
			}

			for (long index = first; index < first + standingNow; index++)
			{
				if (Has(index) == false)
				{
					foes.Add(MakeFoe(index));
				}
			}

			foreach (Foe foe in foes)
			{
				int aheadOfNow = (int)(foe.Index - first);
				bool boss = inStage + aheadOfNow >= mobs;
				int sides = Mathf.Max(3, snapshot.MaxTierNow + 2);

				if (foe.Sides != sides)
				{
					foe.Sides = sides;
					foe.Mesh.sharedMesh = NgonPrism(sides, 0.62f, 0.95f);
				}

				if (foe.Boss != boss)
				{
					foe.Boss = boss;
					foe.Skin.color = boss ? bossColor : enemyColor;
				}

				// 무리 배치 — 맨 앞이 지금 맞는 놈, 뒤는 지그재그로 몰려 있다.
				//
				// ⚠ 좌표는 <b>누적 웨이브</b>로 잡는다 (실측 2026-08-23): 구역 안 번호로만 잡았더니
				//   세계(worldRoot)는 누적으로 흐르는데 적은 구역 안 자리에 서서 <b>화면 밖</b>에 있었다.
				//   흐르는 쪽과 세우는 쪽이 같은 자를 써야 한다.
				float back = aheadOfNow * packSpacing;
				float side = aheadOfNow % 2 == 0 ? 0.9f : -1.0f;
				float wave = WavesDone(snapshot) * waveSpacing;

				foe.Piece.localPosition = new Vector3(
					wave + frontSpot.x + back,
					frontSpot.y,
					frontSpot.z + (aheadOfNow == 0 ? 0f : side * (0.7f + 0.35f * aheadOfNow)));

				bool atFront = foe.Index == first;
				float health = atFront ? 0.82f + 0.18f * (float)snapshot.TargetHealthRatio : 1f;
				float pop = atFront && popLeft > 0f ? 1f + 0.35f * (popLeft / popSeconds) : 1f;
				float bulk = boss ? bossScale : 1f;
				foe.Piece.localScale = new Vector3(health * pop * bulk, pop * bulk, health * pop * bulk);

				// ★ 잡몹도 머리 위 체력바 — 보스는 화면 상단 대형 바가 따로 있어 여기선 안 단다.
				foe.Bar.SetVisible(boss == false);
				if (boss == false)
				{
					foe.Bar.SetRatio(atFront ? (float)snapshot.TargetHealthRatio : 1f);
				}
			}
		}

		/// <summary>이 구역에서 지금 몇 번째 웨이브인가 — 보스는 마지막 웨이브다.</summary>
		private static int WaveOf(int inStage, int mobs, int pack)
		{
			if (inStage >= mobs)
			{
				return Mathf.CeilToInt((float)mobs / pack);
			}

			return inStage / pack;
		}

		/// <summary>
		/// 여태 지나온 웨이브 총수 — <b>흐르는 쪽과 세우는 쪽이 같이 쓰는 자</b>.
		///
		/// ★ 아주 깊은 구역에서 좌표가 커지지 않게 <b>구역 안에서만</b> 센다.
		///   지나온 구역까지 더하면 61구역쯤에서 1,600m 밖이 되어 부동소수점도 흔들린다.
		/// </summary>
		private float WavesDone(IdleSnapshot snapshot)
		{
			int perStage = Mathf.Max(1, snapshot.KillsPerStage);
			int mobs = perStage - 1;
			int pack = Mathf.Max(1, waveSize);
			return WaveOf(snapshot.KillsInStage, mobs, pack);
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
			foe.Bar = IdleHealthBar.Attach(piece.transform, 0.95f, 0.8f, 0.1f,
				barBackColor, enemyBarColor);
			return foe;
		}

		/// <summary>
		/// 세계가 흐른다 — <b>웨이브 단위</b>로 나아간다. 한 무리를 다 잡으면 다음 무리 앞까지 전진.
		/// </summary>
		private void March(IdleSnapshot snapshot, float delta)
		{
			float wanted = -WavesDone(snapshot) * waveSpacing;

			// 구역이 바뀌면 웨이브 번호가 0 으로 돌아온다 — 그때는 <b>끌지 않고 붙인다</b>.
			//   끌면 판이 통째로 뒤로 미끄러져 「되돌아간다」로 보인다. 소품이 절차라 티가 안 난다.
			if (wanted > marchOffset + waveSpacing * 0.5f)
			{
				marchOffset = wanted;
			}

			marchOffset = Mathf.Lerp(marchOffset, wanted, Mathf.Min(1f, delta * marchCatchUp));
			worldRoot.localPosition = new Vector3(marchOffset, 0f, 0f);

			float span = scenery.Count * 3.1f;
			foreach (Transform prop in scenery)
			{
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

		/// <summary>서 있는 인형 중에서 차례를 돌린다 — 쓰러진 자리는 안 때린다.</summary>
		private int NextFighter(IdleSnapshot snapshot)
		{
			for (int tried = 0; tried < dolls.Length; tried++)
			{
				int slot = turn % dolls.Length;
				turn++;

				if (slot < snapshot.Seats.Length && snapshot.Seats[slot].Standing)
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

		/// <summary>맨 앞 적의 머리 위 자리</summary>
		private Vector3 FoeHead()
		{
			return foes.Count > 0
				? foes[0].Piece.position + new Vector3(0f, 0.7f, 0f)
				: transform.TransformPoint(frontSpot) + new Vector3(0f, 0.7f, 0f);
		}

		/// <summary>
		/// 적의 공격 표시. 코어는 초당 피해를 맨 앞 자리에 연속 투입
		/// (<see cref="IdleSquad.Advance"/>). 숫자는 일정 간격, 값은 그 간격 몫
		/// </summary>
		private void Hurt(IdleSnapshot snapshot, float delta)
		{
			if (foes.Count == 0 || snapshot.EnemyDamagePerSecond <= 0d)
			{
				return;
			}

			int front = -1;
			for (int seat = 0; seat < snapshot.Seats.Length && seat < dolls.Length; seat++)
			{
				if (snapshot.Seats[seat].Taken && snapshot.Seats[seat].Standing)
				{
					front = seat;
					break;
				}
			}

			if (front < 0)
			{
				return;
			}

			hurtClock += delta;
			if (hurtClock < hurtNumberSeconds)
			{
				return;
			}

			hurtClock -= hurtNumberSeconds;
			double chunk = snapshot.EnemyDamagePerSecond * hurtNumberSeconds;
			SpawnNumber(dolls[front].position + new Vector3(0f, 1.3f, 0f), "-" + Numerics.BigNumberText.Format(chunk), hurtColor);
		}

		/// <summary>피해 숫자 하나. 내장 글꼴 TextMesh. 카메라를 본다</summary>
		private void SpawnNumber(Vector3 at, string what, Color color)
		{
			GameObject holder = new GameObject("Damage");
			holder.transform.SetParent(transform, false);
			holder.transform.position = at;

			TextMesh text = holder.AddComponent<TextMesh>();
			text.text = what;
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.fontSize = 64;
			text.characterSize = numberSize;
			text.fontStyle = FontStyle.Bold;
			text.anchor = TextAnchor.MiddleCenter;
			text.alignment = TextAlignment.Center;
			text.color = color;
			holder.GetComponent<MeshRenderer>().material = text.font.material;

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
					Destroy(text.gameObject);
					numbers.RemoveAt(at);
					numberAges.RemoveAt(at);
					continue;
				}

				text.transform.position += new Vector3(0f, numberRise * delta, 0f);
				Color tone = text.color;
				text.color = new Color(tone.r, tone.g, tone.b, 1f - life * life);

				if (eye != null)
				{
					text.transform.rotation = eye.transform.rotation;
				}
			}
		}

		private void AdvanceBodies(float delta)
		{
			bool marching = Mathf.Abs(worldRoot.localPosition.x - marchOffset) > 0.02f
				|| Mathf.Abs(marchOffset - Mathf.Round(marchOffset / waveSpacing) * waveSpacing) > 0.15f;

			for (int slot = 0; slot < dolls.Length; slot++)
			{
				if (lungeLeft[slot] > 0f)
				{
					lungeLeft[slot] -= delta;
				}

				float swing = lungeLeft[slot] > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - lungeLeft[slot] / lungeSeconds) * Mathf.PI)
					: 0f;

				float bob = marching ? Mathf.Abs(Mathf.Sin(Time.time * 7f + slot * 1.3f)) * 0.1f : 0f;

				dolls[slot].localPosition = dollSpots[slot] + new Vector3(swing * 0.5f, bob, 0f);
			}

			for (int at = bolts.Count - 1; at >= 0; at--)
			{
				boltAges[at] += delta;
				float gone = Mathf.Clamp01(boltAges[at] / boltSeconds);
				Vector3 target = foes.Count > 0
					? foes[0].Piece.position + new Vector3(0f, 0.1f, 0f)
					: transform.TransformPoint(frontSpot);
				bolts[at].position = Vector3.Lerp(boltFrom[at], target, gone);

				if (gone >= 1f)
				{
					Destroy(bolts[at].gameObject);
					bolts.RemoveAt(at);
					boltAges.RemoveAt(at);
					boltFrom.RemoveAt(at);
				}
			}

			AdvanceNumbers(delta);

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

		/// <summary>일제 사격 — 서 있는 모두가 달려들고 알갱이가 쏟아진다.</summary>
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

			if (made.HasProperty("_BaseColor"))
			{
				made.SetColor("_BaseColor", color);
			}

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
