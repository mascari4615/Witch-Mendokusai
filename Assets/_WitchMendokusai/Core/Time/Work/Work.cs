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
		public WorkType WorkType { get; set; }
		public Guid? Value { get; set; }
		public float Time { get; set; }
		public float CurTime { get; set; }
		public int WorkerID { get; set; }

		// 세이브 복원용. Unity 7 IL2CPP에서 매개변수 이름이 비어 생성자 매칭이 실패
		public Work() { }

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