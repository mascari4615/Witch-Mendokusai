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
using static WitchMendokusai.MDataSOUtil;
using static WitchMendokusai.DataSODefine;

namespace WitchMendokusai
{
	public partial class MDataSO : EditorWindow
	{
		private static MDataSO instance;
		public static MDataSO Instance
		{
			get
			{
				// if (instance == null)
				// {
				// 	ShowMDataSO();
				// }

				return instance;
			}
			private set => instance = value;
		}

		public MDataSO_IdChanger IdChanger { get; private set; }
		public Dictionary<int, MDataSOSlot> DataSOSlots { get; private set; } = new();
		public MDataSOSlot CurSlot { get; private set; }

		public Dictionary<Type, Dictionary<int, DataSO>> DataSOs { get; private set; }
		public Dictionary<int, List<DataSO>> BadIDDataSOs { get; private set; } = new();

		public Type CurType { get; private set; } = null;

		private bool isInit = false;

		[MenuItem("WitchMendokusai/MDataSO")]
		public static void ShowMDataSO()
		{
			// Debug.Log(nameof(ShowMDataSO));
			// (유틸리티 창 여부, 타이틀, 이미 창이 열려있을 때 새로 열지 여부)
			GetWindow<MDataSO>(false, nameof(MDataSO), true);

			// Debug.Log($"{nameof(ShowMDataSO)} End : {instance}");
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
			VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{EDITOR_DIR}MDataSO/MDataSO.uxml");

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

			IdChanger = new();

			SetType(AssetPrefixes.Keys.First());

			isInit = true;
			// Debug.Log($"{nameof(CreateGUI)} End");
		}

