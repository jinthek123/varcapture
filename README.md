# VArchiveHelper **v0.1.1**

Insert 한 번으로 **2번 모니터(기본 인덱스 1)** 를 지정 해상도로 캡처 → 클립보드 → v-archive **모드1 인식(Alt+Insert)**.

DJMAX 등 전체화면 게임용 보조 도구입니다. [v-archive](https://github.com/kokonohanahata/v-archive) 필요.

## 다운로드 · 설치

1. **`VArchiveHelper-win.zip`** 압축 해제 (exe와 dll이 **같은 폴더**에 있어야 함)
2. `appsettings.json` 에서 **`VArchiveExePath`** 를 본인 PC의 v-archive.exe 경로로 수정  
   (UI **적용 및 저장**으로 저장 가능)
3. `VArchiveHelper.exe` 실행 → **캡처 미리보기** 창 확인
4. DJMAX **2번 모니터**에서 **Insert**

**dotnet SDK 불필요.** Windows 10/11 + .NET Framework 4.7.2 이상.

## v-archive 설정

1. **캡처 / 업로드 따로 (모드 1)**
2. 인식 단축키: **Alt+Insert**
3. **모드 2 Insert** 는 다른 키로 변경 (Insert 충돌 방지)

## 설정 (`appsettings.json`)

| 항목 | 설명 |
|------|------|
| MonitorIndex | 0=1번 모니터, **1=2번 모니터** |
| TargetWidth / TargetHeight | v-archive OCR용 가로·세로 (FHD/QHD/UHD/16:10 등) |
| VArchiveExePath | v-archive.exe 절대 경로 |
| UseDxgiCapture | DXGI 모니터 전체 캡처 (권장) |
| UsePhysicalPixels | 물리 픽셀 해상도 사용 |
| UseGameWindowCapture | 창 크롭 (기본 **false**, 잘림 방지) |

미리보기 오른쪽에서 **가로·세로**, FHD/QHD/UHD 프리셋, **선택 모니터 해상도**, v-archive 경로를 바꿀 수 있습니다.

## 사용

- **Insert** → 캡처 → (v-archive 실행) → 클립보드 → Alt+Insert 인식
- Insert가 다른 프로그램과 충돌하면 미리보기 **Insert 동작 실행**
- 트레이 아이콘 우클릭 → 미리보기 창 / 종료

## DJMAX 전체화면

게임 패치 후에도 **DXGI + 물리 픽셀** 로 모니터 전체를 캡처합니다. 미리보기에서 **원본 해상도**와 `[DXGI]` 를 확인하세요.
