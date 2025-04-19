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
	public partial class DataSOWindow : EditorWindow
	{
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
		public Dictionary<int, DataSOSlot> DataSOSlots { get; private set; } = new();
		public DataSOSlot CurSlot { get; private set; } = null;

		public Dictionary<Type, Dictionary<int, DataSO>> DataSOs { get; private set; } = new(); // 직접 접근하기 보다는, GetDataSOs(Type type)를 사용할 것
		public Dictionary<Type, Dictionary<int, List<DataSO>>> BadIdDataSOs { get; private set; } = new();

		public Type CurType { get; private set; } = null;

		private bool isInit = false;

		[MenuItem("WitchMendokusai/DataSOWindow")]
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

			InitEnumData<UnitStatData, UnitStatType>();
			InitEnumData<GameStatData, GameStatType>();
			InitEnumData<DungeonStatData, DungeonStatType>();

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
			VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{EDITOR_DIR}{nameof(DataSO)}/{nameof(DataSOWindow)}.uxml");

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
			{
				// 아직 창이 열리지 않아 Grid가 만들어지지 않은 경우
				// Debug.LogWarning("Grid가 없습니다.");
				// CreateGUI();
				return false;
			}
			grid.Clear();

			// InitDict(CurType);
			Dictionary<int, DataSO> dataSOs = DataSOs[CurType];

			DataSOSlots.Clear();

			List<DataSO> dataSOsSortByID = dataSOs.Values.ToList();
			dataSOsSortByID.Sort((a, b) => a.ID.CompareTo(b.ID));

			foreach (DataSO dataSO in dataSOsSortByID)
			{
				DataSOSlot slot = new((slot) => Selection.activeObject = slot.DataSO);
				slot.SetDataSO(dataSO);
				DataSOSlots.Add(dataSO.ID, slot);
				grid.Add(slot.VisualElement);
			}

			if (selectFirst)
			{
				if (DataSOSlots.Count > 0)
					SelectDataSOSlot(DataSOSlots.Values.First());
			}

			Repaint();
			return true;
		}

		public void SetType(Type type)
		{
			// TODO: 드롭박스 텍스트 변경
			CurType = type;
			UpdateGrid();
		}

		private bool InitDict()
		{
			DataSOs.Clear();
			foreach (Type type in AssetPrefixes.Keys)
				DataSOs[type] = new();

			try
			{
				ForeachDataSO((dataSO) => ProcessDataSO(dataSO), nameof(InitDict), showDialog: false);
				if (BadIdDataSOs.Count > 0)
					IdChanger.StartProcessBadIdDataSOs();

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"{nameof(InitDict)} 중 오류 발생: {ex.Message}");
				return false;
			}
			
			bool ProcessDataSO(DataSO dataSO)
			{
				try
				{
					if (TryGetBaseType(dataSO, out Type type) == false)
					{
						Debug.LogWarning($"Base type not found: {dataSO.name}");
						return false;
					}

					// Debug.Log($"ProcessDataSO({dataSO.ID}, {dataSO.Name}): to {type}");
				
					Dictionary<int, DataSO> dataSOs = DataSOs[type];

					if (dataSOs.ContainsKey(dataSO.ID) == true)
					{
						Debug.LogWarning($"이미 존재하는 키입니다. {dataSO.Name}.{dataSO.ID}");
						ProcessBadIdData(dataSO, type, dataSOs);
						return false;
					}

					dataSOs.Add(dataSO.ID, dataSO);

					SetCorrectAssetName(dataSO);
					return true;
				}
				catch (Exception ex)
				{
					Debug.LogError($"{nameof(ProcessDataSO)} 중 오류 발생: {dataSO}, {dataSO.name} = {ex.Message}");
					return false;
				}
			}

			void ProcessBadIdData(DataSO dataSO, Type type, Dictionary<int, DataSO> dataSOs)
			{
				try
				{
					// Type 별 Dictionary 존재 확인
					if (BadIdDataSOs.ContainsKey(type) == false)
					{
						BadIdDataSOs.Add(type, new());
					}

					// ID 별 List 존재 확인
					if (BadIdDataSOs[type].ContainsKey(dataSO.ID) == false)
					{
						BadIdDataSOs[type].Add(dataSO.ID, new());

						// 이미 존재하던 데이터 추가
						if (BadIdDataSOs[type][dataSO.ID].Contains(dataSOs[dataSO.ID]) == false)
							BadIdDataSOs[type][dataSO.ID].Add(dataSOs[dataSO.ID]);
					}

					// 현재 데이터 추가
					if (BadIdDataSOs[type][dataSO.ID].Contains(dataSO) == false)
						BadIdDataSOs[type][dataSO.ID].Add(dataSO);
				}
				catch (Exception ex)
				{
					Debug.LogError($"{nameof(ProcessBadIdData)} 중 오류 발생: {dataSO}, {type} = {ex.Message}");
				}
			}
		}

		public DataSO AddDataSO(Type type, int newID = -1, string newName = null, string assetPath = null)
		{
			Debug.Log(nameof(AddDataSO));

			ShowDataSOWindow();

			Dictionary<int, DataSO> dataSOs = GetDataSOs(type);

			// 사용되지 않은 ID를 찾는다.
			if (newID == -1)
			{
				newID = 0;
				while (dataSOs.ContainsKey(newID))
					newID++;
			}

			newName ??= $"New_{type.Name}";

			string assetName = ConvertToCorrectAssetName($"{AssetPrefixes[type]}_{newID}_{newName}");
			string path = AssetDatabase.GenerateUniqueAssetPath($"{assetPath}{assetName}.asset");

			Debug.Log($"AddDataSO: {type.Name} {newID} {newName} {path}");

			DataSO newDataSO = CreateInstance(type) as DataSO;
			AssetDatabase.CreateAsset(newDataSO, path);
			newDataSO.ID = newID;
			newDataSO.Name = newName;

			// Addressable 에셋으로 설정
			SetAddressableAsset(newDataSO);

			EditorUtility.SetDirty(newDataSO);
			AssetDatabase.SaveAssets();

			dataSOs.Add(newID, newDataSO);

			// TODO:
			// if (isInit == true)
			// {
			// 	SetType(type);
			// 	SelectDataSOSlot(DataSOSlots[newID]);
			// }

			return newDataSO;
		}

		public Dictionary<int, DataSO> GetDataSOs(Type type)
		{
			if (DataSOs.ContainsKey(type) == false)
				InitDict();
				// InitDict(type);

			return DataSOs[type];
		}

		public DataSO CopyDataSO(DataSO dataSO)
		{
			// Debug.Log(nameof(CopyDataSO));

			ShowDataSOWindow();
		
			if (TryGetBaseType(dataSO, out Type type) == false)
			{
				Debug.LogError("Base type not found");
				return null;
			}

			Dictionary<int, DataSO> dataSOs = GetDataSOs(type);

			if ((type == typeof(DataSO)) || (dataSOs.ContainsKey(dataSO.ID) == false))
			{
				Debug.LogError("복사할 수 없는 데이터입니다.");
				return null;
			}
			string newName = dataSO.Name;

			// 기존 데이터가 숫자로 끝나면, 해당 숫자에 1을 더한 값을 붙인다.
			Match match = Regex.Match(newName, @"\d+$");
			if (match.Success)
			{
				string number = match.Value;
				newName = newName[..^number.Length] + (int.Parse(number) + 1);
				// ..^number.Length: 문자열의 뒤에서부터 number.Length만큼 제외한 문자열
			}
			// 아니라면 "_Copy"를 붙인다.
			else
			{
				newName += "_Copy";
			}

			// 사용되지 않은 ID를 찾는다.
			int newAssetID = dataSO.ID + 1;
			while (dataSOs.ContainsKey(newAssetID))
				newAssetID++;

			string newAssetName = ConvertToCorrectAssetName($"{AssetPrefixes[type]}_{newAssetID}_{newName}");

			// 복사하려는 데이터의 파일 경로를 가져온다.
			string dataSOPath = AssetDatabase.GetAssetPath(dataSO);
			string dataSODir = Path.GetDirectoryName(dataSOPath);

			// 해당 경로에 새로운 이름으로 복사한다.
			string path = AssetDatabase.GenerateUniqueAssetPath($"{dataSODir}/{newAssetName}.asset");
			if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dataSO), path))
			{
				DataSO newDataSO = AssetDatabase.LoadAssetAtPath<DataSO>(path);
				newDataSO.ID = newAssetID;
				newDataSO.Name = newName;

				// Addressable 에셋으로 설정
				SetAddressableAsset(newDataSO);

				dataSOs.Add(newDataSO.ID, newDataSO);
				Debug.Log($"복사 완료: {newDataSO.ID} {newDataSO.Name}");

				if (UpdateGrid())
				{
					if (CurType != type)
					{
						SetType(type);
					}

					Selection.activeObject = newDataSO;
					SelectDataSOSlot(DataSOSlots[newDataSO.ID]);
				}
				return newDataSO;
			}
			else
			{
				Debug.LogError("복사 실패");
				return null;
			}
		}

		public void RemoveDataSO(DataSO dataSO)
		{
			Debug.Log(nameof(RemoveDataSO));

			ShowDataSOWindow();
	
			if (TryGetBaseType(dataSO, out Type type) == false)
			{
				Debug.LogError("Base type not found");
				return;
			}

			Dictionary<int, DataSO> dataSOs = GetDataSOs(type);

			if ((type == typeof(DataSO)) || (dataSOs.ContainsKey(dataSO.ID) == false))
			{
				Debug.LogWarning($"dataSOs({type})에 등록되지 않은 데이터를 삭제 합니다.");
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(dataSO));
				return;
			}

			int id = dataSO.ID;

			if (dataSOs.ContainsKey(id))
				dataSOs.Remove(dataSO.ID);
			AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(dataSO));

			UpdateGrid();

			SelectDataSOSlot(GetNearSlot(id));
		}

		private DataSOSlot GetNearSlot(int startID)
		{
			DataSOSlot slot = null;
			for (int newID = startID; newID < ID_MAX; newID++)
			{
				if (DataSOSlots.TryGetValue(newID, out slot))
					break;
			}
			if (slot == null)
			{
				for (int newID = startID; newID >= 0; newID--)
				{
					if (DataSOSlots.TryGetValue(newID, out slot))
						break;
				}
			}
			return slot;
		}

		public DataSOSlot GetDataSOSlot(DataSO dataSO)
		{
			if (TryGetBaseType(dataSO, out Type type) == false)
			{
				Debug.LogError("Base type not found");
				return null;
			}

			if (DataSOs.ContainsKey(type) == false)
				return null;

			Dictionary<int, DataSO> dataSOs = GetDataSOs(type);
			if (dataSOs.ContainsKey(dataSO.ID) == false)
				return null;

			if (isInit == false)
			{
				// Debug.LogWarning("초기화되지 않았습니다.");
				return null;
			}

			if (DataSOSlots.ContainsKey(dataSO.ID) == false)
			{
				// Debug.LogWarning("선택할 슬롯이 없습니다.");
				return null;
			}

			return DataSOSlots[dataSO.ID];
		}

		public void SelectDataSOSlot(DataSOSlot slot)
		{
			// Debug.Log(nameof(SelectDataSOSlot));

			ShowDataSOWindow();

			if (isInit == false)
			{
				// Debug.LogWarning("초기화되지 않았습니다.");
				return;
			}

			if (slot == null)
			{
				// Debug.LogWarning("선택할 슬롯이 없습니다.");
				return;
			}

			if (TryGetBaseType(slot.DataSO, out Type type) == false)
			{
				Debug.LogError("Base type not found");
				return;
			}

			if (CurType != type)
				SetType(type);

			DataSOSlot oldSlot = CurSlot;
			CurSlot = slot;
			oldSlot?.UpdateUI();
			CurSlot.UpdateUI();

			// Debug.Log($"{nameof(SelectDataSOSlot)} End");
		}

		private void InitEnumData<TData, TEnum>() where TData : DataSO
		{
			// Debug.Log($"{nameof(InitEnumData)} <{typeof(TData).Name}, {typeof(TEnum).Name}>");

			const string PropertyName = "Type";

			Dictionary<int, DataSO> dataSOs = DataSOs[typeof(TData)];
			foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
			{
				if (dataSOs.TryGetValue(Convert.ToInt32(enumValue), out DataSO dataSO))
				{
					TData typedData = dataSO as TData;

					string goodName = Enum.GetName(typeof(TEnum), enumValue);
					if (typedData.Name != goodName)
					{
						Debug.Log($"{typedData.name}의 이름을 업데이트합니다. {typedData.Name} -> {goodName}");
						typedData.Name = goodName;
						EditorUtility.SetDirty(typedData);
					}

					PropertyInfo typeProperty = typeof(TData).GetProperty(PropertyName);
					if (enumValue.ToString() != typeProperty.GetValue(typedData).ToString())
					{
						Debug.Log($"{typedData.name}의 Type을 업데이트합니다. {typeProperty.GetValue(typedData)} -> {enumValue}");
						typeProperty.SetValue(typedData, (int)Enum.Parse(typeof(TEnum), enumValue.ToString()));
						EditorUtility.SetDirty(typedData);
					}
				}
				else
				{
					Debug.Log($"Data를 추가합니다.");
					int newID = Convert.ToInt32(enumValue);
					string newName = Enum.GetName(typeof(TEnum), enumValue);

					TData typedData = AddDataSO(typeof(TData), newID, newName, BASE_DIR) as TData;
					PropertyInfo typeProperty = typeof(TData).GetProperty(PropertyName);
					typeProperty.SetValue(typedData, newID);
				}
			}

			// 유효하지 않은 데이터 삭제
			foreach ((int key, DataSO value) in dataSOs)
			{
				if (Enum.IsDefined(typeof(TEnum), key) == false)
				{
					Debug.Log($"{value.name}을 삭제합니다.");
					RemoveDataSO(value);
				}
			}

			// Debug.Log($"{nameof(InitEnumData)} End");
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