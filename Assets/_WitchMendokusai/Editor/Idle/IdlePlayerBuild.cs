using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 방치 게임만 <b>따로</b> 굽는다 (TASK-WM-406).
	///
	/// ★ 왜 본편 빌드가 아닌가 — 본편은 씬 다섯에 복셀·지형·에셋이 통째로 딸려 와
	///   한 판에 수십 분이고, 요즘은 그 중에 죽는다(exit -1073741819). 방치형을 만져 보려고
	///   그걸 매번 기다릴 이유가 없다. 그리고 <b>이 게임은 원래 따로 낼 물건</b>이다
	///   (2027-02 까지 「출시한 게임 하나」). 그러니 따로 굽는 길이 임시방편이 아니라 정본이다.
	///
	/// ★ 씬 목록을 <b>이 씬 하나로</b> 준다 — 빌드 설정을 안 건드린다(본편 빌드가 그대로 산다).
	///
	/// 배치: Unity -batchmode -quit -executeMethod WitchMendokusai.EditorTools.IdlePlayerBuild.Build
	///   출력 폴더는 환경변수 WM_IDLE_BUILD_DIR 로 바꿀 수 있다.
	/// </summary>
	public static class IdlePlayerBuild
	{
		private const string SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Idle/Idle.unity";
		private const string DEFAULT_DIR = "C:/wm-builds/idle";
		private const string EXE_NAME = "Idle.exe";
		private const string TAG = "[IdleBuild]";

		[MenuItem("WM/Idle/빌드 (이 게임만)")]
		public static void Build()
		{
			// ★ 지어 놓고 빈 씬을 굽는 일이 실제로 있었다 — 굽기 전에 붙을 것이 붙었는지 본다.
			if (IdleSceneBuilder.Verify() == false)
			{
				Fail("씬 검사가 빨갛다 — 이대로 구우면 빈 화면이 나온다");
				return;
			}

			string directory = Environment.GetEnvironmentVariable("WM_IDLE_BUILD_DIR");
			if (string.IsNullOrWhiteSpace(directory))
			{
				directory = DEFAULT_DIR;
			}

			directory = Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
			Directory.CreateDirectory(directory);

			string exePath = Path.Combine(directory, EXE_NAME);

			BuildPlayerOptions options = new BuildPlayerOptions();
			options.scenes = new string[] { SCENE_PATH };
			options.locationPathName = exePath;
			options.target = BuildTarget.StandaloneWindows64;
			options.targetGroup = BuildTargetGroup.Standalone;
			options.options = BuildOptions.None;

			// ★ 안 쓰는 코드를 <b>이 빌드에서만</b> 덜어낸다.
			//   방치형은 씬 하나에 UI 뿐인데 FishNet·PlayFab·FMOD·복셀까지 통째로 구워진다
			//   (실측 2026-08-16: GameAssembly.dll 98.8MB · 배포분 233.8MB).
			//   ⚠ 프로젝트 전역 설정이라 <b>본편 빌드에 같이 영향</b>한다 — 그래서 굽고 나서 되돌린다.
			//   되돌리기를 빠뜨리면 본편이 조용히 다른 설정으로 구워진다.
			NamedBuildTarget named = NamedBuildTarget.Standalone;
			ManagedStrippingLevel before = PlayerSettings.GetManagedStrippingLevel(named);
			PlayerSettings.SetManagedStrippingLevel(named, ManagedStrippingLevel.High);

			Debug.Log(TAG + " 굽는다 (덜어내기 " + before + " → High) → " + exePath);

			BuildReport report;
			try
			{
				report = BuildPipeline.BuildPlayer(options);
			}
			finally
			{
				PlayerSettings.SetManagedStrippingLevel(named, before);
				Debug.Log(TAG + " 덜어내기 설정을 " + before + " 로 되돌렸다");
			}

			BuildSummary summary = report.summary;

			if (summary.result != BuildResult.Succeeded)
			{
				Fail("빌드 실패 — " + summary.result + " (에러 " + summary.totalErrors + "개)");
				return;
			}

			// 「성공」만 믿지 않는다 — 파일이 실제로 있는지 본다.
			if (File.Exists(exePath) == false)
			{
				Fail("성공이라는데 exe 가 없다: " + exePath);
				return;
			}

			// ★ `summary.totalSize` 는 <b>배포 안 하는 것까지</b> 센다 (실측 2026-08-16).
			//   `Idle_BackUpThisFolder_ButDontShipItWithYourGame` 하나가 2.6GB 다 —
			//   IL2CPP 중간 소스와 pdb 라 유니티가 폴더 이름으로 「배포하지 말라」고 적어 뒀다.
			//   그걸 합쳐 「2.8GB」라고 보고하면 <b>도구가 거짓말을 하는 것</b>이고,
			//   실제로 그 숫자를 보고 「군살이 많다」고 잘못 판단했다. 둘을 나눠 적는다.
			double shipped = SizeOf(directory, true) / 1024d / 1024d;
			double everything = SizeOf(directory, false) / 1024d / 1024d;

			// ★ 「구웠다」는 「돈다」가 아니다 — 특히 덜어내기를 High 로 올렸으니 더 그렇다.
			//   켜서 판이 흐르는지는 `.github/scripts/wm-idle-smoke.ps1` 이 본다(실패 경로 셋 다 밟아 봤다).
			//   여기서 자동으로 부르지 않는 이유: 배치 유니티 안에서 플레이어를 또 띄우면
			//   같은 기계에서 창·그래픽 자원을 두 벌 잡는다. 굽고 나서 따로 부른다.
			Debug.Log(TAG + " 다음 — 실제로 도는지: powershell -File .github/scripts/wm-idle-smoke.ps1");

			Debug.Log(TAG + " ✅ 됐다 — " + exePath
				+ " (배포분 " + shipped.ToString("N1") + " MB · 폴더 전체 " + everything.ToString("N1") + " MB · "
				+ summary.totalTime.TotalSeconds.ToString("N0") + "초)");
		}

		/// <summary>
		/// 폴더 크기. <paramref name="shippedOnly"/> 면 <b>배포 안 하는 폴더</b>를 뺀다 —
		/// 유니티가 이름에 그렇게 적어 둔 것들이다.
		/// </summary>
		private static long SizeOf(string directory, bool shippedOnly)
		{
			long total = 0L;

			foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
			{
				if (shippedOnly && IsNotShipped(path))
				{
					continue;
				}

				total += new FileInfo(path).Length;
			}

			return total;
		}

		private static bool IsNotShipped(string path)
		{
			return path.Contains("BackUpThisFolder_ButDontShipItWithYourGame")
				|| path.Contains("BurstDebugInformation_DoNotShip");
		}

		private static void Fail(string reason)
		{
			Debug.LogError(TAG + " " + reason);
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(1);
			}
		}
	}
}
