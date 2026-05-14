using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;
namespace WitchMendokusai
{
	public class NPCMarker : MonoBehaviour
	{
		// HACK:
		[SerializeField] private List<Sprite> sprites;

		private NPCObject npcObject;
		private SpriteRenderer spriteRenderer;
		private Coroutine loop;

		private QuestManager questManager;

		[Inject]
		public void Construct(QuestManager questManager)
		{
			this.questManager = questManager;
		}

		private void Awake()
		{
			npcObject = GetComponentInParent<NPCObject>(true);
			spriteRenderer = GetComponent<SpriteRenderer>();
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
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

			List<QuestSO> dataSOs = npcObject.Data.QuestData;
			if (dataSOs.Count == 0)
				return;

			// 클리어 가능한 퀘스트가 있다면
			bool hasCompletableQuest = dataSOs.Exists(i => questManager.GetQuest(i)?.State == RuntimeQuestState.CanComplete);
			if (hasCompletableQuest)
			{
				// Debug.Log("hasCompletableQuest");
				spriteRenderer.sprite = sprites[0];
				return;
			}

			// 획득 가능한 퀘스트가 있다면
			bool hasLockedQuest = dataSOs.Exists(i => questManager.GetQuestState(i.ID) == QuestState.Locked);
			
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