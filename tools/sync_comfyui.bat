@echo off
rem comfyui/ 의 워크플로와 프리셋을 Unity StreamingAssets 로 복사합니다.
rem 워크플로나 프리셋을 고친 뒤 Unity 에서 실행/빌드하기 전에 더블클릭하세요.
set ROOT=%~dp0..
set DEST=%ROOT%\unity\Assets\StreamingAssets\ComfyUI
if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%ROOT%\comfyui\workflows\drawing_to_silkroad.api.json" "%DEST%\" >nul
copy /Y "%ROOT%\comfyui\presets\silkroad_style.json" "%DEST%\" >nul
echo 복사 완료: %DEST%
pause
