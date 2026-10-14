using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>그림판 화면의 버튼/슬라이더/팔레트를 PaintCanvas 와 연결.</summary>
    public class PaintToolbar : MonoBehaviour
    {
        public PaintCanvas canvas;
        public AppController app;

        [Header("도구 버튼")]
        public Button brushButton;
        public Button eraserButton;
        public Button fillButton;
        public Button undoButton;
        public Button redoButton;

        [Header("굵기")]
        public Slider sizeSlider;
        public Text sizeLabel;

        [Header("색")]
        public Image currentColorPreview;
        public Transform paletteRoot; // 자식 Button 들의 Image 색 = 팔레트 색

        [Header("오른쪽 버튼")]
        public Button sendButton;
        public Button clearButton;
        public Button homeButton;

        [Header("안내 문구 (선택)")]
        public Text toastText;

        [Header("선택 표시 색")]
        public Color normalColor = new Color(0.93f, 0.86f, 0.72f);
        public Color selectedColor = new Color(1f, 0.78f, 0.25f);

        void Awake()
        {
            brushButton.onClick.AddListener(() => canvas.SetTool(PaintTool.Brush));
            eraserButton.onClick.AddListener(() => canvas.SetTool(PaintTool.Eraser));
            fillButton.onClick.AddListener(() => canvas.SetTool(PaintTool.Fill));
            undoButton.onClick.AddListener(canvas.Undo);
            redoButton.onClick.AddListener(canvas.Redo);

            sizeSlider.minValue = 1;
            sizeSlider.maxValue = 40;
            sizeSlider.wholeNumbers = true;
            sizeSlider.value = canvas.brushSize;
            sizeSlider.onValueChanged.AddListener(v => canvas.SetBrushSize((int)v));

            if (paletteRoot != null)
            {
                foreach (Transform child in paletteRoot)
                {
                    var btn = child.GetComponent<Button>();
                    var img = child.GetComponent<Image>();
                    if (btn == null || img == null) continue;
                    Color c = img.color;
                    btn.onClick.AddListener(() => canvas.SetColor(c));
                }
            }

            sendButton.onClick.AddListener(app.SendDrawing);
            clearButton.onClick.AddListener(OnClearPressed);
            homeButton.onClick.AddListener(app.ShowMain);

            canvas.OnStateChanged += Refresh;
        }

        void OnEnable()
        {
            _clearArmedUntil = -1f;
            var label = clearButton ? clearButton.GetComponentInChildren<Text>(true) : null;
            if (label && _clearLabel != null) label.text = _clearLabel;
            Refresh();
        }

        // 전체 지우기 확인: 3초 안에 한 번 더 누르면 지움 (지운 뒤에도 '취소'로 되돌릴 수 있음)
        float _clearArmedUntil = -1f;
        string _clearLabel;

        void OnClearPressed()
        {
            var label = clearButton.GetComponentInChildren<Text>(true);
            if (Time.unscaledTime <= _clearArmedUntil)
            {
                canvas.Clear();
                _clearArmedUntil = -1f;
                if (label && _clearLabel != null) label.text = _clearLabel;
                return;
            }
            _clearArmedUntil = Time.unscaledTime + 3f;
            if (label) { if (_clearLabel == null) _clearLabel = label.text; label.text = "한 번 더!"; }
        }

        void Update()
        {
            // 모두 지우기 버튼 '톡' 나타나는 효과
            if (clearButton && clearButton.gameObject.activeSelf && clearButton.transform.localScale.x < 0.999f)
                clearButton.transform.localScale = Vector3.Lerp(clearButton.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 14f);

            if (_clearArmedUntil > 0 && Time.unscaledTime > _clearArmedUntil)
            {
                _clearArmedUntil = -1f;
                var label = clearButton.GetComponentInChildren<Text>(true);
                if (label && _clearLabel != null) label.text = _clearLabel;
            }
        }

        /// <summary>그림판 하단/상단에 잠깐 안내 문구 (빈 캔버스 등).</summary>
        public void Toast(string msg)
        {
            if (toastText == null) return;
            toastText.text = msg;
            _toastUntil = Time.unscaledTime + 2.5f;
            toastText.gameObject.SetActive(true);
        }
        float _toastUntil;

        void LateUpdate()
        {
            if (toastText && toastText.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                toastText.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (canvas != null) canvas.OnStateChanged -= Refresh;
        }

        void Refresh()
        {
            if (canvas == null) return;
            SetSelected(brushButton, canvas.tool == PaintTool.Brush);
            SetSelected(eraserButton, canvas.tool == PaintTool.Eraser);
            SetSelected(fillButton, canvas.tool == PaintTool.Fill);
            undoButton.interactable = canvas.CanUndo;
            redoButton.interactable = canvas.CanRedo;
            if (sizeLabel) sizeLabel.text = canvas.brushSize.ToString();
            // '모두 지우기'(X)는 지우개를 골랐을 때만 나타남
            if (clearButton)
            {
                bool show = canvas.tool == PaintTool.Eraser;
                if (show && !clearButton.gameObject.activeSelf) clearButton.transform.localScale = Vector3.one * 0.6f;
                clearButton.gameObject.SetActive(show);
            }
            if (currentColorPreview) currentColorPreview.color = canvas.brushColor;
        }

        void SetSelected(Button b, bool on)
        {
            var img = b.GetComponent<Image>();
            if (img) img.color = on ? selectedColor : normalColor;
            // 아이콘 버튼: 선택 시 금빛 테두리(Ring) + 살짝 크게
            var ring = b.transform.Find("Ring");
            if (ring) ring.gameObject.SetActive(on);
            if (ring || img && img.sprite != null && img.type == Image.Type.Simple)
                b.transform.localScale = on ? Vector3.one * 1.12f : Vector3.one;
        }
    }
}
