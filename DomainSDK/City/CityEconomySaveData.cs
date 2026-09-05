using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// CityEconomy 영속 POCO — WorldStageSaveData 의 형제 필드(GridData/Road/Zone SaveData 동형).
	// 재고를 resourceId(int) → 누계(float) 로 직렬화. ResourceId(readonly struct) 직접 직렬화 회피 —
	// 저장층은 primitive(int/float, 검증된 경로), 런타임만 ResourceId(CityEconomy.Load/Save 가 변환).
	// 인구/일자리 등 추가 상태는 first-use(INC-6) 시 필드 추가(데드필드 선제 X). wrapper struct =
	// 미래 필드 확장에 ISavable<T> 타입 불변(확장성).
	// Json.NET 전용 저장 DTO — Unity 직렬화 대상이 아니라 [Serializable] 을 달지 않는다(GameData 주석 참조).
	public struct CityEconomySaveData
	{
		public List<KeyValuePair<int, float>> StockSaveData;
	}
}
