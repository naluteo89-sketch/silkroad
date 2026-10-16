# Silkroad — 실크로드 그림 공방

생성형 AI 기반 실크로드 문화 XR 융합콘텐츠 제작 프로젝트입니다. Unity 그림판에서 그린 그림을 ComfyUI로 실크로드 회화풍 이미지로 변환하고, 원본과 비교·재생성·수정·저장하는 체험을 제공합니다.

## 제작물 정리

| 제작물 | 구현 내용 | 위치 |
|---|---|---|
| Unity 체험 앱 | 시작 → 그림 그리기 → 생성 대기 → 결과 비교·저장 흐름 | `unity/Assets/_Project/Scenes/Main.unity`, `Scripts/Session/` |
| 드로잉 도구 | 브러시, 지우개, 채우기, 색상·굵기 선택, 실행 취소·다시 실행, 초기화 | `unity/Assets/_Project/Scripts/Drawing/`, `Scripts/UI/PaintToolbar.cs` |
| AI 이미지 변환 | 로컬 ComfyUI 및 Comfy Cloud 연동, 더미 모드, 재생성, 오류 처리 | `unity/Assets/_Project/Scripts/Generation/` |
| 결과 화면 | 원본·AI 비교 슬라이더, 액자 합성, 두루마리 연출, 저장 효과 | `unity/Assets/_Project/Scripts/UI/` |
| 결과 저장·QR 공유 | 원본·AI 이미지와 메타데이터 저장, 같은 Wi-Fi에서 이미지 다운로드 | `unity/Assets/_Project/Scripts/Session/`, `Scripts/Share/` |
| XR 전달 인터페이스 | 결과 확정 시 텍스처 전달 이벤트 제공. 실제 전시 장치 연결은 추가 구현 필요 | `unity/Assets/_Project/Scripts/XR/XRResultBridge.cs` |
| 아트 리소스 | 액자, 두루마리, 그림판 아이콘, 한글 폰트, 종이 배경 | `unity/Assets/_Project/Art/`, `Resources/` |
| 디자인 미리보기 | 화면·액자 JPG, 아이콘 PNG, 두루마리 GIF | `Claude outputs/` |
| AI 워크플로·프리셋 | 기본·SDXL·FLUX2·FLUX2 Klein 워크플로, 스타일 비교 프리셋, 프롬프트·노드 매핑 | `comfyui/` |
| 테스트 자료·도구 | 인물·말 드로잉 2종, 파이프라인 점검, 프리셋 비교, 배치·SDK 테스트 | `shared/test-drawings/`, `tools/` |
| 기획·운영 문서 | 기획, API 계약, 환경 설정, 태블릿 빌드·검수 안내 | `docs/`, `shared/contracts/` |

위 목록은 파일과 코드 기준입니다. 실기기 동작 및 실제 서버 생성 성공 여부는 별도 검수가 필요합니다.

## Unity 실행

1. Unity Hub에서 **Unity 6.3 LTS / 6000.3.26f1**을 설치하고 `unity/`를 엽니다. Android 빌드는 Android Build Support가 필요합니다.
2. `Assets/_Project/Scenes/Main.unity`를 엽니다. 씬을 새로 구성할 때는 **Silkroad → 씬 자동 생성 (Build Scene)** 메뉴를 사용합니다.
3. 워크플로·프리셋을 변경했다면 `tools/sync_comfyui.bat`을 실행해 `comfyui/` 원본을 `Assets/StreamingAssets/ComfyUI/`로 복사합니다.
4. 씬의 `ComfyUIClient` 컴포넌트 Inspector에서 `Use Dummy`, `Server Url`, 워크플로·프리셋 경로를 설정합니다. 현재 코드는 앱 내 설정 화면을 숨기므로 Inspector에서 설정합니다.
5. 서버 없이 흐름을 확인할 때는 `Use Dummy`를 켭니다. 실제 생성 시에는 끄고 서버·워크플로·인증 구성을 확인한 뒤 실행합니다.

### ComfyUI 키 설정 및 노출 확인

- 코드에서 사용하는 이름은 `comfy_key`입니다. 확인 시 Unity의 `Assets`, `Packages`, `ProjectSettings`에 `compykey`라는 표기는 없었습니다.
- 에디터는 `C:\keydata\comfy_key.py`의 `comfy_key` 값을 읽습니다. Inspector의 `apiKey`에 직접 입력하면 씬에 저장될 수 있습니다.
- `SilkroadKeyBuildStep.cs`는 빌드 직전에 키를 `StreamingAssets/ComfyUI/comfy_key.txt`로 복사하고 빌드 후 프로젝트에서 삭제합니다. **빌드된 APK 내부에는 키 파일이 남습니다.**
- 2026-10-16 확인: 로컬 `unity/test_fix2.apk`, `unity/test_fix3.apk` 모두 `assets/ComfyUI/comfy_key.txt`를 포함합니다. APK를 전달하면 내부 키를 추출할 수 있습니다.
- 현재 Git 추적 파일에서는 해당 APK 키 값이 발견되지 않았고, 작업 폴더의 임시 키 파일도 없었습니다. APK와 키 파일은 `.gitignore` 대상입니다. 전체 Git 이력의 키 값 유출 여부까지 검증한 것은 아닙니다.
- 키 값은 README에 기록하지 않습니다. 외부 배포용 앱에서는 키를 서버에서 관리하도록 인증 구조를 변경해야 합니다. 해당 APK가 외부에 공유되었다면 포함된 키를 폐기·재발급하는 것이 필요합니다.

## 결과물 저장 및 공유

결과는 `Application.persistentDataPath/Results`에 저장됩니다.

- `*_mine.png`: 사용자 원본 그림
- `*_ai.png`: AI 변환 결과
- `*_meta.json`: 세션, 시드, 워크플로, 생성 시간 등의 메타데이터
- `*_framed.png`: QR 공유 과정에서 액자 합성에 성공하면 저장되는 이미지

QR 공유는 앱 내부 HTTP 서버를 사용합니다. 앱이 실행 중이고 휴대폰과 체험 기기가 같은 Wi-Fi에서 서로 접근할 수 있어야 합니다.

## 문서 및 도구

- [기획](docs/plan.md) · [빠른 안내](docs/quick-guide.md) · [환경 설정](docs/setup-guide.md)
- [API 연동](docs/api-integration.md) · [입출력 계약](shared/contracts/generation-io.md)
- [검수 체크리스트](docs/acceptance-checklist.md)
- [태블릿 빌드](docs/tablet-build-guide.md) · [태블릿 배포 검수](docs/tablet-release-checklist.md)
- `tools/sync_comfyui.bat`: Unity용 워크플로·프리셋 동기화
- `tools/check_comfyui.py`, `tools/run_pipeline_check.bat`: 연결·파이프라인 점검
- `tools/compare_presets.py`, `tools/run_quality_sweep.bat`: 스타일·품질 비교
- `tools/make_ui_icons.py`: UI 아이콘 생성

기존 상세 가이드는 작성 시점에 따라 현재 코드와 다를 수 있습니다. 실행 설정과 키 처리 방식은 위 Unity 안내 및 현재 코드를 기준으로 확인합니다.

모델 파일, ComfyUI 실행 환경, Unity 캐시·빌드, 생성 결과 및 API 키는 저장소 관리 대상에서 제외합니다.