		public void UpdateGrid(bool selectFirst = true)
		{
			// Debug.Log($"{nameof(UpdateGrid)}");

			VisualElement grid = rootVisualElement.Q<VisualElement>(name: "Grid");
			if (grid == null)
			{
				// Debug.LogWarning("Grid이 없습니다.");
				// CreateGUI();
				return;
			}
			grid.Clear();

			InitDic(CurType);
			Dictionary<int, DataSO> dataSOs = DataSOs[CurType];

			DataSOSlots.Clear();

			List<DataSO> dataSOsSortByID = dataSOs.Values.ToList();
			dataSOsSortByID.Sort((a, b) => a.ID.CompareTo(b.ID));

			foreach (DataSO dataSO in dataSOsSortByID)
			{
				MDataSOSlot slot = new((slot) => Selection.activeObject = slot.DataSO);
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

			// Debug.Log($"{nameof(UpdateGrid)} End");
		}

		public void SetType(Type type)
		{
			// TODO: 드롭박스 텍스트 변경
			// Debug.Log($"{nameof(SetType)} <{type.Name}>");
			CurType = type;
			UpdateGrid();
			// Debug.Log($"{nameof(SetType)} End");
		}

		private void InitDic(Type type)
		{
			// Debug.Log($"{nameof(InitDic)} <{type.Name}>");

			Dictionary<int, DataSO> dic = DataSOs[type] = new();
			InitDic(ref dic, type, BASE_DIR);
			// SaveAssets();

			if (BadIDDataSOs.Count > 0)
				IdChanger.StartProcessBadIdDataSOs();

			// Debug.Log($"{nameof(InitDic)} End");

			void InitDic(ref Dictionary<int, DataSO> dic, Type type, string dirPath, bool searchSubDir = true)
			{
				const string extension = ".asset";

				DirectoryInfo dir = new(dirPath);
				foreach (FileInfo file in dir.GetFiles())
				{
					if (string.Compare(file.Extension, extension, StringComparison.Ordinal) != 0)
						continue;

					string filePath = $"{dirPath}/{file.Name}";
					Type fileType = AssetDatabase.GetMainAssetTypeAtPath(filePath);

					if (fileType == null)
						continue;

					// 만약 type이 T이거나 T의 하위 클래스가 아니면 Continue
					if (fileType != type && !fileType.IsSubclassOf(type))
						continue;

					DataSO asset = AssetDatabase.LoadAssetAtPath<DataSO>(filePath);
					if (dic.ContainsKey(asset.ID))
					{
						Debug.LogWarning($"이미 존재하는 키입니다. {file.Name}");

						if (BadIDDataSOs.ContainsKey(asset.ID) == false)
						{
							BadIDDataSOs.Add(asset.ID, new());
							if (BadIDDataSOs[asset.ID].Contains(dic[asset.ID]) == false)
								BadIDDataSOs[asset.ID].Add(dic[asset.ID]);
						}

						if (BadIDDataSOs[asset.ID].Contains(asset) == false)
							BadIDDataSOs[asset.ID].Add(asset);
					}
					else
					{
						dic.Add(asset.ID, asset);

						string goodName = GetGoodName(asset);
						if (asset.name.Equals(goodName) == false)
						{
							Debug.Log($"에셋 이름을 변경합니다. {asset.name} -> {goodName}");
							AssetDatabase.RenameAsset(filePath, goodName);
						}

						// 로드한 에셋에도 Addressable 설정 적용
						bool addressableResult = SetAddressableAsset(asset, filePath);
						if (!addressableResult)
						{
							Debug.LogWarning($"Addressable 설정 실패: {asset.name}");
						}
					}
				}

				if (searchSubDir)
				{
					// dir 아래 모든 폴더 안에 있는 파일을 탐색
					foreach (DirectoryInfo subDir in dir.GetDirectories())
						InitDic(ref dic, type, $"{dirPath}/{subDir.Name}/");
				}
			}
		}

		public DataSO AddDataSO(Type type, int nID = -1, string nName = null, string assetPath = null)
		{
			Debug.Log(nameof(AddDataSO));

			Dictionary<int, DataSO> dic = DataSOs[type];

			// 사용되지 않은 ID를 찾는다.
			if (nID == -1)
			{
				nID = 0;
				while (dic.ContainsKey(nID))
					nID++;
			}

			if (nName == null)
				nName = $"New_{type.Name}";

			string assetName = ConvertToGoodName($"{AssetPrefixes[type]}_{nID}_{nName}");
			string path = AssetDatabase.GenerateUniqueAssetPath($"{assetPath}{assetName}.asset");

			Debug.Log($"AddDataSO: {type.Name} {nID} {nName} {path}");

			DataSO newDataSO = CreateInstance(type) as DataSO;
			AssetDatabase.CreateAsset(newDataSO, path);
			newDataSO.ID = nID;
			newDataSO.Name = nName;

			// Addressable 에셋으로 설정
			SetAddressableAsset(newDataSO, path);

			EditorUtility.SetDirty(newDataSO);
			AssetDatabase.SaveAssets();

			dic.Add(nID, newDataSO);

			if (isInit)
			{
				UpdateGrid();
				SelectDataSOSlot(DataSOSlots[nID]);
			}

			return newDataSO;
		}

		public DataSO CopyDataSO(DataSO dataSO)
		{
			Debug.Log(nameof(CopyDataSO));

			Type type = GetBaseType(dataSO);

			if (type == typeof(DataSO) || DataSOs[type].ContainsKey(dataSO.ID) == false)
			{
				Debug.LogError("복사할 수 없는 데이터입니다.");
				return null;
			}

			Dictionary<int, DataSO> dic = DataSOs[type];

			string nName = dataSO.Name;

			// 기존 데이터가 숫자로 끝나면, 해당 숫자에 1을 더한 값을 붙인다.
			Match match = Regex.Match(nName, @"\d+$");
			if (match.Success)
			{
				string number = match.Value;
				nName = nName.Substring(0, nName.Length - number.Length) + (int.Parse(number) + 1);
			}
			// 아니라면 "_Copy"를 붙인다.
			else
			{
				nName += "_Copy";
			}

			// 사용되지 않은 ID를 찾는다.
			int nID = dataSO.ID + 1;
			while (dic.ContainsKey(nID))
				nID++;

			string assetName = ConvertToGoodName($"{AssetPrefixes[type]}_{nID}_{nName}");

			// 복사하려는 데이터의 파일 경로를 가져온다.
			string dataSOPath = AssetDatabase.GetAssetPath(dataSO);
			string dataSODir = Path.GetDirectoryName(dataSOPath);

			// 해당 경로에 새로운 이름으로 복사한다.
			string path = AssetDatabase.GenerateUniqueAssetPath($"{dataSODir}/{assetName}.asset");
			if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dataSO), path))
			{
				DataSO newDataSO = AssetDatabase.LoadAssetAtPath<DataSO>(path);
				newDataSO.ID = nID;
				newDataSO.Name = nName;

				// Addressable 에셋으로 설정
				SetAddressableAsset(newDataSO, path);


				dic.Add(newDataSO.ID, newDataSO);
				Debug.Log($"복사 완료: {newDataSO.ID} {newDataSO.Name}");

				UpdateGrid();
				SelectDataSOSlot(DataSOSlots[newDataSO.ID]);
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

			Type type = GetBaseType(dataSO);

			if (type == typeof(DataSO) || DataSOs[type].ContainsKey(dataSO.ID) == false)
			{
				Debug.LogError("삭제할 수 없는 데이터입니다.");
				return;
			}

			Dictionary<int, DataSO> dic = DataSOs[type];

			int id = dataSO.ID;

			if (dic.ContainsKey(id))
				dic.Remove(dataSO.ID);
			AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(dataSO));

