using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class DollList : VisualElement
	{
		public const string USS_CLASS = "wm-doll-list";
		public const string USS_SCROLL = "wm-doll-list__scroll";

		public event Action<Doll> OnDollSelected = delegate { };

		private DollBuffer buffer;
		private DollEntry selectedEntry;
		private readonly List<DollEntry> entries = new();
		private readonly ScrollView scrollView;

		public DollList()
		{
			AddToClassList(USS_CLASS);

			scrollView = new ScrollView();
			scrollView.AddToClassList(USS_SCROLL);
			Add(scrollView);
		}

		public void Bind(DollBuffer dollBuffer)
		{
			if (buffer != null)
				buffer.OnDataChanged -= Refresh;

			buffer = dollBuffer;

			if (buffer == null)
				return;

			buffer.OnDataChanged += Refresh;
			Refresh();
		}

		public void Unbind()
		{
			if (buffer != null)
				buffer.OnDataChanged -= Refresh;
			buffer = null;
		}

		public void Refresh()
		{
			if (buffer == null)
				return;

			List<Doll> dolls = buffer.Data;

			while (entries.Count > dolls.Count)
			{
				DollEntry last = entries[^1];
				scrollView.Remove(last);
				entries.RemoveAt(entries.Count - 1);
			}

			while (entries.Count < dolls.Count)
			{
				DollEntry entry = new();
				entry.RegisterCallback<PointerDownEvent>(_ => Select(entry));
				scrollView.Add(entry);
				entries.Add(entry);
			}

			for (int i = 0; i < dolls.Count; i++)
			{
				bool isDummy = dolls[i] != null && dolls[i].ID == Doll.DUMMY_ID;
				entries[i].Bind(dolls[i]);
				entries[i].style.display = isDummy ? DisplayStyle.None : DisplayStyle.Flex;
			}

			if (selectedEntry != null && selectedEntry.Doll != null && dolls.Contains(selectedEntry.Doll) == false)
				Select(FindFirstSelectable());
		}

		private DollEntry FindFirstSelectable()
		{
			foreach (DollEntry entry in entries)
			{
				if (entry.Doll != null && entry.Doll.ID != Doll.DUMMY_ID)
					return entry;
			}
			return null;
		}

		public void SelectFirst()
		{
			Select(FindFirstSelectable());
		}

		private void Select(DollEntry entry)
		{
			if (selectedEntry != null)
				selectedEntry.SetSelected(false);

			selectedEntry = entry;

			if (selectedEntry != null)
				selectedEntry.SetSelected(true);

			OnDollSelected.Invoke(entry?.Doll);
		}
	}
}
