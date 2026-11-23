using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	// InputManager 의 키 바꾸기 조각. 같은 클래스의 partial.
	// 정본은 입력 액션의 바인딩 덮어쓰기이고 (Input System 공식 저장 JSON 을 PlayerPrefs 에), KeybindRegistry 는 그 표시용 거울.
	// 규칙 (예약 키, 충돌) 은 KeybindRegistry.Rebind, 이 파일은 장치 쪽 일만
	public partial class InputManager
	{
		private const string KEYBIND_OVERRIDES_KEY = "WM.Input.Overrides";

		private InputActionRebindingExtensions.RebindingOperation rebindOperation;

		/// <summary> 키를 기다리는 중. 그동안 모든 입력 사건을 버림 (누른 키가 게임 동작이나 메뉴 닫기를 일으키지 않게) </summary>
		public bool IsRebinding { get; private set; }

		private void DisposeRebind()
		{
			rebindOperation?.Dispose();
			rebindOperation = null;
			IsRebinding = false;
		}

		/// <summary> 바꾼 키가 있나. 키 탭의 모두 기본값 버튼 표시용 </summary>
		public static bool HasAnyKeyOverride => PlayerPrefs.HasKey(KEYBIND_OVERRIDES_KEY);

		/// <summary>
		/// 키보드 키 하나를 기다림. 잡히면 newPath 로 불러 주고 (아직 적용 전), ESC 면 onCancelled.
		/// 결정 (예약 키 거절, 맞바꿈 확인) 은 부른 쪽이 하고 ApplyKey 나 SwapKeys 를 부름
		/// </summary>
		public void BeginRebind(InputEventType eventType, Action<string> onCaptured, Action onCancelled)
		{
			if (IsRebinding || TryGetKeyBinding(eventType, out InputAction action, out int bindingIndex) == false)
			{
				onCancelled();
				return;
			}

			string previousPath = KeybindRegistry.GetCurrentPath(eventType);
			IsRebinding = true;
			// 바꾸는 중인 액션은 꺼져 있어야 함 (Input System 규칙). 끝나면 켬
			action.Disable();
			rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
				.WithControlsHavingToMatchPath("<Keyboard>/*")
				.WithCancelingThrough("<Keyboard>/escape")
				.WithMatchingEventsBeingSuppressed()
				.OnComplete(operation =>
				{
					// 잡힌 키는 자동으로 덮어써지므로 읽은 뒤 원래대로 돌림. 적용은 부른 쪽 결정 뒤
					string newPath = action.bindings[bindingIndex].effectivePath;
					RestoreKeyPath(action, bindingIndex, eventType, previousPath);
					EndRebind(action);
					onCaptured(newPath);
				})
				.OnCancel(operation =>
				{
					EndRebind(action);
					onCancelled();
				});
			rebindOperation.Start();
		}

		/// <summary> 진행 중인 기다림을 끝냄 (설정창이 닫힐 때) </summary>
		public void CancelRebind()
		{
			if (IsRebinding)
				rebindOperation.Cancel();
		}

		public void ApplyKey(InputEventType eventType, string path)
		{
			SetKeyPath(eventType, path);
			KeybindRegistry.SetPath(eventType, path);
			SaveKeybinds();
		}

		/// <summary> 두 동작의 키를 서로 바꿈. 저장은 한 번 </summary>
		public void SwapKeys(InputEventType first, InputEventType second)
		{
			if (TryGetKeyBinding(first, out _, out _) == false || TryGetKeyBinding(second, out _, out _) == false)
				throw new InvalidOperationException($"{first}, {second} 중 바꿀 수 있는 키보드 바인딩이 없는 동작이 있음");
			string firstPath = KeybindRegistry.GetCurrentPath(first);
			string secondPath = KeybindRegistry.GetCurrentPath(second);
			SetKeyPath(first, secondPath);
			SetKeyPath(second, firstPath);
			KeybindRegistry.SwapPaths(first, second);
			SaveKeybinds();
		}

		/// <summary> 기본 키로 되돌림. 그 기본 키를 다른 동작이 쓰고 있으면 그쪽도 (연쇄로) 기본으로 돌려 겹침을 없앰. 저장은 한 번 </summary>
		public void ResetKey(InputEventType eventType)
		{
			List<InputEventType> chain = new() { eventType };
			for (int i = 0; i < chain.Count; i++)
			{
				if (KeybindRegistry.TryFindOwner(KeybindRegistry.GetDefaultPath(chain[i]), chain[i], out InputEventType owner) && chain.Contains(owner) == false)
					chain.Add(owner);
			}
			foreach (InputEventType resetType in chain)
			{
				string defaultPath = KeybindRegistry.GetDefaultPath(resetType);
				SetKeyPath(resetType, defaultPath);
				KeybindRegistry.SetPath(resetType, defaultPath);
			}
			SaveKeybinds();
		}

		public void ResetAllKeys()
		{
			inputActionAsset.RemoveAllBindingOverrides();
			KeybindRegistry.ClearOverrides();
			SaveKeybinds();
		}

		/// <summary> 부팅 때. 에디터는 자산이 메모리에 남아 있을 수 있어 먼저 비움 </summary>
		private void LoadKeybinds()
		{
			inputActionAsset.RemoveAllBindingOverrides();
			string json = PlayerPrefs.GetString(KEYBIND_OVERRIDES_KEY, "");
			if (string.IsNullOrEmpty(json) == false)
			{
				try
				{
					inputActionAsset.LoadBindingOverridesFromJson(json);
				}
				catch (ArgumentException exception)
				{
					// 저장값은 사용자 쪽 데이터라 깨졌을 수 있음 (경계). 버리고 기본 키로
					Debug.LogWarning($"[Keybind] 저장된 키 설정을 읽지 못해 기본 키로 되돌림: {exception.Message}");
					inputActionAsset.RemoveAllBindingOverrides();
					PlayerPrefs.DeleteKey(KEYBIND_OVERRIDES_KEY);
				}
			}

			// 직접 고친 저장값이나 옛 판이 예약 키, 고정 키, 관리하지 않는 바인딩에 덮어쓴 경우
			if (HasUnmanagedOverride())
			{
				Debug.LogWarning("[Keybind] 저장된 키 설정에 바꿀 수 없는 키가 있어 기본 키로 되돌림");
				inputActionAsset.RemoveAllBindingOverrides();
				PlayerPrefs.DeleteKey(KEYBIND_OVERRIDES_KEY);
			}

			List<(InputEventType, string)> overrides = new();
			foreach (InputEventType eventType in KeybindRegistry.AllEventTypes)
			{
				if (TryGetKeyBinding(eventType, out InputAction action, out int bindingIndex) == false)
					continue;
				string effectivePath = action.bindings[bindingIndex].effectivePath;
				if (effectivePath != KeybindRegistry.GetDefaultPath(eventType))
					overrides.Add((eventType, effectivePath));
			}
			KeybindRegistry.ReplaceOverrides(overrides);

			// 업데이트로 기본 키가 저장값과 겹친 경우. 어느 쪽도 고르지 않고 전부 기본으로
			if (KeybindRegistry.HasActiveConflict())
			{
				Debug.LogWarning("[Keybind] 저장된 키 설정이 지금 기본 키와 겹쳐 기본 키로 되돌림");
				ResetAllKeys();
			}
		}

		private void SaveKeybinds()
		{
			if (HasAnyBindingOverride())
				PlayerPrefs.SetString(KEYBIND_OVERRIDES_KEY, inputActionAsset.SaveBindingOverridesAsJson());
			else
				PlayerPrefs.DeleteKey(KEYBIND_OVERRIDES_KEY);
			PlayerPrefs.Save();
		}

		// 덮어쓴 바인딩이 모두 우리가 관리하는 (동작의 기본 키 자리, 바꿀 수 있는 동작, 예약 키 아님) 것인가의 반대
		private bool HasUnmanagedOverride()
		{
			foreach (InputActionMap map in inputActionAsset.actionMaps)
			{
				foreach (InputAction action in map.actions)
				{
					for (int i = 0; i < action.bindings.Count; i++)
					{
						string overridePath = action.bindings[i].overridePath;
						if (string.IsNullOrEmpty(overridePath))
							continue;
						bool managed = Enum.TryParse(action.name, out InputEventType eventType)
							&& KeybindRegistry.IsRebindableByDesign(eventType)
							&& TryGetKeyBinding(eventType, out InputAction keyAction, out int keyIndex)
							&& keyAction == action
							&& keyIndex == i
							&& KeybindRegistry.IsReserved(overridePath) == false;
						if (managed == false)
							return true;
					}
				}
			}
			return false;
		}

		private bool HasAnyBindingOverride()
		{
			foreach (InputActionMap map in inputActionAsset.actionMaps)
				foreach (InputBinding binding in map.bindings)
					if (string.IsNullOrEmpty(binding.overridePath) == false)
						return true;
			return false;
		}

		// 기본 경로와 같은 바인딩이 이 동작의 키보드 키. 패드나 두 번째 키 (취소의 ESC) 는 건드리지 않음
		private bool TryGetKeyBinding(InputEventType eventType, out InputAction action, out int bindingIndex)
		{
			action = inputActionAsset[$"{inputEventBindings[eventType]}/{eventType}"];
			string defaultPath = KeybindRegistry.GetDefaultPath(eventType);
			bindingIndex = -1;
			if (string.IsNullOrEmpty(defaultPath))
				return false;
			for (int i = 0; i < action.bindings.Count; i++)
			{
				if (action.bindings[i].path == defaultPath)
				{
					bindingIndex = i;
					return true;
				}
			}
			return false;
		}

		private void SetKeyPath(InputEventType eventType, string path)
		{
			if (TryGetKeyBinding(eventType, out InputAction action, out int bindingIndex) == false)
				throw new InvalidOperationException($"{eventType} 에 바꿀 수 있는 키보드 바인딩이 없음");
			RestoreKeyPath(action, bindingIndex, eventType, path);
		}

		private static void RestoreKeyPath(InputAction action, int bindingIndex, InputEventType eventType, string path)
		{
			if (path == KeybindRegistry.GetDefaultPath(eventType))
				action.RemoveBindingOverride(bindingIndex);
			else
				action.ApplyBindingOverride(bindingIndex, path);
		}

		private void EndRebind(InputAction action)
		{
			rebindOperation.Dispose();
			rebindOperation = null;
			action.Enable();
			UnlockAfterFrame().Forget();
		}

		// 키를 잡은 그 프레임의 입력이 액션으로 흘러도 (ESC 가 취소 동작으로 가는 것 등) 아직 잠긴 채 지나가게 한 프레임 뒤에 풀음
		private async UniTaskVoid UnlockAfterFrame()
		{
			await UniTask.NextFrame();
			IsRebinding = false;
		}
	}
}
