using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	/// <summary>
	/// InputEventType 메타데이터의 단일 출처. enum 항목에 부착된 [InputEvent] attribute 를 스캔해
	/// 카테고리·표시명·기본 바인딩 경로를 자동 수집한다.
	///
	/// 책임:
	/// - 부팅 시 enum vs InputActionAsset 정합성 검증 (action 이름·바인딩 경로 매칭)
	/// - KeybindView/오버레이가 카테고리별로 안내할 수 있도록 entry 노출
	/// - (향후) 리바인드 후 현재 경로 갱신 + persist
	/// </summary>
	public static class KeybindRegistry
	{
		private static readonly Dictionary<InputEventType, InputEventAttribute> attributes = BuildAttributeMap();
		private static readonly Dictionary<InputEventType, string> currentPathOverrides = new();

		public static IEnumerable<InputEventType> AllEventTypes
		{
			get { foreach (InputEventType type in Enum.GetValues(typeof(InputEventType))) yield return type; }
		}

		public static bool TryGetAttribute(InputEventType eventType, out InputEventAttribute attribute)
		{
			return attributes.TryGetValue(eventType, out attribute);
		}

		public static string GetDisplayName(InputEventType eventType)
		{
			return attributes.TryGetValue(eventType, out InputEventAttribute attribute) && string.IsNullOrEmpty(attribute.DisplayName) == false
				? attribute.DisplayName
				: eventType.ToString();
		}

		public static string GetCategory(InputEventType eventType)
		{
			return attributes.TryGetValue(eventType, out InputEventAttribute attribute) && string.IsNullOrEmpty(attribute.Category) == false
				? attribute.Category
				: "기타";
		}

		public static string GetDefaultPath(InputEventType eventType)
		{
			return attributes.TryGetValue(eventType, out InputEventAttribute attribute) ? attribute.DefaultPath : null;
		}

		public static string GetCurrentPath(InputEventType eventType)
		{
			return currentPathOverrides.TryGetValue(eventType, out string overridden) ? overridden : GetDefaultPath(eventType);
		}

		public static IEnumerable<KeybindEntry> EnumerateEntries()
		{
			foreach (InputEventType eventType in AllEventTypes)
			{
				yield return new KeybindEntry(
					eventType,
					GetCategory(eventType),
					GetDisplayName(eventType),
					GetDefaultPath(eventType),
					GetCurrentPath(eventType)
				);
			}
		}

		public static IEnumerable<IGrouping<string, KeybindEntry>> EnumerateGroupedByCategory()
		{
			return EnumerateEntries().GroupBy(entry => entry.Category);
		}

		public static List<(InputEventType A, InputEventType B, string Path)> FindConflicts()
		{
			List<(InputEventType, InputEventType, string)> conflicts = new();
			List<InputEventType> typed = AllEventTypes.ToList();
			for (int i = 0; i < typed.Count; i++)
			{
				string pathA = GetCurrentPath(typed[i]);
				if (string.IsNullOrEmpty(pathA))
					continue;
				for (int j = i + 1; j < typed.Count; j++)
				{
					string pathB = GetCurrentPath(typed[j]);
					if (string.IsNullOrEmpty(pathB))
						continue;
					if (pathA == pathB)
						conflicts.Add((typed[i], typed[j], pathA));
				}
			}
			return conflicts;
		}

		/// <summary>
		/// InputActionAsset 의 실제 바인딩 경로가 attribute 기본 경로와 일치하는지 검증.
		/// 불일치 시 경고 로그 — 동기화 깨진 상태를 빠르게 감지한다.
		/// </summary>
		public static void ValidateAgainstAsset(InputActionAsset asset)
		{
			if (asset == null)
				return;

			foreach (InputEventType eventType in AllEventTypes)
			{
				if (attributes.TryGetValue(eventType, out InputEventAttribute attribute) == false)
				{
					Debug.LogWarning($"[KeybindRegistry] {eventType} 에 [InputEvent] attribute 누락 — 메타데이터 단일 출처에서 빠짐");
					continue;
				}

				InputAction action = FindAction(asset, eventType);
				if (action == null)
				{
					Debug.LogWarning($"[KeybindRegistry] {eventType}: InputActionAsset 에 action 없음");
					continue;
				}

				bool foundDefault = false;
				foreach (InputBinding binding in action.bindings)
				{
					if (binding.path == attribute.DefaultPath)
					{
						foundDefault = true;
						break;
					}
				}
				if (foundDefault == false && string.IsNullOrEmpty(attribute.DefaultPath) == false)
					Debug.LogWarning($"[KeybindRegistry] {eventType}: 기본 경로 '{attribute.DefaultPath}' 가 asset 바인딩에 없음");
			}

			List<(InputEventType, InputEventType, string)> conflicts = FindConflicts();
			foreach ((InputEventType a, InputEventType b, string path) in conflicts)
				Debug.LogWarning($"[KeybindRegistry] 키 충돌: {a} vs {b} 둘 다 '{path}'");
		}

		private static InputAction FindAction(InputActionAsset asset, InputEventType eventType)
		{
			foreach (InputActionMap map in asset.actionMaps)
			{
				foreach (InputAction action in map.actions)
				{
					if (action.name == eventType.ToString())
						return action;
				}
			}
			return null;
		}

		private static Dictionary<InputEventType, InputEventAttribute> BuildAttributeMap()
		{
			Dictionary<InputEventType, InputEventAttribute> result = new();
			Type enumType = typeof(InputEventType);
			foreach (InputEventType value in Enum.GetValues(enumType))
			{
				FieldInfo field = enumType.GetField(value.ToString());
				if (field == null)
					continue;
				InputEventAttribute attribute = field.GetCustomAttribute<InputEventAttribute>();
				if (attribute != null)
					result[value] = attribute;
			}
			return result;
		}
	}
}
