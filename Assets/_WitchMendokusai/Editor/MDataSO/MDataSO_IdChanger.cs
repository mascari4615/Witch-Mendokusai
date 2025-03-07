using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static WitchMendokusai.MDataSOUtil;

namespace WitchMendokusai
{
	public class MDataSO_IdChanger
	{
		private bool processBadIdDataSOs = false;

		public DataSO CurDataSO { get; private set; }

		private VisualElement root;
		private VisualElement thisRoot;
		private VisualElement badIdDataSOsTitle;
		private VisualElement badIdDataSOsRoot;
		private VisualElement targetDataSOsRoot;

		private MDataSOSlot origin;
		private MDataSOSlot target;

		private Button changeButton;
		private Button deleteButton;
		private Button closeButton;

		public MDataSO_IdChanger()
		{
			Init();
		}

		private void Init()
		{
			root = MDataSO.Instance.rootVisualElement;

			thisRoot = root.Q<VisualElement>(name: "IDChanger");
			badIdDataSOsTitle = thisRoot.Q<VisualElement>(name: "BadIdDataSOsTitle");
			badIdDataSOsRoot = thisRoot.Q<VisualElement>(name: "BadIdDataSOs");
			targetDataSOsRoot = thisRoot.Q<VisualElement>(name: "TargetDataSOs");

			origin = new MDataSOSlot(null);
			target = new MDataSOSlot(null);
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
			MDataSO.Instance.Repaint();
		}

		public void SelectDataSO(DataSO dataSO)
		{
			CurDataSO = dataSO;
			UpdateUI();
		}

		public void StartProcessBadIdDataSOs()
		{
			Debug.Log(nameof(StartProcessBadIdDataSOs));

			if (MDataSO.Instance.BadIDDataSOs.Count == 0)
			{
				Debug.Log("No bad ID DataSOs");
				processBadIdDataSOs = false;
				UpdateUI();
				return;
			}

			processBadIdDataSOs = true;

			List<DataSO> curBadIdDataSOs = MDataSO.Instance.BadIDDataSOs.Values.First();
			badIdDataSOsRoot.Clear();
			foreach (DataSO badIdData in curBadIdDataSOs)
			{
				MDataSOSlot target = new((slot) => Selection.activeObject = slot.DataSO);
				target.SetDataSO(badIdData);
				badIdDataSOsRoot.Add(target.VisualElement);
			}

			MDataSO.Instance.Repaint();

			SelectDataSO(curBadIdDataSOs[0]);
		}

		private void UpdateUI()
		{
			// Debug.Log($"{nameof(MDataSO_IdChanger)}.{nameof(UpdateUI)}");

			origin.SetDataSO(CurDataSO);
			target.SetDataSO(null);

			badIdDataSOsTitle.style.display = processBadIdDataSOs ? DisplayStyle.Flex : DisplayStyle.None;
			badIdDataSOsRoot.style.display = processBadIdDataSOs ? DisplayStyle.Flex : DisplayStyle.None;
			thisRoot.style.display = CurDataSO == null ? DisplayStyle.None : DisplayStyle.Flex;
			deleteButton.SetEnabled(processBadIdDataSOs);
			closeButton.SetEnabled(processBadIdDataSOs == false);

			// Debug.Log($"{nameof(MDataSO_IdChanger)}.{nameof(UpdateUI)} End");
		}

		private void CheckID(ChangeEvent<int> evt)
		{
			Debug.Log($"{nameof(MDataSO_IdChanger)}.{nameof(CheckID)} : {evt.newValue}");

			if (CurDataSO == null)
				return;

			int newID = evt.newValue;
			if (newID == CurDataSO.ID)
				return;

			Type type = GetBaseType(CurDataSO);
			if (MDataSO.Instance.DataSOs[type].TryGetValue(newID, out DataSO existingDataSO))
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

			Debug.Log($"{nameof(MDataSO_IdChanger)}.{nameof(CheckID)} End");
		}

		private void ChangeID()
		{
			Debug.Log($"{nameof(MDataSO_IdChanger)}.{nameof(ChangeID)}");

			if (CurDataSO == null)
				return;

			int newID = thisRoot.Q<IntegerField>(name: "IdField").value;
			if (newID == CurDataSO.ID)
				return;

			Type type = GetBaseType(CurDataSO);
			if (MDataSO.Instance.DataSOs[type].TryGetValue(newID, out DataSO existingDataSO))
			{
				Debug.Log("ID already exists");
				return;
			}

			MDataSO.Instance.DataSOs[type].TryGetValue(CurDataSO.ID, out DataSO temp);

			if (processBadIdDataSOs && CurDataSO != temp)
			{
				// processBadIdDataSOs

				CurDataSO.ID = newID;

				List<DataSO> curBadIdDataSOs = MDataSO.Instance.BadIDDataSOs.Values.First();
				int id = curBadIdDataSOs[0].ID;
				MDataSO.Instance.DataSOs[type].Add(newID, CurDataSO);
				curBadIdDataSOs.Remove(CurDataSO);
				if (curBadIdDataSOs.Count == 1)
					MDataSO.Instance.BadIDDataSOs.Remove(id);

				CurDataSO = null;
				StartProcessBadIdDataSOs();
			}
			else
			{
				// (단순 ID 변경) 혹은 (processBadIdDataSOs이지만, DataSOs에 등록된 DataSO의 ID 변경)

				MDataSO.Instance.DataSOs[type].Remove(CurDataSO.ID);
				CurDataSO.ID = newID;
				MDataSO.Instance.DataSOs[type].Add(newID, CurDataSO);
				MDataSO.SaveAssets();

				MDataSO.Instance.UpdateGrid();
				MDataSO.Instance.SelectDataSOSlot(MDataSO.Instance.DataSOSlots[CurDataSO.ID]);

				CurDataSO = null;
				UpdateUI();
			}

			Debug.Log($"{nameof(MDataSO_IdChanger)}.{nameof(ChangeID)} End");
		}

		private void Delete()
		{
			Debug.Log(nameof(Delete));

			if (CurDataSO == null)
				return;

			MDataSO.Instance.RemoveDataSO(CurDataSO);

			if (processBadIdDataSOs)
			{
				List<DataSO> curBadIdDataSOs = MDataSO.Instance.BadIDDataSOs.Values.First();
				int id = curBadIdDataSOs[0].ID;
				curBadIdDataSOs.Remove(CurDataSO);
				if (curBadIdDataSOs.Count == 1)
					MDataSO.Instance.BadIDDataSOs.Remove(id);

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