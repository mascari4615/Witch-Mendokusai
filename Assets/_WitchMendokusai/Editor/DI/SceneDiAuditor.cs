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
	// SceneLifetimeScope.cs 소스 정본과 씬 배치를 교차 검증해 [Inject] 등록 누락을 감지. WM/Audit 메뉴로 수동 실행, EditMode 테스트로 CI 게이트.
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

			// Root 등록 타입도 DirectlyCovered 에 포함 — 씬-배치 컴포넌트와 무관하지만 false-positive 감소.
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

		// 씬을 additive 로 열어 walk 후 닫음 — 비파괴 (저장 X).
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
