using System;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 노드 클래스에 붙여 도메인 마킹 — `NodeRegistry.NodeTypesForDomain` 이 카탈로그 필터링 시 사용.
	/// `Inherited = false` — 베이스 (예: PointFilterNodeBase) 의 attribute 가 자손에 자동 전파 X. 자손마다 명시.
	/// 미마킹 노드 = `NodeDomain.Generic` 으로 처리 — 모든 도메인 카탈로그에서 보임 (fallback).
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class NodeDomainAttribute : Attribute
	{
		public NodeDomain Domain { get; }

		public NodeDomainAttribute(NodeDomain domain)
		{
			Domain = domain;
		}
	}
}
