using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// Addressables 콘텐츠 빌드를 **플레이어 빌드와 떼어내서** 한 번 돌려보고, 실패하면 그 이유를 찍는다.
	///
	/// ★ 왜 (TASK-WM-165 item 12, 2026-08-06): `wm-boot-smoke -Build` 가
	///   `BuildFailedException: Failed to build Addressables content` 로 죽는데,
	///   **정작 무엇이 틀렸는지는 로그에 한 글자도 안 남는다** — `SBP ErrorException` 의 메시지가 비어 있고
	///   로그 전체 예외가 7건뿐이라 더 캘 정보가 없다.
	///
	///   그 상태에서 원인 후보를 계속 추측했고 **네 번 다 틀렸다**(모듈 없음 / 활성 타겟 / 라이선스 /
	///   원격 카탈로그). 다섯 번째 추측 대신 **도구를 만든다** — `BuildPlayer` 를 거치지 않고
	///   `BuildPlayerContent` 를 직접 불러 `result.Error` 를 그대로 받아 찍는다.
	///   플레이어 빌드 파이프라인이 예외를 삼키는 층을 통째로 건너뛰는 게 요점이다.
	///
	/// 사용(배치모드):
	///   Unity.exe -batchmode -quit -projectPath &lt;프로젝트&gt; -logFile &lt;로그&gt; \
	///     -executeMethod WitchMendokusai.EditorTools.AddressablesDiagnose.RunFromCLI
	///   exit 0 = 콘텐츠 빌드 성공 / 1 = 실패(이유는 로그의 [addr-diag] 줄).
	/// </summary>
	public static class AddressablesDiagnose
	{
		private const string LOG_PREFIX = "[addr-diag]";

		[MenuItem("WM/Diagnose/Build Addressables Content Only")]
		private static void RunFromMenu()
		{
			Run();
		}

		public static void RunFromCLI()
		{
			bool succeeded = Run();
			EditorApplication.Exit(succeeded ? 0 : 1);
		}

		private static bool Run()
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				Debug.LogError($"{LOG_PREFIX} AddressableAssetSettings 가 null — Addressables 가 이 프로젝트에 설정돼 있지 않다.");
				return false;
			}

			DumpConfiguration(settings);

			// ★ 여기가 요점: 플레이어 빌드를 거치지 않고 콘텐츠 빌드만 직접 부른다.
			//   `BuildPipeline.BuildPlayer` 경로는 이 실패를 `BuildFailedException` 으로 감싸면서
			//   원래 메시지를 잃어버린다. 여기선 `result.Error` 를 날것으로 받는다.
			AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

			if (result == null)
			{
				Debug.LogError($"{LOG_PREFIX} 결과 객체가 null 이다 — 빌드가 시작조차 못 했다는 뜻.");
				return false;
			}

			Debug.Log($"{LOG_PREFIX} 소요 {result.Duration:F2}s · 등록된 location {result.LocationCount}개");

			if (string.IsNullOrEmpty(result.Error) == false)
			{
				Debug.LogError($"{LOG_PREFIX} FAILED — 이유:");
				Debug.LogError($"{LOG_PREFIX}   {result.Error}");
				return false;
			}

			Debug.Log($"{LOG_PREFIX} OK — 콘텐츠 빌드 성공. 부팅 스모크 실패는 다른 층에 있다.");
			return true;
		}

		/// <summary>
		/// SBP 가 마지막으로 보는 값을 **직접** 들여다본다 — 둘 중 무엇이냐로 고침이 갈리기 때문이다:
		///   ① `buildWindowExtension == null`        → 배치모드엔 빌드 창 확장이 없다(구조적 한계)
		///   ② `EnabledBuildButton() == false`       → 확장은 있는데 「빌드 못 하는 상태」라고 답한다
		/// `ModuleManager` 는 internal 이라 리플렉션으로 본다. 진단 전용이고, 못 읽으면 못 읽었다고 적는다
		/// (여기서 조용히 넘어가면 이 진단기도 「아무것도 안 보고 통과」가 된다).
		/// </summary>
		private static void AppendBuildWindowExtensionProbe(StringBuilder builder, BuildTarget target, BuildTargetGroup group)
		{
			builder.AppendLine($"{LOG_PREFIX} ── SBP 가 보는 마지막 값 (리플렉션) ──");

			System.Type moduleManagerType = typeof(EditorWindow).Assembly.GetType("UnityEditor.Modules.ModuleManager");
			if (moduleManagerType == null)
			{
				builder.AppendLine($"{LOG_PREFIX}   ModuleManager 타입을 못 찾았다 — 이 유니티에선 이 경로로 확인 불가.");
				return;
			}

			object moduleName = InvokeStatic(moduleManagerType, "GetTargetStringFrom", new object[] { target })
				?? InvokeStatic(moduleManagerType, "GetTargetStringFrom", new object[] { group, target });
			builder.AppendLine($"{LOG_PREFIX}   module 이름 : {(moduleName == null ? "(못 읽음)" : moduleName.ToString())}");

			if (moduleName == null)
			{
				return;
			}

			object extension = InvokeStatic(moduleManagerType, "GetBuildWindowExtension", new object[] { moduleName });
			builder.AppendLine($"{LOG_PREFIX}   buildWindowExtension : {(extension == null ? "**null** ← ① 구조적(배치모드엔 창이 없다)" : extension.GetType().Name)}");

			if (extension == null)
			{
				return;
			}

			System.Reflection.MethodInfo enabled = extension.GetType().GetMethod("EnabledBuildButton");
			if (enabled == null)
			{
				builder.AppendLine($"{LOG_PREFIX}   EnabledBuildButton 메서드를 못 찾았다.");
				return;
			}

			object value = enabled.Invoke(extension, null);
			builder.AppendLine($"{LOG_PREFIX}   EnabledBuildButton() : {value} ← false 면 ② (확장은 있는데 빌드 불가라고 답함)");

			if (value is bool enabledValue && enabledValue == false)
			{
				// ★ 확장이 「빌드 못 한다」고 답하는 흔한 이유들을 그대로 찍는다.
				//   데스크톱 확장의 `EnabledBuildButton()` 은 대개 「Install in Build Folder」 같은
				//   *숨은 에디터 설정* 하나로 false 가 된다 — 그건 프로젝트 설정이라 **고칠 수 있다.**
				builder.AppendLine($"{LOG_PREFIX}   ── 그 false 를 만들 만한 값들 ──");
				builder.AppendLine($"{LOG_PREFIX}     installInBuildFolder : {EditorUserBuildSettings.installInBuildFolder}  ← true 면 빌드 버튼이 꺼진다");
				builder.AppendLine($"{LOG_PREFIX}     development          : {EditorUserBuildSettings.development}");
				builder.AppendLine($"{LOG_PREFIX}     buildAppBundle       : {EditorUserBuildSettings.buildAppBundle}");
				builder.AppendLine($"{LOG_PREFIX}     selectedStandaloneTarget : {EditorUserBuildSettings.selectedStandaloneTarget}");
				builder.AppendLine($"{LOG_PREFIX}     activeScriptCompilationDefines 수 : {EditorUserBuildSettings.activeScriptCompilationDefines.Length}");

				// 유니티 6 은 **빌드 프로파일**이 빌드 버튼을 가른다. 활성 프로파일이 깨져 있으면
				// 확장이 「빌드 못 함」이라고 답할 수 있다 — 위 설정들이 다 정상이라 여기가 다음 자리다.
				object activeProfile = ReadStaticProperty(typeof(EditorUserBuildSettings), "activeBuildProfile");
				builder.AppendLine($"{LOG_PREFIX}     activeBuildProfile : {(activeProfile == null ? "null (= 클래식 설정 사용)" : activeProfile.ToString())}");

				// 확장 인스턴스가 들고 있는 상태 — 배치모드에서 초기화가 안 된 흔적이 있는지 본다.
				foreach (System.Reflection.FieldInfo field in extension.GetType().GetFields(
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
				{
					object fieldValue = field.GetValue(extension);
					builder.AppendLine($"{LOG_PREFIX}     ext.{field.Name} = {(fieldValue == null ? "null" : fieldValue.ToString())}");
				}
			}
		}

		private static object ReadStaticProperty(System.Type type, string propertyName)
		{
			System.Reflection.PropertyInfo property = type.GetProperty(
				propertyName,
				System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (property == null)
			{
				return null;
			}

			return property.GetValue(null);
		}

		private static object InvokeStatic(System.Type type, string methodName, object[] args)
		{
			System.Type[] argTypes = new System.Type[args.Length];
			for (int i = 0; i < args.Length; i++)
			{
				argTypes[i] = args[i].GetType();
			}

			System.Reflection.MethodInfo method = type.GetMethod(
				methodName,
				System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
				null,
				argTypes,
				null);

			if (method == null)
			{
				return null;
			}

			return method.Invoke(null, args);
		}

		/// <summary>
		/// 실패 이유가 설정에 있으면 여기 보인다. 그룹마다 스키마가 몇 개인지까지 찍는 이유:
		/// 스키마 0 인 그룹은 빌드에서 통째로 빠지는데, 로그엔 경고 한 줄로만 흐른다.
		/// </summary>
		private static void DumpConfiguration(AddressableAssetSettings settings)
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendLine($"{LOG_PREFIX} ── 설정 ──");

			// ★ SBP 가 실제로 보는 조건 (2026-08-06 소스 추적으로 확정).
			//   `ContentPipeline.CanBuildPlayer` 는 이렇게 생겼다:
			//       if (IsBuildTargetSupported(group, target) == false) { 경고; return true; }   ← 모듈 없음 = 허용
			//       return buildWindowExtension != null ? buildWindowExtension.EnabledBuildButton() : false;
			//   즉 **모듈이 없어서 죽는 게 아니다**(그 경우 통과시킨다). 죽는 건 두 번째 줄이다.
			//   그래서 여기서 첫 줄의 값을 찍어 **어느 가지로 갔는지**를 확정한다.
			BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
			BuildTargetGroup activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
			bool targetSupported = BuildPipeline.IsBuildTargetSupported(activeGroup, activeTarget);

			builder.AppendLine($"{LOG_PREFIX} activeBuildTarget : {activeTarget} (group {activeGroup})");
			builder.AppendLine($"{LOG_PREFIX} IsBuildTargetSupported : {targetSupported}");
			if (targetSupported)
			{
				builder.AppendLine($"{LOG_PREFIX}   → 모듈은 있다. 그러면 SBP 는 buildWindowExtension.EnabledBuildButton() 을 본다.");
				builder.AppendLine($"{LOG_PREFIX}   → 배치모드에서 그게 null 이거나 false 면 「Unable to build with the current configuration」 이 난다.");
			}
			else
			{
				builder.AppendLine($"{LOG_PREFIX}   → 모듈이 없다. 이 가지는 SBP 가 **통과시킨다**(return true) — 원인이 아니다.");
			}

			AppendBuildWindowExtensionProbe(builder, activeTarget, activeGroup);
			builder.AppendLine($"{LOG_PREFIX} BuildRemoteCatalog : {settings.BuildRemoteCatalog}");
			builder.AppendLine($"{LOG_PREFIX} ActivePlayerDataBuilderIndex : {settings.ActivePlayerDataBuilderIndex}");

			IDataBuilder activeBuilder = settings.ActivePlayerDataBuilder;
			builder.AppendLine($"{LOG_PREFIX} ActivePlayerDataBuilder : {(activeBuilder == null ? "null" : activeBuilder.Name)}");

			// ★ 활성 빌더가 *플레이어 빌드를 할 수 있는* 종류인지 확인한다.
			//   PlayMode 전용 빌더(에셋 DB 모드 등)가 활성이면 콘텐츠 빌드가 「현재 구성으로는 빌드 불가」로 죽는다.
			if (activeBuilder != null)
			{
				bool canBuildPlayer = activeBuilder.CanBuildData<AddressablesPlayerBuildResult>();
				builder.AppendLine($"{LOG_PREFIX} 이 빌더가 플레이어 콘텐츠를 만들 수 있나 : {canBuildPlayer}");
				if (canBuildPlayer == false)
				{
					builder.AppendLine($"{LOG_PREFIX}   ⚠ 못 만든다 — 이게 원인일 수 있다(PlayMode 전용 빌더가 활성).");
				}
			}

			List<AddressableAssetGroup> groups = settings.groups;
			builder.AppendLine($"{LOG_PREFIX} 그룹 {(groups == null ? 0 : groups.Count)}개");
			if (groups != null)
			{
				foreach (AddressableAssetGroup group in groups)
				{
					if (group == null)
					{
						builder.AppendLine($"{LOG_PREFIX}   (null 그룹 — 목록이 깨져 있다)");
						continue;
					}

					int schemaCount = group.Schemas == null ? 0 : group.Schemas.Count;
					string mark = schemaCount == 0 ? "  ← 스키마 0 (빌드에서 빠짐)" : string.Empty;
					builder.AppendLine($"{LOG_PREFIX}   {group.Name} : 스키마 {schemaCount} · 엔트리 {group.entries.Count}{mark}");
				}
			}

			Debug.Log(builder.ToString());
		}
	}
}
