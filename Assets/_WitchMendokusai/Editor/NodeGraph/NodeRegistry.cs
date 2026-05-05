using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// `NodeBase` 서브클래스 카탈로그 — Editor 의 Create Node 메뉴에서 사용.
	/// `TypeCache.GetTypesDerivedFrom` 으로 reflection 1회 (Domain Reload 마다).
	/// 도메인 분리는 단계 C 에서 attribute 기반 — 1차는 단순 type 리스트.
	/// </summary>
	public static class NodeRegistry
	{
		public static IEnumerable<Type> AllNodeTypes()
		{
			return TypeCache.GetTypesDerivedFrom<NodeBase>()
				.Where(t => t.IsAbstract == false && t.IsGenericTypeDefinition == false);
		}
	}
}
