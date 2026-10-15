using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>
    /// 결과 비교 화면. 왼쪽 = 내 그림, 오른쪽 = AI 결과.
    /// 가운데 막대를 끌어서 비교합니다.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        public RectTransform compareArea;   // 비교 영역 전체
        public RawImage mineImage;          // 전체를 덮는 내 그림
        public RectTransform aiMask;        // RectMask2D, 오른쪽 부분만 보여줌
        public RawImage aiImage;            // aiMask 안의 AI 결과
        [Tooltip("AI 결과 + 액자를 담는 층 (있으면 막대에 따라 액자도 함께 드러남)")]
        public RectTransform aiLayer;
        public Slider divider;              // 0~1 위치

        [Header("랜덤 액자 (메뉴: Silkroad → 액자 목록 갱신)")]
        public FrameLibrary frameLibrary;
        public Image frameArt;              // AILayer/FrameArt
        /// <summary>현재 결과에 씌운 액자 이름 (메타데이터 기록용)</summary>
        public string CurrentFrameName { get; private set; } = "";

        public Button homeButton;
        public Button retryButton;
        public Button editButton;
        public Button saveButton;
        public Text statusText;

        [Tooltip("결과가 나올 때 오른쪽→가운데로 쓸어오는 연출 시간")]
        public float revealSeconds = 1.2f;

        float _revealT = -1f;
        float _from = 1f, _to = 0.5f, _dur = 1.2f;

        void Awake()
        {
            divider.minValue = 0; divider.maxValue = 1;
            // '내 그림' / 'AI 결과' 글씨는 사용하지 않음 (씬에 남아 있어도 숨김)
            foreach (var labelName in new[] { "MineLabel", "AILabel" })
            {
                var label = compareArea ? compareArea.Find(labelName) : null;
                if (label) label.gameObject.SetActive(false);
            }
            divider.onValueChanged.AddListener(Apply);
        }

        public void Show(Texture mine, Texture ai)
        {
            mineImage.texture = mine;
            if (ai != _lastAi) { _lastAi = ai; PickFrame(); } // 새 결과일 때만 액자 교체 (뒤로 가기로 다시 보면 유지)
            aiImage.texture = ai;
            SetStatus("");
            AnimateTo(1f, 0.5f, revealSeconds);
        }

        /// <summary>저장(확정) 시: 막대를 왼쪽 끝으로 밀어 AI 결과 전체를 보여줌</summary>
        public void ShowFullResult(float seconds = 0.6f) => AnimateTo(divider.value, 0f, seconds);

        void AnimateTo(float from, float to, float seconds)
        {
            _from = from; _to = to; _dur = Mathf.Max(0.01f, seconds); _revealT = 0f;
            divider.SetValueWithoutNotify(from);
            Apply(from);
        }

        // ───────────── 랜덤 액자 ─────────────
        Texture _lastAi;
        int _lastFrame = -1;
        FrameLibrary.Entry _currentFrame;

        /// <summary>
        /// 화면과 같은 모양으로 [AI 결과 + 액자] 를 한 장으로 합침 (공유·저장용). 액자 목록이 없으면 null.
        /// ai 는 읽기 가능한 텍스처여야 함 (서버에서 받은 결과는 해당). 반환 텍스처는 호출한 쪽이 Destroy.
        /// </summary>
        public Texture2D ComposeFramed(Texture2D ai, int maxWidth = 1600)
        {
            var e = _currentFrame;
            if (ai == null || e == null || e.sprite == null || e.window.width <= 0) return null;
            Vector2 box = aiImage.rectTransform.rect.size;
            if (box.x <= 0 || box.y <= 0) return null;
            float k = Mathf.Min(box.x / e.window.width, box.y / e.window.height);
            Vector2 aiSize = box / k; // 액자 픽셀 기준 AI 그림 크기
            Vector2 c = new Vector2(e.window.x + e.window.width * 0.5f, e.window.y + e.window.height * 0.5f);
            float ax0 = c.x - aiSize.x * 0.5f, ay0 = c.y - aiSize.y * 0.5f;

            float s = Mathf.Min(1f, maxWidth / (float)e.size.x);
            int W = Mathf.Max(1, Mathf.RoundToInt(e.size.x * s)), H = Mathf.Max(1, Mathf.RoundToInt(e.size.y * s));
            Color32[] fr = ReadScaled(e.sprite.texture, W, H);
            var o = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = ((y + 0.5f) / s - ay0) / aiSize.y;
                for (int x = 0; x < W; x++)
                {
                    float u = ((x + 0.5f) / s - ax0) / aiSize.x;
                    Color32 f = fr[y * W + x];
                    if (u < 0 || u > 1 || v < 0 || v > 1) { o[y * W + x] = f; continue; }
                    Color32 under = ai.GetPixelBilinear(u, v);
                    under.a = 255;
                    var mix = Color32.Lerp(under, f, f.a / 255f);
                    mix.a = 255;
                    o[y * W + x] = mix;
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Framed" };
            tex.SetPixels32(o);
            tex.Apply(false);
            return tex;
        }

        static Color32[] ReadScaled(Texture src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active; // Blit 이 active 를 바꾸므로 먼저 저장
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            t.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels32();
            Destroy(t);
            return px;
        }

        // ───────────── QR 공유 창 ─────────────
        RectTransform _qrPanel; RawImage _qrImage; Text _qrTitle, _qrSub; Texture2D _qrTex; Coroutine _qrShow;

        /// <summary>저장 후 QR 표시. url 이 null 이면 error 문구만 보여줌. 화면을 누르면 닫힘.</summary>
        public void ShowShareQr(string url, string error, float delay = 1.0f)
        {
            BuildQrPanel();
            if (_qrShow != null) StopCoroutine(_qrShow);
            _qrShow = StartCoroutine(ShowQrRoutine(url, error, delay));
        }

        public void HideShareQr()
        {
            if (_qrShow != null) { StopCoroutine(_qrShow); _qrShow = null; }
            if (_qrPanel) _qrPanel.gameObject.SetActive(false);
        }

        IEnumerator ShowQrRoutine(string url, string error, float delay)
        {
            yield return new WaitForSecondsRealtime(delay); // 저장 연출이 먼저 보이도록
            if (_qrTex != null) { Destroy(_qrTex); _qrTex = null; }
            var m = url != null ? QrCode.Encode(url) : null;
            if (m != null)
            {
                _qrTex = QrCode.ToTexture(m, 8);
                _qrImage.texture = _qrTex;
                _qrImage.gameObject.SetActive(true);
                _qrTitle.text = "휴대폰 카메라로 QR 을 찍어 보세요";
                _qrSub.text = "같은 Wi-Fi 에 연결된 휴대폰에서 그림을 받을 수 있어요\n" + url + "\n\n화면을 누르면 닫혀요";
            }
            else
            {
                _qrImage.gameObject.SetActive(false);
                _qrTitle.text = "QR 공유를 할 수 없어요";
                _qrSub.text = (string.IsNullOrEmpty(error) ? "네트워크를 확인해 주세요" : error) + "\n(그림은 태블릿에 저장됐어요)\n\n화면을 누르면 닫혀요";
            }
            _qrPanel.SetAsLastSibling();
            _qrPanel.gameObject.SetActive(true);
            var cg = _qrPanel.GetComponent<CanvasGroup>();
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.25f) { cg.alpha = t; yield return null; }
            cg.alpha = 1;
            _qrShow = null;
        }

        void BuildQrPanel()
        {
            if (_qrPanel != null) return;
            var font = statusText != null && statusText.font != null ? statusText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _qrPanel = new GameObject("ShareQrPanel", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _qrPanel.SetParent(transform, false);
            _qrPanel.anchorMin = Vector2.zero; _qrPanel.anchorMax = Vector2.one; _qrPanel.offsetMin = _qrPanel.offsetMax = Vector2.zero;
            _qrPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
            _qrPanel.GetComponent<Button>().onClick.AddListener(HideShareQr);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            card.SetParent(_qrPanel, false);
            card.sizeDelta = new Vector2(680, 860);
            var cimg = card.GetComponent<Image>(); cimg.color = new Color(0.96f, 0.91f, 0.80f, 1f); cimg.raycastTarget = false;

            _qrTitle = MakeText(card, "Title", font, 40, new Vector2(0, 360), new Vector2(640, 70));
            _qrImage = new GameObject("QR", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _qrImage.transform.SetParent(card, false);
            _qrImage.rectTransform.sizeDelta = new Vector2(520, 520);
            _qrImage.rectTransform.anchoredPosition = new Vector2(0, 40);
            _qrImage.raycastTarget = false;
            _qrSub = MakeText(card, "Sub", font, 26, new Vector2(0, -320), new Vector2(640, 200));
            _qrPanel.gameObject.SetActive(false);
        }

        static Text MakeText(Transform parent, string name, Font font, int size, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.rectTransform.sizeDelta = box; t.rectTransform.anchoredPosition = pos;
            t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.25f, 0.16f, 0.08f); t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void PickFrame()
        {
            if (frameArt == null && aiLayer != null)
            {
                var t = aiLayer.Find("FrameArt");
                if (t) frameArt = t.GetComponent<Image>();
            }
            var e = frameLibrary != null ? frameLibrary.Pick(ref _lastFrame) : null;
            if (frameArt == null || e == null)
            {
                CurrentFrameName = frameArt != null && frameArt.sprite != null ? frameArt.sprite.name : "";
                return; // 목록이 없으면 씬에 넣어 둔 액자 그대로
            }

            // 액자의 투명 창이 AI 그림 영역을 덮도록 크기·위치 맞춤 (창 ≤ 그림 → 틈이 생기지 않음)
            var img = aiImage.rectTransform;
            Vector2 box = img.rect.size;
            float k = Mathf.Min(box.x / e.window.width, box.y / e.window.height);
            var r = frameArt.rectTransform;
            r.anchorMin = r.anchorMax = img.anchorMin;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = (Vector2)e.size * k;
            Vector2 winCenter = new Vector2(e.window.x + e.window.width * 0.5f, e.window.y + e.window.height * 0.5f);
            Vector2 imgCenter = img.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - img.pivot, box);
            r.anchoredPosition = imgCenter - (winCenter - (Vector2)e.size * 0.5f) * k;
            _currentFrame = e;
            frameArt.sprite = e.sprite;
            frameArt.preserveAspect = false;
            frameArt.enabled = true;
            CurrentFrameName = e.name;
        }

        // ───────────── 저장 완료 연출 (스마트폰 캡처처럼) ─────────────
        Image _flash; RawImage _thumb; RectTransform _toast; Text _toastText; Coroutine _saveFx;

        /// <summary>찰칵 플래시 → 썸네일이 오른쪽 아래로 줄어들며 날아감 → "저장 완료" 알림 후 사라짐</summary>
        public void PlaySavedEffect(Texture saved, string message = "저장 완료")
        {
            BuildSaveFx();
            if (_saveFx != null) StopCoroutine(_saveFx);
            _saveFx = StartCoroutine(SaveFxRoutine(saved, message));
        }

        void BuildSaveFx()
        {
            if (_flash != null) return;
            var panel = (RectTransform)transform;

            _flash = new GameObject("SaveFlash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _flash.transform.SetParent(panel, false);
            var fr = _flash.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            _flash.color = new Color(1, 1, 1, 0); _flash.raycastTarget = false;

            _thumb = new GameObject("SaveThumb", typeof(RectTransform), typeof(RawImage), typeof(Outline), typeof(Shadow)).GetComponent<RawImage>();
            _thumb.transform.SetParent(panel, false);
            _thumb.raycastTarget = false;
            var ol = _thumb.GetComponent<Outline>(); ol.effectColor = Color.white; ol.effectDistance = new Vector2(6, -6);
            var sh = _thumb.GetComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(10, -10);
            _thumb.gameObject.SetActive(false);

            _toast = new GameObject("SaveToast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _toast.SetParent(panel, false);
            _toast.anchorMin = _toast.anchorMax = new Vector2(0.5f, 0f); _toast.pivot = new Vector2(0.5f, 0f);
            _toast.sizeDelta = new Vector2(420, 90); _toast.anchoredPosition = new Vector2(0, 150);
            var bg = _toast.GetComponent<Image>(); bg.color = new Color(0.1f, 0.08f, 0.06f, 0.85f); bg.raycastTarget = false;
            _toastText = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            _toastText.transform.SetParent(_toast, false);
            var tr = _toastText.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            _toastText.font = statusText != null && statusText.font != null ? statusText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _toastText.fontSize = 40; _toastText.alignment = TextAnchor.MiddleCenter; _toastText.color = Color.white; _toastText.raycastTarget = false;
            _toast.GetComponent<CanvasGroup>().alpha = 0;
        }

        IEnumerator SaveFxRoutine(Texture saved, string message)
        {
            var panel = (RectTransform)transform;
            foreach (var t in new Transform[] { _flash.transform, _thumb.transform, _toast }) t.SetAsLastSibling();

            // 썸네일 시작 위치 = 화면의 AI 그림 위치
            var corners = new Vector3[4];
            aiImage.rectTransform.GetWorldCorners(corners);
            Vector2 a = panel.InverseTransformPoint(corners[0]), b = panel.InverseTransformPoint(corners[2]);
            Vector2 startSize = b - a, startPos = (a + b) / 2f;
            Rect pr = panel.rect;
            Vector2 endSize = startSize * 0.22f;
            Vector2 endPos = new Vector2(pr.xMax - endSize.x / 2f - 60f, pr.yMin + endSize.y / 2f + 160f);

            var tr = _thumb.rectTransform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
            _thumb.texture = saved; _thumb.color = Color.white;
            tr.sizeDelta = startSize; tr.anchoredPosition = startPos; tr.localRotation = Quaternion.identity;
            _thumb.gameObject.SetActive(true);
            var cg = _toast.GetComponent<CanvasGroup>();
            _toastText.text = message; // (폰트에 없는 기호는 □로 보여서 글자만)

            // 1) 찰칵 플래시
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            { _flash.color = new Color(1, 1, 1, Mathf.Lerp(0.85f, 0f, t)); yield return null; }
            _flash.color = new Color(1, 1, 1, 0);

            // 2) 썸네일이 오른쪽 아래로 줄어들며 이동 + 알림 등장
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                float k = 1f - Mathf.Pow(1f - t, 3f);
                tr.sizeDelta = Vector2.Lerp(startSize, endSize, k);
                tr.anchoredPosition = Vector2.Lerp(startPos, endPos, k);
                tr.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -4f, k));
                cg.alpha = k;
                yield return null;
            }
            tr.sizeDelta = endSize; tr.anchoredPosition = endPos; cg.alpha = 1;

            // 3) 잠깐 머무름
            yield return new WaitForSecondsRealtime(1.4f);

            // 4) 오른쪽으로 빠지며 사라짐
            Vector2 outPos = endPos + new Vector2(endSize.x + 120f, 0);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                tr.anchoredPosition = Vector2.Lerp(endPos, outPos, t);
                _thumb.color = new Color(1, 1, 1, 1f - t);
                cg.alpha = 1f - t;
                yield return null;
            }
            _thumb.gameObject.SetActive(false);
            cg.alpha = 0;
            _saveFx = null;
        }

        void OnDisable()
        {
            if (_saveFx != null) { StopCoroutine(_saveFx); _saveFx = null; }
            if (_flash) _flash.color = new Color(1, 1, 1, 0);
            if (_thumb) _thumb.gameObject.SetActive(false);
            if (_toast) _toast.GetComponent<CanvasGroup>().alpha = 0;
            HideShareQr();
        }

        public void SetStatus(string s) { if (statusText) statusText.text = s; }

        void Update()
        {
            if (_revealT >= 0f)
            {
                _revealT += Time.unscaledDeltaTime / _dur;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_revealT));
                float v = Mathf.Lerp(_from, _to, k);
                divider.SetValueWithoutNotify(v);
                Apply(v);
                if (_revealT >= 1f) _revealT = -1f;
            }
        }

        void Apply(float v)
        {
            aiMask.anchorMin = new Vector2(v, 0);
            aiMask.anchorMax = new Vector2(1, 1);
            aiMask.offsetMin = Vector2.zero;
            aiMask.offsetMax = Vector2.zero;
            FitLayer();
        }

        // AI 층(또는 AI 이미지)은 항상 비교 영역 전체 크기로 오른쪽에 고정 → 막대가 움직여도 그림이 따라 움직이지 않음
        void FitLayer()
        {
            var target = aiLayer != null ? aiLayer : aiImage.rectTransform;
            target.sizeDelta = new Vector2(compareArea.rect.width, 0);
        }

        void LateUpdate()
        {
            // 화면 크기가 바뀌어도 맞도록
            FitLayer();
        }
    }
}
