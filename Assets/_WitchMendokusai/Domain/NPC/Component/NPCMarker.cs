using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VContainer;
namespace WitchMendokusai
{
	/// <summary>
	/// NPC 머리 위 표식 자리와 퀘스트 상태. 그리기는 NPCNameplateView (UI Toolkit HUD 층) 가 맡음
	/// 이 오브젝트의 위치가 표식 꼬리 끝이 닿는 머리 높이. 옛 SpriteRenderer 표식은 프리팹에서 꺼 둠
	/// </summary>
	public class NPCMarker : MonoBehaviour
	{
		public static readonly List<NPCMarker> Active = new();

		[Tooltip("퀘스트 상태를 다시 고르는 간격 (초)")]
		[SerializeField, Min(0.05f)] private float refreshInterval = .5f;

		public NPCObject NPC { get; private set; }
		public NPCQuestMark Mark { get; private set; }

		private Coroutine loop;
		private QuestManager questManager;

		[Inject]
		public void Construct(QuestManager questManager)
		{
			this.questManager = questManager;
		}

		private void Awake()
		{
			NPC = GetComponentInParent<NPCObject>(true);
		}

		private void OnEnable()
		{
			Active.Add(this);
			loop = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			Active.Remove(this);
			if (loop != null)
				StopCoroutine(loop);
		}

		private IEnumerator Loop()
		{
			WaitForSeconds wait = new(refreshInterval);
			// 장면 NPC 는 OnEnable 이 VContainer 주입보다 먼저 올 수 있음. 주입될 때까지 기다림
			while (questManager == null)
				yield return null;

			while (true)
			{
				Mark = NPCQuestMarkRule.Resolve(NPC.Data.QuestData, questManager);
				yield return wait;
			}
		}
	}
}
