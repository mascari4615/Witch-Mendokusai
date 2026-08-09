using System.Collections;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계로 들어가는 문 하나 (TASK-WM-217).
	///
	/// 「혼자 하기 / 같이 하기」를 묻지 않는다. 들어가면 세계가 있고, 그 세계가
	/// 멀리 있으면 거기 붙고 <b>없으면 내 안에서 띄운다.</b> 사람은 그 차이를 몰라도 된다.
	///
	/// ★ 왜 「못 붙으면 실패」가 아닌가: 인터넷이 없다고 게임이 안 열리면 그건 게임이 아니다.
	///   접속은 <b>더 좋은 경우</b>이지 <b>필요 조건</b>이 아니다.
	/// </summary>
	public sealed class WorldLinkProvider : MonoBehaviour
	{
		public static WorldLinkProvider Instance { get; private set; }

		[Header("멀리 있는 세계")]
		[SerializeField] private string remoteUrl = "ws://127.0.0.1:5199/ws";

		[Tooltip("이만큼 기다려도 안 붙으면 내 안의 세계로 들어간다 (초).")]
		[SerializeField] private float connectTimeoutSeconds = 2f;

		private WebWorldClient remote;

		/// <summary>지금 이어진 줄. 아직 안 들어갔으면 null.</summary>
		public IWorldLink Current { get; private set; }

		/// <summary>내 안의 세계에 들어와 있나 — 화면에 조용히 알려줄 때만 쓴다.</summary>
		public bool IsLocalWorld { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
		}

		/// <summary>세계로 들어간다. 이미 들어와 있으면 그대로 둔다.</summary>
		public void Enter()
		{
			if (Current != null)
				return;

			StartCoroutine(EnterRoutine());
		}

		private IEnumerator EnterRoutine()
		{
			remote = gameObject.AddComponent<WebWorldClient>();
			remote.SetServerUrl(remoteUrl);
			remote.Connect();

			float waited = 0f;
			while (waited < connectTimeoutSeconds && remote.IsLinked == false)
			{
				waited += Time.unscaledDeltaTime;
				yield return null;
			}

			if (remote.IsLinked)
			{
				Current = remote;
				IsLocalWorld = false;
				yield break;
			}

			// 못 붙었다 — 조용히 내 안의 세계로. 사람에게는 그냥 「세계에 들어왔다」.
			remote.Disconnect();
			Destroy(remote);
			remote = null;

			Current = new LocalWorldLink();
			IsLocalWorld = true;
		}

		/// <summary>세계에서 나온다.</summary>
		public void Leave()
		{
			if (remote != null)
			{
				remote.Disconnect();
				Destroy(remote);
				remote = null;
			}

			Current = null;
			IsLocalWorld = false;
		}
	}
}
