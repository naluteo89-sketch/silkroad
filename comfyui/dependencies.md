# ComfyUI 실행 환경 · 의존성

> 모델 파일과 ComfyUI 실행 환경은 저장소에 넣지 않습니다. 이 문서에 이름·위치·버전을 기록하고, 같은 절차로 설치합니다.

## 0. Comfy Cloud 사용 시

- 설치 불필요. comfy.org 구독(Standard 이상) + API 키만 필요
- 모델은 Comfy Cloud 가 제공하는 목록 중에서 선택 → `tools/check_comfyui.py --server https://cloud.comfy.org` 로 이름 확인
- 확정한 모델 이름·구독 플랜·키 관리 담당자를 아래 표에 기록

| 항목 | 값 |
|---|---|
| 구독 플랜 | Pro |
| checkpoint 이름 | sd_xl_base_1.0.safetensors |
| ControlNet 이름 | Hishambarakat__controlnet__controlnet-canny-sdxl-1.0.fp16.safetensors |
| 키 발급/관리 담당 | |

## 1. ComfyUI (직접 설치 시)

| 항목 | 값 | 비고 |
|---|---|---|
| 설치 형태 | Windows Portable (권장) 또는 Desktop | |
| 버전 | _착수 시 기록_ (예: v0.3.x, 커밋 해시) | 업데이트 시 이 표 갱신 |
| 실행 옵션 | `--listen 0.0.0.0 --port 8188` | 태블릿/다른 PC 에서 접속하려면 필수 |
| GPU | _착수 시 기록_ (모델명, VRAM) | SDXL + ControlNet 은 VRAM 8GB 이상 권장 |

Portable: `run_nvidia_gpu.bat` 마지막 줄 끝에 ` --listen 0.0.0.0` 추가.
Desktop: Settings → Server-Config → Host `0.0.0.0`.

## 2. 모델 (워크플로 `drawing_to_silkroad.api.json` 기준)

| 노드 | 파일 이름 (워크플로에 적힌 이름) | 넣을 폴더 | 출처 |
|---|---|---|---|
| 1 CheckpointLoaderSimple | `sd_xl_base_1.0.safetensors` | `ComfyUI/models/checkpoints/` | stabilityai/stable-diffusion-xl-base-1.0 (Hugging Face) |
| 7 ControlNetLoader | `controlnet-canny-sdxl-1.0.safetensors` | `ComfyUI/models/controlnet/` | SDXL 용 Canny ControlNet (예: diffusers/controlnet-canny-sdxl-1.0 의 safetensors 를 이 이름으로 저장) |

- 파일 이름이 다르면 워크플로 JSON 의 `ckpt_name`, `control_net_name` 을 실제 이름으로 고칩니다.
- checkpoint 와 ControlNet 은 **같은 계열(SDXL)** 이어야 합니다. 이름만 보고 섞지 않습니다.
- 각 모델의 라이선스·상업적 이용 조건을 확인하고 아래에 기록합니다.

| 모델 | 라이선스 | 확인일 | 확인자 |
|---|---|---|---|
| | | | |

## 3. 커스텀 노드

현재 워크플로는 **기본 노드만 사용**합니다 (`Canny`, `ImageScale`, `ControlNetApplyAdvanced` 모두 기본 제공).
Lineart/Scribble 전처리기 등 커스텀 노드를 추가하면 아래에 저장소 주소와 커밋을 고정해 기록합니다.

| 노드 패키지 | 저장소 | 커밋/버전 | 추가 이유 |
|---|---|---|---|
| | | | |

## 4. 후보 비교 기록 (1단계)

| 날짜 | 변경한 변수 1개 | 값 | 결과 폴더 (tools/out/…) | 메모 |
|---|---|---|---|---|
| | denoise | 0.75 → 0.6 | | |
