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
		private const string SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Idle/IdleV2.unity";
		private const string DEFAULT_DIR = "C:/wm-builds/idle";
		private const string EXE_NAME = "Idle.exe";
		private const string TAG = "[IdleBuild]";

		/// <summary>「이건 본편이 아니다」 — 본편 진단 장치가 이 빌드에서 안 뜨게 하는 표식.</summary>
		private const string IDLE_DEFINE = "WM_IDLE";

		/// <summary>
		/// <b>빨리 보기</b> 판을 고르는 환경변수 (`mono`). 비우면 파는 판(IL2CPP)이다.
		///
		/// ★ 왜 두 갈래인가 (사용자 컨펌 2026-08-17) — 노트북 한 판이 <b>12.7분</b>이고
		///   그 대부분이 IL2CPP(C# → C++ 재컴파일)다. 눈으로 한 번 보려고 매번 그걸 기다릴 이유가 없다.
		///
		/// ⚠ 그런데 <b>이 판은 검사가 아니다</b>. 여태 런타임을 깨뜨린 것은 IL2CPP + 덜어내기(High)
		///   조합이었다. Mono 판만 굽고 초록을 받으면 <b>파는 물건은 안 본 채</b> 초록이 된다.
		///   그래서 ① 폴더 이름에 `-mono` 를 박아 섞이지 않게 하고 ② 부르는 쪽(워크플로)이
		///   이 판에서는 「켜서 도나」 검사를 <b>건너뛰되 그 사실을 경고로 남긴다</b>.
		///   또 Mono 는 C# 그대로라 <b>디컴파일이 한 방</b>이다 — 파는 판으로 쓰면 안 된다.
		/// </summary>
		private const string BACKEND_ENV = "WM_IDLE_BACKEND";

		/// <summary>저장 삭제. 디버그. 플레이 중이 아닐 때용 (플레이 중엔 화면 버튼). 메뉴 경로는 영문만 (rules/unity.md)</summary>
		[MenuItem("WM/Idle/Wipe Save Data")]
		public static void WipeSave()
		{
			if (EditorUtility.DisplayDialog("Idle 데이터 초기화",
				"저장 파일(본 저장, .bak, .broken)을 지운다. 되돌릴 수 없다.", "지운다", "취소") == false)
			{
				return;
			}

			int deleted = WitchMendokusai.IdleSaveStore.Wipe();
			Debug.Log("[Idle] 데이터 초기화: " + deleted + "개 파일 삭제");
		}

		[MenuItem("WM/Idle/Build (This Game Only)")]
		public static void Build()
		{
			// ★ 지어 놓고 빈 씬을 굽는 일이 실제로 있었다 — 굽기 전에 붙을 것이 붙었는지 본다.
			if (IdleV2SceneBuilder.Verify() == false)
			{
				Fail("씬 검사가 빨갛다 — 이대로 구우면 빈 화면이 나온다");
				return;
			}

			string directory = Environment.GetEnvironmentVariable("WM_IDLE_BUILD_DIR");
			if (string.IsNullOrWhiteSpace(directory))
			{
				directory = DEFAULT_DIR;
			}

			bool quickLook = string.Equals(
				Environment.GetEnvironmentVariable(BACKEND_ENV), "mono", StringComparison.OrdinalIgnoreCase);

			directory = Path.Combine(directory,
				DateTime.Now.ToString("yyyyMMdd-HHmmss") + (quickLook ? "-mono" : string.Empty));
			Directory.CreateDirectory(directory);

			string exePath = Path.Combine(directory, EXE_NAME);

			BuildPlayerOptions options = new BuildPlayerOptions();
			options.scenes = new string[] { SCENE_PATH };
			options.locationPathName = exePath;
			options.target = BuildTarget.StandaloneWindows64;
			options.targetGroup = BuildTargetGroup.Standalone;
			options.options = BuildOptions.None;

			// ★ <b>이건 본편이 아니다</b>라고 코드에 알린다 — 이 빌드에만 붙는 표식이다.
			//   본편 진단 장치는 어디서나 스스로 뜬다(씬에 아무것도 안 놔도). 실측 2026-08-16:
			//   방치형 exe 를 켜니 `[DeviceLog] 전송 실패 (401)` 이 찍혔다 —
			//   <b>팔 게임이 남의 서버를 부르고 있었다.</b> 빌드 도장 표시기도 같이 뜬다.
			//   본편에는 그대로 필요하므로 지우지 않고 이 빌드에서만 안 뜨게 한다.
			//
			// ⚠ `PlayerSettings.SetScriptingDefineSymbols` 로 하면 <b>안 된다</b> (실측):
			//   그 자리에서 재컴파일이 걸려 빌드가 「Unknown (에러 0개)」로 죽는다.
			//   `extraScriptingDefines` 가 바로 이 용도이고, 프로젝트 설정을 아예 안 건드린다.
			options.extraScriptingDefines = new string[] { IDLE_DEFINE };

			// ★ 안 쓰는 코드를 <b>이 빌드에서만</b> 덜어낸다.
			//   방치형은 씬 하나에 UI 뿐인데 FishNet·PlayFab·FMOD·복셀까지 통째로 구워진다
			//   (실측 2026-08-16: GameAssembly.dll 98.8MB · 배포분 233.8MB).
			//   ⚠ 프로젝트 전역 설정이라 <b>본편 빌드에 같이 영향</b>한다 — 그래서 굽고 나서 되돌린다.
			//   되돌리기를 빠뜨리면 본편이 조용히 다른 설정으로 구워진다.
			NamedBuildTarget named = NamedBuildTarget.Standalone;
			ManagedStrippingLevel before = PlayerSettings.GetManagedStrippingLevel(named);
			PlayerSettings.SetManagedStrippingLevel(named, ManagedStrippingLevel.High);

			// ★ 빨리 보기 판만 Mono 로 내린다. 이것도 전역 설정이라 <b>반드시 되돌린다</b> —
			//   빠뜨리면 다음 본편 빌드가 조용히 Mono 로 구워져 팔 물건이 뜯기는 채로 나간다.
			ScriptingImplementation backendBefore = PlayerSettings.GetScriptingBackend(named);
			if (quickLook)
			{
				PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
			}

			Debug.Log(TAG + " 굽는다 (덜어내기 " + before + " → High · 방식 "
				+ (quickLook ? "Mono <빨리 보기 — 파는 판 아님>" : backendBefore.ToString()) + ") → " + exePath);

			BuildReport report;
			try
			{
				report = BuildPipeline.BuildPlayer(options);
			}
			finally
			{
				PlayerSettings.SetManagedStrippingLevel(named, before);
				PlayerSettings.SetScriptingBackend(named, backendBefore);
				Debug.Log(TAG + " 설정을 되돌렸다 (덜어내기 " + before + " · 방식 " + backendBefore + " · 표식 원복)");
			}

			// ★ 굽는 순간이 유일한 기회다 — 유니티 6 은 빌드 보고서를 파일로 안 남긴다 (TASK-WM-409).
			//   ⚠ 이 줄은 한 번 <b>병합에서 유실됐다</b>(`71cdaa6c`) — 사라지면 예산 시험이
			//     조용히 Ignore 로 넘어가고, 크기가 늘어도 아무도 모른다.
			BuildInventory.Write(report);

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
			// ★ 지난 판이 남긴 <b>배포 안 하는 폴더</b>를 치운다 (실측 2026-08-16).
			//   한 판에 2GB 가 남고, 14판을 굽자 디스크 여유가 <b>486MB</b> 까지 떨어져
			//   IL2CPP 링크가 실패했다("Building GameAssembly.pdb failed") —
			//   에러 메시지는 디스크와 아무 상관 없어 보였다.
			//   유니티가 폴더 이름에 「배포하지 말라」고 적어 둔 것들이니 굽고 나면 남길 이유가 없다.
			SweepOldLeftovers(Path.GetDirectoryName(directory), directory);

			double shipped = SizeOf(directory, true) / 1024d / 1024d;
			double everything = SizeOf(directory, false) / 1024d / 1024d;

			// ★ 「구웠다」는 「돈다」가 아니다 — 특히 덜어내기를 High 로 올렸으니 더 그렇다.
			//   켜서 판이 흐르는지는 `.github/scripts/wm-idle-smoke.ps1` 이 본다(실패 경로 셋 다 밟아 봤다).
			//   여기서 자동으로 부르지 않는 이유: 배치 유니티 안에서 플레이어를 또 띄우면
			//   같은 기계에서 창·그래픽 자원을 두 벌 잡는다. 굽고 나서 따로 부른다.
			Debug.Log(TAG + " 다음 — 실제로 도는지: powershell -File .github/scripts/wm-idle-smoke.ps1");

			if (quickLook)
			{
				Debug.LogWarning(TAG + " ⚠ 이 판은 <빨리 보기(Mono)>다 — 파는 판(IL2CPP)이 아니고 검사로도 못 쓴다");
			}

			// ★ <b>어느 판을 구웠는지 글로 남긴다</b> (실측 2026-08-17).
			//   부르는 쪽(워크플로)이 「가장 최근에 고쳐진 폴더」로 고르면 <b>틀린 판</b>을 집는다 —
			//   바로 위 <see cref="SweepOldLeftovers"/> 가 <b>지난 판 폴더를 건드려</b> 그 시각을
			//   갱신하기 때문이다. 실제로 방금 구운 042844 대신 직전 030659 가 검사됐다.
			//   폴더 시각은 믿을 게 못 되고, 구운 쪽이 아는 사실을 그대로 적어 주는 게 맞다.
			WriteLastBuildMark(Path.GetDirectoryName(directory), exePath);

			Debug.Log(TAG + " ✅ 됐다 — " + exePath
				+ " (배포분 " + shipped.ToString("N1") + " MB · 폴더 전체 " + everything.ToString("N1") + " MB · "
				+ summary.totalTime.TotalSeconds.ToString("N0") + "초)");
		}

		/// <summary>방금 구운 판의 경로를 남긴다 — 부르는 쪽이 추측하지 않게.</summary>
		private static void WriteLastBuildMark(string root, string exePath)
		{
			try
			{
				File.WriteAllText(Path.Combine(root, "last-build.txt"), exePath);
			}
			catch (System.Exception error)
			{
				// 표시를 못 남겨도 빌드를 세우지는 않는다 — 부르는 쪽이 옛 방식으로 되돌아갈 뿐이다.
				Debug.LogWarning(TAG + " 표시 남기기 실패(무시): " + error.Message);
			}
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

		/// <summary>지난 판들의 「배포하지 말 것」 폴더를 치운다. 이번 판은 안 건드린다.</summary>
		private static void SweepOldLeftovers(string root, string keeping)
		{
			if (Directory.Exists(root) == false)
			{
				return;
			}

			long freed = 0L;

			foreach (string one in Directory.GetDirectories(root))
			{
				if (string.Equals(one, keeping, System.StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				foreach (string junk in Directory.GetDirectories(one))
				{
					if (IsNotShipped(junk) == false)
					{
						continue;
					}

					try
					{
						freed += SizeOf(junk, false);
						Directory.Delete(junk, true);
					}
					catch (System.Exception error)
					{
						// 못 치워도 빌드를 세우지는 않는다 — 치우기는 곁일이다.
						Debug.LogWarning(TAG + " 지난 판 정리 실패(무시): " + error.Message);
					}
				}
			}

			if (freed > 0L)
			{
				Debug.Log(TAG + " 지난 판이 남긴 " + (freed / 1024d / 1024d / 1024d).ToString("N1")
					+ " GB 를 치웠다 (배포 안 하는 폴더)");
			}
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
