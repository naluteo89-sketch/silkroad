@echo off
chcp 65001 >nul
rem 가상환경으로 comfy-sdk 테스트 실행. 추가 옵션 예: run_sdk_test.bat --seeds 3
cd /d "%~dp0.."
if not exist .venv\Scripts\python.exe (
    echo 가상환경이 없어요. 먼저 tools\setup_venv.bat 을 실행하세요.
    pause
    exit /b 1
)
.venv\Scripts\python.exe tools\comfy_sdk_test.py %*
pause
