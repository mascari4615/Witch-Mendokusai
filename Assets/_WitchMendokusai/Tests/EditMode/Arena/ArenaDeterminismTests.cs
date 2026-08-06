using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 투기장 시뮬레이션이 **결정적**이라는 전제를 기계가 지킨다.
	///
	/// ★ 왜 이게 필요한가: PlayMode 검증이 막혀 있는 동안 투기장의 「여러 틱 수렴」은
	///   `ArenaFullMatchSimTests` 가 대신 본다. 그 시험이 의미를 가지려면 **같은 입력이 같은 결과**를
	///   내야 한다 — 비결정적이면 「가끔 초록」이 되고, 그건 아무 말도 안 하는 것과 같다.
	///
	/// ★ 그 전제는 지금 **한 줄에만** 걸려 있다: 투기장 코드가 `TacticDriver.Navigator` 를 안 세운다.
	///   `Lane`(투기장의 유일한 난수)은 `Navigator != null` 일 때만 읽히기 때문이다.
	///
	///   예전엔 근거가 둘이었다 — `Lane` 초기화가 선언 자리에 있어 유니티가 예외를 뱉고 **값이 항상 0**
	///   이었다(WM-194 `40f99cfe` 가 그걸 고쳐 `Awake` 로 옮겼다. 개척에선 그 0 때문에 마수가 한 줄로
	///   몰려왔다). 이제 `Lane` 은 진짜 난수라 **버팀목이 하나 빠졌다.**
	///   누가 투기장에 Navigator 를 배선하면 그 순간 시뮬레이션이 비결정적이 되고, 회귀는
	///   **간헐적 실패**로 나타난다 — 사람이 가장 늦게 알아채는 형태다. 그래서 여기서 막는다.
	/// </summary>
	public sealed class ArenaDeterminismTests
	{
		private const string ARENA_SOURCE_ROOT = "Assets/_WitchMendokusai/Domain/Arena";

		private static List<string> ArenaSourceFiles()
		{
			string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ARENA_SOURCE_ROOT);

			// ★ 「대상 0건 = 통과」 방지. 경로가 바뀌면 이 시험은 조용히 아무것도 안 보게 된다.
			Assert.IsTrue(
				Directory.Exists(root),
				$"투기장 소스 폴더를 못 찾았다: {root}\n" +
				"위반이 없는 게 아니라 아무것도 검사하지 않은 것이다 — 경로 상수를 갱신할 것.");

			List<string> files = new List<string>(Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories));

			Assert.Greater(
				files.Count,
				10,
				$"투기장 .cs 를 {files.Count}개밖에 못 찾았다 — 스캔이 깨진 것으로 본다.");

			return files;
		}

		[Test]
		public void 투기장은_TacticDriver_에_Navigator_를_세우지_않는다()
		{
			// `driver.Navigator = ...` / `x.Navigator=y` 같은 대입만 잡는다(비교 `==` 는 제외).
			Regex assignment = new Regex(@"\.Navigator\s*=(?!=)");
			List<string> offenders = new List<string>();

			foreach (string file in ArenaSourceFiles())
			{
				string text = File.ReadAllText(file);
				if (assignment.IsMatch(text))
				{
					offenders.Add(Path.GetFileName(file));
				}
			}

			if (offenders.Count > 0)
			{
				Assert.Fail(
					"투기장 코드가 `TacticDriver.Navigator` 를 세운다: " + string.Join(", ", offenders) + "\n\n" +
					"그러면 `Lane`(난수)이 읽히기 시작해 **투기장 시뮬레이션이 비결정적이 된다.**\n" +
					"`ArenaFullMatchSimTests` 는 여러 틱을 굴려 수렴을 보는 시험이라, 비결정적이 되면\n" +
					"「가끔 빨간」 상태가 된다 — 회귀인지 운인지 구분이 안 되는 최악의 형태다.\n\n" +
					"정말 투기장에 내비게이션이 필요하면, 이 시험을 지우지 말고 **시드를 고정**하는 쪽으로\n" +
					"(예: 매치 config 의 시드로 Lane 을 결정) 바꾼 뒤 이 시험을 그 규칙으로 고쳐 쓸 것.");
			}
		}

		[Test]
		public void 투기장_난수는_Lane_하나뿐이다()
		{
			// 새 난수가 슬쩍 들어오면 위 시험(Navigator 한 줄)만으로는 결정성을 못 지킨다.
			Regex randomUse = new Regex(@"\bRandom\s*\.");
			List<string> hits = new List<string>();

			foreach (string file in ArenaSourceFiles())
			{
				foreach (string line in File.ReadAllLines(file))
				{
					string trimmed = line.TrimStart();
					if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*"))
					{
						continue; // 주석에서 난수를 *언급*하는 건 위반이 아니다
					}

					if (randomUse.IsMatch(line))
					{
						hits.Add($"{Path.GetFileName(file)}: {trimmed}");
					}
				}
			}

			// 현재 정본 = `TacticDriver.Lane` 한 줄. 늘어나면 결정성 근거를 다시 세워야 한다.
			Assert.AreEqual(
				1,
				hits.Count,
				"투기장 난수 사용이 1곳(=`TacticDriver.Lane`)이 아니다:\n  " + string.Join("\n  ", hits) + "\n\n" +
				"투기장 시뮬레이션의 결정성은 「난수가 실제로 읽히지 않는다」에 기대고 있다.\n" +
				"난수를 새로 들이면 `ArenaFullMatchSimTests` 가 간헐적으로 빨개질 수 있다 —\n" +
				"들이려면 시드를 고정하고, 이 시험을 그 규칙으로 고쳐 쓸 것.");
		}
	}
}
