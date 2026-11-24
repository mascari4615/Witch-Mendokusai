using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 키 탭: 동작마다 키 버튼 (누르면 새 키를 기다림), 기본값 복원, 모두 기본값.
	// 규칙은 KeybindRegistry.Rebind, 장치 쪽은 InputManager.Rebind. 여기는 줄과 확인 창만
	// 충돌하면 확인 창을 띄우고 수락하면 두 동작의 키를 맞바꿈 (사용자 2026-11-23)
	public partial class SettingView
	{
		private const string TAB_KEY_KEYS = "keys";
		private const string KEY_WAITING_CLASS = "wm-setting-key--waiting";
		private const string KEY_WAITING_TEXT = "키를 누르세요 (ESC 취소)";
		private const string KEY_FIXED_TEXT = "고정";
		private const string MOVE_FIXED_KEYS = "W A S D";

		private readonly Dictionary<InputEventType, Button> keyButtons = new();
		private readonly Dictionary<InputEventType, Button> keyResetButtons = new();
		private Label keyMessage;
		private InputEventType? waitingKeyEvent;

		private VisualElement BuildKeysContent()
		{
			VisualElement content = new VisualElement();

			Label hint = new Label("키를 고르고 바꿀 키를 누르세요. ESC 로 취소");
			hint.AddToClassList("wm-setting-credit-hint");
			content.Add(hint);

			ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("wm-setting-key-scroll");
			scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			content.Add(scroll);
			FollowFocus(scroll);

			// 안내, 알림, 모두 기본값은 묶음을 바꿔도 남으므로 묶음 상자는 스크롤 안에만
			VisualElement pages = new VisualElement();
			scroll.Add(pages);

			foreach (IGrouping<string, KeybindEntry> group in KeybindRegistry.EnumerateVisibleEntries().GroupBy(entry => entry.Category))
			{
				Label header = new Label(group.Key);
				header.AddToClassList("wm-setting-header");
				pages.Add(header);
				if (group.Key == KeybindRegistry.MOVE_CATEGORY)
					pages.Add(CreateFixedKeyRow(KeybindRegistry.MOVE_DISPLAY_NAME, MOVE_FIXED_KEYS));
				foreach (KeybindEntry entry in group)
					pages.Add(CreateKeyRow(entry));
			}
			BuildSections(TAB_KEY_KEYS, pages);

			keyMessage = new Label();
			keyMessage.AddToClassList("wm-setting-key-message");
			content.Add(keyMessage);

			Button resetAll = new Button(OnResetAllKeys) { text = "모두 기본값" };
			resetAll.AddToClassList("wm-setting-btn");
			resetAll.AddToClassList(NAV_CLASS);
			content.Add(resetAll);

			KeybindRegistry.OnChanged += RefreshKeyRows;
			return content;
		}

		private VisualElement CreateFixedKeyRow(string label, string keys)
		{
			VisualElement row = CreateKeyRowFrame(label);
			Label fixedKeys = new Label(keys);
			fixedKeys.AddToClassList("wm-setting-key-fixed");
			row.Add(fixedKeys);
			return row;
		}

		private VisualElement CreateKeyRow(KeybindEntry entry)
		{
			VisualElement row = CreateKeyRowFrame(entry.DisplayName);
			bool rebindable = KeybindRegistry.IsRebindable(entry.EventType);

			Button keyButton = new Button(() => BeginKeyRebind(entry.EventType)) { text = KeybindFormat.FormatPath(entry.CurrentPath) };
			keyButton.AddToClassList("wm-setting-key");
			keyButton.AddToClassList(NAV_CLASS);
			// 마우스와 안내 창 키는 보이기만 하고 못 바꿈
			keyButton.SetEnabled(rebindable);
			row.Add(keyButton);
			keyButtons[entry.EventType] = keyButton;

			if (rebindable)
			{
				Button reset = new Button(() => OnResetKey(entry.EventType)) { text = "기본값" };
				reset.AddToClassList("wm-setting-key-reset");
				reset.AddToClassList(NAV_CLASS);
				row.Add(reset);
				keyResetButtons[entry.EventType] = reset;
			}
			else
			{
				Label fixedLabel = new Label(KEY_FIXED_TEXT);
				fixedLabel.AddToClassList("wm-setting-key-fixed");
				row.Add(fixedLabel);
			}
			return row;
		}

		private static VisualElement CreateKeyRowFrame(string label)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("wm-setting-row");
			Label name = new Label(label);
			name.AddToClassList("wm-setting-key-label");
			row.Add(name);
			return row;
		}

		private void BeginKeyRebind(InputEventType eventType)
		{
			if (inputManager.IsRebinding)
				return;
			UiSfx.Play(UiSfxEvent.Confirm);
			keyMessage.text = "";
			waitingKeyEvent = eventType;
			keyButtons[eventType].text = KEY_WAITING_TEXT;
			keyButtons[eventType].AddToClassList(KEY_WAITING_CLASS);
			inputManager.BeginRebind(eventType, newPath => OnKeyCaptured(eventType, newPath), OnKeyCancelled);
		}

		private void OnKeyCancelled()
		{
			waitingKeyEvent = null;
			RefreshKeyRows();
		}

		private void OnKeyCaptured(InputEventType eventType, string newPath)
		{
			waitingKeyEvent = null;
			keyMessage.text = "";
			newPath = KeybindRegistry.NormalizePath(newPath);
			switch (KeybindRegistry.Check(eventType, newPath, out InputEventType owner))
			{
				case KeybindRegistry.RebindCheck.Reserved:
					keyMessage.text = $"{KeybindFormat.FormatPath(newPath)} 키는 쓸 수 없습니다";
					UiSfx.Play(UiSfxEvent.Cancel);
					break;
				case KeybindRegistry.RebindCheck.Ok:
					inputManager.ApplyKey(eventType, newPath);
					UiSfx.Play(UiSfxEvent.Confirm);
					break;
				case KeybindRegistry.RebindCheck.Conflict:
					AskSwapKeys(eventType, owner, newPath);
					break;
			}
			RefreshKeyRows();
		}

		private void AskSwapKeys(InputEventType eventType, InputEventType owner, string newPath)
		{
			string ownerName = KeybindRegistry.GetDisplayName(owner);
			string eventName = KeybindRegistry.GetDisplayName(eventType);
			string currentText = KeybindFormat.FormatPath(KeybindRegistry.GetCurrentPath(eventType));
			string newText = KeybindFormat.FormatPath(newPath);
			AskConfirmWithSound($"'{ownerName}' 와 키를 맞바꿀까요?",
				$"{newText} 는 지금 '{ownerName}' 키입니다. 맞바꾸면 '{eventName}' 는 {newText}, '{ownerName}' 는 {currentText} 가 됩니다.",
				"맞바꾸기", () => inputManager.SwapKeys(eventType, owner));
		}

		private void OnResetKey(InputEventType eventType)
		{
			if (inputManager.IsRebinding)
				return;
			// 그 기본 키를 다른 동작이 쓰고 있으면 InputManager 가 연쇄로 함께 돌림
			inputManager.ResetKey(eventType);
			UiSfx.Play(UiSfxEvent.Confirm);
		}

		private void OnResetAllKeys()
		{
			if (inputManager.IsRebinding)
				return;
			inputManager.ResetAllKeys();
			keyMessage.text = "";
			UiSfx.Play(UiSfxEvent.Confirm);
		}

		private void RefreshKeyRows()
		{
			foreach (KeyValuePair<InputEventType, Button> pair in keyButtons)
			{
				bool isWaiting = waitingKeyEvent == pair.Key;
				pair.Value.EnableInClassList(KEY_WAITING_CLASS, isWaiting);
				pair.Value.text = isWaiting ? KEY_WAITING_TEXT : KeybindFormat.FormatPath(KeybindRegistry.GetCurrentPath(pair.Key));
			}
			foreach (KeyValuePair<InputEventType, Button> pair in keyResetButtons)
				pair.Value.SetEnabled(KeybindRegistry.HasOverride(pair.Key));
		}
	}
}
