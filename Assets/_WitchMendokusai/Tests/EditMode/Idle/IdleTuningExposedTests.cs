using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 손잡이가 <b>인스펙터에 실려 있나</b> (TASK-WM-406).
	///
	/// ★ 왜 필요한가 — 「수치 하드코딩 금지」는 룰인데, 지키는지 <b>아무도 안 세고 있었다</b>.
	///   그래서 이 루프가 넣은 것들(폭주·뽑기·도감·가방·환생·오프라인)이 전부 코어에만 있고
	///   `IdleTuningSO` 에는 <b>43개가 빠져 있었다</b>(실측 2026-08-17).
	///   「이 게임의 재미는 전부 이 숫자들에 들어 있다」고 적어 놓고 그 숫자를 못 만지는 상태였다.
	///
	/// ★ 이 시험은 <b>글자를 읽는다</b>(리플렉션이 아니라). 코어는 엔진을 모르고 SO 는 엔진이라
	///   한 판에 같이 못 세운다. 대신 두 파일의 <b>본문</b>을 견준다 —
	///   새 손잡이를 코어에 넣고 SO 에 안 실으면 여기서 먼저 걸린다.
	///
	/// ★ 일부러 안 싣는 것은 <see cref="NotForTheInspector"/> 에 <b>이유와 함께</b> 적는다.
	///   말없이 빼면 다음 사람이 「빠뜨린 것」인지 「일부러」인지 알 수 없다.
	/// </summary>
	public sealed class IdleTuningExposedTests
	{
		/// <summary>인스펙터에 안 싣는 것 — 이유를 여기 적는다.</summary>
		private static readonly string[] NotForTheInspector =
		{
			// (지금은 없다. 빼려면 이유를 옆에 적을 것.)
		};

		[Test]
		public void EveryKnob_IsOnTheInspector()
		{
			string root = FindProjectRoot();
			string core = File.ReadAllText(Path.Combine(root,
				"Assets/_WitchMendokusai/DomainSDK/Idle/IdleTuning.cs"));
			string exposed = File.ReadAllText(Path.Combine(root,
				"Assets/_WitchMendokusai/Idle/IdleTuningSO.cs"));

			MatchCollection knobs = Regex.Matches(core,
				// ⚠ 스칼라만 보면 <b>경제의 뼈대</b>를 놓친다 (실측 2026-08-17): 이 시험을 세운
				//   바로 그날, 생산자 값·산출·잠재 범위(GeometricScale) 셋이 인스펙터 밖에
				//   남아 있는 걸 못 잡았다. 감시가 반만 보면 「초록」이 「안 봤음」이 된다.
				@"public\s+(?:double|int|long|float|bool|GeometricScale|IUpgradeCurve)\s+(\w+)\s*(?:\{|=|;)");
			Assert.Greater(knobs.Count, 20, "손잡이를 하나도 못 찾았다 — 이 시험이 아무것도 안 보고 있다");

			string missing = string.Empty;
			int counted = 0;

			foreach (Match knob in knobs)
			{
				string name = knob.Groups[1].Value;

				if (Array.IndexOf(NotForTheInspector, name) >= 0)
				{
					continue;
				}

				counted++;

				// SO 는 `Name = camelName,` 꼴로 넘긴다.
				if (Regex.IsMatch(exposed, @"\b" + Regex.Escape(name) + @"\s*=") == false)
				{
					missing += (missing.Length > 0 ? ", " : string.Empty) + name;
				}
			}

			TestContext.WriteLine("[손잡이] 코어 " + counted + "개 · 인스펙터에 실림 "
				+ (counted - (missing.Length > 0 ? missing.Split(',').Length : 0)) + "개");

			Assert.AreEqual(string.Empty, missing,
				"IdleTuningSO 에 안 실린 손잡이가 있다 (밸런싱이 코드 작업이 된다): " + missing);
		}

		/// <summary>시험이 어디서 돌든 저장소 뿌리를 찾는다 — 엔진 안팎 둘 다.</summary>
		private static string FindProjectRoot()
		{
			DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory);

			while (at != null)
			{
				if (Directory.Exists(Path.Combine(at.FullName, "Assets/_WitchMendokusai")))
				{
					return at.FullName;
				}

				at = at.Parent;
			}

			throw new DirectoryNotFoundException("저장소 뿌리를 못 찾았다 — Assets/_WitchMendokusai 가 없다");
		}
	}
}
