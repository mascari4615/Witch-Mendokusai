// 대화 하네스 진입점 — 유니티 없이 순수 로직 시험을 돌리고, 진짜 원고까지 훑는다.
using System;
using System.IO;

internal static class Program
{
	private static int passed;
	private static int failed;

	private static void Main(string[] args)
	{
		string repoRoot = FindRepoRoot();

		Console.WriteLine("== EditMode 시험 파일 (원본 그대로 실행) ==");
		(int testPassed, int testFailed) = EditModeRunner.RunAll();
		passed += testPassed;
		failed += testFailed;

		Console.WriteLine();
		Console.WriteLine("== 게임에 들어간 원고 (전부) ==");
		ShippedScript.Run(Path.Combine(repoRoot, "Assets", "_WitchMendokusai"), Check);

		Console.WriteLine();
		Console.WriteLine("== 설계 문서 원고 (있으면) ==");
		RealDocs.Run(FindNarrativeDocs(repoRoot));

		Console.WriteLine();
		Console.WriteLine($"passed={passed} failed={failed}");
		Environment.Exit(failed == 0 ? 0 : 1);
	}

	private static void Check(string name, bool ok)
	{
		if (ok)
		{
			passed++;
			Console.WriteLine($"  PASS  {name}");
			return;
		}
		failed++;
		Console.WriteLine($"  FAIL  {name}");
	}

	/// <summary>
	/// 설계 문서 원고 폴더. 이 저장소 밖(형제 `memo/`)에 있고, 작업 트리 배치가 사람마다 달라
	/// 몇 군데를 훑어본다. 없으면 그 부분만 건너뛴다 — 저장소만 받은 사람도 하네스는 돌아야 한다.
	/// </summary>
	private static string FindNarrativeDocs(string repoRoot)
	{
		string[] candidates =
		{
			Path.Combine(repoRoot, "..", "memo", "wm", "design", "narrative"),
			Path.Combine(repoRoot, "..", "karmoddrine", "memo", "wm", "design", "narrative"),
			Path.Combine(repoRoot, "..", "..", "karmoddrine", "memo", "wm", "design", "narrative"),
			Path.Combine(repoRoot, "..", "..", "memo", "wm", "design", "narrative"),
		};
		foreach (string candidate in candidates)
		{
			string full = Path.GetFullPath(candidate);
			if (Directory.Exists(full))
			{
				return full;
			}
		}
		return Path.GetFullPath(candidates[0]);
	}

	/// <summary>이 프로젝트 루트(Assets 가 있는 곳). 어디서 실행해도 같은 것을 보게.</summary>
	private static string FindRepoRoot()
	{
		DirectoryInfo directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "Assets", "_WitchMendokusai")))
			{
				return directory.FullName;
			}
			directory = directory.Parent;
		}
		return Directory.GetCurrentDirectory();
	}
}
