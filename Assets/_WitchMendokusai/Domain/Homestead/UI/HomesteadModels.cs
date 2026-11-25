using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 목록 한 칸. 칸 격자와 줄 목록이 같이 씀
	public sealed class HomesteadEntry
	{
		public string Id { get; set; } = string.Empty;
		public Sprite Icon { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Sub { get; set; } = string.Empty;
		// 칸 구석의 수
		public string Count { get; set; } = string.Empty;
		// 없는 것, 잠긴 것
		public bool IsDim { get; set; }
		// 지금 할 수 있는 일이 있음
		public bool IsMarked { get; set; }
		// 아직 못 본 것. 그림자로만
		public bool IsUnknown { get; set; }
		public Func<HomesteadDetail> Detail { get; set; }
	}

	public sealed class HomesteadDetail
	{
		public string Title { get; set; } = string.Empty;
		public string Note { get; set; } = string.Empty;
		public Sprite Icon { get; set; }
		public List<string> Lines { get; } = new();
		public List<HomesteadCostLine> Costs { get; } = new();
		public string CostTitle { get; set; } = "필요한 것";
		// 0 보다 작으면 막대 없음
		public float Bar { get; set; } = -1f;
		public string BarText { get; set; } = string.Empty;
		public List<HomesteadAction> Actions { get; } = new();
	}

	public sealed class HomesteadCostLine
	{
		public Sprite Icon { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Have { get; set; }
		public int Need { get; set; }
		public bool IsEnough => Have >= Need;
	}

	public sealed class HomesteadAction
	{
		public string Label { get; set; } = string.Empty;
		public HomesteadResult Can { get; set; } = HomesteadResult.Success;
		public Func<HomesteadResult> Run { get; set; }
		public UiSfxEvent Sound { get; set; } = UiSfxEvent.Confirm;
	}

	// 화면 하나의 내용. 화면 틀 (HomesteadPageView) 은 하나뿐이고 내용만 갈림
	public interface IHomesteadPage
	{
		string Title { get; }
		Sprite Icon { get; }
		IReadOnlyList<string> Tabs { get; }
		// 이 탭에서 칸 격자로 보일지. 아니면 줄 목록
		bool IsGrid(int tab);
		List<HomesteadEntry> Entries(int tab);
		// 머리 줄 오른쪽 글. 회관 단계나 길드 레벨
		string Status { get; }
		string EmptyText(int tab);
	}
}
