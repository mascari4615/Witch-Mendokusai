using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 원고 번호 겹침 판정의 회귀 잠금.
	///
	/// ★ 왜 이게 사고인가: 「이 대화 봤나」·「그때 뭐라고 했나」는 전부 번호로 기록된다.
	///   겹치면 서로 다른 두 원고가 **한 칸을 같이 쓴다** — 한쪽을 보면 다른 쪽도 본 것이 되고,
	///   「처음 만났을 때만」 대사가 엉뚱한 데서 안 나온다. 안 터지고 흔적도 없다.
	///
	/// 자산을 복제해 새 원고를 만드는 게 제일 흔한 방법인데, 그러면 번호까지 복제된다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptIdAuditTest
	{
		private static List<DialogueScriptIdAudit.Entry> Entries(params (int id, string name)[] items)
		{
			List<DialogueScriptIdAudit.Entry> entries = new();
			for (int i = 0; i < items.Length; i++)
			{
				entries.Add(new DialogueScriptIdAudit.Entry(items[i].id, items[i].name));
			}
			return entries;
		}

		[Test]
		public void DistinctIds_AreFine()
		{
			Assert.That(DialogueScriptIdAudit.FindDuplicates(Entries((5200, "오프닝"), (5201, "기능확인"))).Count,
				Is.EqualTo(0));
		}

		[Test]
		public void SameId_IsReportedWithBothNames()
		{
			List<string> problems = DialogueScriptIdAudit.FindDuplicates(
				Entries((5200, "오프닝"), (5200, "오프닝 복사본")));

			Assert.That(problems.Count, Is.EqualTo(1));
			Assert.That(problems[0].Contains("오프닝"), Is.True);
			Assert.That(problems[0].Contains("오프닝 복사본"), Is.True,
				"어느 둘이 부딪혔는지 안 알려주면 찾으러 다녀야 한다");
		}

		[Test]
		public void UnnumberedAssets_AreNotCounted()
		{
			// 번호를 아직 안 매긴 건 다른 흠이다. 여기서 같이 잡으면 새 자산을 만들 때마다
			// 「겹쳤다」가 떠서 이 검사는 곧 무시당한다.
			List<string> problems = DialogueScriptIdAudit.FindDuplicates(
				Entries((DataSO.NONE_ID, "새 원고 1"), (DataSO.NONE_ID, "새 원고 2")));

			Assert.That(problems.Count, Is.EqualTo(0));
		}

		[Test]
		public void ThreeWayCollision_IsOneLine()
		{
			// 번호 하나에 대해 한 줄이면 된다 — 짝마다 한 줄씩 찍으면 콘솔이 도배된다.
			List<string> problems = DialogueScriptIdAudit.FindDuplicates(
				Entries((7, "가"), (7, "나"), (7, "다")));

			Assert.That(problems.Count, Is.EqualTo(1));
		}

		[Test]
		public void NothingToCheck_IsQuiet()
		{
			Assert.That(DialogueScriptIdAudit.FindDuplicates(null).Count, Is.EqualTo(0));
			Assert.That(DialogueScriptIdAudit.FindDuplicates(Entries()).Count, Is.EqualTo(0));
		}
	}
}
