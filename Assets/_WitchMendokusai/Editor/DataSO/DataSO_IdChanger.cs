using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static WitchMendokusai.DataSOUtil;

namespace WitchMendokusai
{
	public class DataSO_IdChanger
	{
		private bool processBadIdDataSOs = false;

		public DataSO CurDataSO { get; private set; }

		private VisualElement root;
		private VisualElement thisRoot;
		private VisualElement badIdDataSOsTitle;
		private VisualElement badIdDataSOsRoot;
		private VisualElement targetDataSOsRoot;

		private DataSOSlot origin;
		private DataSOSlot target;

		private Button changeButton;
		private Button deleteButton;
		private Button closeButton;

		public DataSO_IdChanger()
		{
			Init();
		}

		private void Init()
		{
			root = DataSOWindow.Instance.rootVisualElement;

			thisRoot = root.Q<VisualElement>(name: "IDChanger");
			badIdDataSOsTitle = thisRoot.Q<VisualElement>(name: "BadIdDataSOsTitle");
			badIdDataSOsRoot = thisRoot.Q<VisualElement>(name: "BadIdDataSOs");
			targetDataSOsRoot = thisRoot.Q<VisualElement>(name: "TargetDataSOs");

			origin = new DataSOSlot(null);
			target = new DataSOSlot(null);
			targetDataSOsRoot.Add(origin.VisualElement);
			targetDataSOsRoot.Add(target.VisualElement);

			changeButton = thisRoot.Q<Button>(name: "BTN_ChangeID");
			changeButton.clicked += ChangeID;

			deleteButton = thisRoot.Q<Button>(name: "BTN_Del");
			deleteButton.clicked += Delete;

			closeButton = thisRoot.Q<Button>(name: "BTN_Close");
			closeButton.clicked += Close;

			IntegerField integerField = thisRoot.Q<IntegerField>(name: "IdField");
			integerField.RegisterValueChangedCallback(CheckID);

			UpdateUI();
			DataSOWindow.Instance.Repaint();
		}

		public void SelectDataSO(DataSO dataSO)
		{
			CurDataSO = dataSO;
			UpdateUI();
		}

		public void StartProcessBadIdDataSOs()
		{
			Debug.Log(nameof(StartProcessBadIdDataSOs));

			if (DataSOWindow.Instance.BadIDDataSOs.Count == 0)
			{
				Debug.Log("No bad ID DataSOs");
				processBadIdDataSOs = false;
				UpdateUI();
				return;
			}

			processBadIdDataSOs = true;

			List<DataSO> curBadIdDataSOs = DataSOWindow.Instance.BadIDDataSOs.Values.First();
			badIdDataSOsRoot.Clear();
			foreach (DataSO badIdData in curBadIdDataSOs)
			{
				DataSOSlot target = new((slot) => Selection.activeObject = slot.DataSO);
				target.SetDataSO(badIdData);
				badIdDataSOsRoot.Add(target.VisualElement);
			}

			DataSOWindow.Instance.Repaint();

			SelectDataSO(curBadIdDataSOs[0]);
		}

		private void UpdateUI()
		{
			// Debug.Log($"{nameof(DataSOWindow_IdChanger)}.{nameof(UpdateUI)}");

			origin.SetDataSO(CurDataSO);
			target.SetDataSO(null);

			badIdDataSOsTitle.style.display = processBadIdDataSOs ? DisplayStyle.Flex : DisplayStyle.None;
			badIdDataSOsRoot.style.display = processBadIdDataSOs ? DisplayStyle.Flex : DisplayStyle.None;
			thisRoot.style.display = CurDataSO == null ? DisplayStyle.None : DisplayStyle.Flex;
			deleteButton.SetEnabled(processBadIdDataSOs);
			closeButton.SetEnabled(processBadIdDataSOs == false);

			// Debug.Log($"{nameof(DataSOWindow_IdChanger)}.{nameof(UpdateUI)} End");
		}

