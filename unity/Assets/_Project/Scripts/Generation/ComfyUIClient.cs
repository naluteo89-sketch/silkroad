using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Silkroad
{
    /// <summary>
    /// ComfyUI 와 통신하는 클라이언트.
    ///  - useDummy = true  : 서버 없이 가짜 결과(세피아 필터)를 돌려줌 → 지금 테스트용
    ///  - useDummy = false : 실제 ComfyUI 서버에 업로드 → 실행 → 결과 다운로드
    ///
    /// 원본 워크플로/프리셋은 저장소의 comfyui/ 폴더에서 관리하고,
    /// tools/sync_comfyui.bat 으로 StreamingAssets/ComfyUI/ 에 복사해서 사용합니다.
    /// 자리표시자 목록은 comfyui/node-mapping.json 참고.
    /// </summary>
    /// <summary>생성 작업 1건의 추적 정보 (결과 메타데이터로 저장).</summary>
    [Serializable]
    public class GenerationInfo
    {
        public string promptId;
        public long seed;
        public string theme;
        public string preset;
        public string workflow;
    }

    public partial class ComfyUIClient : MonoBehaviour
    {
        [Header("모드")]
        [Tooltip("체크하면 서버 없이 가짜 결과를 냅니다. 실제 연결 시 체크 해제.")]
        public bool useDummy = true;
        public float dummyDelaySeconds = 2.5f;

        [Header("서버")]
        public string serverUrl = "https://cloud.comfy.org";
        [Tooltip("비워 두면 C:\\keydata\\comfy_key.py 의 comfy_key 를 자동 사용 (권장). 여기 직접 쓰면 씬 파일에 저장되니 주의.")]
        public string apiKey = "";
        [Tooltip("Auto: 주소가 comfy.org / comfy.app 이면 Cloud API(v2), 아니면 로컬 ComfyUI API")]
        public ServerType serverType = ServerType.Auto;

        public enum ServerType { Auto, LocalComfyUI, ComfyCloudV2 }

        [Tooltip("자동 표시용 (수정 불필요). Api Key 칸을 비워 두면 C:\\keydata\\comfy_key.py 에서 읽습니다.")]
        public string keyStatus = "";

#if UNITY_EDITOR
        // Inspector 에서 이 오브젝트를 볼 때마다 키를 찾을 수 있는지 표시 (키 자체는 저장하지 않음)
        void OnValidate()
        {
            string k = apiKey;
            string from = "Inspector 직접 입력";
            if (string.IsNullOrEmpty(k))
            {
                from = KeyFilePath;
                try { k = System.IO.File.Exists(KeyFilePath) ? ParseKey(System.IO.File.ReadAllText(KeyFilePath)) : null; }
                catch { k = null; }
            }
            if (k == null) keyStatus = "❌ 파일 없음: " + KeyFilePath;
            else if (k.Length == 0) keyStatus = "❌ 파일은 있으나 comfy_key = \"...\" 를 못 찾음";
            else keyStatus = "✅ 키 찾음 (" + k.Length + "자, 끝 " + k.Substring(Math.Max(0, k.Length - 4)) + ") — " + from;
        }

        [ContextMenu("API 키 다시 확인")]
        void RecheckKey() { OnValidate(); Debug.Log("[ComfyCloud] " + keyStatus); }
#endif

        public bool IsCloud(string url = null)
        {
            if (serverType == ServerType.ComfyCloudV2) return true;
            if (serverType == ServerType.LocalComfyUI) return false;
            string u = (url ?? serverUrl).ToLowerInvariant();
            return u.Contains("comfy.org") || u.Contains("comfy.app");
        }
        [Tooltip("StreamingAssets 기준 경로")]
        public string workflowPath = "ComfyUI/drawing_to_silkroad.api.json";
        [Tooltip("StreamingAssets 기준 경로. 파일이 없으면 아래 Fallback Preset 사용")]
        public string presetPath = "ComfyUI/silkroad_style.json";
        [Tooltip("결과를 가져올 SaveImage 노드 ID (comfyui/node-mapping.json 의 output.node)")]
        public string outputNodeId = "9";

        [Header("프리셋 파일이 없을 때 쓰는 기본값")]
        public GenerationPreset fallbackPreset = new GenerationPreset();

        [Header("타임아웃")]
        public float timeoutSeconds = 120f;
        public float pollIntervalSeconds = 1f;

        Coroutine _running;

        const string PrefUrl = "comfy_url";
        const string PrefDummy = "comfy_dummy";

        /// <summary>
        /// 앱 안 Comfy 설정 화면(테스트용) 사용 여부: 에디터 또는 Development Build 에서만.
        /// 정식 빌드에서는 설정 버튼이 사라지고, 기기에 저장된 테스트 값도 무시 → Inspector 값만 사용.
        /// </summary>
        public static bool TestSettingsEnabled => Application.isEditor || Debug.isDebugBuild;

        void Awake()
        {
            // 앱 안 설정 화면은 없앰 → Inspector 값만 사용. 예전에 기기/에디터에 저장된 테스트 값은 지움.
            if (PlayerPrefs.HasKey(PrefUrl) || PlayerPrefs.HasKey(PrefDummy))
            {
                PlayerPrefs.DeleteKey(PrefUrl);
                PlayerPrefs.DeleteKey(PrefDummy);
                PlayerPrefs.Save();
            }
            // 설정 창이 처음부터 꺼져 있어도 메인 화면의 '설정' 버튼은 숨김
            var sv = FindAnyObjectByType<SettingsView>(FindObjectsInactive.Include);
            if (sv != null)
            {
                if (sv.openButton) sv.openButton.gameObject.SetActive(false);
                sv.gameObject.SetActive(false);
            }
        }

        /// <summary>설정 화면에서 호출. 기기에 저장되어 다음 실행에도 유지.</summary>
        public void SaveSettings(string url, bool dummy)
        {
            serverUrl = string.IsNullOrWhiteSpace(url) ? serverUrl : url.Trim();
            useDummy = dummy;
            PlayerPrefs.SetString(PrefUrl, serverUrl);
            PlayerPrefs.SetInt(PrefDummy, dummy ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>서버에 연결되는지 확인 (GET /system_stats).</summary>
        public void TestConnection(string url, Action<bool, string> onResult)
        {
            StartCoroutine(TestRoutine(url, onResult));
        }

        IEnumerator TestRoutine(string url, Action<bool, string> onResult)
        {
            bool cloud = IsCloud(url);
            if (cloud && string.IsNullOrEmpty(apiKey)) yield return LoadKey();
            // Cloud: 목록 API 는 501 이라, 없는 job 을 조회 → 404 면 인증 통과 / 401 이면 키 문제
            string path = cloud ? "/api/v2/jobs/00000000-0000-0000-0000-000000000000" : "/system_stats";
            using (var req = UnityWebRequest.Get(url.Trim().TrimEnd('/') + path))
            {
                req.timeout = 8;
                if (cloud) CloudAuth(req); else AddAuth(req);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success ||
                    (cloud && req.responseCode > 0 && req.responseCode != 401 && req.responseCode != 403 && req.responseCode < 500) ||
                    (cloud && req.responseCode == 501)) // 서버가 응답했고 인증 거부가 아니면 OK
                    onResult?.Invoke(true, cloud ? "Comfy Cloud 연결 성공! (인증 OK)" : "연결 성공!");
                else if (req.responseCode == 401) onResult?.Invoke(false, "API 키가 틀렸어요 (401)");
                else if (req.responseCode == 402 || req.responseCode == 429) onResult?.Invoke(false, "구독/사용량 문제 (" + req.responseCode + ")");
                else onResult?.Invoke(false, "연결 실패: " + req.error);
            }
        }

        /// <summary>
        /// 생성 요청. onStatus 로 "업로드 중 / 대기 중 / 생성 중 / 결과 받는 중" 단계를 알려줍니다.
        /// </summary>
        public void Generate(byte[] png, Action<string> onStatus, Action<Texture2D, GenerationInfo> onDone, Action<string> onError)
            => Generate(png, 0, onStatus, onDone, onError);

        /// <param name="variation">같은 원본의 재요청 횟수 (새 그림 = 0). 프리셋 seed 에 더해짐.</param>
        public void Generate(byte[] png, int variation, Action<string> onStatus, Action<Texture2D, GenerationInfo> onDone, Action<string> onError)
        {
            Cancel();
            _variation = variation;
            _running = StartCoroutine(useDummy ? DummyRoutine(png, onStatus, onDone)
                : IsCloud() ? CloudRoutine(png, onStatus, onDone, onError)
                : RealRoutine(png, onStatus, onDone, onError));
        }

        public void Cancel()
        {
            if (_running != null) StopCoroutine(_running);
            _running = null;
            if (_activeRequest != null)
            {
                try { _activeRequest.Abort(); _activeRequest.Dispose(); } catch { }
                _activeRequest = null;
            }
        }

        UnityWebRequest _activeRequest;
        int _variation;
        int _lastTheme = -1;

        /// <summary>새 그림(variation 0) = 기본 테마(빈칸) / 재요청 = 테마 중 랜덤 1개 (직전과 다르게).</summary>
        string PickTheme(GenerationPreset p, out string name)
        {
            name = "기본";
            if (_variation <= 0 || p == null || p.themes == null) return "";
            var pool = new System.Collections.Generic.List<int>();
            for (int i = 0; i < p.themes.Length; i++)
                if (p.themes[i] != null && !string.IsNullOrWhiteSpace(p.themes[i].prompt)) pool.Add(i);
            if (pool.Count == 0) return "";
            if (pool.Count > 1) pool.Remove(_lastTheme);
            int pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            _lastTheme = pick;
            name = string.IsNullOrEmpty(p.themes[pick].name) ? "테마" + (pick + 1) : p.themes[pick].name;
            return p.themes[pick].prompt;
        }

        /// <summary>프리셋 seed 가 있으면 seed + 재요청 횟수, 없으면 랜덤.</summary>
        long PickSeed(GenerationPreset p) => p != null && p.seed > 0 ? p.seed + _variation : UnityEngine.Random.Range(1, int.MaxValue);

        // ───────────── 더미 모드 ─────────────
        IEnumerator DummyRoutine(byte[] png, Action<string> onStatus, Action<Texture2D, GenerationInfo> onDone)
        {
            var info = new GenerationInfo { promptId = "dummy-" + Guid.NewGuid().ToString("N").Substring(0, 8), seed = UnityEngine.Random.Range(1, int.MaxValue), preset = "dummy", workflow = "dummy" };
            onStatus?.Invoke("업로드 중");
            yield return new WaitForSeconds(dummyDelaySeconds * 0.2f);
            onStatus?.Invoke("생성 중");
            yield return new WaitForSeconds(dummyDelaySeconds * 0.8f);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(png);
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var rnd = new System.Random();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color32 c = px[i];
                // 세피아 + 포스터라이즈 + 비네팅 + 종이 노이즈 → "변환된 느낌"만 냄
                float r = c.r * 0.393f + c.g * 0.769f + c.b * 0.189f;
                float g = c.r * 0.349f + c.g * 0.686f + c.b * 0.168f;
                float b = c.r * 0.272f + c.g * 0.534f + c.b * 0.131f;
                r = Mathf.Lerp(c.r, r, 0.6f); g = Mathf.Lerp(c.g, g, 0.6f); b = Mathf.Lerp(c.b, b, 0.6f);
                r = Mathf.Round(r / 32f) * 32f; g = Mathf.Round(g / 32f) * 32f; b = Mathf.Round(b / 32f) * 32f;
                float dx = (x / (float)w) - 0.5f, dy = (y / (float)h) - 0.5f;
                float vig = 1f - Mathf.Clamp01((dx * dx + dy * dy) * 1.4f);
                float n = (float)rnd.NextDouble() * 18f - 9f;
                px[i] = new Color32(
                    (byte)Mathf.Clamp(r * vig + n, 0, 255),
                    (byte)Mathf.Clamp(g * vig + n, 0, 255),
                    (byte)Mathf.Clamp(b * vig + n, 0, 255), 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            _running = null;
            onDone?.Invoke(tex, info);
        }

        // ───────────── 실제 ComfyUI ─────────────
        IEnumerator RealRoutine(byte[] png, Action<string> onStatus, Action<Texture2D, GenerationInfo> onDone, Action<string> onError)
        {
            string baseUrl = serverUrl.TrimEnd('/');
            string fileName = "unity_paint_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

            var info = new GenerationInfo { workflow = workflowPath };

            // 1) 그림 업로드
            onStatus?.Invoke("업로드 중");
            var form = new WWWForm();
            form.AddBinaryData("image", png, fileName, "image/png");
            form.AddField("overwrite", "true");
            string uploadedName;
            using (var req = UnityWebRequest.Post(baseUrl + "/upload/image", form))
            {
                AddAuth(req);
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.result != UnityWebRequest.Result.Success)
                { Fail(onError, "업로드 실패: " + req.error + "\n" + req.downloadHandler.text); yield break; }
                uploadedName = Extract(req.downloadHandler.text, "\"name\"\\s*:\\s*\"([^\"]+)\"") ?? fileName;
            }

            // 2) 워크플로 + 프리셋 읽기 (안드로이드는 StreamingAssets 가 apk 안이라 UnityWebRequest 사용)
            string wfText = null, presetText = null, readError = null;
            yield return ReadStreaming(workflowPath, t => wfText = t, e => readError = e);
            if (wfText == null) { Fail(onError, "워크플로 파일을 읽지 못했습니다: " + readError); yield break; }
            yield return ReadStreaming(presetPath, t => presetText = t, e => { });

            var preset = fallbackPreset;
            if (!string.IsNullOrEmpty(presetText))
            {
                try { preset = JsonUtility.FromJson<GenerationPreset>(presetText); }
                catch (Exception e) { Debug.LogWarning("[ComfyUI] 프리셋 파싱 실패, 기본값 사용: " + e.Message); }
            }

            string themeText = PickTheme(preset, out info.theme);
            string wf = preset.Apply(wfText, Escape, themeText)
                .Replace("{{INPUT_IMAGE}}", Escape(uploadedName))
                .Replace("{{SEED}}", (info.seed = PickSeed(preset)).ToString());
            info.preset = preset.name;
            string outNode = preset.OutputNodeOr(outputNodeId); // 프리셋 값 우선 (Flux2 = "9")

            var sb = new StringBuilder();
            sb.Append("{\"prompt\":").Append(wf);
            sb.Append(",\"client_id\":\"").Append(Guid.NewGuid().ToString()).Append('"');
            if (!string.IsNullOrEmpty(apiKey))
                sb.Append(",\"extra_data\":{\"api_key_comfy_org\":\"").Append(Escape(apiKey)).Append("\"}");
            sb.Append('}');

            // 3) 실행 요청
            string promptId;
            using (var req = new UnityWebRequest(baseUrl + "/prompt", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                AddAuth(req);
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.result != UnityWebRequest.Result.Success)
                { Fail(onError, "실행 요청 실패: " + req.error + "\n" + req.downloadHandler.text); yield break; }
                promptId = Extract(req.downloadHandler.text, "\"prompt_id\"\\s*:\\s*\"([^\"]+)\"");
                if (promptId == null) { Fail(onError, "prompt_id 를 받지 못했습니다:\n" + req.downloadHandler.text); yield break; }
                info.promptId = promptId;
            }
            onStatus?.Invoke("대기 중");

            // 4) 완료될 때까지 기록(history) 확인
            float start = Time.realtimeSinceStartup;
            string outName = null, outSub = "", outType = "output";
            while (outName == null)
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds) { Fail(onError, "시간 초과"); yield break; }
                yield return new WaitForSeconds(pollIntervalSeconds);
                using (var req = UnityWebRequest.Get(baseUrl + "/history/" + promptId))
                {
                    AddAuth(req);
                    _activeRequest = req;
                    yield return req.SendWebRequest();
                    _activeRequest = null;
                    if (req.result != UnityWebRequest.Result.Success) continue;
                    string txt = req.downloadHandler.text;
                    if (txt.Contains("\"status_str\": \"error\"") || txt.Contains("\"status_str\":\"error\""))
                    { Fail(onError, "ComfyUI 실행 오류:\n" + Shorten(txt)); yield break; }
                    // 지정한 출력 노드의 결과만 사용 (최근 이미지 추측 금지)
                    int outIdx = txt.IndexOf("\"outputs\"", StringComparison.Ordinal);
                    int nodeIdx = outIdx < 0 ? -1 : txt.IndexOf("\"" + outNode + "\"", outIdx, StringComparison.Ordinal);
                    if (nodeIdx >= 0)
                    {
                        var m = Regex.Match(txt.Substring(nodeIdx),
                            "\"filename\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"subfolder\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"type\"\\s*:\\s*\"output\"");
                        if (m.Success) { outName = m.Groups[1].Value; outSub = m.Groups[2].Value; }
                    }
                }
                if (outName == null)
                {
                    // 대기열에서 내 작업이 실행 중인지 / 기다리는 중인지 확인
                    using (var q = UnityWebRequest.Get(baseUrl + "/queue"))
                    {
                        AddAuth(q);
                        _activeRequest = q;
                        yield return q.SendWebRequest();
                        _activeRequest = null;
                        if (q.result == UnityWebRequest.Result.Success)
                        {
                            string qt = q.downloadHandler.text;
                            int id = qt.IndexOf(promptId, StringComparison.Ordinal);
                            int run = qt.IndexOf("queue_running", StringComparison.Ordinal);
                            int pend = qt.IndexOf("queue_pending", StringComparison.Ordinal);
                            if (id >= 0)
                            {
                                bool running = run >= 0 && run < id && (pend < 0 || pend > id || pend < run);
                                onStatus?.Invoke(running ? "생성 중" : "대기 중");
                            }
                        }
                    }
                }
            }

            // 5) 결과 이미지 다운로드
            onStatus?.Invoke("결과 받는 중");
            string url = baseUrl + "/view?filename=" + UnityWebRequest.EscapeURL(outName) +
                         "&subfolder=" + UnityWebRequest.EscapeURL(outSub) + "&type=" + outType;
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                AddAuth(req);
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.result != UnityWebRequest.Result.Success)
                { Fail(onError, "결과 다운로드 실패: " + req.error); yield break; }
                _running = null;
                onDone?.Invoke(DownloadHandlerTexture.GetContent(req), info);
            }
        }

        static IEnumerator ReadStreaming(string relPath, Action<string> ok, Action<string> fail)
        {
            string url = Application.streamingAssetsPath + "/" + relPath;
            if (!url.Contains("://")) url = "file://" + url;
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) ok(req.downloadHandler.text);
                else fail(url + " (" + req.error + ")");
            }
        }

        void AddAuth(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        }

        void Fail(Action<string> onError, string msg)
        {
            Debug.LogError("[ComfyUI] " + msg);
            _running = null;
            onError?.Invoke(msg);
        }

        static string Extract(string text, string pattern)
        {
            var m = Regex.Match(text ?? "", pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        static string Escape(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

        static string Shorten(string s) => s == null ? "" : s.Length > 600 ? s.Substring(0, 600) + "..." : s;
    }
}
