// 하네스 전용 대역 — 대화 코드가 「이름만」 닿는 게임 쪽 타입들. 실제 동작은 여기서 검증 대상이 아니다.
using System.Collections.Generic;

namespace WitchMendokusai
{
	public class DataManager
	{
	}

	public struct EffectInfoData
	{
		public EffectType Type;
		public int DataSoID;
		public ArithmeticOperator ArithmeticOperator;
		public int Value;
	}

	/// <summary>실제 인터페이스와 같은 모양 — 대화의 좁은 통로가 이 위에 얹힌다는 것만 확인한다.</summary>
	public interface IEffectRunner
	{
		void ApplyEffects(List<EffectInfo> effectInfos);
		void ApplyEffects(List<EffectInfoData> effectInfoData);
		void ApplyEffect(EffectInfo effectInfo);
		void BindDataManager(DataManager dataManager);
	}
}
