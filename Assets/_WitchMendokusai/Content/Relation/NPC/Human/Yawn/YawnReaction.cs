using System.Collections;
using UnityEngine;

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

		private void OnEnable()
		{
			GameEventManager.Instance.RegisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			GameEventManager.Instance.RegisterCallback(GameEventType.OnResearchComplete, OnResearchComplete);
		}

		private void OnDisable()
		{
			GameEventManager.Instance.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			GameEventManager.Instance.UnregisterCallback(GameEventType.OnResearchComplete, OnResearchComplete);
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
			UIManager.Instance.SpeechBubble.Show(transform, reaction);
		}

		private void OnResearchComplete()
		{
			UIManager.Instance.SpeechBubble.Show(transform, "오, 뭔가 알아냈어?");
		}

		[ContextMenu("Test/OnDungeonReturn")]
		private void TestOnDungeonReturn() => OnDungeonReturn();

		[ContextMenu("Test/OnResearchComplete")]
		private void TestOnResearchComplete() => OnResearchComplete();
	}
}
