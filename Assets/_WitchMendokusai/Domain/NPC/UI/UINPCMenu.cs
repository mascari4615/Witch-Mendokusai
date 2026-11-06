using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace WitchMendokusai
{
	[RequireComponent(typeof(CanvasGroup))]
	public class UINPCMenu : UIPanel
	{
		public NPCPanelType CurPanelType { get; private set; } = NPCPanelType.None;

		[SerializeField] private GameObject buttonsParent;
		[SerializeField] private UISlot talkOption;

		[SerializeField] private UISlot questOption;
		[SerializeField] private CanvasGroup questEachParent;
		[SerializeField] private UISlot[] questEachOptions;

		[SerializeField] private GameObject optionPrefab;
		[SerializeField] private Transform optionsParent;
		private readonly List<UISlot> options = new();

		[SerializeField] private Image headerPortrait;
		[SerializeField] private TMPro.TextMeshProUGUI headerName;

		private CanvasGroup canvasGroup;
		private NPCObject curNPC = null;

		private UIManager uiManager;
		private CameraManager cameraManager;
		private ChatManager chatManager;
		private QuestManager questManager;

		public override bool IsFullscreen => true;

		[Inject]
		public void Construct(UIManager uiManager, CameraManager cameraManager, ChatManager chatManager, QuestManager questManager)
		{
			this.uiManager = uiManager;
			this.cameraManager = cameraManager;
			this.chatManager = chatManager;
			this.questManager = questManager;
		}

		protected override void OnInit()
		{
			canvasGroup = GetComponent<CanvasGroup>();

			// 버튼 초기화
			{
				// 대화 버튼
				talkOption.Init();
				talkOption.SetClickAction((slot) => { Talk(); });

				// 퀘스트 버튼
				questOption.Init();
				questOption.SetClickAction((_) => OpenQuestList());
				questOption.SetSelectAction((_) =>
				{
					if (questEachParent.gameObject.activeSelf)
						questEachParent.gameObject.SetActive(false);
				});
				questEachParent.gameObject.SetActive(false);
				for (int i = 0; i < questEachOptions.Length; i++)
				{
					questEachOptions[i].SetSlotIndex(i);
					questEachOptions[i].Init();
					questEachOptions[i].SetClickAction((slot) => { SelectQuest(slot.Index); });
				}

				// NPC 버튼
				for (int i = 0; i < (int)NPCPanelType.Count; i++)
				{
					NPCPanelType panelType = (NPCPanelType)i;
					IUIPanel panel = uiManager.NPC.Panels[panelType];

					UISlot newSlot = Instantiate(optionPrefab, optionsParent).GetComponent<UISlot>();
					newSlot.Init();
					newSlot.SetSlotIndex((int)panelType);
					newSlot.SetSlot(panel.PanelIcon, panel.Name, string.Empty);
					newSlot.SetClickAction((slot) => { SetPanel(panelType); });

					options.Add(newSlot);
				}
			}

			// 초기화
			SetPanel(NPCPanelType.None);
		}

		private void SetPanel(NPCPanelType newPanelType)
		{
			if (CurPanelType == newPanelType)
				return;

			NPCPanelType originPanelType = CurPanelType;
			CurPanelType = newPanelType;

			// 전 패널 처리
			{
				if (originPanelType == NPCPanelType.None)
				{
					buttonsParent.SetActive(false);
				}
			}

			// 새 패널 처리
			{
				if (CurPanelType == NPCPanelType.None)
				{
					// NPC 버튼들 띄우기 (선택지)
					buttonsParent.SetActive(true);
				}
				else
				{
					// NPC UI 띄우기
					uiManager.NPC.SetPanel(CurPanelType, curNPC);
				}
			}
		}

		protected override void OnOpen()
		{
			canvasGroup.SetVisible(false);

			NPC curNPCData = curNPC.UnitData as NPC;
			List<NPCPanelType> panelTypes = curNPCData.GetPanelTypeList();
			for (int i = 0; i < options.Count; i++)
				options[i].gameObject.SetActive(panelTypes.Contains((NPCPanelType)options[i].Index)); // Index는 PanelType의 인덱스와 동일

			UpdateQuestButtons();
			questEachParent.gameObject.SetActive(false);

			cameraManager.SetUICameraMode(UICameraMode.NPC, true);
			cameraManager.SetSelecting(isSelecting: false, shouldAnimate: false);

			// 대화 불가 시 바로 선택 모드로 전환
			bool canTalk = chatManager.TryGetChatData(curNPC.UnitData.ID.ToString(), out _);
			talkOption.gameObject.SetActive(canTalk);
			headerPortrait.sprite = curNPCData.Sprite;
			headerName.text = curNPCData.Name;
			RebuildMenuNavigation();

			if (canTalk)
			{
				Talk();
			}
			else
			{
				Debug.LogWarning($"ChatData not found: {curNPC.UnitData.ID}");
				OnChatFinish();
			}
		}

		protected override void OnClose()
		{
			cameraManager.SetUICameraMode(UICameraMode.NPC, false);
		}

		public override void UpdateUI()
		{
		}

		public override void SetNPC(NPCObject npc)
		{
			curNPC = npc;
		}

		private void UpdateQuestButtons()
		{
			NPC curNPCData = curNPC.UnitData as NPC;
			List<QuestSO> questData = curNPCData.QuestData;
			int questCount = questData.Count;
			List<UISlot> visibleQuests = new();

			for (int i = 0; i < questEachOptions.Length; i++)
			{
				bool isVisible = i < questCount && questManager.GetQuestState(questData[i].ID) != QuestState.Completed;
				questEachOptions[i].gameObject.SetActive(isVisible);
				if (isVisible)
				{
					questEachOptions[i].SetSlot(questData[i]);
					visibleQuests.Add(questEachOptions[i]);
				}
			}

			questOption.gameObject.SetActive(visibleQuests.Count > 0);

			for (int i = 0; i < visibleQuests.Count; i++)
			{
				visibleQuests[i].SetNavigation(new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnLeft = questOption.Selectable,
					selectOnUp = i > 0 ? visibleQuests[i - 1].Selectable : null,
					selectOnDown = i < visibleQuests.Count - 1 ? visibleQuests[i + 1].Selectable : null
				});
			}
		}

		// 보이는 칸만으로 위아래 이동 구성
		private void RebuildMenuNavigation()
		{
			List<UISlot> visible = GetVisibleMenuSlots();
			for (int i = 0; i < visible.Count; i++)
			{
				Selectable right = visible[i] == questOption ? FirstVisibleQuest()?.Selectable : null;
				visible[i].SetNavigation(new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnUp = i > 0 ? visible[i - 1].Selectable : null,
					selectOnDown = i < visible.Count - 1 ? visible[i + 1].Selectable : null,
					selectOnRight = right
				});
			}
		}

		private List<UISlot> GetVisibleMenuSlots()
		{
			List<UISlot> visible = new();
			if (talkOption.gameObject.activeSelf)
				visible.Add(talkOption);
			if (questOption.gameObject.activeSelf)
				visible.Add(questOption);
			foreach (UISlot option in options)
				if (option.gameObject.activeSelf)
					visible.Add(option);
			return visible;
		}

		private UISlot FirstVisibleQuest() => questEachOptions.FirstOrDefault(slot => slot.gameObject.activeSelf);

		private void OpenQuestList()
		{
			questEachParent.gameObject.SetActive(true);
			FirstVisibleQuest().Select();
		}

		private void PlayMenuAppear()
		{
			List<UISlot> visible = GetVisibleMenuSlots();
			for (int i = 0; i < visible.Count; i++)
				if (visible[i].TryGetComponent(out UIChoiceRow row))
					row.PlayAppear(i);
		}

		#region Button Actions
		public void Talk()
		{
			buttonsParent.SetActive(false);
			cameraManager.SetSelecting(false);

			uiManager.Chat.StartChat(curNPC, OnChatFinish);
		}

		private void OnChatFinish()
		{
			canvasGroup.SetVisible(true);

			SetPanel(NPCPanelType.None);
			buttonsParent.SetActive(true);
			List<UISlot> visible = GetVisibleMenuSlots();
			if (visible.Count > 0)
				visible[0].Select();
			PlayMenuAppear();

			cameraManager.SetSelecting(true);
		}

		private void SelectQuest(int index)
		{
			NPC curNPCData = curNPC.UnitData as NPC;
			QuestSO questData = curNPCData.QuestData[index];

			QuestState state = questManager.GetQuestState(questData.ID);
			switch (state)
			{
				case QuestState.Locked:
					questManager.UnlockQuest(questData);
					questManager.AddQuest(RuntimeQuestFactory.FromQuestSO(questData, questManager.CreateCriteriaContext()));
					break;
				case QuestState.Unlocked:
					// TODO: 퀘스트 진행 중 대사 출력
					break;
				case QuestState.Completed:
				default:
					Debug.LogError($"Invalid Quest State : {state}");
					break;
			}

			Invoke(nameof(Close), .1f);
		}

		public void Close()
		{
			SetPanel(NPCPanelType.None);
		}
		#endregion
	}
}