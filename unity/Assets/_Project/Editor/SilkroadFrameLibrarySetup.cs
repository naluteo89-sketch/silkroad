using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 랜덤 액자: Art/Frames/ 의 PNG 들을 FrameLibrary 에 등록하고,
    /// 각 액자의 가운데 투명 창 위치를 자동으로 찾습니다.
    /// 폴더에 PNG 를 넣거나 지우면 목록이 자동 갱신됩니다.
    /// </summary>
    public class SilkroadFrameLibrarySetup : AssetPostprocessor
    {
        const string FramesDir = "Assets/_Project/Art/Frames";
        const string LibraryPath = "Assets/_Project/Configs/FrameLibrary.asset";
        const byte WindowAlpha = 200; // 이 값보다 투명하면 '창' 으로 봄

        [MenuItem("Silkroad/액자 목록 갱신 (랜덤 액자)", priority = 13)]
        public static void SetupMenu()
        {
            var lib = Rebuild(out string report);

            // 열린 씬의 결과 화면에 연결 (한 번만 하면 됨)
            var rv = Object.FindFirstObjectByType<Silkroad.ResultView>(FindObjectsInactive.Include);
            string sceneMsg;
            if (rv == null) sceneMsg = "⚠ 씬에서 ResultView 를 찾지 못했어요. Main 씬을 열고 다시 실행하세요.";
            else
            {
                var frameTr = rv.aiLayer ? rv.aiLayer.Find("FrameArt") : null;
                if (frameTr == null) sceneMsg = "⚠ AILayer/FrameArt 가 없어요. 먼저 'Silkroad → 액자 이미지 적용' 을 실행하세요.";
                else
                {
                    Undo.RecordObject(rv, "Frame Library");
                    rv.frameLibrary = lib;
                    rv.frameArt = frameTr.GetComponent<Image>();
                    EditorUtility.SetDirty(rv);
                    EditorSceneManager.MarkSceneDirty(rv.gameObject.scene);
                    EditorSceneManager.SaveScene(rv.gameObject.scene);
                    sceneMsg = "결과 화면에 연결 완료 (씬 저장됨)";
                }
            }
            EditorUtility.DisplayDialog("Silkroad 랜덤 액자", report + "\n\n" + sceneMsg +
                "\n\n앞으로는 Art/Frames 폴더에 PNG 를 넣기만 하면 자동으로 목록에 추가돼요.", "확인");
            Selection.activeObject = lib;
        }

        // 폴더에 액자를 넣거나 지우면 자동 갱신
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith(FramesDir + "/") && IsImage(p));
            if (!touched) return;
            EditorApplication.delayCall += () => { Rebuild(out string r); Debug.Log("[Silkroad] 액자 목록 자동 갱신\n" + r); };
        }

        static bool IsImage(string p)
        {
            var e = Path.GetExtension(p).ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg";
        }

        public static FrameLibrary Rebuild(out string report)
        {
            var lib = AssetDatabase.LoadAssetAtPath<FrameLibrary>(LibraryPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<FrameLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            // 이전에 '사용 안 함' 으로 끈 액자는 그대로 유지
            var disabled = new HashSet<string>(lib.frames.Where(f => f != null && !f.enabled).Select(f => f.name));
            var lines = new List<string>();
            var list = new List<FrameLibrary.Entry>();

            if (Directory.Exists(FramesDir))
            {
                foreach (var path in Directory.GetFiles(FramesDir).Where(IsImage).OrderBy(p => p))
                {
                    string assetPath = path.Replace('\\', '/');
                    string name = Path.GetFileNameWithoutExtension(assetPath);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null) { lines.Add("✕ " + name + " : 스프라이트로 가져오지 못함 ('아트 이미지 다시 가져오기' 후 재시도)"); continue; }

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!tex.LoadImage(File.ReadAllBytes(assetPath))) { lines.Add("✕ " + name + " : 이미지를 읽지 못함"); continue; }
                        if (!FindWindow(tex, out RectInt win, out string why)) { lines.Add("✕ " + name + " : " + why); continue; }
                        list.Add(new FrameLibrary.Entry
                        {
                            name = name, sprite = sprite,
                            size = new Vector2Int(tex.width, tex.height),
                            window = win, enabled = !disabled.Contains(name),
                        });
                        lines.Add($"✓ {name} ({tex.width}×{tex.height}) 창 {win.width}×{win.height}");
                    }
                    finally { Object.DestroyImmediate(tex); }
                }
            }

            lib.frames = list;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            report = $"액자 {list.Count}개 등록 ({FramesDir})\n" + string.Join("\n", lines);
            return lib;
        }

        /// <summary>이미지 가운데에서 시작해 이어진 투명 영역(창)의 범위를 찾음.</summary>
        static bool FindWindow(Texture2D tex, out RectInt win, out string why)
        {
            win = default; why = null;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32(); // 왼쪽 아래부터
            int start = (h / 2) * w + (w / 2);
            if (px[start].a >= WindowAlpha) { why = "가운데가 투명하지 않음 (액자 안쪽을 투명하게 뚫은 PNG 필요)"; return false; }

            var seen = new bool[px.Length];
            var queue = new int[px.Length];
            int head = 0, tail = 0;
            queue[tail++] = start; seen[start] = true;
            int minX = w, maxX = 0, minY = h, maxY = 0;
            while (head < tail)
            {
                int i = queue[head++];
                int x = i % w, y = i / w;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (x > 0) Visit(i - 1); if (x < w - 1) Visit(i + 1);
                if (y > 0) Visit(i - w); if (y < h - 1) Visit(i + w);
            }
            void Visit(int j) { if (!seen[j] && px[j].a < WindowAlpha) { seen[j] = true; queue[tail++] = j; } }

            if (minX == 0 || minY == 0 || maxX == w - 1 || maxY == h - 1)
            { why = "가운데 투명 영역이 바깥까지 이어짐 (액자 테두리가 끊겨 있는지 확인)"; return false; }
            win = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }
    }
}
