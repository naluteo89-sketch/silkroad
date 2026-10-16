using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Silkroad
{
    /// <summary>
    /// Comfy Cloud API v2 (https://cloud.comfy.org, Authorization: Bearer 키).
    ///   1) POST /api/v2/assets            그림 업로드 → asset id
    ///   2) POST /api/v2/jobs              { workflow } → job id (comfy-sdk 와 같은 형식)
    ///   3) GET  /api/v2/jobs/{id}         status: queued / running / succeeded / failed / canceled / expired
    ///   4) outputs[] 중 출력 노드(outputNodeId)의 이미지 → GET /api/v2/assets/{id}/content (서명 URL 로 302)
    /// 워크플로의 "{{INPUT_IMAGE}}" 는 {"__type":"core/ASSET","info":{"id":...}} 로 바뀝니다.
    /// 클라우드에서 쓸 모델 이름은 Comfy Cloud 모델 목록에 있는 이름이어야 합니다.
    /// </summary>
    public partial class ComfyUIClient
    {
        [Serializable] class CloudAsset { public string id; }
        [Serializable] class CloudOutput { public string node_id; public string type; public string id; public string url; }
        [Serializable] class CloudJob { public string id; public string status; public CloudOutput[] outputs; }

        IEnumerator CloudRoutine(byte[] png, Action<string> onStatus, Action<Texture2D, GenerationInfo> onDone, Action<string> onError)
        {
            if (string.IsNullOrEmpty(apiKey)) yield return LoadKey();
            if (string.IsNullOrEmpty(apiKey)) { Fail(onError, "Comfy Cloud API 키를 찾지 못했습니다. C:\\keydata\\comfy_key.py 의 comfy_key 를 확인하세요."); yield break; }
            string baseUrl = serverUrl.TrimEnd('/');
            var info = new GenerationInfo { workflow = workflowPath };

            // 1) 업로드
            onStatus?.Invoke("업로드 중");
            string fileName = "unity_paint_" + Guid.NewGuid().ToString("N") + ".png";
            // comfy-sdk(comfy_low.transport.post_assets) 와 같은 형식: 텍스트 필드 → file 순서
            string boundary = "----silkroad" + Guid.NewGuid().ToString("N");
            var ms = new System.IO.MemoryStream();
            void W(string t) { var b = Encoding.UTF8.GetBytes(t); ms.Write(b, 0, b.Length); }
            W("--" + boundary + "\r\nContent-Disposition: form-data; name=\"content_type\"\r\n\r\nimage/png\r\n");
            W("--" + boundary + "\r\nContent-Disposition: form-data; name=\"file_path\"\r\n\r\n" + fileName + "\r\n");
            W("--" + boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"" + fileName + "\"\r\nContent-Type: image/png\r\n\r\n");
            ms.Write(png, 0, png.Length);
            W("\r\n--" + boundary + "--\r\n");
            string assetId;
            using (var req = new UnityWebRequest(baseUrl + "/api/v2/assets", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(ms.ToArray());
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);
                req.SetRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
                CloudAuth(req);
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.result != UnityWebRequest.Result.Success) { Fail(onError, CloudError("업로드 실패", req)); yield break; }
                assetId = JsonUtility.FromJson<CloudAsset>(req.downloadHandler.text)?.id;
                if (string.IsNullOrEmpty(assetId)) { Fail(onError, "업로드 응답에 id 가 없습니다:\n" + Shorten(req.downloadHandler.text)); yield break; }
            }

            // 2) 워크플로 + 프리셋
            string wfText = null, presetText = null, readError = null;
            yield return ReadStreaming(workflowPath, t => wfText = t, e => readError = e);
            if (wfText == null) { Fail(onError, "워크플로 파일을 읽지 못했습니다: " + readError); yield break; }
            if (!wfText.Contains("\"{{INPUT_IMAGE}}\""))
            { Fail(onError, "워크플로에 그림 입력 {{INPUT_IMAGE}}가 없습니다. 고정 이미지로 생성하지 않도록 중단했습니다."); yield break; }
            yield return ReadStreaming(presetPath, t => presetText = t, e => { });
            var preset = fallbackPreset;
            if (!string.IsNullOrEmpty(presetText))
            {
                try { preset = JsonUtility.FromJson<GenerationPreset>(presetText); }
                catch (Exception e) { Debug.LogWarning("[ComfyCloud] 프리셋 파싱 실패, 기본값 사용: " + e.Message); }
            }
            info.preset = preset.name;
            string outNode = preset.OutputNodeOr(outputNodeId); // 프리셋 값 우선 (Flux2 = "9")
            info.seed = PickSeed(preset);
            string themeText = PickTheme(preset, out info.theme);
            string wf = preset.Apply(wfText, Escape, themeText)
                .Replace("\"{{INPUT_IMAGE}}\"", "{\"__type\":\"core/ASSET\",\"info\":{\"id\":\"" + Escape(assetId) + "\"}}")
                .Replace("{{SEED}}", info.seed.ToString());
            info.inputAssetId = assetId;
            info.outputNode = outNode;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                info.inputSha256 = BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
            Debug.Log("[ComfyCloud] input=" + info.inputSha256 + " asset=" + assetId + " outputNode=" + outNode);

            // 3) 실행 요청
            var body = new StringBuilder();
            body.Append("{\"workflow\":").Append(wf);
            body.Append('}'); // metadata 는 Comfy Cloud(비 deployment)에서 거부(422)되므로 보내지 않음
            string jobId;
            using (var req = new UnityWebRequest(baseUrl + "/api/v2/jobs", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Idempotency-Key", Guid.NewGuid().ToString()); // 중복 제출 방지
                CloudAuth(req);
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.result != UnityWebRequest.Result.Success) { Fail(onError, CloudError("실행 요청 실패", req)); yield break; }
                jobId = JsonUtility.FromJson<CloudJob>(req.downloadHandler.text)?.id;
                if (string.IsNullOrEmpty(jobId)) { Fail(onError, "job id 를 받지 못했습니다:\n" + Shorten(req.downloadHandler.text)); yield break; }
                info.promptId = jobId;
            }
            onStatus?.Invoke("대기 중");

            // 4) 상태 확인
            float start = Time.realtimeSinceStartup;
            string outAssetId = null;
            while (outAssetId == null)
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                { Fail(onError, "시간 초과 (job " + jobId + " 는 서버에서 계속 진행 중일 수 있음)"); yield break; }
                yield return new WaitForSeconds(pollIntervalSeconds);
                using (var req = UnityWebRequest.Get(baseUrl + "/api/v2/jobs/" + jobId))
                {
                    CloudAuth(req);
                    _activeRequest = req;
                    yield return req.SendWebRequest();
                    _activeRequest = null;
                    if (req.result != UnityWebRequest.Result.Success) continue; // 일시적 네트워크 오류는 다음 폴링에서 재시도
                    string txt = req.downloadHandler.text;
                    var job = JsonUtility.FromJson<CloudJob>(txt);
                    switch (job?.status)
                    {
                        case "queued": onStatus?.Invoke("대기 중"); break;
                        case "running": onStatus?.Invoke("생성 중"); break;
                        case "failed": case "canceled": case "expired":
                            Fail(onError, "ComfyUI 실행 " + job.status + ":\n" + Shorten(txt)); yield break;
                        case "succeeded":
                            if (job.outputs != null)
                                foreach (var o in job.outputs)
                                    if (o != null && o.type == "image" && o.node_id == outNode && !string.IsNullOrEmpty(o.id))
                                    { outAssetId = o.id; break; }
                            if (outAssetId == null) { Fail(onError, "완료됐지만 지정한 출력 노드 " + outNode + "의 이미지가 없습니다 (job " + jobId + ")."); yield break; }
                            info.outputAssetId = outAssetId;
                            Debug.Log("[ComfyCloud] job=" + jobId + " result=" + outAssetId);
                            break;
                    }
                }
            }

            // 5) 결과 다운로드 (서명 URL 로 리다이렉트 → 그 URL 은 인증 헤더 없이 받음)
            onStatus?.Invoke("결과 받는 중");
            string signedUrl = null;
            using (var req = UnityWebRequest.Get(baseUrl + "/api/v2/assets/" + outAssetId + "/content"))
            {
                req.redirectLimit = 0;
                CloudAuth(req);
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.responseCode >= 300 && req.responseCode < 400) signedUrl = req.GetResponseHeader("Location");
                else if (req.responseCode == 200 && req.downloadHandler.data != null && req.downloadHandler.data.Length > 0)
                {
                    var direct = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    direct.LoadImage(req.downloadHandler.data);
                    _running = null;
                    onDone?.Invoke(direct, info);
                    yield break;
                }
                if (string.IsNullOrEmpty(signedUrl)) { Fail(onError, CloudError("결과 주소를 받지 못했습니다", req)); yield break; }
            }
            using (var req = UnityWebRequestTexture.GetTexture(signedUrl))
            {
                _activeRequest = req;
                yield return req.SendWebRequest();
                _activeRequest = null;
                if (req.result != UnityWebRequest.Result.Success) { Fail(onError, "결과 다운로드 실패: " + req.error); yield break; }
                _running = null;
                onDone?.Invoke(DownloadHandlerTexture.GetContent(req), info);
            }
        }

        public const string KeyFilePath = @"C:\keydata\comfy_key.py";
        public const string BuildKeyFile = "ComfyUI/comfy_key.txt"; // 빌드할 때만 잠깐 생성됨 (SilkroadKeyBuildStep)

        /// <summary>키 찾기: 에디터는 C:\keydata\comfy_key.py, 빌드(태블릿)는 StreamingAssets 의 comfy_key.txt</summary>
        IEnumerator LoadKey()
        {
#if UNITY_EDITOR
            try
            {
                if (System.IO.File.Exists(KeyFilePath)) apiKey = ParseKey(System.IO.File.ReadAllText(KeyFilePath));
            }
            catch (Exception e) { Debug.LogWarning("[ComfyCloud] 키 파일 읽기 실패: " + e.Message); }
            yield break;
#else
            string t = null;
            yield return ReadStreaming(BuildKeyFile, x => t = x, e => { });
            if (!string.IsNullOrEmpty(t)) apiKey = t.Trim();
#endif
        }

        /// <summary>comfy_key = "..." 형태에서 값만 추출</summary>
        public static string ParseKey(string py)
        {
            var m = System.Text.RegularExpressions.Regex.Match(py ?? "", @"comfy_key\s*=\s*[rRbBuU]?['""]([^'""]+)['""]");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        void CloudAuth(UnityWebRequest req)
        {
            req.SetRequestHeader("Authorization", "Bearer " + apiKey); // SDK 와 동일: Bearer 만
            try { req.SetRequestHeader("User-Agent", "silkroad-unity/1.0"); } catch (Exception) { } // 일부 플랫폼은 UA 변경 불가
        }

        static string CloudError(string what, UnityWebRequest req)
        {
            string hint = req.responseCode == 401 ? " → API 키 확인"
                        : req.responseCode == 402 || req.responseCode == 429 ? " → 구독/크레딧/요청 한도 확인"
                        : req.responseCode == 422 ? " → 워크플로 오류 (모델 이름이 Comfy Cloud 에 있는지 확인)"
                        : "";
            return what + " (" + req.responseCode + ")" + hint + "\n" + Shorten(req.downloadHandler?.text ?? req.error);
        }
    }
}
