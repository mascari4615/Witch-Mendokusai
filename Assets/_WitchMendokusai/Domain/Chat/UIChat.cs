using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using static WitchMendokusai.SOHelper;
using DG.Tweening;

namespace WitchMendokusai
{
	public class UIChat : MonoBehaviour
	{
		[SerializeField] private Image unitImage;
		[SerializeField] private TextMeshProUGUI unitName;
		[SerializeField] private TextMeshProUGUI lineText;
		[SerializeField] private CanvasGroup chatCanvasGroup;
		[SerializeField] private CanvasGroup bubbleCanvasGroup;

		private NPCObject curNPC;
		private int unitID;
		private Action endAction;

		private ChatManager chatManager;
		private CameraManager cameraManager;
		private DataManager dataManager;
		private InputManager inputManager;
		private PlayerProvider playerProvider;

		public static bool IsChatting { get; private set; } = false;

		[Inject]
		public void Construct(ChatManager chatManager, CameraManager cameraManager, DataManager dataManager, InputManager inputManager, PlayerProvider playerProvider)
		{
			this.chatManager = chatManager;
			this.cameraManager = cameraManager;
			this.dataManager = dataManager;
			this.inputManager = inputManager;
			this.playerProvider = playerProvider;
		}

		private void Start()
		{
			chatCanvasGroup.alpha = 0;
			bubbleCanvasGroup.alpha = 0;
		}

		public void StartChat(NPCObject npc, Action onChatFinished = null)
		{
			if (chatManager.TryGetChatData(npc.UnitData.ID.ToString(), out List<LineData> curChatData) == false)
			{
				Debug.LogWarning($"ChatData not found: {npc.UnitData.ID}");
				onChatFinished?.Invoke();
				return;
			}

			cameraManager.SetUICameraMode(UICameraMode.NPC, true);
			cameraManager.SetNPC(npc.transform);

			curNPC = npc;
			endAction = onChatFinished;

			IsChatting = true;

			StartCoroutine(ChatLoop(curChatData));
		}

		private IEnumerator ChatLoop(List<LineData> curChatData)
		{
			StartCoroutine(BubbleLoop());

			chatCanvasGroup.DOFade(1, 0.2f);
			bubbleCanvasGroup.DOFade(1, 0.2f);

			unitImage.color = Color.clear;
			unitName.text = string.Empty;
			lineText.text = string.Empty;

			yield return null;

			unitImage.color = Color.white;

			foreach (LineData lineData in curChatData)
			{
				Unit unit = null;

				if (lineData.unitID == 0)
					unit = Get<Doll>(dataManager.CurDollID);
				else if (lineData.unitID == -1)
					unit = curNPC.Data;

				unitID = lineData.unitID;
				unitImage.sprite = unit.Sprite;
				unitImage.transform.DOScaleY(.9f, .02f).OnComplete(() => unitImage.transform.DOScaleY(1, .02f));
				unitName.text = unit.Name;

				Coroutine coroutine = StartCoroutine(PrintLine(lineData));

				do yield return null;
				while (lineText.text != lineData.line && inputManager.IsAnyKeyPressedThisFrame == false);

				StopCoroutine(coroutine);
				lineText.text = lineData.line;

				do yield return null;
				while (inputManager.IsAnyKeyPressedThisFrame == false);
			}

			IsChatting = false;

			chatCanvasGroup.DOFade(0, 0.2f);
			bubbleCanvasGroup.DOFade(0, 0.2f);

			StopAllCoroutines();

			endAction?.Invoke();
		}

		private IEnumerator PrintLine(LineData lineData)
		{
			const float waitTime = 0.05f;
			WaitForSecondsRealtime wait = new(waitTime);
			StringBuilder s = new();

			s.Clear();
			foreach (char c in lineData.line)
			{
				s.Append(c);
				lineText.text = s.ToString();
				if (c != ' ')
					RuntimeManager.PlayOneShot("event:/SFX/Equip");
				yield return wait;
			}
		}

		public IEnumerator BubbleLoop()
		{
			const float BubblePadding = 30f;
			RectTransform bubbleRectTransform = bubbleCanvasGroup.GetComponent<RectTransform>();
			float bubbleWidth = bubbleRectTransform.sizeDelta.x;

			while (true)
			{
				Vector3 targetPos = unitID == 0 ?
					playerProvider.Current.transform.position :
					curNPC.transform.position;
				bubbleCanvasGroup.transform.position = GetVec(targetPos + Vector3.up);

				yield return null;
			}

			Vector3 GetVec(Vector3 worldPos)
			{
				float bubbleHeight = bubbleRectTransform.sizeDelta.y;
				Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

				return new Vector3(
					Mathf.Clamp(screenPos.x, bubbleWidth / 2 + BubblePadding, Screen.width - bubbleWidth / 2 - BubblePadding),
					Mathf.Clamp(screenPos.y + 40, BubblePadding, Screen.height - bubbleHeight - BubblePadding), 0);
			}
		}
	}
}
