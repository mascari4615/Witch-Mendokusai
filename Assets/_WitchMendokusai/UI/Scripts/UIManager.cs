using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum MCanvasType
	{
		None,
		Dungeon,
	}

	public enum MPanelType
	{
		None,
		Tab,
		Setting,
		DungeonResult,
		NPC,
	}

	public class UIManager : Singleton<UIManager>
	{
		public MCanvasType CurCanvas { get; private set; }
		private readonly Dictionary<MCanvasType, UIPanel> canvasUIs = new();

		public MPanelType CurOverlay { get; private set; }
		private readonly Dictionary<MPanelType, UIPanel> overlayUIs = new();

		private UITab tab;
		[field: SerializeField] public CutSceneModule CutSceneModule { get; private set; }
		private UISetting setting;
		private UIFloatingText damage;
		private UIPopup popup;
		public UIChat Chat { get; private set; }
		public UIMap Map { get; private set; }
		private UIDungeon dungeon;
		private UIDungeonResult dungeonResult;
		public UINPC Npc { get; private set; }

		public UITransition Transition { get; private set; }
		private UIStagePopup stagePopup;
		public UIStatus Status { get; private set; }

		protected override void Awake()
		{
			base.Awake();

			tab = FindFirstObjectByType<UITab>(FindObjectsInactive.Include);
			CutSceneModule = FindFirstObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			damage = FindFirstObjectByType<UIFloatingText>(FindObjectsInactive.Include); ;
			popup = FindFirstObjectByType<UIPopup>(FindObjectsInactive.Include);
			setting = FindFirstObjectByType<UISetting>(FindObjectsInactive.Include);
			Chat = FindFirstObjectByType<UIChat>(FindObjectsInactive.Include);
			dungeon = FindFirstObjectByType<UIDungeon>(FindObjectsInactive.Include);
			dungeonResult = FindFirstObjectByType<UIDungeonResult>(FindObjectsInactive.Include);
			Npc = FindFirstObjectByType<UINPC>(FindObjectsInactive.Include); ;
			Map = FindFirstObjectByType<UIMap>(FindObjectsInactive.Include);

			canvasUIs[MCanvasType.Dungeon] = FindFirstObjectByType<UIDungeon>(FindObjectsInactive.Include);

			overlayUIs[MPanelType.Tab] = tab;
			overlayUIs[MPanelType.Setting] = setting;
			overlayUIs[MPanelType.DungeonResult] = dungeonResult;
			overlayUIs[MPanelType.NPC] = Npc;

			Transition = FindFirstObjectByType<UITransition>(FindObjectsInactive.Include);
			stagePopup = FindFirstObjectByType<UIStagePopup>(FindObjectsInactive.Include);
			Status = FindFirstObjectByType<UIStatus>(FindObjectsInactive.Include);
		}

		private void Start()
		{
			foreach (UIPanel uiPanel in canvasUIs.Values)
			{
				uiPanel.Init();
				uiPanel.SetActive(false);
			}

			foreach (UIPanel uiPanel in overlayUIs.Values)
			{
				uiPanel.Init();
				uiPanel.SetActive(false);
			}

			SetCanvas(MCanvasType.None);
			SetOverlay(MPanelType.None);

			Status.Init();
		}

		public void PopDamage(DamageInfo damageInfo, Vector3 pos = default)
		{
			TextType textType = DamageUtil.DamageTypeToTextType(damageInfo.type);
			StartCoroutine(damage.AniTextUI(textType, damageInfo.damage.ToString(), pos));
		}

		public void PopText(string msg, TextType textType, Vector3 pos = default)
		{
			StartCoroutine(damage.AniTextUI(textType, msg, pos));
		}

		public void StagePopup(Stage stage)
		{
			stagePopup.Popup(stage);
		}

		public void Popup(DataSO dataSO)
		{
			popup.Popup(dataSO);
		}

		public void ToggleOverlayUI_Tab()
		{
			if (CurOverlay != MPanelType.None)
			{
				if (CurOverlay != MPanelType.Setting)
					SetOverlay(MPanelType.None);
			}
			else
			{
				SetOverlay(MPanelType.Tab);
			}
		}

		public void ToggleOverlayUI_Setting()
		{
			if (CurOverlay != MPanelType.None)
			{
				SetOverlay(MPanelType.None);
			}
			else
			{
				SetOverlay(MPanelType.Setting);
			}
		}

		public void SetOverlay(MPanelType overlayUI)
		{
			if (CurOverlay == overlayUI)
				return;

			if (CurOverlay != MPanelType.None)
				overlayUIs[CurOverlay].SetActive(false);

			CurOverlay = overlayUI;
			if (overlayUIs.TryGetValue(overlayUI, out var uiPanel))
			{
				uiPanel.SetActive(true);
				uiPanel.UpdateUI();
			}
		}

		public void SetCanvas(MCanvasType newCanvas)
		{
			if (CurCanvas == newCanvas)
				return;

			if (CurCanvas != MCanvasType.None)
				canvasUIs[CurCanvas].SetActive(false);

			CurCanvas = newCanvas;
			if (CurCanvas == MCanvasType.None)
			{
				CameraManager.Instance.SetCamera(CameraType.Normal);
			}
			else
			{
				canvasUIs[CurCanvas].SetActive(true);
				canvasUIs[CurCanvas].UpdateUI();
			}
		}

		public void ToggleStatus()
		{
			Status.gameObject.SetActive(!Status.gameObject.activeSelf);

			if (Status.gameObject.activeSelf)
				Status.UpdateUI();
		}
	}
}