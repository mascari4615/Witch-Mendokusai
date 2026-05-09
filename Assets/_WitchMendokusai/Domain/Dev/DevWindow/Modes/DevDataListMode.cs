using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 텍스트 list 형태의 데이터 모드 (Mobs/Stages/Quests). 슬롯 그리드 (Items 모드) 대비
	/// 시각 정보가 적은 DataSO 에 적합 — 코드명 + Name 한 줄 버튼.
	/// 클릭 시 commandTokens + ref 형태로 명령 실행 (예: `quest unlock Q_42`).
	/// </summary>
	public class DevDataListMode<T> : IDevMode where T : DataSO
	{
		public string Id { get; }
		public string DisplayName { get; }
		public VisualElement Root { get; }

		private readonly string codePrefix;
		private readonly string[] commandTokens;
		private readonly TextField searchField;
		private readonly ScrollView listScroll;
		private readonly List<T> allItems = new();

		private string currentSearch = string.Empty;

		public DevDataListMode(string id, string displayName, string codePrefix, params string[] commandTokens)
		{
			Id = id;
			DisplayName = displayName;
			this.codePrefix = codePrefix;
			this.commandTokens = commandTokens;

			Root = new VisualElement();
			Root.AddToClassList("wm-dev-mode-list");

			Label hint = new($"클릭: {string.Join(' ', commandTokens)} <ref>");
			hint.AddToClassList("wm-dev-mode-list__hint");
			hint.pickingMode = PickingMode.Ignore;
			Root.Add(hint);

			searchField = new TextField();
			searchField.AddToClassList("wm-dev-mode-list__search");
			searchField.RegisterValueChangedCallback(evt =>
			{
				currentSearch = evt.newValue ?? string.Empty;
				Refresh();
			});
			Root.Add(searchField);

			listScroll = new ScrollView(ScrollViewMode.Vertical);
			listScroll.AddToClassList("wm-dev-mode-list__scroll");
			Root.Add(listScroll);
		}

		public void OnActivate()
		{
			LoadAll();
			Refresh();
		}

		public void OnDeactivate() { }

		private void LoadAll()
		{
			allItems.Clear();
			SOHelper.ForEach<T>(item =>
			{
				if (item != null)
					allItems.Add(item);
			});
			allItems.Sort((a, b) => a.ID.CompareTo(b.ID));
		}

		private void Refresh()
		{
			listScroll.Clear();

			for (int i = 0; i < allItems.Count; i++)
			{
				T item = allItems[i];
				if (Matches(item) == false)
					continue;

				string label = $"{codePrefix}{item.ID}  {item.Name}";
				T captured = item;
				Button button = new(() => Activate(captured))
				{
					text = label,
				};
				button.AddToClassList("wm-dev-mode-list__item");
				listScroll.Add(button);
			}
		}

		private bool Matches(T item)
		{
			if (string.IsNullOrEmpty(currentSearch))
				return true;
			if (item.Name != null && item.Name.IndexOf(currentSearch, StringComparison.OrdinalIgnoreCase) >= 0)
				return true;
			if (item.ID.ToString().Contains(currentSearch))
				return true;
			return false;
		}

		private void Activate(T item)
		{
			if (DevWindowController.Instance == null)
				return;
			if (commandTokens == null || commandTokens.Length == 0)
				return;

			string commandName = commandTokens[0];
			string reference = $"{codePrefix}{item.ID}";

			if (commandTokens.Length == 1)
			{
				DevWindowController.Instance.InvokeCommand(commandName, reference);
				return;
			}

			// commandTokens[1..] + reference 를 args 로 결합 (예: ["quest", "unlock"] + "Q_42")
			string[] args = new string[commandTokens.Length];
			for (int i = 1; i < commandTokens.Length; i++)
				args[i - 1] = commandTokens[i];
			args[commandTokens.Length - 1] = reference;
			DevWindowController.Instance.InvokeCommand(commandName, args);
		}
	}
}
