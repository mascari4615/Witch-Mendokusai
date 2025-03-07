using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 조건적 수행 노드 </summary>
	public abstract class DecoraterNode : Node
	{
		public Node child;
	}
}