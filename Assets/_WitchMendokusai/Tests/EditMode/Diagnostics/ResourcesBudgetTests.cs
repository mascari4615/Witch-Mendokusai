using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using WitchMendokusai.EditorTools;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// <b>Resources 예산</b> — 전역 창고가 <b>더 무거워지는 것</b>을 막는다 (TASK-WM-408).
	///
	/// ★ 왜 상한이 필요한가: `Resources/` 에 넣은 것은 <b>씬과 무관하게 모든 제품 빌드에</b>
	///   그 참조 그래프째로 실린다. 그래서 프리팹 하나를 무심코 넣으면
	///   방치형 배포분이 조용히 몇 MB 씩 분다 — 굽기 전에는 아무도 모른다.
	///   실측 2026-08-16: 그렇게 해서 Idle 이 안 쓰는 스테이지·NPC 메시·아이콘 시트가 실렸다.
	///
	/// ★ 이 판은 <b>절대 크기를 재는 시험이 아니다</b>. 「늘었나」를 잡는 시험이다.
	///   이관(단계 3·4)으로 줄면 <c>BUDGET_MB</c> 를 <b>줄여서</b> 새 바닥을 못 박는다 —
	///   그래야 되돌아가는 것도 잡힌다.
	/// </summary>
	public sealed class ResourcesBudgetTests
	{
		/// <summary>
		/// 상한(MB). 2026-08-16 기준 실측값 + 여유 10%.
		/// ⚠ 올릴 때는 <b>왜 늘어야 하는지</b>를 커밋 메시지에 적을 것. 그냥 올리면 이 시험이 의미를 잃는다.
		/// </summary>
		/// <b>2026-08-17 실측 9.0MB</b> + 여유 10%.
		/// 아침엔 27.0MB(상한 28.5) 였다 — A/B/C 이관으로 <b>9.0MB</b> 까지 내려왔고,
		/// 규칙대로 상한도 같이 내린다. 안 내리면 되돌아가는 것을 못 잡는다.
		/// 남은 9.0MB 는 필요 비용이다 — 갈무리 폰트 5.2MB(Dynamic 모드라 빼면 한글이 □) · TMP 폴백 ~2MB.
		private const float BUDGET_MB = 10.0f;

		[Test]
		public void Resources_StaysWithinBudget()
		{
			// ★ 재는 것 = <b>실제로 구워진 바이트</b> (디스크 크기가 아니다 — 그건 첫 계측에서 틀렸다).
			//   보고서가 없으면(= 이 기계에서 아직 안 구웠다) 이 판은 건너뛴다. 없는 것을 근거로 빨간불을 켜지 않는다.
			List<BuildInventory.Item> items = BuildInventory.Read();
			if (items.Count == 0)
			{
				Assert.Ignore("빌드 인벤토리가 없다 — 한 번 굽고 나서 재는 판이다 (" + BuildInventory.INVENTORY_PATH + ")");
				return;
			}

			ulong bytes = BuildInventory.ResourcesBytes();
			float mb = bytes / 1024f / 1024f;

			if (mb <= BUDGET_MB) { Assert.Pass(); return; }

			// 넘었으면 <b>어디서</b> 넘었는지까지 보여 준다 — 숫자만 주면 다음 사람이 또 헤맨다.
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(string.Format(
				"Resources 때문에 빌드에 실린 바이트가 예산을 넘었다 — {0:N1} MB > {1:N1} MB", mb, BUDGET_MB));
			sb.AppendLine("Resources 안의 것은 씬과 무관하게 <모든 제품> 빌드에 그래프째로 실린다 (TASK-WM-408).");
			sb.AppendLine("무거운 것 (R = Resources 때문):");
			int shown = 0;
			foreach (BuildInventory.Item i in items)
			{
				if (i.FromResources == false) { continue; }
				if (shown++ >= 12) { break; }
				sb.AppendLine(string.Format("  {0,8:N0} KB  {1}", i.Bytes / 1024f, i.Path));
			}
			Assert.Fail(sb.ToString());
		}
	}
}