			UpdateGrid();

			SelectDataSOSlot(GetNearSlot(id));
		}

		private MDataSOSlot GetNearSlot(int startID)
		{
			MDataSOSlot slot = null;
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

		public MDataSOSlot GetDataSOSlot(DataSO dataSO)
		{
			Type type = GetBaseType(dataSO);
			if (DataSOs.ContainsKey(type) == false)
				return null;

			Dictionary<int, DataSO> dic = DataSOs[type];
			if (dic.ContainsKey(dataSO.ID) == false)
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

		public void SelectDataSOSlot(MDataSOSlot slot)
		{
			// Debug.Log(nameof(SelectDataSOSlot));

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

			Type type = GetBaseType(slot.DataSO);
			if (CurType != type)
				SetType(type);

			MDataSOSlot oldSlot = CurSlot;
			CurSlot = slot;
			oldSlot?.UpdateUI();
			CurSlot.UpdateUI();

			// Debug.Log($"{nameof(SelectDataSOSlot)} End");
		}

		public void InitEnumData<TData, TEnum>() where TData : DataSO
		{
			// Debug.Log($"{nameof(InitEnumData)} <{typeof(TData).Name}, {typeof(TEnum).Name}>");

			InitDic(typeof(TData));

			const string PropertyName = "Type";

			var dic = DataSOs[typeof(TData)];
			foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
			{
				if (dic.TryGetValue(Convert.ToInt32(enumValue), out DataSO dataSO))
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
					Type type = typeof(TData);
					int nID = Convert.ToInt32(enumValue);
					string nName = Enum.GetName(typeof(TEnum), enumValue);

					//FIXME: 경로 수정
					TData typedData = AddDataSO(type, nID, nName, BASE_DIR) as TData;
					PropertyInfo typeProperty = typeof(TData).GetProperty(PropertyName);
					typeProperty.SetValue(typedData, nID);
				}
			}

			// 유효하지 않은 데이터 삭제
			foreach (var (key, value) in dic)
			{
				if (Enum.IsDefined(typeof(TEnum), key) == false)
				{
					Debug.Log($"{value.name}을 삭제합니다.");
					RemoveDataSO(value);
				}
			}

			// Debug.Log($"{nameof(InitEnumData)} End");
		}

		[MenuItem("WitchMendokusai/SaveAssets")]
		public static void SaveAssets()
		{
			Debug.Log(nameof(SaveAssets));

			if (Instance == null)
			{
				Debug.LogError("인스턴스가 존재하지 않습니다.");
				return;
			}

			foreach (var dic in Instance.DataSOs.Values)
				foreach (DataSO dataSO in dic.Values)
				{
					if (dataSO == null)
					{
						Debug.Log($"{nameof(SaveAssets)}: {dic.GetType().Name} is null.");
						continue;
					}
					EditorUtility.SetDirty(dataSO);
				}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// Debug.Log($"{nameof(SaveAssets)} is executed.");
		}

		private string GetGoodName(DataSO dataSO)
		{
			return ConvertToGoodName($"{AssetPrefixes[GetBaseType(dataSO)]}_{dataSO.ID}_{dataSO.Name}");
		}

		private string ConvertToGoodName(string name)
		{
			// 파일 이름에 사용할 수 없는 문자와 공백을 제거
			Regex regex = new(string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()) + " ")));
			return regex.Replace(name, string.Empty);
		}

		private void OnValidate()
		{
			// Debug.Log("OnValidate is executed.");
		}

		private void OnFocus()
		{
			// Debug.Log("OnFocus is executed.");
		}

		private void OnLostFocus()
		{
			// Debug.Log("OnLostFocus is executed.");
		}

		private void OnProjectChange()
		{
			// Debug.Log("OnProjectChange is executed.");
		}

		private void OnSelectionChange()
		{
			// Debug.Log("OnSelectionChange is executed.");
		}

		private void OnInspectorUpdate()
		{
			// Debug.Log("OnInspectorUpdate is executed.");
		}

		private void OnHierarchyChange()
		{
			// Debug.Log("OnHierarchyChange is executed.");
		}

		private void OnGUI()
		{
			// Debug.Log("OnGUI is executed.");
		}
	}
}