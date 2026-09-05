using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(RuleEntry), menuName = "WM/Variable/Entry/RuleEntry")]
	public class RuleEntry : BaseEntry
	{
		public List<EventEntry> TriggeredBy => triggeredBy;
		public List<EventEntry> Triggers => triggers;
		public List<Criteria> Criteria => criteria;
		public List<Modification> Modifications => modifications;

		[SerializeField] private List<EventEntry> triggeredBy;
		[SerializeField] private List<EventEntry> triggers;
		// Criteria 는 추상 클래스다 — [SerializeField] 로는 Unity 가 통째로 무시해 인스펙터에 넣어도 저장이 안 됐다
		// (자산 YAML 에 남아 있던 criteria 항목이 옛 구체 클래스 시절 잔해). 다형 타입은 [SerializeReference] 가 정본.
		[SerializeReference] private List<Criteria> criteria;
		[SerializeField] private List<Modification> modifications;
	}


	[Serializable]
	public class Modification
	{
		public FactEntry FactEntry;
		public ArithmeticOperator ArithmeticOperator;
		public int Value;
	}
}