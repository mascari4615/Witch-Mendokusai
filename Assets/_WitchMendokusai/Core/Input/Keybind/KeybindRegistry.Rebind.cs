using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// KeybindRegistry 의 키 바꾸기 조각. 장치와 입력 액션은 건드리지 않는 순수 규칙 (예약 키, 충돌 찾기, 맞바꿈).
	// 실제 바인딩 교체와 저장은 InputManager.Rebind. 여기 값은 표시용 거울이고 정본은 입력 액션
	public static partial class KeybindRegistry
	{
		public enum RebindCheck
		{
			Ok,
			/// <summary> 이미 그 키 </summary>
			Same,
			/// <summary> 예약 키 (ESC, F1, 이동) </summary>
			Reserved,
			/// <summary> 다른 동작이 쓰는 키. 맞바꿀지 물어야 함 </summary>
			Conflict,
		}

		/// <summary> 바뀌었을 때 (안내 창, 전체 메뉴 글자 갱신용) </summary>
		public static event Action OnChanged = delegate { };

		private const string KEYBOARD_PREFIX = "<Keyboard>/";

		/// <summary> 이동은 InputEventType 이 아니라 Move 축이라 레지스트리 밖. 안내 창과 키 탭이 같은 분류 이름과 줄 이름으로 고정 줄을 둠 </summary>
		public const string MOVE_CATEGORY = "이동";
		public const string MOVE_DISPLAY_NAME = "이동";

		// 취소, 안내 창, 이동 (합성 바인딩이라 레지스트리 밖) 의 키는 다른 동작에 줄 수 없음
		private static readonly HashSet<string> RESERVED_PATHS = new()
		{
			"<Keyboard>/escape",
			"<Keyboard>/f1",
			"<Keyboard>/w",
			"<Keyboard>/a",
			"<Keyboard>/s",
			"<Keyboard>/d",
			"<Keyboard>/upArrow",
			"<Keyboard>/downArrow",
			"<Keyboard>/leftArrow",
			"<Keyboard>/rightArrow",
		};

		public static bool IsReserved(string path) => RESERVED_PATHS.Contains(path);

		/// <summary> 키보드 배치별 경로 (<Keyboard>/#(w)) 로 잡힌 키를 기본 경로 형식 (<Keyboard>/w) 으로. 예약 키와 충돌 비교가 같은 글자로 하게 </summary>
		public static string NormalizePath(string path)
		{
			const string LAYOUT_PREFIX = "<Keyboard>/#(";
			if (path != null && path.StartsWith(LAYOUT_PREFIX, StringComparison.Ordinal) && path.EndsWith(")", StringComparison.Ordinal))
				return KEYBOARD_PREFIX + path.Substring(LAYOUT_PREFIX.Length, path.Length - LAYOUT_PREFIX.Length - 1).ToLowerInvariant();
			return path;
		}

		/// <summary> 빌드와 무관하게 바꾸도록 설계된 동작 (기본 키가 키보드이고 고정이 아님). 저장값 검증용 </summary>
		public static bool IsRebindableByDesign(InputEventType eventType)
			=> attributes.TryGetValue(eventType, out InputEventAttribute attribute)
				&& attribute.Rebindable
				&& string.IsNullOrEmpty(attribute.DefaultPath) == false
				&& attribute.DefaultPath.StartsWith(KEYBOARD_PREFIX, StringComparison.Ordinal);

		/// <summary> 설정창 키 탭에서 바꿀 수 있는 동작인가. 이 빌드에서 받고, 고정이 아니고, 기본 키가 키보드 키 </summary>
		public static bool IsRebindable(InputEventType eventType)
		{
			if (attributes.TryGetValue(eventType, out InputEventAttribute attribute) == false)
				return false;
			return attribute.Rebindable
				&& IsEnabledInThisBuild(eventType)
				&& string.IsNullOrEmpty(attribute.DefaultPath) == false
				&& attribute.DefaultPath.StartsWith(KEYBOARD_PREFIX, StringComparison.Ordinal);
		}

		/// <summary> 키 탭에 보일 동작 (바꿀 수 있거나 고정으로 보여 줄 것). 이 빌드에서 받는 것만 </summary>
		public static IEnumerable<KeybindEntry> EnumerateVisibleEntries()
			=> EnumerateEntries().Where(entry => IsEnabledInThisBuild(entry.EventType));

		/// <summary> path 를 지금 쓰는 다른 동작. 이 빌드에서 받지 않는 동작은 무시 (릴리스에서 안 쓰는 키와는 겹쳐도 상관없음) </summary>
		public static bool TryFindOwner(string path, InputEventType except, out InputEventType owner)
		{
			foreach (InputEventType eventType in AllEventTypes)
			{
				if (eventType == except || IsEnabledInThisBuild(eventType) == false)
					continue;
				if (GetCurrentPath(eventType) == path)
				{
					owner = eventType;
					return true;
				}
			}
			owner = default;
			return false;
		}

		public static RebindCheck Check(InputEventType eventType, string newPath, out InputEventType owner)
		{
			owner = default;
			if (IsReserved(newPath))
				return RebindCheck.Reserved;
			if (GetCurrentPath(eventType) == newPath)
				return RebindCheck.Same;
			return TryFindOwner(newPath, eventType, out owner) ? RebindCheck.Conflict : RebindCheck.Ok;
		}

		public static bool HasOverride(InputEventType eventType) => currentPathOverrides.ContainsKey(eventType);

		/// <summary> 값만 맞춤 (입력 액션은 InputManager 가 따로). 기본 경로와 같으면 덮어쓴 값을 지움 </summary>
		public static void SetPath(InputEventType eventType, string path)
		{
			StorePath(eventType, path);
			OnChanged();
		}

		public static void SwapPaths(InputEventType first, InputEventType second)
		{
			string firstPath = GetCurrentPath(first);
			string secondPath = GetCurrentPath(second);
			StorePath(first, secondPath);
			StorePath(second, firstPath);
			OnChanged();
		}

		public static void ClearOverrides()
		{
			currentPathOverrides.Clear();
			OnChanged();
		}

		/// <summary> 저장값을 불러온 뒤 한 번에 맞춤 </summary>
		public static void ReplaceOverrides(IEnumerable<(InputEventType EventType, string Path)> overrides)
		{
			currentPathOverrides.Clear();
			foreach ((InputEventType eventType, string path) in overrides)
				StorePath(eventType, path);
			OnChanged();
		}

		/// <summary> 이 빌드에서 받는 동작끼리 같은 키를 쓰는 곳이 있나. 업데이트로 기본 키가 저장값과 겹친 경우를 잡음 </summary>
		public static bool HasActiveConflict()
		{
			HashSet<string> seen = new();
			foreach (InputEventType eventType in AllEventTypes)
			{
				string path = GetCurrentPath(eventType);
				if (IsEnabledInThisBuild(eventType) == false || string.IsNullOrEmpty(path))
					continue;
				if (seen.Add(path) == false)
					return true;
			}
			return false;
		}

		private static void StorePath(InputEventType eventType, string path)
		{
			if (path == GetDefaultPath(eventType))
				currentPathOverrides.Remove(eventType);
			else
				currentPathOverrides[eventType] = path;
		}
	}
}
