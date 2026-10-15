@echo off
chcp 65001 >nul
rem comfyui\presets\compare 의 프롬프트들을 같은 그림·같은 seed 로 생성해서 한 화면에 비교
rem 내 그림으로 비교: run_compare.bat --images C:\그림폴더
cd /d "%~dp0.."
if not exist .venv\Scripts\python.exe ( echo 먼저 tools\setup_venv.bat 실행 & pause & exit /b 1 )
.venv\Scripts\python.exe tools\compare_presets.py %*
pause
