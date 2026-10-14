@echo off
chcp 65001 >nul
rem C:\silkroad\.venv 가상환경을 만들고 comfy-sdk 를 설치합니다. (처음 한 번만)
cd /d "%~dp0.."

where uv >nul 2>nul
if %errorlevel%==0 (
    echo [uv] 가상환경 생성...
    if not exist .venv uv venv .venv --python 3.12
    echo [uv] comfy-sdk 설치...
    uv pip install --python .venv\Scripts\python.exe comfy-sdk
) else (
    echo [python] 가상환경 생성...
    if not exist .venv python -m venv .venv
    .venv\Scripts\python.exe -m ensurepip --upgrade
    .venv\Scripts\python.exe -m pip install --upgrade pip comfy-sdk
)

echo.
.venv\Scripts\python.exe -c "import comfy_sdk; print('[OK] comfy-sdk 설치 완료')"
pause
