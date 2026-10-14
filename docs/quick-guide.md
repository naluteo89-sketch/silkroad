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

## 2. ComfyUI 생성 품질 확인 (1주차)

> 🔑 API 키는 `C:\keydata\comfy_key.py` 의 `comfy_key` 변수에서 자동으로 읽습니다 (파이썬 도구, Unity 에디터, 안드로이드 빌드 모두). 키를 코드·씬·깃에 넣지 마세요. 빌드 시에만 apk 안에 들어가고 프로젝트 폴더에서는 자동 삭제됩니다.

### 2-A. Comfy Cloud API 사용 (기본)

1. comfy.org 가입 → Standard 이상 구독 → **platform.comfy.org 에서 API 키 발급**
2. 명령 프롬프트:
   ```
   cd C:\silkroad
   python tools\check_comfyui.py --server https://cloud.comfy.org
   ```
   → 모델 이름 [실패] 가 나오면, 표시된 '서버에 있는 파일' 이름으로 `comfyui\workflows\drawing_to_silkroad.api.json` 의 `ckpt_name` / `control_net_name` 수정
3. `python tools\comfy_batch_test.py --server https://cloud.comfy.org --seeds 3`
4. 결과 판정·프리셋 조정은 아래 6~8번과 동일
5. Unity: `ComfyUI (설정)` → Server Url `https://cloud.comfy.org`, **Api Key 는 비워 둠**, Use Dummy 해제

### 2-B. 직접 GPU PC 에 설치하는 경우

1. GPU PC에 ComfyUI 설치 (Windows Portable 권장)
2. 모델 2개를 받아 넣기 → `comfyui/dependencies.md` 참고
   - `models/checkpoints/sd_xl_base_1.0.safetensors`
   - `models/controlnet/controlnet-canny-sdxl-1.0.safetensors`
3. ComfyUI 실행 (`run_nvidia_gpu.bat` 끝에 ` --listen 0.0.0.0` 추가)
4. 연결·모델 점검: `python tools\check_comfyui.py` → 전부 [OK] 인지 확인
5. 테스트:
   ```
   cd C:\silkroad
   python tools\comfy_batch_test.py --seeds 3
   ```
6. `tools\out\날짜\` 결과 보고 `results.csv` 판정 칸 채우기 (목표 30개 중 24개)
7. 마음에 안 들면 `comfyui\presets\silkroad_style.json` 값 **하나씩** 바꿔 재실행
   - `denoise` ↓ = 원본에 가깝게 / ↑ = 더 많이 바뀜
   - `controlStrength` ↑ = 선·배치 더 유지
   - `positive` = 화풍 문구
8. 바꾼 뒤 `tools\sync_comfyui.bat` 더블클릭 → Unity로 복사

---

## 3. Unity ↔ ComfyUI 연결 (2주차)

1. Unity Hierarchy **ComfyUI (설정)** 클릭
2. Inspector에서 **Use Dummy 체크 해제**, Server Url `http://127.0.0.1:8188`
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
| 생성 결과가 그림과 너무 다름 | denoise 낮추기, controlStrength 높이기 |
| 그 외 오류 | Console 캡처 → Claude에게 |

자세한 설명: `docs/setup-guide.md`, `docs/api-integration.md`
