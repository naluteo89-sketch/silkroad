@echo off
chcp 65001 >nul
rem 모델 없이 업로드 -> 실행 -> 다운로드 전체 흐름만 확인 (그림 색 반전 결과가 나오면 성공)
cd /d "%~dp0.."
if not exist .venv\Scripts\python.exe (
    echo 가상환경이 없어요. 먼저 tools\setup_venv.bat 을 실행하세요.
    pause
    exit /b 1
)
.venv\Scripts\python.exe tools\comfy_sdk_test.py --workflow comfyui\workflows\pipeline_check.api.json %*
pause
