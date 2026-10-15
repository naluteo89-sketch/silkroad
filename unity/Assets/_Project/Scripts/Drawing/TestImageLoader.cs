using System;
using System.IO;
using UnityEngine;

namespace Silkroad
{
    /// <summary>
    /// (테스트용) 그림판에 이미지를 불러와 바로 보내 볼 수 있게 함. 에디터 / Development Build 에서만 동작.
    ///   Ctrl+V : 클립보드의 이미지(캡처·복사한 그림) 또는 탐색기에서 복사한 이미지 파일 붙여넣기  (Windows 에디터)
    ///   Ctrl+O : 이미지 파일 선택 창 (에디터)
    /// 불러온 이미지는 비율을 유지해 도화지 가운데에 놓이고, 그 위에 이어서 그릴 수 있음 (취소로 되돌리기 가능).
    /// PaintCanvas 가 자동으로 붙임 → 따로 설정할 것 없음.
    /// </summary>
    public class TestImageLoader : MonoBehaviour
    {
        PaintCanvas _canvas;

        void Awake() => _canvas = GetComponent<PaintCanvas>();

        void Update()
        {
            if (!ComfyUIClient.TestSettingsEnabled || _canvas == null) return;
            bool ctrl, v, o;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            v = kb.vKey.wasPressedThisFrame;
            o = kb.oKey.wasPressedThisFrame;
#else
            ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            v = Input.GetKeyDown(KeyCode.V);
            o = Input.GetKeyDown(KeyCode.O);
#endif
            if (!ctrl) return;
            if (v) PasteClipboard();
            else if (o) OpenFile();
        }

        void PasteClipboard()
        {
#if UNITY_EDITOR_WIN
            string path = Path.Combine(Application.temporaryCachePath, "silkroad_clipboard.img");
            if (File.Exists(path)) File.Delete(path);
            string p = path.Replace("'", "''");
            string ps =
                "Add-Type -AssemblyName System.Windows.Forms; Add-Type -AssemblyName System.Drawing; " +
                "$i = [System.Windows.Forms.Clipboard]::GetImage(); " +
                "if ($i -ne $null) { $i.Save('" + p + "', [System.Drawing.Imaging.ImageFormat]::Png); exit 0 } " +
                "if ([System.Windows.Forms.Clipboard]::ContainsFileDropList()) { " +
                "  $f = [System.Windows.Forms.Clipboard]::GetFileDropList() | Where-Object { $_ -match '\\.(png|jpe?g)$' } | Select-Object -First 1; " +
                "  if ($f) { Copy-Item -LiteralPath $f -Destination '" + p + "'; exit 0 } } " +
                "exit 1";
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                    "-NoProfile -STA -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"")
                { CreateNoWindow = true, UseShellExecute = false };
                using (var proc = System.Diagnostics.Process.Start(psi))
                    proc.WaitForExit(8000);
            }
            catch (Exception e) { Toast("붙여넣기 실패: " + e.Message); return; }
            if (!File.Exists(path)) { Toast("클립보드에 이미지가 없어요"); return; }
            LoadFile(path, "붙여넣기");
#else
            Toast("붙여넣기는 Windows 에디터에서만 돼요");
#endif
        }

        void OpenFile()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("테스트 그림 불러오기", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path)) LoadFile(path, Path.GetFileName(path));
#endif
        }

        void LoadFile(string path, string label)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) { Toast("이미지를 읽지 못했어요"); return; }
                _canvas.LoadImage(tex);
                Toast("불러옴: " + label + " (취소로 되돌리기)");
            }
            catch (Exception e) { Toast("불러오기 실패: " + e.Message); }
            finally { Destroy(tex); }
        }

        void Toast(string msg)
        {
            var tb = FindAnyObjectByType<PaintToolbar>();
            if (tb) tb.Toast(msg);
            Debug.Log("[Silkroad 테스트] " + msg);
        }
    }
}
