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
	// DataSOWindow 의 자산 다루기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 DataSOWindow.cs 를 본다.
	public partial class DataSOWindow : EditorWindow
	{
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

				InitEnumData<UnitStatData, UnitStatType>();
				InitEnumData<GameStatData, GameStatType>();
				InitEnumData<DungeonStatData, DungeonStatType>();
				InitEnumData<AspectData, AspectType>();
				InitEnumData<UpgradeData, UpgradeType>();

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
			// 	SelectEntryByDataSO(newDataSO);
			// }

			return newDataSO;
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
						SetType(type);

					Selection.activeObject = newDataSO;
					SelectEntryByDataSO(newDataSO);
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

			DataSO nearDataSO = GetNearDataSO(id);
			if (nearDataSO != null)
				SelectEntryByDataSO(nearDataSO);
		}

		private DataSO GetNearDataSO(int startID)
		{
			Dictionary<int, DataSO> dataSOs = DataSOs[CurType];
			for (int newID = startID; newID < ID_MAX; newID++)
			{
				if (dataSOs.TryGetValue(newID, out DataSO found))
					return found;
			}
			for (int newID = startID; newID >= 0; newID--)
			{
				if (dataSOs.TryGetValue(newID, out DataSO found))
					return found;
			}
			return null;
		}

		private void InitEnumData<TData, TEnum>() where TData : DataSO
		{
			Debug.Log($"{nameof(InitEnumData)} <{typeof(TData).Name}, {typeof(TEnum).Name}>");

			const string PropertyName = "Type";

			Dictionary<int, DataSO> dataSOs = DataSOs[typeof(TData)];
			foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
			{
				// 값이 -1이면 무시
				if (Convert.ToInt32(enumValue) == -1)
					continue;

				// 이름이 NONE이면 무시
				string enumName = Enum.GetName(typeof(TEnum), enumValue);
				if (enumName == "NONE")
					continue;

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

					string path = AssetFolderOverride.ContainsKey(typeof(TData)) ?
						AssetFolderOverride[typeof(TData)] : GetSelectedFolderPath();

					TData typedData = AddDataSO(typeof(TData), newID, newName, path) as TData;
					PropertyInfo typeProperty = typeof(TData).GetProperty(PropertyName);
					typeProperty.SetValue(typedData, newID);
				}
			}

			List<DataSO> invalidDataSOs = new();

			// 유효하지 않은 데이터 삭제
			foreach ((int key, DataSO value) in dataSOs)
			{
				if (Enum.IsDefined(typeof(TEnum), key) == false)
				{
					Debug.Log($"{value.name}을 삭제합니다.");
					invalidDataSOs.Add(value);
				}
			}

			foreach (DataSO dataSO in invalidDataSOs)
			{
				RemoveDataSO(dataSO);
			}

			// Debug.Log($"{nameof(InitEnumData)} End");
		}

		private static string GetSelectedFolderPath()
		{
			string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
			if (string.IsNullOrEmpty(assetPath))
				return "Assets/";
			if (Directory.Exists(assetPath))
				return assetPath + "/";
			return Path.GetDirectoryName(assetPath).Replace("\\", "/") + "/";
		}
	}
}
