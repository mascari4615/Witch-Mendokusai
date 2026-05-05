// TASK-WM-038 단계 B — DataSO Type 1개당 IEditableEntryProvider 어댑터.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// DataSO Type 1개당 IEditableEntryProvider. DataSOWindow 가 Type 전환 시 새 provider 생성.
	/// (구) DataSOSlot 그리드 빌드 + Selection.activeObject 연동 + Add/Copy/Remove 위임.
	/// </summary>
	public class DataSOTypeProvider : IEditableEntryProvider
	{
		private readonly Type type;
		private readonly Dictionary<int, DataSO> dataSOs;
		private readonly List<EntryDescriptor> entries = new();

		public string Id => type.Name;
		public string DisplayName => type.Name;
		public Sprite Icon => null;
		public IReadOnlyList<string> SubGroups => null;

		public bool CanAdd => true;
		public bool CanCopy => true;
		public bool CanRemove => true;

		public DataSOTypeProvider(Type type, Dictionary<int, DataSO> dataSOs)
		{
			this.type = type;
			this.dataSOs = dataSOs;
		}

		public void OnActivate()
		{
			entries.Clear();
			List<DataSO> sorted = dataSOs.Values.ToList();
			sorted.Sort((a, b) => a.ID.CompareTo(b.ID));
			foreach (DataSO dataSO in sorted)
			{
				if (dataSO == null)
					continue;
				entries.Add(new EntryDescriptor(
					id: dataSO.ID.ToString(),
					displayName: dataSO.Name,
					icon: dataSO.Sprite,
					source: dataSO));
			}
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<EntryDescriptor> GetEntries() => entries;

		/// <summary>Inspector 가 디테일 책임 — 빈 element 반환.</summary>
		public VisualElement BuildDetail(EntryDescriptor entry) => new();

		public void Add() => DataSOWindow.Instance.AddDataSO(type);

		public void Copy(EntryDescriptor entry)
		{
			if (entry.Source is DataSO dataSO)
				DataSOWindow.Instance.CopyDataSO(dataSO);
		}

		public void Remove(EntryDescriptor entry)
		{
			if (entry.Source is DataSO dataSO)
				DataSOWindow.Instance.RemoveDataSO(dataSO);
		}

		public void OnEntryActivated(EntryDescriptor entry)
		{
			if (entry.Source is UnityEngine.Object asset)
				Selection.activeObject = asset;
		}
	}
}
