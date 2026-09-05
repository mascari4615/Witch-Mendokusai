using SimColor = WitchMendokusai.Numerics.Color;
using SimVector2 = WitchMendokusai.Numerics.Vector2;
using SimVector2Int = WitchMendokusai.Numerics.Vector2Int;
using SimVector3 = WitchMendokusai.Numerics.Vector3;
using SimVector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	/// <summary>
	/// 판정 층(DomainSDK.Numerics) 과 엔진(UnityEngine) 사이 값 변환. 엔진 다리는 여기 한 곳
	/// DomainSDK 는 UnityEngine 을 모름 (asmdef noEngineReferences). 변환은 Core 의 확장 메서드
	/// 방향이 코드에 보임: <c>ToUnity()</c> 는 판정 값을 화면으로, <c>ToSim()</c> 은 엔진 값을 판정 세계로
	/// </summary>
	public static class NumericsUnityBridge
	{
		public static UnityEngine.Vector2 ToUnity(this SimVector2 value) => new UnityEngine.Vector2(value.x, value.y);
		public static UnityEngine.Vector2Int ToUnity(this SimVector2Int value) => new UnityEngine.Vector2Int(value.x, value.y);
		public static UnityEngine.Vector3 ToUnity(this SimVector3 value) => new UnityEngine.Vector3(value.x, value.y, value.z);
		public static UnityEngine.Vector3Int ToUnity(this SimVector3Int value) => new UnityEngine.Vector3Int(value.x, value.y, value.z);
		public static UnityEngine.Color ToUnity(this SimColor value) => new UnityEngine.Color(value.r, value.g, value.b, value.a);

		public static SimVector2 ToSim(this UnityEngine.Vector2 value) => new SimVector2(value.x, value.y);
		public static SimVector2Int ToSim(this UnityEngine.Vector2Int value) => new SimVector2Int(value.x, value.y);
		public static SimVector3 ToSim(this UnityEngine.Vector3 value) => new SimVector3(value.x, value.y, value.z);
		public static SimVector3Int ToSim(this UnityEngine.Vector3Int value) => new SimVector3Int(value.x, value.y, value.z);
		public static SimColor ToSim(this UnityEngine.Color value) => new SimColor(value.r, value.g, value.b, value.a);
	}
}
