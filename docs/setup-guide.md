# 개발 환경 설정 가이드 (silkroad)

**실행 기기:** 뮤패드 K10 PLUS (Android 13, Helio G99, 2000×1200, 10.4")
**개발 PC:** Windows + Unity 6.3 LTS
**저장소 위치:** `C:\silkroad`

Unity를 처음 써도 따라 할 수 있게 정리했어요.

---
## 1단계. Unity 설치하기

1. https://unity.com/download 에서 **Unity Hub**를 받아 설치합니다.
2. Unity Hub를 실행하고 Unity 계정으로 로그인합니다(무료 Personal 라이선스면 충분해요).
3. 왼쪽 **Installs** → **Install Editor** → **Unity 6.3 (LTS)** 버전 옆 **Install**을 누릅니다.
   - 모듈 선택 화면에서 **Android Build Support**를 꼭 체크하고, 그 아래 **OpenJDK**와 **Android SDK & NDK Tools**도 같이 체크하세요. 태블릿용 빌드에 필요해요.
   - 이미 설치했다면 Installs → 6.3 옆 톱니바퀴 → **Add modules**에서 추가할 수 있어요.
   - Visual Studio Community도 같이 설치하면 나중에 스크립트를 열어 볼 때 편합니다.

## 2단계. 프로젝트 열기

`C:\silkroad\unity` 폴더가 이미 Unity 프로젝트 폴더예요. 새로 만들지 않고 **열기만** 하면 됩니다.

1. Unity Hub → **Projects** → 오른쪽 위 **Add ▾ → Add project from disk**
2. `C:\silkroad\unity` 폴더를 선택합니다.
3. 목록에 생긴 `unity` 프로젝트의 **Editor Version** 칸에서 설치한 **6.3** 을 고른 뒤 클릭해서 엽니다.
   - 처음 열 때는 Library 폴더를 만드느라 몇 분 걸려요.
   - "Input System 활성화 / 재시작" 창이 뜨면 **No** 를 눌러도 되고 **Yes** 를 눌러도 됩니다(둘 다 동작).
4. 아래 **Console** 창에 빨간 오류가 없으면 성공이에요.
   - `com.unity.ugui` 패키지 관련 오류가 나면: **Window → Package Manager** 에서 **Unity UI** 를 설치(또는 업데이트)하세요.

---

## 3단계. 씬 자동 생성 → 실행

1. 상단 메뉴에 새로 생긴 **Silkroad → 씬 자동 생성 (Build Scene)**을 누릅니다.
2. "씬이 만들어졌어요!" 창이 뜨면 **확인**을 누릅니다.
3. 화면 위쪽 가운데 **▶ (Play)** 버튼을 누릅니다.
4. **시작하기 → 그림 그리기 → 보내기 → "생성 중..." → 결과 비교 화면** 순서로 동작하는지 확인합니다.
   - 지금은 **더미 모드**라서 AI 대신 세피아 필터를 입힌 가짜 결과가 나와요.
   - 결과 화면의 가운데 막대를 드래그하면 **내 그림 / AI 결과**를 비교할 수 있어요.
5. 다시 ▶ 버튼을 누르면 실행이 멈춥니다.

> **팁:** 태블릿과 같은 화면으로 보려면 Game 창 위쪽 해상도 드롭다운 → **+** → 이름 `K10`, Width `2000`, Height `1200`을 추가해서 고르세요.

### 들어 있는 기능

| 화면 | 기능 |
|---|---|
| 메인 | 시작하기, 종료, 설정(서버 주소·더미 모드) |
| 그림판 | 브러시, 지우개, 채우기, 취소(Undo), 복원(Redo), 굵기 슬라이더, 20색 팔레트, 보내기, 전체 지우기, 메인으로 |
| 생성 중 | 아이콘이 통통 튀는 로딩 화면. 실패하면 오류 메시지와 돌아가기 버튼이 나와요 |
| 결과 | 내 그림/AI 결과 비교 슬라이더, 홈, 재요청(새 시드로 다시 생성), 그림 수정(그리던 그림으로 돌아가기), 저장 |

결과는 자동으로 저장돼요. 저장 위치는 `C:\Users\<이름>\AppData\LocalLow\<회사명>\<프로젝트명>\Results`이고, 실제 경로는 Console 창에서 확인할 수 있어요.

---

### 체험 흐름에서 지켜지는 규칙
- **시작하기** 를 누를 때마다 새 세션이 열리고 캔버스가 비워져요 (이전 사람 그림이 보이지 않음).
- 빈 캔버스로 보내기를 누르면 "먼저 그림을 그려 주세요!" 안내만 나와요.
- 재요청이 실패하면 "이전 결과로" 버튼으로 직전 결과를 다시 볼 수 있어요.
- 결과 화면 **저장** = 결과 확정 (XR 쪽으로 전달). 원본/결과/메타데이터는 자동 저장돼요.

## 4단계. 나만의 디자인으로 바꾸기

자동으로 만들어진 UI는 단색 사각형이라 **이미지만 갈아 끼우면** 원하는 디자인이 돼요.

1. 직접 만든 PNG(버튼, 배경, 프레임 등)를 Project 창으로 끌어다 놓습니다.
2. 그 PNG를 클릭하고 Inspector에서 **Texture Type**을 **Sprite (2D and UI)**로 바꾼 뒤 **Apply**를 누릅니다.
3. Hierarchy에서 `SilkroadCanvas → PaintPanel → Frame`(또는 원하는 버튼)을 클릭합니다.
4. Inspector의 **Image → Source Image** 칸에 PNG를 끌어다 놓고, **Color**를 흰색으로 바꿉니다.

| 바꾸고 싶은 것 | 위치 |
|---|---|
| 종이 색(지우개 색) | `DrawingCanvas` → PaintCanvas → Background Color |
| 캔버스 해상도 | `DrawingCanvas` → PaintCanvas → Texture Width/Height |
| 버튼 선택 시 색 | `PaintPanel` → PaintToolbar → Selected Color |
| 팔레트 색 | `PaintPanel → Palette → Color0~19`의 Image Color |
| 로딩 아이콘 | `LoadingPanel → Icon`의 Source Image |

> ⚠️ **씬 자동 생성을 다시 누르면** 씬이 처음 상태로 새로 만들어져서 직접 꾸민 내용이 사라져요. 꾸미기 시작한 뒤에는 누르지 마세요.

---
## 5단계. 실제 ComfyUI 연결하기

1. GPU PC 에 ComfyUI 설치 → `comfyui/dependencies.md` 의 모델 2개를 받아 해당 폴더에 넣기
2. ComfyUI 를 `--listen 0.0.0.0` 으로 실행 (dependencies.md 참고)
3. **Unity 없이 먼저 테스트**: 명령 프롬프트에서
   ```
   cd C:\silkroad
   python tools\comfy_batch_test.py --server http://127.0.0.1:8188 --seeds 1
   ```
   `tools\out\<시각>\` 에 결과 그림이 생기면 워크플로 정상.
4. Unity 에서 Hierarchy 의 **`ComfyUI (설정)`** → **Use Dummy** 체크 해제 → ▶ 실행 → 보내기

워크플로·프리셋은 `comfyui/` 폴더에서 고치고 **`tools\sync_comfyui.bat`** 을 더블클릭해서 Unity 로 복사합니다. 자세한 내용: `docs/api-integration.md`

## 6단계. 뮤패드 K10 PLUS 에 설치하기

### 6-1. Unity 를 안드로이드용으로 바꾸기 (처음 한 번)
1. **File → Build Profiles** → 왼쪽에서 **Android** 선택 → **Switch Platform** (몇 분 걸려요)
2. 상단 메뉴 **Silkroad → 안드로이드 태블릿 설정 적용** 클릭
   (가로 화면 고정, http 통신 허용, 64비트(ARM64) 설정이 자동으로 들어갑니다)

### 6-2. 태블릿 준비 (처음 한 번)
1. 태블릿 **설정 → 태블릿 정보 → 소프트웨어 정보 → 빌드 번호** 를 7번 연속 터치 → "개발자 모드" 켜짐
2. **설정 → 개발자 옵션 → USB 디버깅** 켜기
3. USB 케이블로 PC 와 연결 → 태블릿에 뜨는 "USB 디버깅 허용" 에서 **허용**

### 6-3. 설치 & 실행
1. Build Profiles 창에서 **Run Device** 에 태블릿이 보이는지 확인 (안 보이면 Refresh)
2. **Build And Run** → 저장할 파일 이름(예: `AIPaint.apk`) 입력
3. 빌드가 끝나면 태블릿에서 앱이 자동으로 실행됩니다. 이후엔 태블릿 앱 목록에서 실행하면 돼요.

> USB 연결이 안 되면: **Build** 로 apk 파일만 만든 뒤, 태블릿으로 복사해서 파일 앱에서 눌러 설치해도 됩니다(“출처를 알 수 없는 앱” 허용 필요).

### 6-4. 태블릿 ↔ PC 의 ComfyUI 연결
태블릿에서는 ComfyUI 를 돌릴 수 없으니 **그래픽카드가 있는 PC 에서 ComfyUI 를 실행**하고, 태블릿이 같은 Wi-Fi 로 접속합니다.

1. **PC 에서 ComfyUI 를 외부 접속 허용으로 실행**
   - 포터블 버전: `run_nvidia_gpu.bat` 을 메모장으로 열어 맨 끝에 ` --listen 0.0.0.0` 추가 후 저장 → 실행
   - 데스크톱 앱: 설정(Settings) → Server-Config → **Host** 를 `0.0.0.0` 으로
2. **PC 의 IP 확인**: 시작 → `cmd` → `ipconfig` → "IPv4 주소" (예: `192.168.0.10`)
3. **방화벽 허용**: 처음 실행 시 뜨는 Windows 방화벽 창에서 **개인 네트워크 허용**
   (안 떴다면: Windows 보안 → 방화벽 → 고급 설정 → 인바운드 규칙 → 새 규칙 → 포트 TCP 8188 허용)
4. 태블릿 브라우저로 `http://192.168.0.10:8188` 이 열리는지 먼저 확인
5. 앱 메인 화면 오른쪽 아래 **설정** → 서버 주소에 `http://192.168.0.10:8188` 입력 → **연결 테스트** → "연결 성공!" 이면 **더미 모드 체크 해제** → **저장**

설정은 태블릿에 저장되므로, PC 의 IP 가 바뀌어도 **다시 빌드할 필요 없이 설정 창에서만 고치면** 됩니다.

> 💡 PC 의 IP 가 자꾸 바뀌면 공유기 설정에서 PC 에 "고정 IP(DHCP 예약)" 를 걸어 두면 편해요.

---
## 폴더 구성

```
C:\silkroad
 ├─ unity/                         Unity 프로젝트 (Hub 에서 이 폴더를 엶)
 │   └─ Assets/
 │       ├─ _Project/
 │       │   ├─ Scenes/Main.unity          (씬 자동 생성으로 만들어짐)
 │       │   ├─ Scripts/
 │       │   │   ├─ Drawing/    PaintCanvas         그리기 엔진 (브러시/지우개/채우기/취소/복원)
 │       │   │   ├─ Generation/ ComfyUIClient, GenerationPreset   ComfyUI 통신 / 프리셋
 │       │   │   ├─ UI/         PaintToolbar, LoadingView, ResultView, SettingsView, KoreanFontFixer
 │       │   │   ├─ Session/    AppController       세션·요청 관리, 화면 전환, 저장
 │       │   │   └─ XR/         XRResultBridge      확정 결과 → XR 전달 자리
 │       │   ├─ Editor/SilkroadSceneBuilder        메뉴: 씬 자동 생성 / 안드로이드 설정
 │       │   ├─ Art/Fonts/                         한글 폰트 (OFL)
 │       │   ├─ Prefabs/  Configs/                 (비어 있음, 이후 사용)
 │       └─ StreamingAssets/ComfyUI/               실행 시 읽는 워크플로·프리셋 복사본
 ├─ comfyui/      workflows/ presets/ node-mapping.json dependencies.md   ← 원본은 여기
 ├─ shared/       contracts/ (입출력 규격)  test-drawings/ (테스트 그림)
 ├─ docs/         plan.md  api-integration.md  acceptance-checklist.md  setup-guide.md
 └─ tools/        sync_comfyui.bat  comfy_batch_test.py
```

## 문제 해결

- **메뉴에 Silkroad가 안 보여요** → Console 창(Window → General → Console)에 빨간 에러가 있으면 스크립트가 컴파일되지 않은 거예요. 에러 내용을 복사해서 알려 주세요.
- **"InputSystem" 관련 에러가 나요** → Edit → Project Settings → Player → Other Settings → **Active Input Handling** 을 **Input Manager (Old)** 또는 **Both** 로 바꾸고 Unity 를 재시작하세요.
- **클릭해도 그려지지 않아요** → Hierarchy에 `EventSystem`이 있는지 확인하세요. 씬 자동 생성을 다시 하면 새로 만들어져요.
- **한글이 □로 보여요** → 프로젝트에 한글 폰트가 들어 있어서 씬 자동 생성 때 자동으로 적용돼요. 그래도 깨지면 씬 자동 생성을 한 번 더 눌러 주세요.
- **태블릿에서 "연결 실패"** → ① PC와 태블릿이 같은 Wi-Fi인지 ② ComfyUI를 `--listen 0.0.0.0`으로 켰는지 ③ 방화벽에서 8188 포트를 허용했는지 ④ 주소가 `http://`로 시작하는지 확인하세요.
- **Build Profiles에 태블릿이 안 보여요** → USB 케이블이 충전 전용이 아닌지, 태블릿의 USB 모드가 "파일 전송"인지 확인하세요.
