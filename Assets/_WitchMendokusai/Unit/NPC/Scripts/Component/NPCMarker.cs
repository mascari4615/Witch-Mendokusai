using UnityEngine;
using System.Collections;
using System.Collections.Generic;
namespace WitchMendokusai
{
	public class NPCMarker : MonoBehaviour
	{
		// HACK:
		[SerializeField] private List<Sprite> sprites;

		private NPCObject npcObject;
		private SpriteRenderer spriteRenderer;
		private Coroutine loop;

		private void Awake()
		{
			npcObject = GetComponentInParent<NPCObject>(true);
			spriteRenderer = GetComponent<SpriteRenderer>();
		}

		private void OnEnable()
		{
			loop = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
		}

		private IEnumerator Loop()
		{
			WaitForSeconds wait = new(.5f);

			while (true)
			{
				SetSprite();
				yield return wait;
			}
		}

		private void SetSprite()
		{
			spriteRenderer.sprite = null;

			List<QuestSO> dataSOs = npcObject.Data.QuestDatas;
			if (dataSOs.Count == 0)
				return;

			QuestManager questManager = QuestManager.Instance;

			// 클리어 가능한 퀘스트가 있다면
			bool hasCompletableQuest = dataSOs.Exists(i => questManager.GetQuest(i)?.State == RuntimeQuestState.CanComplete);
			if (hasCompletableQuest)
			{
				// Debug.Log("hasCompletableQuest");
				spriteRenderer.sprite = sprites[0];
				return;
			}

			// 획득 가능한 퀘스트가 있다면
			bool hasLockedQuest = dataSOs.Exists(i =>
			{
				return QuestManager.Instance.GetQuestState(i.ID) == QuestState.Locked;
			});
			
			if (hasLockedQuest)
			{
				// Debug.Log("hasLockedQuest");
				spriteRenderer.sprite = sprites[1];
				return;
			}

			// 진행중인 퀘스트가 있다면
			bool hasWorkingQuest = dataSOs.Exists(i => questManager.GetQuest(i)?.State <= RuntimeQuestState.Working);
			if (hasWorkingQuest)
			{
				// Debug.Log("hasWorkingQuest");
				spriteRenderer.sprite = sprites[2];
				return;
			}
		}
	}
}