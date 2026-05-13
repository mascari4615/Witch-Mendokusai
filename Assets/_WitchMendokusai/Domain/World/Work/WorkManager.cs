using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public enum WorkListType
{
		DollWork,
		DummyWork,
		VQuestWork
	}

	public class WorkManager : IDisposable
	{
		public Dictionary<WorkListType, List<Work>> Works { get; private set; } = new()
		{
			{WorkListType.DollWork, new()},
			{WorkListType.DummyWork, new()},
			{WorkListType.VQuestWork, new()}
		};

		private QuestManager questManager;
		private IDisposable questWorkStartedSub;

		[Inject]
		public void Construct(QuestManager questManager, ISubscriber<QuestWorkStartedEvent> questWorkStartedSubscriber)
		{
			this.questManager = questManager;
			questWorkStartedSub = questWorkStartedSubscriber.Subscribe(OnQuestWorkStarted);
		}

		public void Dispose() => questWorkStartedSub?.Dispose();

		public void Init(Dictionary<WorkListType, List<Work>> works)
		{
			Works = works;
		}

		private void OnQuestWorkStarted(QuestWorkStartedEvent evt)
		{
			Work work = new(evt.WorkerID, WorkType.QuestWork, evt.QuestGuid, evt.WorkTime);
			AddWork(work);
		}

		public void TickEachWorks()
		{
			foreach (List<Work> works in Works.Values)
				TickWorks(works);
		}

		public void TickWorks(List<Work> works)
		{
			for (int i = works.Count - 1; i >= 0; i--)
			{
				Work work = works[i];
				work.Tick();
				if (work.IsCompleted())
				{
					switch (work.WorkType)
					{
						case WorkType.QuestWork:
							questManager.EndQuestWork(work.Value);
							break;
					}
					works.RemoveAt(i);
				}
			}
		}

		public int GetWorkCount(WorkListType workListType)
		{
			return Works[workListType].Count;
		}

		public bool TryGetWorkByDollID(WorkListType workListType, int dollID, out Work targetWork)
		{
			foreach (Work work in Works[workListType])
			{
				if (work.WorkerID == dollID)
				{
					targetWork = work;
					return true;
				}
			}
			targetWork = null;
			return false;
		}

		public bool TryGetWorkByQuestGuid(Guid? questGuid, out Work targetWork)
		{
			foreach (List<Work> works in Works.Values)
			{
				foreach (Work work in works)
				{
					if (work.WorkType == WorkType.QuestWork && work.Value == questGuid)
					{
						targetWork = work;
						return true;
					}
				}
			}

			targetWork = null;
			return false;
		}

		public void AddWork(Work work)
		{
			if (work.WorkerID == WorkConstants.NONE_WORKER_ID)
			{
				Works[WorkListType.VQuestWork].Add(work);
			}
			else if (work.WorkerID == Doll.DUMMY_ID)
			{
				Works[WorkListType.DummyWork].Add(work);
			}
			else
			{
				if (TryGetWorkByDollID(WorkListType.DollWork, work.WorkerID, out _))
					return;
				Works[WorkListType.DollWork].Add(work);
			}
		}

		public void CancelWork(int dollID)
		{
			for (int i = Works[WorkListType.DollWork].Count - 1; i >= 0; i--)
			{
				if (Works[WorkListType.DollWork][i].WorkerID == dollID)
				{
					Works[WorkListType.DollWork].RemoveAt(i);
					return;
				}
			}
		}
	}
}