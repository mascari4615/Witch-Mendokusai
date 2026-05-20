using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace WitchMendokusai.Editor.DI
{
	/// <summary>
	/// TASK-WM-109-C — 씬 직접배치 컴포넌트의 DI 등록 누락을 *자동* 감지.
	///
	/// 배경: TASK-WM-109 이슈 3 — World.unity 에 직접 배치된 컴포넌트 (Dummy/MineralBase/
	/// InteractiveMarker/AutoAimMarker 등) 가 SceneLifetimeScope 에 등록 누락되면
	/// 부팅 시 [Inject] Construct 호출 0 → 매니저 ref 가 null → 사용 시점 NRE.
	/// 매번 NRE → stack trace → .meta GUID 검색 → .unity grep → 등록 추가 → 재발 사이클.
	/// 본 도구가 정본 SceneLifetimeScope.cs 와 씬 placement 를 *교차 검증* 해 사이클 종결.
	///
	/// 정본 = SceneLifetimeScope.cs *소스* (regex 파싱). 별도 registry 도입 X — 자기참조
	/// 패턴 (resgistration 추가 = audit 자동 인식). SceneLifetimeScope 가 다음 3 카테고리로
	/// 씬 컴포넌트의 [Inject] 를 해소:
	///   ① 「Directly Covered」 = Register*&lt;T&gt; / 명시적 container.Inject(x) foreach —
	///      *이 타입 컴포넌트 1 개만* [Inject] 해소. sibling/child 별도 처리 필요.
	///   ② 「Hierarchy Covered」 = container.InjectGameObject(x.gameObject) foreach —
	///      VContainer 표준 *whole-hierarchy* 주입 → 본인 + 자손 컴포넌트 모두 해소.
	///   ③ 「Root Scope」 = RootLifetimeScope (DDOL 영역, 본 감사 범위 외).
	///
	/// 「Inject-needing」 컴포넌트 = MonoBehaviour 서브클래스 중 [Inject] 가 멤버
	/// (field / property / method) 에 1+ 존재. 씬에 그런 컴포넌트가 *카테고리 ①·② 어디에도*
	/// 커버되지 않으면 NRE 후보 → 보고. 「Inject-needing」 자체 reflection 으로 결정 (수동
	/// 유지 X). InteractiveMarker 등 known 타입이 누락 분기에 들어가면 즉시 회귀 발견.
	///
	/// 한계: container.Inject (component-only) 가 자식까지 *cascade* 하는 경우 (Player.Construct
	/// → 자식 코드 호출 패턴) 는 코드 의도여서 정적 audit 으로 판정 불가 — 그 cascade 대상이
	/// SceneLifetimeScope 에 명시 안 됐어도 본 도구는 보고함 (false-positive 가능). 해소 =
	/// 보고된 컴포넌트의 부모 GameObject 가 cascade 경로면 SceneLifetimeScope 에 「container.Inject」
	/// 라인 한 줄 추가 (자기참조 sync).
	/// </summary>
	public static class SceneDiAuditor
	{
		private const string SCENE_LIFETIME_SCOPE_PATH =
			"Assets/_WitchMendokusai/Domain/Application/Scripts/DI/SceneLifetimeScope.cs";

		private const string ROOT_LIFETIME_SCOPE_PATH =
			"Assets/_WitchMendokusai/Domain/Application/Scripts/DI/RootLifetimeScope.cs";

		// 「scene-direct」 = 본 폴더 하위 모든 .unity. 패키지 / 외부 씬 제외.
		public const string SCENES_ROOT = "Assets/_WitchMendokusai/Scenes";

		private static readonly Regex DIRECT_REGISTER_PATTERN = new Regex(
			@"(?:RegisterInHierarchyIfPresent|RegisterComponentInNewPrefab|RegisterComponentOnNewGameObject|RegisterLeaf|ResolveIfPresent|BootGuard\.EagerResolve|Resources\.Load|builder\.Register)<(\w+)>",
			RegexOptions.Compiled);

		// container.Inject(x) foreach — component-only. `\s` 가 LF/CR/TAB/SPACE 포함, 한 줄/여러 줄 모두 매치.
		private static readonly Regex INJECT_FOREACH_PATTERN = new Regex(
			@"foreach\s*\(\s*(\w+)\s+\w+\s+in\s+FindObjectsByType<\1>\s*\([^)]*\)\s*\)\s*container\.Inject\(\s*\w+\s*\)\s*;",
			RegexOptions.Compiled | RegexOptions.Singleline);

		// container.InjectGameObject(x.gameObject) foreach — whole-hierarchy.
		private static readonly Regex INJECT_GAMEOBJECT_FOREACH_PATTERN = new Regex(
			@"foreach\s*\(\s*(\w+)\s+\w+\s+in\s+FindObjectsByType<\1>\s*\([^)]*\)\s*\)\s*container\.InjectGameObject\(",
			RegexOptions.Compiled | RegexOptions.Singleline);

		/// <summary>
		/// 모든 로드된 어셈블리에서 MonoBehaviour 서브클래스 중 [Inject] 가 1+ 멤버에 붙은 타입 수집.
		/// </summary>
		public static HashSet<Type> CollectInjectConsumingTypes()
		{
			HashSet<Type> types = new HashSet<Type>();
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] assemblyTypes;
				try
				{
					assemblyTypes = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException reflectionTypeLoadException)
				{
					assemblyTypes = reflectionTypeLoadException.Types.Where(loadedType => loadedType != null).ToArray();
				}

				foreach (Type type in assemblyTypes)
				{
					if (type == null)
					{
						continue;
					}
					if (type.IsAbstract == true)
					{
						continue;
					}
					if (typeof(MonoBehaviour).IsAssignableFrom(type) == false)
					{
						continue;
					}
					if (HasInjectAttribute(type) == true)
					{
						types.Add(type);
					}
				}
			}
			return types;
		}

		private static bool HasInjectAttribute(Type type)
		{
			BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

			Type current = type;
			while (current != null && current != typeof(MonoBehaviour) && current != typeof(object))
			{
				foreach (MemberInfo member in current.GetMembers(flags))
				{
					if (member.IsDefined(typeof(InjectAttribute), inherit: false) == true)
					{
						return true;
					}
				}
				current = current.BaseType;
			}
			return false;
		}

		/// <summary>
		/// SceneLifetimeScope.cs 소스를 파싱해 「Directly Covered」 + 「Hierarchy Covered」 타입 이름을 수집.
		/// </summary>
		public static SceneCoverageMap CollectSceneCoverage()
		{
			string fullPath = Path.GetFullPath(SCENE_LIFETIME_SCOPE_PATH);
			if (File.Exists(fullPath) == false)
			{
				throw new FileNotFoundException(
					$"SceneLifetimeScope 소스 미발견 — audit 진행 불가: {SCENE_LIFETIME_SCOPE_PATH}");
			}

			string source = File.ReadAllText(fullPath);

			HashSet<string> direct = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match match in DIRECT_REGISTER_PATTERN.Matches(source))
			{
				direct.Add(match.Groups[1].Value);
			}
			foreach (Match match in INJECT_FOREACH_PATTERN.Matches(source))
			{
				direct.Add(match.Groups[1].Value);
			}

			HashSet<string> hierarchy = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match match in INJECT_GAMEOBJECT_FOREACH_PATTERN.Matches(source))
			{
				hierarchy.Add(match.Groups[1].Value);
				direct.Add(match.Groups[1].Value); // InjectGameObject 도 본인 컴포넌트는 직접 커버.
			}

			// Root scope 도 등록 타입은 「씬 직접배치 NRE」 와 무관하지만, 본 감사의 false-positive 를
			// 줄이기 위해 Root 등록 타입도 「Directly Covered」 에 포함 (예: GameManager / TimeManager).
			if (File.Exists(Path.GetFullPath(ROOT_LIFETIME_SCOPE_PATH)) == true)
			{
				string rootSource = File.ReadAllText(Path.GetFullPath(ROOT_LIFETIME_SCOPE_PATH));
				foreach (Match match in DIRECT_REGISTER_PATTERN.Matches(rootSource))
				{
					direct.Add(match.Groups[1].Value);
				}
			}

			return new SceneCoverageMap(direct, hierarchy);
		}

		/// <summary>
		/// 본 폴더 하위 모든 .unity 경로 (패키지 제외).
		/// </summary>
		public static IEnumerable<string> EnumerateProjectScenePaths()
		{
			foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SCENES_ROOT }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path) == true)
				{
					continue;
				}
				if (path.StartsWith("Packages/") == true)
				{
					continue;
				}
				yield return path;
			}
		}

		/// <summary>
		/// 단일 씬 audit. 씬을 additive 로 열어 walk 후 닫음 (저장 X — 비파괴).
		/// </summary>
		public static List<SceneDiOffender> AuditScene(
			string scenePath,
			SceneCoverageMap coverage,
			HashSet<Type> injectConsumingTypes)
		{
			List<SceneDiOffender> offenders = new List<SceneDiOffender>();

			Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
			try
			{
				foreach (GameObject root in scene.GetRootGameObjects())
				{
					WalkHierarchy(scenePath, root, parentHierarchyCovers: false, coverage, injectConsumingTypes, offenders);
				}
			}
			finally
			{
				EditorSceneManager.CloseScene(scene, removeScene: true);
			}

			return offenders;
		}

		private static void WalkHierarchy(
			string scenePath,
			GameObject gameObject,
			bool parentHierarchyCovers,
			SceneCoverageMap coverage,
			HashSet<Type> injectConsumingTypes,
			List<SceneDiOffender> offenders)
		{
			// 이 GameObject 자체가 InjectGameObject 대상 타입이면 자손까지 hierarchy-covered.
			bool selfTriggersHierarchy = false;
			MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
			foreach (MonoBehaviour component in components)
			{
				if (component == null)
				{
					continue;
				}
				if (coverage.HierarchyCovered.Contains(component.GetType().Name) == true)
				{
					selfTriggersHierarchy = true;
					break;
				}
			}

			bool effectiveHierarchyCovers = parentHierarchyCovers == true || selfTriggersHierarchy == true;

			foreach (MonoBehaviour component in components)
			{
				if (component == null)
				{
					continue;
				}
				Type type = component.GetType();
				if (injectConsumingTypes.Contains(type) == false)
				{
					continue;
				}
				if (effectiveHierarchyCovers == true)
				{
					continue;
				}
				if (coverage.DirectlyCovered.Contains(type.Name) == true)
				{
					continue;
				}

				offenders.Add(new SceneDiOffender
				{
					ScenePath = scenePath,
					GameObjectPath = BuildGameObjectPath(component.transform),
					ComponentTypeName = type.Name,
					ComponentFullTypeName = type.FullName,
				});
			}

			foreach (Transform child in gameObject.transform)
			{
				WalkHierarchy(scenePath, child.gameObject, effectiveHierarchyCovers, coverage, injectConsumingTypes, offenders);
			}
		}

		private static string BuildGameObjectPath(Transform transform)
		{
			List<string> segments = new List<string>();
			Transform current = transform;
			while (current != null)
			{
				segments.Add(current.name);
				current = current.parent;
			}
			segments.Reverse();
			return string.Join("/", segments);
		}
	}

	public sealed class SceneCoverageMap
	{
		public HashSet<string> DirectlyCovered { get; }
		public HashSet<string> HierarchyCovered { get; }

		public SceneCoverageMap(HashSet<string> directlyCovered, HashSet<string> hierarchyCovered)
		{
			DirectlyCovered = directlyCovered;
			HierarchyCovered = hierarchyCovered;
		}
	}

	public sealed class SceneDiOffender
	{
		public string ScenePath;
		public string GameObjectPath;
		public string ComponentTypeName;
		public string ComponentFullTypeName;
	}
}
