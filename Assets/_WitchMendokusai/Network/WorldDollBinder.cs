using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Network;
using WitchMendokusai.Net;
using SimVector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계가 보내온 인형들을 <b>화면에 세운다</b> (TASK-WM-217 단계 3).
	///
	/// FishNet 이 스폰·디스폰으로 해 주던 일을 여기가 대신한다 — 다만 <b>몸은 서버가 스폰하지 않는다.</b>
	/// 각 창이 목록을 보고 자기 화면에만 세운다(서버는 「누가 어디 있다」만 안다).
	/// 그래서 서버가 유니티일 필요가 없다 = 웹 창도 같은 목록으로 같은 사람들을 그린다.
	///
	/// 「누가 왔고 누가 갔나」와 「걸음을 얼마나 보내나」는 판정 층(<see cref="DollRoster"/> ·
	/// <see cref="MoveIntent"/>)이 정하고, 여기는 <b>그 답대로 오브젝트를 만들고 옮기기만</b> 한다.
	/// </summary>
	public sealed class WorldDollBinder : MonoBehaviour
	{
		/// <summary>Resources 에 이 이름의 프리팹이 있으면 그걸로 몸을 세운다. 없으면 임시 몸.</summary>
		private const string DOLL_RESOURCE = "WorldDoll";

		/// <summary>걸음을 보내는 주기 — 매 프레임 보내면 세계가 말을 너무 많이 듣는다.</summary>
		private const float SEND_INTERVAL = 0.1f;

		/// <summary>남의 인형이 순간이동해 보이지 않게 따라가는 속도(1 = 즉시).</summary>
		private const float FOLLOW_LERP = 12f;

		private readonly DollRoster roster = new DollRoster();
		private readonly Dictionary<int, Transform> bodies = new Dictionary<int, Transform>();
		private GameObject prefab;
		private float sendCooldown;

		private IWorldLink Link => WorldLinkProvider.Instance != null ? WorldLinkProvider.Instance.Current : null;

		private void Update()
		{
			IWorldLink link = Link;
			if (link == null || link.IsLinked == false)
			{
				if (bodies.Count > 0)
					ClearBodies();

				return;
			}

			DollView[] dolls = link.Dolls;
			SyncBodies(dolls, link.MyDollId);
			SendMyStep(link, dolls, Time.deltaTime);
		}

		private void OnDestroy() => ClearBodies();

		private void SyncBodies(DollView[] dolls, int myDollId)
		{
			RosterChange change = roster.Sync(dolls, myDollId);

			for (int i = 0; i < change.Left.Count; i++)
			{
				if (bodies.TryGetValue(change.Left[i], out Transform body))
				{
					if (body != null)
						Destroy(body.gameObject);

					bodies.Remove(change.Left[i]);
				}
			}

			for (int i = 0; i < change.Appeared.Count; i++)
			{
				int dollId = change.Appeared[i];
				roster.TryGetPosition(dollId, out SimVector3 spawn);
				bodies[dollId] = CreateBody(dollId, spawn);
			}

			float follow = 1f - Mathf.Exp(-FOLLOW_LERP * Time.deltaTime);
			foreach (KeyValuePair<int, Transform> entry in bodies)
			{
				if (entry.Value == null)
					continue;

				if (roster.TryGetPosition(entry.Key, out SimVector3 target) == false)
					continue;

				// 세계는 20 번/초 알려주고 화면은 그보다 자주 그린다 — 그 사이를 메워야 걷는 것처럼 보인다.
				Vector3 desired = new Vector3(target.x, entry.Value.position.y, target.z);
				entry.Value.position = Vector3.Lerp(entry.Value.position, desired, follow);
			}
		}

		private Transform CreateBody(int dollId, SimVector3 position)
		{
			if (prefab == null)
				prefab = Resources.Load<GameObject>(DOLL_RESOURCE);

			GameObject body = prefab != null
				? Instantiate(prefab)
				: GameObject.CreatePrimitive(PrimitiveType.Capsule);

			body.name = $"WorldDoll {dollId}";
			body.transform.position = new Vector3(position.x, 0f, position.z);
			return body.transform;
		}

		private void SendMyStep(IWorldLink link, DollView[] dolls, float deltaTime)
		{
			sendCooldown -= deltaTime;
			if (sendCooldown > 0f)
				return;

			sendCooldown = SEND_INTERVAL;

			if (LocalPlayerProbeBridge.TryGetPose(out float x, out float _, out float z, out float _) == false)
				return;

			// 세계가 아는 내 자리 — 이게 기준이다. 화면의 내 캐릭터를 기준으로 삼으면
			// 세계가 걸음을 잘라낼 때마다 둘이 벌어져 영원히 따라잡지 못한다.
			SimVector3 known = SimVector3.zero;
			if (dolls != null)
			{
				for (int i = 0; i < dolls.Length; i++)
				{
					if (dolls[i] != null && dolls[i].id == link.MyDollId)
					{
						known = new SimVector3(dolls[i].x, 0f, dolls[i].z);
						break;
					}
				}
			}

			if (MoveIntent.TryStep(known, new SimVector3(x, 0f, z), WorldSim.MAX_STEP, out SimVector3 step))
				link.RequestMove(step.x, step.z);
		}

		private void ClearBodies()
		{
			foreach (KeyValuePair<int, Transform> entry in bodies)
			{
				if (entry.Value != null)
					Destroy(entry.Value.gameObject);
			}

			bodies.Clear();
			roster.Clear();
		}
	}
}
