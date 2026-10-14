using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 빌드할 때만 C:\keydata\comfy_key.py 의 키를 StreamingAssets/ComfyUI/comfy_key.txt 로 넣고,
    /// 빌드가 끝나면 지웁니다 → 키가 프로젝트/깃에는 남지 않고 apk 안에만 들어감.
    /// </summary>
    public class SilkroadKeyBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        static string Target => Path.Combine(Application.streamingAssetsPath, ComfyUIClient.BuildKeyFile);

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!File.Exists(ComfyUIClient.KeyFilePath))
            {
                Debug.LogWarning("[Silkroad] " + ComfyUIClient.KeyFilePath + " 가 없어 키 없이 빌드합니다 (더미/로컬 서버만 동작).");
                return;
            }
            string key = ComfyUIClient.ParseKey(File.ReadAllText(ComfyUIClient.KeyFilePath));
            if (string.IsNullOrEmpty(key)) { Debug.LogWarning("[Silkroad] comfy_key 변수를 찾지 못했습니다."); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.WriteAllText(Target, key);
            Debug.Log("[Silkroad] 빌드에 API 키 포함 (빌드 후 자동 삭제)");
        }

        public void OnPostprocessBuild(BuildReport report) => Cleanup();

        [InitializeOnLoadMethod]
        static void Cleanup() // 빌드 실패로 남은 경우도 에디터 재시작 시 정리
        {
            if (File.Exists(Target)) File.Delete(Target);
            if (File.Exists(Target + ".meta")) File.Delete(Target + ".meta");
        }
    }
}
