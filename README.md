# silkroad
생성형 AI 기반 실크로드 문화 XR 융합콘텐츠 제작

**담당 기능:** 사용자 드로잉 이미지 변환 — Unity 그림판에서 그린 그림을 ComfyUI 로 실크로드 회화 풍 이미지로 바꾸고, 원본과 비교·재생성·수정하는 체험.

| 폴더 | 내용 |
|---|---|
| `unity/` | Unity 6.3 프로젝트 (Hub → Add project from disk 로 이 폴더를 엶) |
| `shared/` | Unity ↔ ComfyUI 입출력 계약, 테스트용 드로잉 |
| `docs/` | 기획(plan), API 연동, 검수 체크리스트, 개발 환경 설정 가이드 |
| `tools/` | UI 아이콘 생성(`make_ui_icons.py`) |

## 빠른 시작

1. `docs/setup-guide.md` 대로 Unity 6.3 LTS(+Android Build Support) 설치 후 `unity/` 열기
2. 메뉴 **Silkroad → 씬 자동 생성** → ▶ 실행 (기본은 더미 모드: 서버 없이 흐름 확인)
3. ComfyUI 워크플로는 정리됨 (2026-10-15) → 새 워크플로를 `unity/Assets/StreamingAssets/ComfyUI/` 에 넣기
4. 앱 ‘설정’에서 서버 주소 입력, 더미 모드 해제

모델 파일, ComfyUI 실행 환경, Unity `Library/Temp/Builds`, 생성 결과는 저장소에 넣지 않습니다 (`.gitignore`).
