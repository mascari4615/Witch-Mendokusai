using System;
using System.Collections.Generic;
using System.Linq;
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

		public DataSO CurDataSO { get; private set; } = null;

		private VisualElement root = null;
		private VisualElement thisRoot = null;
		private VisualElement badIdDataSOsTitle = null;
		private VisualElement badIdDataSOsRoot = null;
		private VisualElement targetDataSOsRoot = null;

		private DataSO targetDataSO = null;

		private Button changeButton = null;
		private Button deleteButton = null;
		private Button closeButton = null;

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
			DataSOWindow.ShowDataSOWindow();
			UpdateUI();
		}

		public void StartProcessBadIdDataSOs()
		{
			Debug.Log(nameof(StartProcessBadIdDataSOs));

			if (DataSOWindow.Instance.BadIdDataSOs.Count == 0)
			{
				Debug.Log("No bad ID DataSOs");
				processBadIdDataSOs = false;
				UpdateUI();
				return;
			}

			processBadIdDataSOs = true;

			List<DataSO> curBadIdDataSOs = DataSOWindow.Instance.BadIdDataSOs.Values.First().Values.First();
			badIdDataSOsRoot.Clear();
			foreach (DataSO badIdData in curBadIdDataSOs)
			{
				DiscoveryCard card = new(ToEntry(badIdData));
				DataSO captured = badIdData;
				card.OnClicked += () => Selection.activeObject = captured;
				badIdDataSOsRoot.Add(card);
			}

			DataSOWindow.Instance.Repaint();

			SelectDataSO(curBadIdDataSOs[0]);
		}

		private void UpdateUI()
		{
			targetDataSO = null;
			RebuildTargetCards();

			badIdDataSOsTitle.style.display = processBadIdDataSOs ? DisplayStyle.Flex : DisplayStyle.None;
			badIdDataSOsRoot.style.display = processBadIdDataSOs ? DisplayStyle.Flex : DisplayStyle.None;
			thisRoot.style.display = CurDataSO == null ? DisplayStyle.None : DisplayStyle.Flex;
			deleteButton.SetEnabled(processBadIdDataSOs);
			closeButton.SetEnabled(processBadIdDataSOs == false);
		}

		private void RebuildTargetCards()
		{
			targetDataSOsRoot.Clear();
			if (CurDataSO != null)
				targetDataSOsRoot.Add(new DiscoveryCard(ToEntry(CurDataSO)));
			if (targetDataSO != null)
				targetDataSOsRoot.Add(new DiscoveryCard(ToEntry(targetDataSO)));
		}

		private void CheckID(ChangeEvent<int> evt)
		{
			Debug.Log($"{nameof(DataSO_IdChanger)}.{nameof(CheckID)} : {evt.newValue}");

			if (CurDataSO == null)
				return;

			int newID = evt.newValue;
			if (newID == CurDataSO.ID)
				return;

			if (TryGetBaseType(CurDataSO, out Type type) == false)
			{
				Debug.LogError($"Base type not found for {CurDataSO.name}");
				return;
			}

			Dictionary<int, DataSO> dataSOs = DataSOWindow.Instance.GetDataSOs(type);
			if (dataSOs.TryGetValue(newID, out DataSO existingDataSO))
			{
				Debug.Log("ID already exists");
				targetDataSO = existingDataSO;
				RebuildTargetCards();
				changeButton.SetEnabled(false);
			}
			else
			{
				targetDataSO = null;
				RebuildTargetCards();
				changeButton.SetEnabled(true);
			}
		}

		private void ChangeID()
		{
			Debug.Log($"{nameof(DataSO_IdChanger)}.{nameof(ChangeID)}");

			if (CurDataSO == null)
				return;

			int newID = thisRoot.Q<IntegerField>(name: "IdField").value;
			if (newID == CurDataSO.ID)
				return;

			if (TryGetBaseType(CurDataSO, out Type type) == false)
			{
				Debug.LogError($"Base type not found for {CurDataSO.name}");
				return;
			}

			Dictionary<int, DataSO> dataSOs = DataSOWindow.Instance.GetDataSOs(type);

			if (dataSOs.TryGetValue(newID, out DataSO existingDataSO))
			{
				Debug.Log("ID already exists");
				return;
			}

			dataSOs.TryGetValue(CurDataSO.ID, out DataSO dataSO);
			if (processBadIdDataSOs && (CurDataSO != dataSO))
			{
				CurDataSO.ID = newID;
				SetCorrectAssetName(CurDataSO);

				List<DataSO> curBadIdDataSOs = DataSOWindow.Instance.BadIdDataSOs[type].Values.First();
				int id = curBadIdDataSOs[0].ID;
				dataSOs.Add(newID, CurDataSO);
				curBadIdDataSOs.Remove(CurDataSO);
				if (curBadIdDataSOs.Count == 1)
				{
					DataSOWindow.Instance.BadIdDataSOs[type].Remove(id);

					if (DataSOWindow.Instance.BadIdDataSOs[type].Count == 0)
						DataSOWindow.Instance.BadIdDataSOs.Remove(type);
				}

				CurDataSO = null;
				StartProcessBadIdDataSOs();
			}
			else
			{
				dataSOs.Remove(CurDataSO.ID);
				CurDataSO.ID = newID;
				dataSOs.Add(newID, CurDataSO);
				SaveAsset(CurDataSO);

				if (DataSOWindow.Instance.CurType != type)
					DataSOWindow.Instance.SetType(type);
				else
					DataSOWindow.Instance.UpdateGrid();

				DataSOWindow.Instance.SelectEntryByDataSO(CurDataSO);

				CurDataSO = null;
				UpdateUI();
			}
		}

		private void Delete()
		{
			Debug.Log(nameof(Delete));

			if (CurDataSO == null)
				return;

			if (TryGetBaseType(CurDataSO, out Type type) == false)
			{
				Debug.LogError($"Base type not found for {CurDataSO.name}");
				return;
			}

			DataSOWindow.Instance.RemoveDataSO(CurDataSO);

			if (processBadIdDataSOs)
			{
				List<DataSO> curBadIdDataSOs = DataSOWindow.Instance.BadIdDataSOs[type].Values.First();
				int id = curBadIdDataSOs[0].ID;
				curBadIdDataSOs.Remove(CurDataSO);
				if (curBadIdDataSOs.Count == 1)
				{
					DataSOWindow.Instance.BadIdDataSOs[type].Remove(id);

					if (DataSOWindow.Instance.BadIdDataSOs[type].Count == 0)
						DataSOWindow.Instance.BadIdDataSOs.Remove(type);
				}

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

		private static EntryDescriptor ToEntry(DataSO dataSO)
		{
			return new EntryDescriptor(
				id: dataSO.ID.ToString(),
				displayName: dataSO.Name,
				icon: dataSO.Sprite,
				source: dataSO);
		}
	}
}
