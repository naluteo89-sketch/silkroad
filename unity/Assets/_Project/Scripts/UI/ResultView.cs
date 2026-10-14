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
            divider.onValueChanged.AddListener(Apply);
        }

        public void Show(Texture mine, Texture ai)
        {
            mineImage.texture = mine;
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
