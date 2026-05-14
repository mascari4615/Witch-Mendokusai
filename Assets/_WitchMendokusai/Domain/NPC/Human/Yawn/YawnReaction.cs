using System.Collections;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class YawnReaction : MonoBehaviour
	{
		[SerializeField] private string[] returnReactions =
		{
			"...수고했어.",
			"아, 왔구나.",
			"어 잠깐만, 어디 뒀더라...",
			"...왔어?"
		};
		[SerializeField] private int delayedStateIndex = 3;
		[SerializeField] private float delaySeconds = 2f;

		private GameEventManager gameEventManager;
		private UIManager uiManager;

		[Inject]
		public void Construct(GameEventManager gameEventManager, UIManager uiManager)
		{
			this.gameEventManager = gameEventManager;
			this.uiManager = uiManager;
		}

		private void OnEnable()
		{
			gameEventManager.RegisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			gameEventManager.RegisterCallback(GameEventType.OnResearchComplete, OnResearchComplete);
		}

		private void OnDisable()
		{
			gameEventManager.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			gameEventManager.UnregisterCallback(GameEventType.OnResearchComplete, OnResearchComplete);
		}

		private void OnDungeonReturn()
		{
			StartCoroutine(ReturnReactionRoutine());
		}

		private IEnumerator ReturnReactionRoutine()
		{
			IdleStateBehavior idle = GetComponent<IdleStateBehavior>();
			int stateIndex = idle != null ? idle.CurrentStateIndex : 0;

			if (stateIndex == delayedStateIndex)
				yield return new WaitForSeconds(delaySeconds);

			string reaction = (returnReactions != null && stateIndex < returnReactions.Length)
				? returnReactions[stateIndex]
				: "...수고했어.";
			uiManager.SpeechBubble.Show(transform, reaction);
		}

		private void OnResearchComplete()
		{
			uiManager.SpeechBubble.Show(transform, "오, 뭔가 알아냈어?");
		}

		[ContextMenu("Test/OnDungeonReturn")]
		private void TestOnDungeonReturn() => OnDungeonReturn();

		[ContextMenu("Test/OnResearchComplete")]
		private void TestOnResearchComplete() => OnResearchComplete();
	}
}