		private void CheckID(ChangeEvent<int> evt)
		{
			Debug.Log($"{nameof(DataSO_IdChanger)}.{nameof(CheckID)} : {evt.newValue}");

			if (CurDataSO == null)
				return;

			int newID = evt.newValue;
			if (newID == CurDataSO.ID)
				return;

			Type type = GetBaseType(CurDataSO);
			if (DataSOWindow.Instance.DataSOs[type].TryGetValue(newID, out DataSO existingDataSO))
			{
				Debug.Log("ID already exists");
				target.SetDataSO(existingDataSO);
				changeButton.SetEnabled(false);
			}
			else
			{
				target.SetDataSO(null);
				changeButton.SetEnabled(true);
			}

			Debug.Log($"{nameof(DataSO_IdChanger)}.{nameof(CheckID)} End");
		}

		private void ChangeID()
		{
			Debug.Log($"{nameof(DataSO_IdChanger)}.{nameof(ChangeID)}");

			if (CurDataSO == null)
				return;

			int newID = thisRoot.Q<IntegerField>(name: "IdField").value;
			if (newID == CurDataSO.ID)
				return;

			Type type = GetBaseType(CurDataSO);
			if (DataSOWindow.Instance.DataSOs.TryGetValue(type, out Dictionary<int, DataSO> dataSOs) == false)
			{
				Debug.Log("DataSOs not found");
				DataSOWindow.Instance.InitDict(type);
			}

			if (dataSOs.TryGetValue(newID, out DataSO existingDataSO))
			{
				Debug.Log("ID already exists");
				return;
			}

			dataSOs.TryGetValue(CurDataSO.ID, out DataSO dataSO);
			if (processBadIdDataSOs && (CurDataSO != dataSO))
			{
				// processBadIdDataSOs

				CurDataSO.ID = newID;

				List<DataSO> curBadIdDataSOs = DataSOWindow.Instance.BadIDDataSOs.Values.First();
				int id = curBadIdDataSOs[0].ID;
				dataSOs.Add(newID, CurDataSO);
				curBadIdDataSOs.Remove(CurDataSO);
				if (curBadIdDataSOs.Count == 1)
					DataSOWindow.Instance.BadIDDataSOs.Remove(id);

				CurDataSO = null;
				StartProcessBadIdDataSOs();
			}
			else
			{
				// (단순 ID 변경) 혹은 (processBadIdDataSOs이지만, DataSOs에 등록된 DataSO의 ID 변경)

				dataSOs.Remove(CurDataSO.ID);
				CurDataSO.ID = newID;
				dataSOs.Add(newID, CurDataSO);
				DataSOUtil.SaveAsset(CurDataSO);

				DataSOWindow.Instance.UpdateGrid();
				DataSOWindow.Instance.SelectDataSOSlot(DataSOWindow.Instance.DataSOSlots[CurDataSO.ID]);

				CurDataSO = null;
				UpdateUI();
			}

			Debug.Log($"{nameof(DataSO_IdChanger)}.{nameof(ChangeID)} End");
		}

		private void Delete()
		{
			Debug.Log(nameof(Delete));

			if (CurDataSO == null)
				return;

			DataSOWindow.Instance.RemoveDataSO(CurDataSO);

			if (processBadIdDataSOs)
			{
				List<DataSO> curBadIdDataSOs = DataSOWindow.Instance.BadIDDataSOs.Values.First();
				int id = curBadIdDataSOs[0].ID;
				curBadIdDataSOs.Remove(CurDataSO);
				if (curBadIdDataSOs.Count == 1)
					DataSOWindow.Instance.BadIDDataSOs.Remove(id);

				StartProcessBadIdDataSOs();
			}
			else
			{
				CurDataSO = null;
				UpdateUI();
			}
		}

		private void Close()
		{
			CurDataSO = null;
			UpdateUI();
		}
	}
}