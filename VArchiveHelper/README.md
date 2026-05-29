# VArchiveHelper v0.1.1

Insert 한 번으로 **2번 모니터(기본 인덱스 1)** 를 2560×1440으로 캡처 → 클립보드 → v-archive **모드1 인식(Alt+Insert)**.

## v-archive 설정

1. **캡처 / 업로드 따로 (모드 1)**
2. 인식 단축키: **Alt+Insert** (기본값)
3. **모드 2 Insert** 는 다른 키로 바꾸거나 모드 1만 사용 (Insert 충돌 방지)

## 설정 (`appsettings.json`)

처음 사용 시 `appsettings.example.json` 을 복사해 `appsettings.json` 으로 이름을 바꾼 뒤 편집합니다.

| 항목 | 기본 | 설명 |
|------|------|------|
| MonitorIndex | 1 | 0=1번 모니터, **1=2번 모니터** |
| TargetWidth/Height | 2560/1440 | v-archive OCR 해상도 |
| VArchiveExePath | (본인 경로) | v-archive.exe 절대 경로 |
| UseDxgiCapture | true | DXGI 모니터 전체 캡처 (권장) |
| UsePhysicalPixels | true | 네이티브 2560×1440 등 물리 해상도 |
| UseGameWindowCapture | false | 창 크롭 (잘림 유발, 기본 끔) |
| PreviewIntervalMs | 500 | 미리보기 갱신 간격 |

캡처 순서: **DXGI → GDI → (선택) Window**. 트레이·미리보기에 `[DXGI]` / `[GDI]` 표시.

## 실행

1. Releases zip 또는 `dotnet build -c Release` 후 **`VArchiveHelper/bin/Release/net472/`** 에서 실행 (구 `net472_fix`·`net472_build` 폴더는 사용하지 않음)
2. **캡처 미리보기** 창에서 화면 확인
3. DJMAX **2번 모니터**에서 **Insert** (또는 미리보기 **Insert 동작 실행**)

exe와 **같은 폴더**에 `appsettings.json`, DLL이 있어야 합니다.

## 캡처 미리보기 UI

- **실시간 미리보기**: v-archive로 보내는 것과 동일한 화면
- **오른쪽 설정**: 가로·세로(px), FHD/QHD/UHD/16:10 프리셋, 모니터 인덱스, v-archive 경로
- **적용 및 저장** → `appsettings.json` 저장 후 미리보기 갱신
- **선택 모니터 해상도**: 현재 모니터 네이티브 해상도를 가로·세로에 채움
- **Insert 동작 실행**: Insert 키 없이 테스트
- 창 X → 트레이 상주, 우클릭으로 다시 열기

## 주의

- 모니터 해상도가 2560×1440이 아니면 비율 없이 리사이즈됩니다.
- v-archive는 **캡처 후** 자동 실행 (게임 포커스 유지).
- Insert 단축키가 다른 프로그램과 충돌하면 미리보기 **Insert 동작 실행** 사용.

## DJMAX 전체화면

게임 업데이트 후 GDI만으로는 옛 화면이 나올 수 있습니다. `UseDxgiCapture`·`UsePhysicalPixels` 를 켜 두고, 미리보기에서 **원본 2560×1440**·`[DXGI]` 를 확인하세요.

`UseGameWindowCapture` 는 기본 **false** (모니터 전체 캡처).
