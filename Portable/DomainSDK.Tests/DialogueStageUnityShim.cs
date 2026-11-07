using System;

namespace UnityEngine
{
	// Domain/Narrative/Stage 엔진 밖 빌드용 표시 속성
	[AttributeUsage(AttributeTargets.Field)]
	internal sealed class SerializeField : Attribute
	{
	}
}
