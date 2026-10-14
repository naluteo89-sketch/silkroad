# ComfyUI API 연동 (현재 구현)

두 방식 모두 지원합니다. `ComfyUI (설정)` 의 **Server Type = Auto** 이면 주소로 자동 선택.

| 방식 | 주소 | 인증 | 구현 |
|---|---|---|---|
| **Comfy Cloud API v2 (기본 사용 예정)** | `https://cloud.comfy.org` | `Authorization: Bearer <API 키>` | `ComfyUIClient.Cloud.cs` |
| 로컬/사내 GPU PC ComfyUI | `http://<IP>:8188` | 없음 | `ComfyUIClient.cs` |

## Comfy Cloud v2 흐름

| 단계 | 요청 | 상태 표시 |
|---|---|---|
| 1 | `POST /api/v2/assets` multipart (`file`, `content_type`, `file_path`, `expires_in=86400`) → `id` | 업로드 중 |
| 2 | 워크플로의 `"{{INPUT_IMAGE}}"` → `{"__type":"core/ASSET","info":{"id":"<asset id>"}}` 로 치환 | |
| 3 | `POST /api/v2/jobs` `{workflow, extra_data.api_key_comfy_org, metadata}` + `Idempotency-Key` 헤더 → job `id` | 대기 중 |
| 4 | `GET /api/v2/jobs/{id}` 반복 — `queued`/`running`/`succeeded`/`failed`/`canceled`/`expired` | 대기 중 / 생성 중 |
| 5 | `outputs[]` 중 `node_id == 12` 이미지 → `GET /api/v2/assets/{id}/content` (302) → 서명 URL 을 **인증 헤더 없이** 다운로드 | 결과 받는 중 |

준비:
0. API 키: `C:\keydata\comfy_key.py` 의 `comfy_key` 를 자동으로 읽음 (도구·에디터·빌드). 빌드 때만 `StreamingAssets/ComfyUI/comfy_key.txt` 가 잠깐 생겼다 삭제됨
1. comfy.org 계정 + **Standard 이상 구독** (무료 플랜은 API 불가) → platform.comfy.org 에서 API 키 발급
2. Unity `ComfyUI (설정)` → Server Url `https://cloud.comfy.org`, Api Key 는 비워 둠, Use Dummy 해제
3. 모델 이름은 **Comfy Cloud 에 있는 모델 이름**이어야 함 → `python tools/check_comfyui.py --server https://cloud.comfy.org` 로 확인 후 워크플로 JSON 수정
4. `python tools/comfy_batch_test.py --server https://cloud.comfy.org --seeds 1`

주의:
- API 키는 빌드(apk)에 포함됨 → 체험 전용 키를 만들고 사용량/결제 한도를 걸어 둘 것. 외부 배포 시엔 중계 서버로 키를 숨기는 방식 검토
- 공식 문서상 Cloud API 는 실험적(변경 가능) — 오류 시 응답 본문이 화면/Console 에 그대로 표시됨
- 태블릿은 인터넷(https)만 되면 되므로 로컬 방식의 방화벽/IP 설정 불필요

---

# 로컬 ComfyUI 방식

구현 위치: `unity/Assets/_Project/Scripts/Generation/ComfyUIClient.cs`

## 흐름

| 단계 | 요청 | Unity 상태 표시 | 실패 시 메시지 |
|---|---|---|---|
| 1 | `POST /upload/image` (multipart `image`, `overwrite=true`) → `name` 보관 | 업로드 중 | `업로드 실패` (서버 꺼짐/주소 오류 포함) |
| 2 | StreamingAssets 에서 워크플로·프리셋 읽고 자리표시자 치환 | 업로드 중 | `워크플로 파일을 읽지 못했습니다` |
| 3 | `POST /prompt` `{prompt, client_id[, extra_data.api_key_comfy_org]}` → `prompt_id` | 대기 중 | `실행 요청 실패` (+ node_errors: 모델 이름·노드 오류) |
| 4 | 1초마다 `GET /history/{prompt_id}`, 결과 없으면 `GET /queue` 로 실행/대기 판정 | 대기 중 ↔ 생성 중 | `ComfyUI 실행 오류` (status_str=error) / `시간 초과` (120초) |
| 5 | `outputs["12"].images[0]` → `GET /view?filename&subfolder&type=output` | 결과 받는 중 | `결과 다운로드 실패` |

- 결과는 **지정 출력 노드(`outputNodeId`, 기본 12)** 에서만 가져온다. 최근 이미지 검색 없음.
- 사용자가 ‘취소’/홈으로 나가면 Unity 쪽 요청만 중단한다. 서버의 GPU 작업은 취소하지 않는다 (`/interrupt` 는 공유 서버에서 다른 사람 작업을 멈출 수 있음).
- 늦게 도착한 응답은 `AppController` 가 세션 ID·요청 번호로 걸러서 버린다.
- 재요청이 실패하면 오류 화면의 버튼이 ‘이전 결과로’ 가 되어 직전 결과를 그대로 보여준다.

## 설정 위치

| 값 | 위치 |
|---|---|
| 서버 주소, 더미 모드 | 앱 메인 화면 ‘설정’ (PlayerPrefs 에 저장) / Unity Inspector `ComfyUI (설정)` |
| API 키 | Inspector `Api Key` (Comfy.org API 노드 또는 프록시 토큰 사용 시) |
| 워크플로·프리셋 경로 | Inspector `Workflow Path`, `Preset Path` (StreamingAssets 기준) |
| 제한 시간, 폴링 간격 | Inspector `Timeout Seconds` (120), `Poll Interval Seconds` (1) |

## 워크플로 교체 절차

1. ComfyUI 웹에서 워크플로 수정 → **Workflow → Export (API)** 로 저장
2. 입력/프롬프트/seed 등 값 자리에 자리표시자 입력 (숫자 자리는 따옴표 없이: `"seed": {{SEED}}`)
3. `comfyui/workflows/drawing_to_silkroad.api.json` 에 덮어쓰고 `comfyui/node-mapping.json` 의 노드 ID 갱신
4. SaveImage 노드 ID 가 바뀌었으면 Unity Inspector `Output Node Id` 도 변경
5. `tools/sync_comfyui.bat` 실행 → StreamingAssets 로 복사
6. `python tools/comfy_batch_test.py --seeds 1` 로 먼저 동작 확인

## 네트워크 (태블릿 ↔ GPU PC)

1. ComfyUI 를 `--listen 0.0.0.0` 으로 실행
2. Windows 방화벽 인바운드 TCP 8188 허용 (개인 네트워크)
3. PC IP 확인 (`ipconfig`) — 공유기에서 DHCP 예약으로 고정 권장
4. 태블릿 브라우저에서 `http://<PC IP>:8188` 열리는지 확인 → 앱 ‘설정’ 에 같은 주소 입력 → 연결 테스트 (`GET /system_stats`)
5. 안드로이드는 http 평문 통신을 막기 때문에 Unity 메뉴 **Silkroad → 안드로이드 태블릿 설정 적용** 이 필요 (http 허용)

## 후속 검토

- WebSocket `/ws?clientId=` 진행 이벤트 (`execution_success`, `execution_error`) 로 폴링 대체 — 다중 단말 운영 시
- 다중 단말/외부망: 중계 API (인증, 작업 소유권, 요청 제한) 추가
