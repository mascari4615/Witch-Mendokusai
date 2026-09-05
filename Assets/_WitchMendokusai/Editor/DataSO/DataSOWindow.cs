using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using static WitchMendokusai.DataSOUtil;
using static WitchMendokusai.DataSODefine;

namespace WitchMendokusai
{
	[InitializeOnLoad]
	public partial class DataSOWindow : EditorWindow
	{
		static DataSOWindow()
		{
			Debug.Log("DataSOWindow static constructor is called.");
		}

		private static DataSOWindow instance;
		public static DataSOWindow Instance
		{
			get
			{
				if (instance == null)
				{
					ShowDataSOWindow();
				}

				return instance;
			}
			private set => instance = value;
		}

		public static DataSO_IdChanger IdChanger { get; private set; } = null;

		private DataExplorerView dataExplorerView;
		private DataSOTypeProvider currentProvider;
		public EntryDescriptor ActiveEntry { get; private set; }

		public Dictionary<Type, Dictionary<int, DataSO>> DataSOs { get; private set; } = new(); // 직접 접근하기 보다는, GetDataSOs(Type type)를 사용할 것
		public Dictionary<Type, Dictionary<int, List<DataSO>>> BadIdDataSOs { get; private set; } = new();

		public Type CurType { get; private set; } = null;

		private bool isInit = false;

		[MenuItem("WM/DataSOWindow")]
		public static void ShowDataSOWindow()
		{
			// Debug.Log(nameof(ShowDataSOWindow));
			// (유틸리티 창 여부, 타이틀, 이미 창이 열려있을 때 Focus 여부)
			GetWindow<DataSOWindow>(false, nameof(DataSOWindow), true);

			// Debug.Log($"{nameof(ShowDataSOWindow)} End : {instance}");
		}

		private void OnEnable()
		{
			// Debug.Log(nameof(OnEnable));

			if (instance == null)
			{
				// Debug.Log("인스턴스를 생성합니다.");
				instance = this;
			}
			else
			{
				Debug.LogWarning("이미 인스턴스가 존재합니다.");
				Close();
				return;
			}

			DataSOs = new();

			if (EditorSetting.Data.InitDataSODictOnCompile == false)
			{
				Debug.LogWarning("InitDataSODictOnCompile이 비활성화되어 있습니다.");
				return;
			}

			bool initDictResult = InitDict();
			if (initDictResult == false)
			{
				Debug.LogError($"{nameof(OnEnable)} 중 오류 발생: InitDict 실패");
				return;
			}

			if (isInit)
				SetType(AssetPrefixes.Keys.First());

			// SaveAssets();
			// Debug.Log($"{nameof(OnEnable)} End : {instance}");
		}

		private void OnDestroy()
		{
			// Debug.Log($"{nameof(OnDestroy)} : {instance}, rootVisualElement: {rootVisualElement}");
			instance = null;
		}

		public void CreateGUI()
		{
			// Debug.Log(nameof(CreateGUI));

			VisualElement root = rootVisualElement;
			string[] uxmlGuids = AssetDatabase.FindAssets($"t:VisualTreeAsset {nameof(DataSOWindow)}");
			VisualTreeAsset visualTree = uxmlGuids.Length > 0
				? AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath(uxmlGuids[0]))
				: null;

			// Instantiate UXML
			VisualElement labelFromUXML = visualTree.Instantiate();
			root.Add(labelFromUXML);

			DropdownField dropdown = rootVisualElement.Q<DropdownField>(name: "Menu");
			dropdown.choices = AssetPrefixes.Keys.Select(type => type.Name).ToList();
			dropdown.RegisterValueChangedCallback(ev =>
			{
				string typeName = ev.newValue;
				Type type = AssetPrefixes.Keys.First(t => t.Name == typeName);
				SetType(type);
			});

			Button initDictButton = rootVisualElement.Q<Button>(name: "InitDictButton");
			initDictButton.RegisterCallback<MouseUpEvent>(ev =>
			{
				if (EditorUtility.DisplayDialog("InitDict", "갱신 하시겠습니까?", "Yes", "No") == true)
				{
					InitDict();
					UpdateGrid();
				}
			});

			IdChanger = new();

			SetType(AssetPrefixes.Keys.First());

			isInit = true;
			// Debug.Log($"{nameof(CreateGUI)} End");
		}

		// 창이 한 번이라도 열려 Grid가 존재한다면 Grid 갱신 후 true 반환
		public bool UpdateGrid(bool selectFirst = true)
		{
			VisualElement grid = rootVisualElement.Q<VisualElement>(name: "Grid");
			if (grid == null)
				return false;

			if (dataExplorerView == null)
			{
				dataExplorerView = new DataExplorerView();
				dataExplorerView.OnEntrySelected += entry =>
				{
					currentProvider?.OnEntryActivated(entry);
					ActivateEntry(entry);
				};
				grid.Clear();
				grid.Add(dataExplorerView);
			}

			currentProvider = new DataSOTypeProvider(CurType, DataSOs[CurType]);
			dataExplorerView.SetActiveProvider(currentProvider);
			ActiveEntry = null;

			if (selectFirst)
			{
				IReadOnlyList<EntryDescriptor> entries = currentProvider.GetEntries();
				if (entries.Count > 0)
					SelectEntryByDataSO(entries[0].Source as DataSO);
			}

			Repaint();
			return true;
		}

		/// <summary>외부 (DataSOInspector, DataSO_IdChanger) 가 DataSO 선택 시 카드 강조 + Inspector 연동.</summary>
		public void SelectEntryByDataSO(DataSO dataSO)
		{
			if (dataSO == null || currentProvider == null)
				return;

			IReadOnlyList<EntryDescriptor> entries = currentProvider.GetEntries();
			for (int i = 0; i < entries.Count; i++)
			{
				if (((UnityEngine.Object)entries[i].Source) == dataSO)
				{
					ActivateEntry(entries[i]);
					currentProvider.OnEntryActivated(entries[i]);
					return;
				}
			}
		}

		private void ActivateEntry(EntryDescriptor entry)
		{
			if (ActiveEntry != null)
				dataExplorerView.SetEntryActive(ActiveEntry, false);
			ActiveEntry = entry;
			if (entry != null)
				dataExplorerView.SetEntryActive(entry, true);
		}

		/// <summary>DataSOInspector 가 entry 메타 갱신 시 호출 — 그리드 재빌드 (현재 Type 의 entry list 다시).</summary>
		public void RefreshGrid() => UpdateGrid(selectFirst: false);

		public void SetType(Type type)
		{
			// TODO: 드롭박스 텍스트 변경
			CurType = type;
			UpdateGrid();
		}

		public Dictionary<int, DataSO> GetDataSOs(Type type)
		{
			if (DataSOs.ContainsKey(type) == false)
				InitDict();
			// InitDict(type);

			return DataSOs[type];
		}

		// private void OnValidate() => Debug.Log("OnValidate is executed.");
		// private void OnFocus() => Debug.Log("OnFocus is executed.");
		// private void OnLostFocus() => Debug.Log("OnLostFocus is executed.");
		// private void OnProjectChange() => Debug.Log("OnProjectChange is executed.");
		// private void OnSelectionChange() => Debug.Log("OnSelectionChange is executed.");
		// private void OnInspectorUpdate() => Debug.Log("OnInspectorUpdate is executed.");
		// private void OnHierarchyChange() => Debug.Log("OnHierarchyChange is executed.");
		// private void OnGUI() => Debug.Log("OnGUI is executed.");
	}
}