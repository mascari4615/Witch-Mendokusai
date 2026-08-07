using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-173 — 마력을 보냈을 때 <b>얼마가 도착한다고 답하는지</b>를 지킨다.
	///
	/// ★ 계산층(감쇠식)에는 이미 시험이 있다. 여기서 보는 건 <b>내가 그 위에 얹은 층</b>이다 —
	///   망을 들고 있다가 답해 주고, 도착할 때마다 알리는 부분. 그 부분엔 시험이 0개였다.
	/// </summary>
	public class LeylineDirectorTests
	{
		private GameObject host;
		private LeylineDirector director;
		private readonly List<(string From, string To, float Sent, float Arrived)> delivered
			= new List<(string, string, float, float)>();

		[SetUp]
		public void SetUp()
		{
			host = new GameObject("LeylineDirectorTestHost");
			director = host.AddComponent<LeylineDirector>();
			delivered.Clear();
			director.OnManaDelivered += (from, to, sent, arrived) => delivered.Add((from, to, sent, arrived));
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(host);
		}

		private void BuildLine(float firstLength, float secondLength)
		{
			director.AddNode("샘", LeylineNodeKind.Source);
			director.AddNode("중계석", LeylineNodeKind.Relay);
			director.AddNode("공방", LeylineNodeKind.Sink);
			director.AddEdge("샘", "중계석", firstLength);
			director.AddEdge("중계석", "공방", secondLength);
		}

		[Test]
		public void 보내면_도착량이_나오고_한_번_알린다()
		{
			BuildLine(3f, 3f);

			float arrived = director.Send("샘", "공방", 20f);

			Assert.Greater(arrived, 0f);
			Assert.LessOrEqual(arrived, 20f);          // 오는 길에 늘어나지는 않는다.
			Assert.AreEqual(1, delivered.Count);
			Assert.AreEqual("샘", delivered[0].From);
			Assert.AreEqual("공방", delivered[0].To);
			Assert.AreEqual(20f, delivered[0].Sent);
			Assert.AreEqual(arrived, delivered[0].Arrived);
		}

		[Test]
		public void 길이_멀수록_적게_도착한다()
		{
			BuildLine(3f, 3f);
			float near = director.Send("샘", "공방", 20f);

			TearDown();
			SetUp();
			BuildLine(30f, 30f);
			float far = director.Send("샘", "공방", 20f);

			Assert.Less(far, near);
		}

		[Test]
		public void 길이_없으면_도착_0_이고_터지지_않는다()
		{
			director.AddNode("샘", LeylineNodeKind.Source);
			director.AddNode("외딴집", LeylineNodeKind.Sink);
			// 배선을 안 깐다 — 둘은 이어져 있지 않다.

			float arrived = director.Send("샘", "외딴집", 20f);

			Assert.AreEqual(0f, arrived);
			Assert.AreEqual(1, delivered.Count); // 「0 도착」도 알린다 — 조용히 사라지면 안 보인다.
		}

		[Test]
		public void 없는_거점으로_보내도_터지지_않고_0_이다()
		{
			BuildLine(3f, 3f);

			float arrived = director.Send("샘", "그런곳없다", 20f);

			Assert.AreEqual(0f, arrived);
		}

		[Test]
		public void 아무것도_안_보내면_아무것도_안_도착한다()
		{
			BuildLine(3f, 3f);

			float arrived = director.Send("샘", "공방", 0f);

			Assert.AreEqual(0f, arrived);
		}
	}
}
