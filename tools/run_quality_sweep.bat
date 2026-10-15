@echo off
chcp 65001 >nul
rem 생성 단계 수(steps) 3단계 비교: 20 / 30 / 45  (같은 seed 라 차이만 비교 가능)
rem 팔다리 이상·뭉개짐이 steps 를 올리면 줄어드는지 확인용. 시간도 results.csv 에 기록됨
cd /d "%~dp0.."
if not exist .venv\Scripts\python.exe ( echo 먼저 tools\setup_venv.bat 실행 & pause & exit /b 1 )
for %%S in (20 30 45) do (
  echo.
  echo ===== steps=%%S =====
  .venv\Scripts\python.exe tools\comfy_sdk_test.py --set steps=%%S
)
echo.
echo 결과: tools\out\ 폴더의 최근 3개 *_sdk 폴더를 비교하세요 (settings.json 에 steps 값 기록됨)
pause
