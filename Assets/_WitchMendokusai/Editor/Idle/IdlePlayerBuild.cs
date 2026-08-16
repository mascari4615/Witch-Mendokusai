using System;
using System.IO;
using UnityEditor;
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

			Debug.Log(TAG + " 굽는다 → " + exePath);
			BuildReport report = BuildPipeline.BuildPlayer(options);
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

			double megabytes = summary.totalSize / 1024d / 1024d;
			Debug.Log(TAG + " ✅ 됐다 — " + exePath
				+ " (" + megabytes.ToString("N1") + " MB · "
				+ summary.totalTime.TotalSeconds.ToString("N0") + "초)");
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
