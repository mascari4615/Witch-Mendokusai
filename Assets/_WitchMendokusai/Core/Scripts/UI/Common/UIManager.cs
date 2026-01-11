using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIManager : Singleton<UIManager>
	{
		// Public Properties
		public List<IUIPanelGroup> PanelGroups { get; private set; } = new();
		public UITab Tab { get; private set; }
		public UINPC NPC { get; private set; }
		public UITransition Transition { get; private set; }
		public UIChat Chat { get; private set; }
		public UIStatus Status { get; private set; }
		public CutSceneModule CutSceneModule { get; private set; }
		[field: SerializeField] public Canvas BaseCanvas { get; private set; }
	
		[SerializeField] private UIDungeon dungeonPrefab = null;
		[SerializeField] private UIBuild buildPrefab = null;
		[SerializeField] private UIAdventurerGuild adventurerGuildPrefab = null;

		private UIFloatingText damage;
		private UIPopup popup;
		private UIAdventurerGuild adventurerGuild;
		private UIStagePopup stagePopup;

		public bool IsFullscreenPanelActive => PanelGroups.Any(ui => ui.IsPanelOpen && ui.TryGetCurPanel(out UIPanel panel) && panel.IsFullscreen);

		protected override void Awake()
		{
			base.Awake();

			// Content UIs
			Instantiate(dungeonPrefab, BaseCanvas.transform);
			Instantiate(buildPrefab, BaseCanvas.transform);
			Instantiate(adventurerGuildPrefab, BaseCanvas.transform);

			// Common UIs
			CutSceneModule = FindFirstObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			damage = FindFirstObjectByType<UIFloatingText>(FindObjectsInactive.Include);
			popup = FindFirstObjectByType<UIPopup>(FindObjectsInactive.Include);
			Chat = FindFirstObjectByType<UIChat>(FindObjectsInactive.Include);
			adventurerGuild = FindFirstObjectByType<UIAdventurerGuild>(FindObjectsInactive.Include);
			adventurerGuild.gameObject.SetActive(false);

			Transition = FindFirstObjectByType<UITransition>(FindObjectsInactive.Include);
			stagePopup = FindFirstObjectByType<UIStagePopup>(FindObjectsInactive.Include);
			Status = FindFirstObjectByType<UIStatus>(FindObjectsInactive.Include);

			Tab = FindFirstObjectByType<UITab>(FindObjectsInactive.Include);
			NPC = FindFirstObjectByType<UINPC>(FindObjectsInactive.Include);
		}

		private void Start()
		{
			Status.Init();
			Status.gameObject.SetActive(false);

			RegisterOverlayUI(Tab);
			RegisterOverlayUI(NPC);
		}

		public void RegisterOverlayUI(IUIPanelGroup ui)
		{
			if (ui == null || PanelGroups.Contains(ui))
				return;

			PanelGroups.Add(ui);
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

		public void ToggleTabUI()
		{
			if (Tab.CurPanelType == TabPanelType.Setting)
				return;

			if (Tab.IsPanelOpen)
				Tab.ClosePanel();
			else
				Tab.SetPanel(TabPanelType.TabMenu);
		}

		public void OnCancelInput()
		{
			// 닫을 수 있는 UI 닫기
			foreach (IUIPanelGroup ui in PanelGroups)
			{
				if (ui.IsPanelOpen && ui.CanBeClosedByCancelInput)
				{
					ui.ClosePanel();
					return;
				}
			}

			// 아무것도 닫힌 게 없으면 설정 열기
			Tab.SetPanel(TabPanelType.Setting);
		}

		public void ToggleStatus()
		{
			Status.gameObject.SetActive(!Status.gameObject.activeSelf);

			if (Status.gameObject.activeSelf)
				Status.UpdateUI();
		}
	}
}