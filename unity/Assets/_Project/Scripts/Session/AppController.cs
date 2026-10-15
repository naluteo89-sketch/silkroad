using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>
    /// 체험 세션 흐름.
    ///   Home → Drawing → (업로드/대기/생성/수신) → Result
    ///   Result → Drawing (그림 수정) / Result → 재요청 / Result → Home
    /// 세션 ID·요청 번호로 늦게 도착한 응답이 현재 화면을 덮어쓰지 않게 막습니다.
    /// </summary>
    public class AppController : MonoBehaviour
    {
        [Header("화면(패널)")]
        public GameObject mainPanel;
        public GameObject paintPanel;
        public GameObject loadingPanel;
        public GameObject resultPanel;

        [Header("연결")]
        public PaintCanvas paintCanvas;
        public PaintToolbar paintToolbar;
        public ComfyUIClient comfy;
        public LoadingView loadingView;
        public ResultView resultView;

        [Header("공통 두루마리 배경 (모든 화면이 같은 종이 위에 그려짐)")]
        [Tooltip("메인 화면이 나올 때 왼쪽→오른쪽으로 펼쳐짐. 그림판·결과로 넘어가도 종이는 그대로")]
        public ScrollReveal scrollStage;

        [Header("메인 화면 버튼")]
        public Button startButton;
        public Button quitButton;

        [Header("저장 시 QR 공유 (같은 Wi-Fi 휴대폰에서 받기)")]
        public bool shareWithQr = true;
        string _shareUrl;   // 현재 결과의 공유 주소 (결과마다 한 번만 만듦)
        string _shareError;

        [Header("결과 자동 저장 (원본/결과 PNG + 메타데이터 JSON)")]
        public bool autoSaveResults = true;

        public string SessionId { get; private set; }
        public string SaveFolder => Path.Combine(Application.persistentDataPath, "Results");

        byte[] _pendingPng;       // 보냈지만 아직 결과가 없는 원본
        Texture2D _pendingMine;
        string _pendingSourceId;
        byte[] _lastPng;          // 현재 결과와 짝인 원본
        string _sourceId;         // 원본 ID (재요청 시 같은 값 유지)
        Texture2D _lastMine;
        Texture2D _lastAi;
        GenerationInfo _lastInfo;
        int _variation;           // 같은 원본의 재요청 횟수 → 프리셋 seed 에 더함
        string _savedStamp;       // 현재 결과를 이미 저장했으면 그 파일 이름 앞부분
        bool _published;

        int _requestSeq;          // 요청마다 1씩 증가
        int _activeRequest = -1;  // 지금 기다리는 요청 번호 (-1 = 없음)
        DateTime _requestStart;

        void Awake()
        {
            Application.targetFrameRate = 60;               // 안드로이드 기본 30fps → 선이 부드럽게
            Screen.sleepTimeout = SleepTimeout.NeverSleep;  // 전시/체험용: 화면 꺼짐 방지
            startButton.onClick.AddListener(NewSession);
            if (quitButton) quitButton.gameObject.SetActive(false); // 종료 버튼은 사용하지 않음 (체험용 앱)
            loadingView.backButton.onClick.AddListener(CancelAndBack);
            resultView.homeButton.onClick.AddListener(ShowMain);
            resultView.retryButton.onClick.AddListener(Retry);
            resultView.editButton.onClick.AddListener(ShowPaint);
            resultView.saveButton.onClick.AddListener(Confirm);
        }

        void Start() => ShowMain();

        void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            paintPanel.SetActive(panel == paintPanel);
            loadingPanel.SetActive(panel == loadingPanel);
            resultPanel.SetActive(panel == resultPanel);
        }

        public void ShowMain()
        {
            CancelPending();
            Show(mainPanel);
            if (scrollStage) scrollStage.Replay();
        }

        public void ShowPaint() => Show(paintPanel);

        /// <summary>시작하기: 새 세션. 이전 사람의 그림/결과를 모두 비웁니다.</summary>
        public void NewSession()
        {
            CancelPending();
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            paintCanvas.ResetCanvas();
            ClearPending();
            _lastPng = null;
            _sourceId = null;
            _lastInfo = null;
            _savedStamp = null;
            _published = false;
            _shareUrl = null; _shareError = null;
            if (_lastMine != null) { Destroy(_lastMine); _lastMine = null; }
            if (_lastAi != null) { Destroy(_lastAi); _lastAi = null; }
            ShowPaint();
        }

        /// <summary>보내기</summary>
        public void SendDrawing()
        {
            if (_activeRequest >= 0) return; // 중복 제출 방지
            if (paintCanvas.IsEmpty())
            {
                if (paintToolbar) paintToolbar.Toast("먼저 그림을 그려 주세요!");
                return;
            }
            // 결과가 오기 전까지는 '대기 중 원본' 으로만 보관 (실패해도 이전 결과 짝이 깨지지 않게)
            _pendingPng = paintCanvas.GetPNG();
            _pendingSourceId = Guid.NewGuid().ToString("N").Substring(0, 12);
            if (_pendingMine != null) Destroy(_pendingMine);
            _pendingMine = paintCanvas.CreateSnapshot();
            Request(_pendingPng, 0);
        }

        /// <summary>재요청: 같은 원본·스타일, 새 seed</summary>
        public void Retry()
        {
            if (_activeRequest >= 0) return;
            if (_lastPng == null) { ShowPaint(); return; }
            ClearPending();
            Request(_lastPng, _variation + 1);
        }

        void Request(byte[] png, int variation)
        {
            int reqId = ++_requestSeq;
            string session = SessionId;
            _activeRequest = reqId;
            _requestStart = DateTime.Now;

            Show(loadingPanel);
            loadingView.ShowLoading();
            comfy.Generate(png, variation,
                status => { if (IsCurrent(reqId, session)) loadingView.SetStatus(status); },
                (tex, info) =>
                {
                    if (!IsCurrent(reqId, session)) { Destroy(tex); return; } // 늦게 온 응답은 버림
                    _activeRequest = -1;
                    _variation = variation; // 성공했을 때만 → 실패한 재요청은 같은 seed 로 다시 시도
                    OnResult(tex, info);
                },
                err =>
                {
                    if (!IsCurrent(reqId, session)) return;
                    _activeRequest = -1;
                    // 재요청 실패 시 직전 결과는 그대로 보존 → '이전 결과' 로 돌아감
                    loadingView.ShowError(err, _lastAi != null ? "이전 결과로" : "그림으로 돌아가기");
                });
        }

        bool IsCurrent(int reqId, string session) => reqId == _activeRequest && session == SessionId;

        void OnResult(Texture2D ai, GenerationInfo info)
        {
            if (_pendingPng != null)
            {
                // 새 그림의 첫 결과 → 원본 교체
                _lastPng = _pendingPng;
                _sourceId = _pendingSourceId;
                if (_lastMine != null) Destroy(_lastMine);
                _lastMine = _pendingMine;
                _pendingPng = null; _pendingMine = null; _pendingSourceId = null;
            }
            if (_lastAi != null) Destroy(_lastAi);
            _lastAi = ai;
            _lastInfo = info;
            _savedStamp = null;
            _shareUrl = null; _shareError = null;
            _published = false;
            Show(resultPanel);
            resultView.Show(_lastMine, _lastAi);
            if (autoSaveResults) SaveFiles();
        }

        void CancelAndBack()
        {
            CancelPending();
            ClearPending();
            if (_lastAi != null) { Show(resultPanel); resultView.Show(_lastMine, _lastAi); }
            else ShowPaint();
        }

        void ClearPending()
        {
            if (_pendingMine != null) Destroy(_pendingMine);
            _pendingPng = null; _pendingMine = null; _pendingSourceId = null;
        }

        void CancelPending()
        {
            if (_activeRequest >= 0) comfy.Cancel(); // 화면 이탈 = 결과를 안 받는 것. 서버 작업은 그대로 둠
            _activeRequest = -1;
        }

        /// <summary>결과 확정(저장 버튼): 파일 저장 + XR 쪽으로 전달.</summary>
        void Confirm()
        {
            if (_lastAi == null) return;
            resultView.ShowFullResult(); // 저장 누르면 바뀐 전체 그림(AI 결과)을 보여줌
            if (!SaveFiles()) return; // 실패 시에는 상단에 오류 문구가 남음
            if (!_published)
            {
                XRResultBridge.Publish(_lastMine, _lastAi);
                _published = true;
            }
            resultView.SetStatus("");
            resultView.PlaySavedEffect(_lastAi); // 캡처 완료처럼 잠깐 표시 후 사라짐
            if (shareWithQr) ShareResult();
        }

        /// <summary>[AI 결과 + 액자] 한 장을 만들어 태블릿 공유 서버에 올리고 QR 표시.</summary>
        void ShareResult()
        {
            if (_shareUrl == null)
            {
                Texture2D framed = null;
                try
                {
                    framed = resultView.ComposeFramed(_lastAi);
                    byte[] png = (framed != null ? framed : _lastAi).EncodeToPNG();
                    if (framed != null && _savedStamp != null)
                    {
                        try { File.WriteAllBytes(Path.Combine(SaveFolder, _savedStamp + "_framed.png"), png); } catch { }
                    }
                    _shareUrl = ShareServer.Instance.Publish(png, out _shareError);
                }
                catch (Exception e) { _shareError = e.Message; }
                finally { if (framed != null) Destroy(framed); }
            }
            resultView.ShowShareQr(_shareUrl, _shareError);
        }

        /// <summary>원본/결과 PNG + 메타데이터 JSON 저장. 같은 결과는 한 번만 저장.</summary>
        bool SaveFiles()
        {
            if (_lastAi == null) return false;
            if (_savedStamp != null) return true;
            try
            {
                Directory.CreateDirectory(SaveFolder);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                File.WriteAllBytes(Path.Combine(SaveFolder, stamp + "_mine.png"), _lastPng);
                File.WriteAllBytes(Path.Combine(SaveFolder, stamp + "_ai.png"), _lastAi.EncodeToPNG());
                var meta = new ResultMeta
                {
                    sessionId = SessionId,
                    requestId = _requestSeq,
                    sourceImageId = _sourceId,
                    promptId = _lastInfo?.promptId,
                    seed = _lastInfo?.seed ?? 0,
                    styleId = _lastInfo?.preset,
                    workflow = _lastInfo?.workflow,
                    frame = resultView.CurrentFrameName,
                    theme = _lastInfo?.theme,
                    width = _lastAi.width,
                    height = _lastAi.height,
                    elapsedSeconds = (float)(DateTime.Now - _requestStart).TotalSeconds,
                    createdAt = DateTime.Now.ToString("o"),
                };
                File.WriteAllText(Path.Combine(SaveFolder, stamp + "_meta.json"), JsonUtility.ToJson(meta, true));
                _savedStamp = stamp;
                Debug.Log("[Silkroad] 저장 위치: " + SaveFolder);
                return true;
            }
            catch (Exception e)
            {
                resultView.SetStatus("저장 실패: " + e.Message);
                return false;
            }
        }

        [Serializable]
        class ResultMeta
        {
            public string sessionId;
            public int requestId;
            public string sourceImageId;
            public string promptId;
            public long seed;
            public string styleId;
            public string workflow;
            public string frame;
            public string theme;
            public int width;
            public int height;
            public float elapsedSeconds;
            public string createdAt;
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
