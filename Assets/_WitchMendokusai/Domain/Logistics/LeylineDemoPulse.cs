using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-173 — <b>시연용 더미</b>. 「샘 → 중계석 → 공방」 세 거점을 깔고, 하루가 지날 때마다 마력을 흘려
	/// 얼마가 도착하는지 콘솔에 남긴다. 거리가 멀수록 새는 게 눈에 보인다.
	///
	/// ★ 왜 시간이 아니라 하루냐: 이 세계의 시계는 실제 1초에 게임 2분이 흐른다 —
	///   시각마다 남기면 <b>실제 30초에 한 줄씩</b> 쌓여 콘솔이 이걸로 덮인다.
	///   경고 하나가 콘솔을 84번 덮었던 전례가 있어(다른 슬롯 실측) 처음부터 하루 단위로 둔다.
	///
	/// ★ 로어 아니다. 마력이 어디서 나서 어디로 가는지는 사용자가 정할 문제고, 이건 자리표다.
	///   진짜 거점이 생기면 부트스트랩 토글로 통째로 끈다.
	/// </summary>
	public class LeylineDemoPulse : MonoBehaviour
	{
		[SerializeField] private string sourceId = "샘";
		[SerializeField] private string relayId = "중계석";
		[SerializeField] private string sinkId = "공방";
		[SerializeField] private float sourceToRelay = 6f;
		[SerializeField] private float relayToSink = 9f;

		[Tooltip("하루가 지날 때마다 흘려보내는 마력 양.")]
		[SerializeField] private float sendPerDay = 20f;

		[Tooltip("하루가 지날 때마다 콘솔에 남길지. 꺼 두면 돌아도 눈에 안 보인다.")]
		[SerializeField] private bool logEachDay = true;

		private LeylineDirector director;
		private WorldClock worldClock;

		/// <summary>코드로 얹을 때 더미 값을 한 번에 넣는다.</summary>
		public void Configure(LeylineDirector target, string demoSource, string demoRelay, string demoSink,
			float demoSourceToRelay, float demoRelayToSink, float demoSendPerDay)
		{
			director = target;
			sourceId = demoSource;
			relayId = demoRelay;
			sinkId = demoSink;
			sourceToRelay = demoSourceToRelay;
			relayToSink = demoRelayToSink;
			sendPerDay = demoSendPerDay;
		}

		// init-order-ok: 세계 시계는 씬 정적 배치라 Start 시점 존재. 없으면 조용히 쉰다(다른 감독들과 같은 폴백).
		private void Start()
		{
			if (director == null)
			{
				director = GetComponent<LeylineDirector>();
			}

			if (director == null)
			{
				return;
			}

			director.AddNode(sourceId, LeylineNodeKind.Source);
			director.AddNode(relayId, LeylineNodeKind.Relay);
			director.AddNode(sinkId, LeylineNodeKind.Sink);
			director.AddEdge(sourceId, relayId, sourceToRelay);
			director.AddEdge(relayId, sinkId, relayToSink);

			if (WorldClock.TryGetExistingInstance(out worldClock) == false)
			{
				return;
			}

			worldClock.OnDayChanged += OnDayChanged;
		}

		private void OnDestroy()
		{
			if (worldClock != null)
			{
				worldClock.OnDayChanged -= OnDayChanged;
			}
		}

		private void OnDayChanged(int day)
		{
			float arrived = director.Send(sourceId, sinkId, sendPerDay);

			if (logEachDay == true)
			{
				float lost = sendPerDay - arrived;
				Debug.Log($"[마력의 강] {sourceId} → {sinkId} : 보낸 {sendPerDay:0.#} → 도착 {arrived:0.#}"
					+ $" (오는 길에 {lost:0.#} 샘)");
			}
		}
	}
}
