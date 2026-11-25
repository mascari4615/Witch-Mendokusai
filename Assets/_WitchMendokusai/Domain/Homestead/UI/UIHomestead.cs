using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public enum HomesteadPanelType
	{
		None = -1,
		Village = 0,
		Guild = 1,
		Store = 2,
		Museum = 3,
		Wardrobe = 4,
		Seeds = 5,
		Ranch = 6,
		Room = 7,
		Mine = 8,
	}

	// 생활 화면 묶음. 마을에 처음 들어설 때 HomesteadManager 가 만듦
	public sealed class UIHomestead : UIPanelGroup<HomesteadPanelType>
	{
		public override bool CanBeClosedByCancelInput => true;
		public override HomesteadPanelType DefaultPanel => HomesteadPanelType.None;

		private HomesteadManager manager;
		private readonly List<UIToolkitPanel> created = new();
		private readonly Dictionary<HomesteadPanelType, UIHomesteadPanel> pages = new();

		public SeedPage Seeds { get; private set; }
		public MinePage Mine { get; private set; }

		[Inject]
		public void Construct(UIManager uiManager) => SetUIManager(uiManager);

		public void Open(HomesteadManager owner) => manager = owner;

		public override void Init()
		{
			Seeds = new SeedPage(manager);
			Mine = new MinePage(manager);
			Add(HomesteadPanelType.Village, new VillagePage(manager));
			Add(HomesteadPanelType.Guild, new GuildPage(manager));
			Add(HomesteadPanelType.Store, new StorePage(manager));
			Add(HomesteadPanelType.Museum, new MuseumPage(manager));
			Add(HomesteadPanelType.Wardrobe, new WardrobePage(manager));
			Add(HomesteadPanelType.Seeds, Seeds);
			Add(HomesteadPanelType.Ranch, new RanchPage(manager));
			Add(HomesteadPanelType.Mine, Mine);
			UIRoomPanel room = UIManager.CreateToolkitPanel<UIRoomPanel>();
			Panels[HomesteadPanelType.Room] = room;
			created.Add(room);
		}

		private void Add(HomesteadPanelType type, IHomesteadPage page)
		{
			UIHomesteadPanel panel = UIManager.CreateToolkitPanel<UIHomesteadPanel>();
			panel.SetPage(page);
			Panels[type] = panel;
			pages[type] = panel;
			created.Add(panel);
		}

		public void Show(HomesteadPanelType type, int tab = 0)
		{
			if (pages.TryGetValue(type, out UIHomesteadPanel panel))
				panel.StartAt(tab);
			SetPanel(type);
		}

		public void Dispose()
		{
			foreach (UIToolkitPanel panel in created)
			{
				if (panel != null)
					Destroy(panel);
			}
			created.Clear();
		}
	}
}
