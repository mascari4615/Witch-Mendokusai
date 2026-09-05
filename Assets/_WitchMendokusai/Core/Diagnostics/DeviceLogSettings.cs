using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 기기 로그 릴레이 설정. 주소·주기·상한 전부 여기로 노출한다
    /// (수치 하드코딩 금지 룰). `Resources/DeviceLogSettings.asset` 단 하나가 정본.
    ///
    /// **토큰은 여기 안 넣는다** — WM 은 공개 레포다. 토큰은 ① 환경변수
    /// `WM_DEVICE_LOG_TOKEN` (에디터·PC) ② `Resources/DeviceLogToken.txt`
    /// (빌드 시 생성, gitignore) 순으로 읽는다.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(DeviceLogSettings), menuName = "WM/DeviceLogSettings")]
    public class DeviceLogSettings : ScriptableObject
    {
        [field: Header("보내기")]
        [field: Tooltip("끄면 릴레이 자체를 설치하지 않는다 (게임 흐름 0 영향).")]
        [field: SerializeField] public bool Enabled { get; private set; } = true;

        [field: Tooltip("에디터에서도 보낼지. 기본 끔 — 에디터는 콘솔이 이미 정본.")]
        [field: SerializeField] public bool EnabledInEditor { get; private set; } = false;

        [field: Tooltip("서버 수신 주소. 노트북 prod yawnbot.")]
        [field: SerializeField] public string Endpoint { get; private set; } = "https://yawnbot.mascari4615.com/device-log";

        [field: Header("주기 · 크기")]
        [field: Tooltip("몇 초마다 모아 보낼지.")]
        [field: SerializeField] public float FlushIntervalSeconds { get; private set; } = 3f;

        [field: Tooltip("한 번에 보낼 최대 줄 수.")]
        [field: SerializeField] public int MaxLinesPerBatch { get; private set; } = 200;

        [field: Tooltip("메모리 버퍼 최대 줄 수. 넘치면 오래된 *비에러* 줄부터 버린다.")]
        [field: SerializeField] public int BufferCapacity { get; private set; } = 2000;

        [field: Tooltip("이 줄 수 이상 쌓이면 주기를 기다리지 않고 바로 보낸다.")]
        [field: SerializeField] public int ImmediateFlushThreshold { get; private set; } = 100;

        [field: Tooltip("에러급은 주기를 기다리지 않고 바로 보낸다 (죽기 직전 유언 확보).")]
        [field: SerializeField] public bool FlushImmediatelyOnError { get; private set; } = true;

        [field: Header("무엇을 보낼까")]
        [field: Tooltip("일반 Log 까지 보낼지. 끄면 warning 이상만.")]
        [field: SerializeField] public bool IncludeInfoLogs { get; private set; } = true;

        [field: Tooltip("스택 트레이스를 함께 보낼 최소 등급 이상 (에러급은 항상 보낸다).")]
        [field: SerializeField] public bool IncludeStackForWarnings { get; private set; } = false;

        [field: Header("기기 저장 (크래시 대비)")]
        [field: Tooltip("보내기 전에 기기 파일에 먼저 적는다. 못 보낸 줄은 다음 실행 때 밀어 넣는다.")]
        [field: SerializeField] public bool SpoolToDisk { get; private set; } = true;

        [field: Tooltip("기기 스풀 파일 1개 최대 바이트.")]
        [field: SerializeField] public long SpoolMaxBytes { get; private set; } = 16 * 1024 * 1024;

        [field: Tooltip("기기에 남겨둘 지난 실행 세션 수.")]
        [field: SerializeField] public int SpoolKeepSessions { get; private set; } = 5;

        [field: Header("네트워크")]
        [field: Tooltip("전송 타임아웃(초).")]
        [field: SerializeField] public int RequestTimeoutSeconds { get; private set; } = 10;

        [field: Tooltip("연속 실패가 이만큼 쌓이면 다음 시도까지 쉬는 시간을 늘린다.")]
        [field: SerializeField] public int BackoffAfterFailures { get; private set; } = 3;

        [field: Tooltip("쉬는 시간 상한(초).")]
        [field: SerializeField] public float MaxBackoffSeconds { get; private set; } = 60f;
    }
}
