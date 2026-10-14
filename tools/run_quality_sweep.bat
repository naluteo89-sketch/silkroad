@echo off
chcp 65001 >nul
rem 변환 강도(denoise) 3단계 비교: 0.75 / 0.85 / 0.95  (같은 seed 라 차이만 비교 가능)
cd /d "%~dp0.."
if not exist .venv\Scripts\python.exe ( echo 먼저 tools\setup_venv.bat 실행 & pause & exit /b 1 )
for %%D in (0.75 0.85 0.95) do (
  echo.
  echo ===== denoise=%%D =====
  .venv\Scripts\python.exe tools\comfy_sdk_test.py --set denoise=%%D
)
echo.
echo 결과: tools\out\ 폴더의 최근 3개 *_sdk 폴더를 비교하세요 (settings.json 에 denoise 값 기록됨)
pause
