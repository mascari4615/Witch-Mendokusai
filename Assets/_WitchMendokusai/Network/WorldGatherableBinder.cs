using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계에 서 있는 <b>주울 것</b>을 게임 화면에 세우고, 가장 가까운 것을 줍는다 (TASK-WM-217).
	///
	/// ★ 왜 필요한가: 세계는 주울 것을 갖고 있는데 게임 창에는 <b>아무것도 안 보였다</b> —
	///   웹 창에서만 주울 수 있는 세계였다. 「게임 창과 웹 창이 같이 논다」가 목표라면 여기가 빈 자리다.
	///
	/// 모양은 <b>네모</b>다(그래픽은 나중에). 지금 확인해야 할 것은 「보이고, 닿고, 줍힌다」뿐이다.
	/// </summary>
	public sealed class WorldGatherableBinder : MonoBehaviour
	{
		/// <summary>세계가 정한 손 닿는 거리 — 창은 미리 보여 주기만 한다(판정은 서버).</summary>
		private const float REACH = 2.5f;

		private const float BODY_HEIGHT = 0.9f;

		private readonly Dictionary<int, Transform> bodies = new Dictionary<int, Transform>();
		private readonly List<int> gone = new List<int>();

		private IWorldLink link;
		private InputManager inputManager;
		private Material nearMaterial;
		private Material farMaterial;

		private void OnEnable()
		{
			// 문은 통신 층이 세우고 여기 꽂아 둔다 — 로비·게임은 통신을 몰라야 한다.
			link = Net.WorldDoor.Current;
		}

		private void Start()
		{
			inputManager = InputManager.Instance;
			inputManager?.RegisterInputEvent(InputEventType.Gather, InputEventResponseType.Performed, GatherNearest);
		}

		private void OnDestroy()
		{
			inputManager?.UnregisterInputEvent(InputEventType.Gather, InputEventResponseType.Performed, GatherNearest);
		}

		private void Update()
		{
			// 문이 늦게 열릴 수 있다 — 사용 시점에 다시 묻는다(init-order-ok: lazy resolve).
			if (link == null)
				link = Net.WorldDoor.Current;

			if (link == null)
				return;

			GatherableView[] alive = link.Gatherables;
			HashSet<int> seen = new HashSet<int>();

			for (int i = 0; i < alive.Length; i++)
			{
				GatherableView node = alive[i];
				seen.Add(node.id);

				// `== null` 까지 봐야 한다. 사전에 남아 있어도 **오브젝트가 이미 파괴됐으면**
				// 유니티는 그것을 null 처럼 취급하고, 그 다음 줄 `body.position` 이 NRE 로 터진다.
				// 아래 정리 구간(`&& body != null`)은 진작 그렇게 보고 있었는데 여기만 빠져 있었다.
				//
				// 실측 2026-08-13: 부팅 스모크가 10초 만에 NRE **181회** — 매 프레임 여기서 났다.
				// 야간 빌드가 나흘간 죽어 있어(2.1 전용 API 한 줄) 그동안 아무도 못 봤다.
				if (bodies.TryGetValue(node.id, out Transform body) == false || body == null)
				{
					body = CreateBody(node.id);
					bodies[node.id] = body;
				}

				body.position = new Vector3(node.x, BODY_HEIGHT * 0.5f, node.z);
				Paint(body, WithinReach(node));
			}

			// 뽑아 간 자리는 사라진다 — 세계에서 빠진 것을 화면에 남겨 두면 헛손질을 부른다.
			gone.Clear();
			foreach (KeyValuePair<int, Transform> entry in bodies)
			{
				if (seen.Contains(entry.Key) == false)
					gone.Add(entry.Key);
			}

			for (int i = 0; i < gone.Count; i++)
			{
				if (bodies.TryGetValue(gone[i], out Transform body) && body != null)
					Destroy(body.gameObject);

				bodies.Remove(gone[i]);
			}
		}

		/// <summary>가장 가까운 것을 줍는다 — 정말 줍히는지는 세계가 본다(멀면 아무 일도 안 일어난다).</summary>
		private void GatherNearest()
		{
			if (link == null)
				return;

			GatherableView[] alive = link.Gatherables;
			int nearestId = 0;
			float nearest = float.MaxValue;

			for (int i = 0; i < alive.Length; i++)
			{
				float distance = DistanceToMe(alive[i]);
				if (distance >= nearest)
					continue;

				nearest = distance;
				nearestId = alive[i].id;
			}

			if (nearestId == 0 || nearest > REACH)
				return;

			link.RequestGather(nearestId);
		}

		private bool WithinReach(GatherableView node) => DistanceToMe(node) <= REACH;

		private float DistanceToMe(GatherableView node)
		{
			WorldDollView[] dolls = link.Dolls;
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id != link.MyDollId)
					continue;

				float dx = dolls[i].x - node.x;
				float dz = dolls[i].z - node.z;
				return Mathf.Sqrt(dx * dx + dz * dz);
			}

			return float.MaxValue;
		}

		private Transform CreateBody(int nodeId)
		{
			GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
			body.name = $"Gatherable {nodeId}";
			body.transform.localScale = new Vector3(0.5f, BODY_HEIGHT, 0.5f);

			// 부딪히면 걷다가 걸린다 — 이건 주울 것이지 벽이 아니다.
			Collider collider = body.GetComponent<Collider>();
			if (collider != null)
				Destroy(collider);

			return body.transform;
		}

		private void Paint(Transform body, bool near)
		{
			Renderer renderer = body.GetComponent<Renderer>();
			if (renderer == null)
				return;

			if (nearMaterial == null)
			{
				nearMaterial = new Material(renderer.sharedMaterial) { color = new Color(0.79f, 0.69f, 0.42f) };
				farMaterial = new Material(renderer.sharedMaterial) { color = new Color(0.36f, 0.42f, 0.32f) };
			}

			Material wanted = near ? nearMaterial : farMaterial;
			if (renderer.sharedMaterial != wanted)
				renderer.sharedMaterial = wanted;
		}
	}
}
