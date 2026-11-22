using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 던전 숙련 막대 HUD (사용자 2026-11-22 22:26 '조각 얼마나 모였는지 보이는 전용 UI'). 스킬창 (RunEquipmentHud) 위에 막대마다 한 줄
	// 줄: 이름, 채움 막대 (막대 색은 숙련 자료), 모은 수/문턱. 찬 막대는 빛남, 확정하면 확정한 줄만 남음. 스타일은 DollMasteryHud.uss
	public class DollMasteryHud : VisualElement
	{
		public const string USS_ROOT = "wm-mastery";
		public const string USS_ROW = USS_ROOT + "__row";
		public const string USS_ROW_READY = USS_ROW + "--ready";
		public const string USS_ROW_HIDDEN = USS_ROW + "--hidden";
		public const string USS_NAME = USS_ROOT + "__name";
		public const string USS_BAR = USS_ROOT + "__bar";
		public const string USS_FILL = USS_ROOT + "__fill";
		public const string USS_COUNT = USS_ROOT + "__count";
		public const string USS_HIDDEN = USS_ROOT + "--hidden";

		private readonly List<Row> rows = new();
		private DollMasteryData shownData;
		private int shownVersion = -1;

		public DollMasteryHud()
		{
			name = "DollMasteryHud";
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);
		}

		public void SetVisible(bool isVisible) => EnableInClassList(USS_HIDDEN, isVisible == false);

		// 판 상태 버전이 바뀔 때만 다시 그림
		public void Refresh(DollMasteryData data, DollMastery mastery, int version)
		{
			if (version == shownVersion && data == shownData)
				return;
			shownVersion = version;
			EnsureRows(data);
			for (int track = 0; track < rows.Count; track++)
			{
				Row row = rows[track];
				int progress = mastery.Progress(track);
				int threshold = mastery.Threshold(track);
				row.Fill.style.width = Length.Percent(100f * progress / threshold);
				row.Count.text = $"{progress}/{threshold}";
				row.Root.EnableInClassList(USS_ROW_READY, mastery.IsReady(track));
				bool isOtherConfirmed = mastery.ConfirmedTrack != DollMastery.NO_TRACK && mastery.ConfirmedTrack != track;
				row.Root.EnableInClassList(USS_ROW_HIDDEN, isOtherConfirmed);
			}
		}

		public void ResetShown()
		{
			shownVersion = -1;
			shownData = null;
		}

		private void EnsureRows(DollMasteryData data)
		{
			if (data == shownData)
				return;
			shownData = data;
			Clear();
			rows.Clear();
			foreach (DollMasteryTrackData track in data.Tracks)
			{
				Row row = new(track);
				rows.Add(row);
				Add(row.Root);
			}
		}

		private sealed class Row
		{
			public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
			public readonly VisualElement Fill = new() { pickingMode = PickingMode.Ignore };
			public readonly Label Count = new() { pickingMode = PickingMode.Ignore };

			public Row(DollMasteryTrackData track)
			{
				Root.AddToClassList(USS_ROW);
				Label trackName = new(track.DisplayName) { pickingMode = PickingMode.Ignore };
				trackName.AddToClassList(USS_NAME);
				trackName.style.color = track.Color;
				VisualElement bar = new() { pickingMode = PickingMode.Ignore };
				bar.AddToClassList(USS_BAR);
				Fill.AddToClassList(USS_FILL);
				// 막대 색은 숙련 자료 (조각 색과 같음). 나머지 모양은 USS
				Fill.style.backgroundColor = track.Color;
				bar.Add(Fill);
				Count.AddToClassList(USS_COUNT);
				Root.Add(trackName);
				Root.Add(bar);
				Root.Add(Count);
			}
		}
	}
}
