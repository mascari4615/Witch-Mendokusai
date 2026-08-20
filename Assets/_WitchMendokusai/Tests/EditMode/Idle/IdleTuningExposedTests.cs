using System;
using System.Collections.Generic;
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


		/// <summary>
		/// ★ 인스펙터의 <b>기본값이 코어와 같은가</b> — 다르면 <b>시험이 재는 판과 사람이 노는 판이 다르다</b>.
		///
		/// ⚠ 이게 없던 동안 실제로 둘이 갈려 있었다 (실측 2026-08-17). 시험·시뮬·곡선 표는 전부
		///   코어 기본값으로 돌고, 사람이 켜는 게임은 SO(그리고 .asset)의 값으로 돈다.
		///   그 둘이 다르면 「일곱 날 시뮬」도 「깊이 표」도 <b>아무도 안 노는 판</b>을 잰 것이 된다.
		///   조용하기까지 하다 — 어느 쪽도 틀린 값이 아니라 그냥 <b>다른 값</b>이라서.
		///
		/// ★ 지금 갈린 둘은 <see cref="KnownDrift"/> 에 적어 뒀다(어느 쪽이 맞는지는 밸런스라
		///   사용자 결정 — decision-sheet U8). 새로 갈리는 것은 여기서 곧바로 빨개진다.
		/// </summary>
		[Test]
		public void TheInspectorDefaults_MatchTheCore()
		{
			string root = FindProjectRoot();
			string core = File.ReadAllText(Path.Combine(root,
				"Assets/_WitchMendokusai/DomainSDK/Idle/IdleTuning.cs"));
			string exposed = File.ReadAllText(Path.Combine(root,
				"Assets/_WitchMendokusai/Idle/IdleTuningSO.cs"));

			Dictionary<string, string> coreDefaults = new Dictionary<string, string>();

			foreach (Match one in Regex.Matches(core,
				@"public\s+(?:double|int|long|float|bool)\s+(\w+)\s*\{\s*get;\s*set;\s*\}\s*=\s*([^;]+);"))
			{
				coreDefaults[one.Groups[1].Value] = Tidy(one.Groups[2].Value);
			}

			Assert.Greater(coreDefaults.Count, 20, "코어 기본값을 못 읽었다 — 시험이 아무것도 안 보고 있다");

			Dictionary<string, string> exposedDefaults = new Dictionary<string, string>();

			foreach (Match one in Regex.Matches(exposed,
				@"\[SerializeField\]\s+private\s+(?:double|int|long|float|bool)\s+(\w+)\s*=\s*([^;]+);"))
			{
				exposedDefaults[one.Groups[1].Value] = Tidy(one.Groups[2].Value);
			}

			string drifted = string.Empty;
			int compared = 0;

			foreach (Match one in Regex.Matches(exposed, @"^\s+(\w+)\s*=\s*(\w+),\s*$", RegexOptions.Multiline))
			{
				string knob = one.Groups[1].Value;
				string field = one.Groups[2].Value;

				if (coreDefaults.ContainsKey(knob) == false || exposedDefaults.ContainsKey(field) == false)
				{
					continue;
				}

				compared++;

				if (coreDefaults[knob] == exposedDefaults[field])
				{
					continue;
				}

				if (Array.IndexOf(KnownDrift, knob) >= 0)
				{
					continue;
				}

				drifted += (drifted.Length > 0 ? ", " : string.Empty)
					+ knob + "(코어 " + coreDefaults[knob] + " ≠ 인스펙터 " + exposedDefaults[field] + ")";
			}

			TestContext.WriteLine("[기본값] 견준 손잡이 " + compared + "개 · 알려진 어긋남 " + KnownDrift.Length + "개");

			Assert.AreEqual(string.Empty, drifted,
				"인스펙터 기본값이 코어와 다르다 — 시험이 재는 판과 사람이 노는 판이 갈린다: " + drifted);
		}

		/// <summary>
		/// <b>이미 갈려 있는</b> 것 — 어느 쪽이 맞는지는 밸런스라 사용자가 정한다 (decision-sheet U8).
		///
		/// 둘 다 「초반이 얼마나 빠른가」를 정하는 값이라, 고르는 순간 첫 10분의 감이 바뀐다.
		///   · TargetHealthByStage — 코어 3 · 인스펙터 10 (1단계 대상 체력)
		///   · BaseAttackSpeed     — 코어 3 · 인스펙터 1  (레벨 0 의 초당 타격)
		/// 합치면 사람이 노는 판의 초반 전투가 시뮬보다 <b>열 배 가까이</b> 느리다.
		/// </summary>
		private static readonly string[] KnownDrift = { "TargetHealthByStage", "BaseAttackSpeed" };

		/// <summary>견주기 좋게 다듬는다 — 공백만 지운다(꼴은 그대로 봐야 진짜 차이가 보인다).</summary>
		private static string Tidy(string text)
		{
			return text.Replace(" ", string.Empty).Trim();
		}

		/// <summary>시험이 어디서 돌든 저장소 뿌리를 찾는다 — 엔진 안팎 둘 다.</summary>
		private static string FindProjectRoot()
		{
			// ★ 유니티 안에서는 dataPath 의 부모가 곧 저장소 뿌리다 — 이걸 먼저 본다 (TASK-WM-416).
			//   예전엔 AppContext.BaseDirectory 에서 위로 훑었는데, 테스트 러너에서 그 값은
			//   *에디터 설치 폴더*(…/Unity/Hub/Editor/…/Unity.exe)라 프로젝트를 영영 못 만났다.
			//   그래서 이 파일의 검사들이 「저장소 뿌리를 못 찾았다」로 늘 빨갰다(실측 2026-08-21).
			string dataPath = UnityEngine.Application.dataPath;

			if (string.IsNullOrEmpty(dataPath) == false
				&& Directory.Exists(Path.Combine(dataPath, "_WitchMendokusai")))
			{
				return Directory.GetParent(dataPath).FullName;
			}

			// 유니티 밖(순수 dotnet)에서도 돌 수 있게 — 일하는 자리에서 위로 훑는다.
			DirectoryInfo at = new DirectoryInfo(Directory.GetCurrentDirectory());

			while (at != null)
			{
				if (Directory.Exists(Path.Combine(at.FullName, "Assets/_WitchMendokusai")))
				{
					return at.FullName;
				}

				at = at.Parent;
			}

			throw new DirectoryNotFoundException(
				"저장소 뿌리를 못 찾았다 — Assets/_WitchMendokusai 가 없다 "
				+ $"(dataPath={dataPath}, cwd={Directory.GetCurrentDirectory()})");
		}
	}
}
