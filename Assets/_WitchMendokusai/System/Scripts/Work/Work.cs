using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum WorkType
	{
		QuestWork,
	}

	public class Work
	{
		public WorkType WorkType { get; private set; }
		public Guid? Value { get; private set; }
		public float Time { get; private set; }
		public float CurTime { get; private set; }
		public int WorkerID { get; private set; }

		public Work(int workerID, WorkType workType, Guid? value, float time, float curTime = 0)
		{
			WorkerID = workerID;
			WorkType = workType;
			Value = value;
			Time = time;
			CurTime = curTime;
		}

		public void Tick()
		{
			CurTime = Mathf.Clamp(CurTime + TimeManager.TICK, 0, Time);
		}
		public float GetProgress()
		{
			return CurTime / Time;
		}
		public bool IsCompleted()
		{
			return CurTime >= Time;
		}
	}
}