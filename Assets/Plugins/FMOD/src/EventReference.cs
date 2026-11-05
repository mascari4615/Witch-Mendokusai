using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FMODUnity
{
    // Unity 7: 이름이 GUID 인 구조체는 자체 타입으로 저장되고 다시 읽으면 0. 저장 전용 이름
    [Serializable]
    public struct SerializedGuid
    {
        public int Data1;
        public int Data2;
        public int Data3;
        public int Data4;

        public static implicit operator FMOD.GUID(SerializedGuid value) =>
            new FMOD.GUID { Data1 = value.Data1, Data2 = value.Data2, Data3 = value.Data3, Data4 = value.Data4 };

        public static implicit operator SerializedGuid(FMOD.GUID value) =>
            new SerializedGuid { Data1 = value.Data1, Data2 = value.Data2, Data3 = value.Data3, Data4 = value.Data4 };
    }

    [Serializable]
    public struct EventReference
    {
        [FormerlySerializedAs("Guid")]
        public SerializedGuid GuidData;

        public FMOD.GUID Guid
        {
            get => GuidData;
            set => GuidData = value;
        }

#if UNITY_EDITOR
#if FMOD_SERIALIZE_GUID_ONLY
        [NonSerialized]
#endif
        public string Path;

        public static Func<string, FMOD.GUID> GuidLookupDelegate;

        public override string ToString()
        {
            return string.Format("{0} ({1})", Guid, Path);
        }

        public bool IsNull
        {
            get
            {
                return string.IsNullOrEmpty(Path) && Guid.IsNull;
            }
        }

        public static EventReference Find(string path)
        {
            if (GuidLookupDelegate == null)
            {
                throw new InvalidOperationException("EventReference.Find called before EventManager was initialized");
            }

            return new EventReference { Path = path, Guid = GuidLookupDelegate(path) };
        }
#else
        public override string ToString()
        {
            return Guid.ToString();
        }

        public bool IsNull
        {
            get
            {
                return Guid.IsNull;
            }
        }
#endif
    }
}
