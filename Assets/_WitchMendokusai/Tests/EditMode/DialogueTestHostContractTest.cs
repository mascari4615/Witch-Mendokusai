using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-211 — **흉내가 진짜보다 더 살아 있지 않은지** 지키는 시험.
	///
	/// ★ 왜 있나: 유니티 없이 도는 빠른 검사 쪽이 컴포넌트를 붙일 때 <c>Awake</c> 를 대신 불러 주고 있었다.
	///   그래서 빠른 검사 255개가 전부 초록인 동안 진짜 유니티 검사 둘은 빨간 채였다(2026-08-08).
	///   초록이 「된다」가 아니라 「흉내에서만 된다」를 뜻하게 된 것이다 — 가장 나쁜 종류의 초록이다.
	///
	/// ★ 이 시험이 지키는 약속 두 줄:
	/// 그냥 붙이면 붙는 즉시 도는 코드는 안 돈다(진짜 유니티가 편집 모드에서 그렇게 한다).
	/// 도우미로 붙이면 돈다.
	/// 두 줄이 양쪽 세계에서 똑같이 성립해야 두 세계가 같은 물건이다.
	/// 누가 흉내 쪽에 다시 친절을 넣으면 첫 줄이 몇 초 만에 빨개진다.
	/// </summary>
	public sealed class DialogueTestHostContractTest
	{
		// 러너는 「먼저 깬 하나」를 static 으로 들고 있다 — 앞선 시험이 남긴 것을 치우고 시작한다.
		private static void ClearRunnerInstance()
		{
			while (DialogueRunner.Instance != null)
			{
				Object.DestroyImmediate(DialogueRunner.Instance);
			}
		}

		[Test]
		public void PlainAttach_DoesNotRunAwake()
		{
			ClearRunnerInstance();

			GameObject host = new("DialogueRunnerPlainAttach");
			host.AddComponent<DialogueRunner>();

			Assert.That(DialogueRunner.Instance, Is.Null,
				"그냥 붙이는 것만으로 Awake 가 돌면, 빠른 검사가 진짜 유니티보다 더 친절한 것이다");

			Object.DestroyImmediate(host);
		}

		[Test]
		public void HostAttach_RunsAwake()
		{
			ClearRunnerInstance();

			DialogueRunner runner = DialogueTestHost.Attach<DialogueRunner>("DialogueRunnerHostAttach");

			Assert.That(DialogueRunner.Instance, Is.SameAs(runner),
				"도우미로 붙이면 붙는 즉시 도는 배선이 실제로 돈다");

			Object.DestroyImmediate(runner.gameObject);
		}
	}
}
