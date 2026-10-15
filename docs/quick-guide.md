# silkroad 간단 제작 가이드

전체 흐름: **Unity 실행 확인 → ComfyUI 품질 확인 → 태블릿 연결 → 디자인 입히기 → 검수**

---

## 1. Unity에서 실행 확인 (오늘)

1. Unity Hub → 프로젝트 임포트 → `C:\silkroad\unity` 열기 (6000.3.26f1)
2. ▶ 실행 중이면 먼저 멈추기
3. 메뉴 **Silkroad → 씬 자동 생성**
4. Game 창 **Free Aspect → + → 2000×1200** 추가 후 선택
5. ▶ 실행 → 시작하기 → 그리기 → 보내기 → 결과 비교 (더미 결과)
6. Console 에 빨간 줄이 있으면 캡처해서 Claude에게 전달

> ⚠️ 씬 자동 생성은 처음 한 번만. 디자인을 바꾼 뒤 다시 누르면 초기화됨.

---

## 1-1. 두루마리 연출

메뉴 **Silkroad → 두루마리 연출 추가 (씬 유지)** → ▶ 실행하면 시작 화면 종이가 펼쳐지고 제목·버튼이 나타남.
각 Panel 의 **ScrollReveal** 에서 속도 조절, **Roller** 오브젝트에 두루마리 막대 이미지 넣기.

---

## 2. ComfyUI 워크플로 (정리됨 · 새로 준비 예정)

> 🔑 API 키는 `C:\keydata\comfy_key.py` 의 `comfy_key` 변수에서 자동으로 읽습니다 (Unity 에디터, 안드로이드 빌드). 키를 코드·씬·깃에 넣지 마세요.

이전 워크플로·프리셋·테스트 도구는 2026-10-15 에 모두 정리했습니다. Unity 의 Comfy Cloud 연결 코드는 그대로 있어서 파일만 넣으면 동작합니다.

**워크플로 넣는 곳** (현재 비어 있음, 새 워크플로 받으면 여기에):
- `unity/Assets/StreamingAssets/ComfyUI/drawing_to_silkroad.api.json` — ComfyUI 에서 **Export (API)** 한 JSON. 입력 그림·프롬프트·seed 자리에 자리표시자 `{{INPUT_IMAGE}}` `{{POSITIVE}}` `{{NEGATIVE}}` `{{SEED}}` `{{STEPS}}` `{{CFG}}` (숫자는 따옴표 없이)
- `unity/Assets/StreamingAssets/ComfyUI/silkroad_style.json` — 프롬프트·steps·cfg·seed·`outputNode`(결과 SaveImage 노드 ID)
- **테마:** `positive` 중간의 `{{THEME}}` 자리에 테마 문장이 들어감. 처음 보내기 = 빈칸(기본 테마), **재요청 = `themes` 3개 중 랜덤 1개** (직전과 다르게). 테마는 `themes` 의 name/prompt 를 고치거나 추가
- 파일이 없으면 생성 시 "워크플로 파일을 읽지 못했습니다" → 그동안은 **Use Dummy** 로 테스트

---

## 3. Unity ↔ ComfyUI 연결 (2주차)

1. Unity Hierarchy **ComfyUI (설정)** 클릭
2. Inspector에서 **Use Dummy 체크 해제**, Server Url `https://cloud.comfy.org` (직접 설치 PC면 `http://PC의IP:8188`)
3. ▶ 실행 → 그림 → 보내기 → 실제 AI 결과 확인

---

## 4. 태블릿 설치 (3주차)

1. **File → Build Profiles → Android → Switch Platform**
2. 메뉴 **Silkroad → 안드로이드 태블릿 설정 적용**
3. 태블릿: 설정 → 태블릿 정보 → 빌드번호 7번 터치 → 개발자옵션 → **USB 디버깅 ON**
4. USB 연결 → **Build And Run**
5. PC IP 확인 (`cmd` → `ipconfig`), 방화벽 8188 허용
6. 앱 메인 **설정** → `http://PC의IP:8188` → 연결 테스트 → 더미 해제 → 저장

---

## 5. 디자인 입히기 (4주차)

1. 두루마리·액자·버튼 PNG를 `Assets/_Project/Art/`에 넣기
2. PNG 클릭 → Texture Type **Sprite (2D and UI)** → Apply
3. Hierarchy에서 바꿀 오브젝트 선택 → Image의 **Source Image**에 끌어넣기, Color 흰색
4. 제목 문구: `MainPanel → Title`의 Text 수정

### 5-1. 랜덤 액자

1. 액자 PNG 를 `Assets/_Project/Art/Frames/` 에 넣기 (**가운데 그림 들어갈 자리는 투명**하게 뚫린 PNG)
2. 처음 한 번만: 메뉴 **Silkroad → 액자 목록 갱신 (랜덤 액자)** → 등록 결과 창 확인
3. 이후에는 폴더에 넣거나 지우기만 하면 목록 자동 갱신 → 결과가 나올 때마다 그중 하나가 랜덤으로 씌워짐
4. 특정 액자를 빼려면 `Assets/_Project/Configs/FrameLibrary` 클릭 → 해당 항목 **Enabled** 체크 해제
   - 랜덤을 끄려면 **Random** 체크 해제 (첫 번째 액자만 사용)
5. 창 비율이 16:9 와 다르면 액자 테두리가 그림 가장자리를 조금 덮음 (틈은 생기지 않음)

---

## 6. 검수 (5~6주차)

- `docs/acceptance-checklist.md` 항목 A01~A16 체크
- 생성 시간: 중앙값 20초 / 95% 40초 이내 목표
- 연속 30회 체험해서 멈춤·꼬임 없는지 확인

---

## 막히면

| 증상 | 해결 |
|---|---|
| Silkroad 메뉴가 회색 | ▶ 실행 멈추기 |
| 클릭해도 안 그려짐 | 씬 자동 생성 다시 (디자인 전일 때만) |
| 태블릿 연결 실패 | 같은 Wi-Fi? `--listen 0.0.0.0`? 방화벽 8188? |
| 생성 결과가 그림과 너무 다름 | positive 의 '구도 유지' 문장 강화, cfg 올리기 |
| 그 외 오류 | Console 캡처 → Claude에게 |

자세한 설명: `docs/setup-guide.md`, `docs/api-integration.md`
