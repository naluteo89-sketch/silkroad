using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Silkroad
{
    public enum PaintTool { Brush, Eraser, Fill }

    /// <summary>
    /// RawImage 위에 마우스/터치로 그림을 그리는 캔버스.
    /// 브러시 / 지우개 / 채우기 / 취소(Undo) / 복원(Redo) / 전체 지우기 지원.
    /// 구조: 이 RawImage = 도화지(종이 질감) / 자식 "Ink" RawImage = 그린 내용(투명 배경).
    /// 지우개는 종이 질감이 다시 보이게 하고, 보내기·결과 비교에는 종이+그림을 합친 이미지를 씁니다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class PaintCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("캔버스 해상도 (16:9 권장)")]
        public int textureWidth = 1280;
        public int textureHeight = 720;

        [Header("도화지 질감 이미지 (비우면 아래 단색 사용) - AI 로 보낼 때도 포함")]
        public Texture2D paperTexture;
        [Header("종이(배경) 색 - 질감 이미지가 없을 때")]
        public Color32 backgroundColor = new Color32(250, 244, 230, 255);

        [Header("현재 도구 상태")]
        public PaintTool tool = PaintTool.Brush;
        public Color32 brushColor = new Color32(0, 0, 0, 255);
        [Range(1, 40)] public int brushSize = 4;

        [Header("채우기 허용 오차 (0~255)")]
        [Range(0, 255)] public int fillTolerance = 40;

        [Header("되돌리기 최대 횟수")]
        public int maxHistory = 20; // 태블릿 메모리 고려

        /// <summary>도구/색/크기/히스토리가 바뀔 때 UI 갱신용.</summary>
        public event Action OnStateChanged;

        public Texture2D Texture => _tex;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        RawImage _image;      // 도화지
        RawImage _inkImage;   // 그린 내용
        Color32[] _paper;     // 캔버스 해상도로 맞춘 도화지 픽셀
        Texture2D _shownPaper;
        RectTransform _rect;
        Texture2D _tex;
        Color32[] _pixels;
        bool _dirty;
        bool _drawing;
        Vector2 _lastPos;
        int _pointerId = int.MinValue; // 멀티터치 시 첫 손가락만 사용

        readonly List<Color32[]> _undo = new List<Color32[]>();
        readonly List<Color32[]> _redo = new List<Color32[]>();

        void Awake()
        {
            _image = GetComponent<RawImage>();
            _rect = (RectTransform)transform;
            _tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "PaintTexture"
            };
            _pixels = new Color32[textureWidth * textureHeight];
            FillAll(Clear32);

            var inkGo = new GameObject("Ink", typeof(RectTransform), typeof(RawImage));
            var irt = (RectTransform)inkGo.transform;
            irt.SetParent(transform, false);
            irt.SetSiblingIndex(0);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            _inkImage = inkGo.GetComponent<RawImage>();
            _inkImage.raycastTarget = false; // 입력은 이 오브젝트(도화지)가 받음
            _inkImage.texture = _tex;
            _inkImage.color = Color.white;

            if (paperTexture == null) paperTexture = Resources.Load<Texture2D>(DefaultPaperResource);
            if (ComfyUIClient.TestSettingsEnabled && GetComponent<TestImageLoader>() == null)
                gameObject.AddComponent<TestImageLoader>(); // (테스트용) Ctrl+V / Ctrl+O 로 이미지 불러오기
            SetPaper(paperTexture);
        }

        /// <summary>Inspector 에서 비워 두면 쓰는 기본 도화지 (Assets/_Project/Resources/SilkroadPaper.png).</summary>
        public const string DefaultPaperResource = "SilkroadPaper";

        static readonly Color32 Clear32 = new Color32(0, 0, 0, 0);

        /// <summary>도화지 질감 교체 (null = 단색 backgroundColor).</summary>
        public void SetPaper(Texture2D paper)
        {
            paperTexture = paper;
            _paper = new Color32[textureWidth * textureHeight];
            if (paper == null)
            {
                for (int i = 0; i < _paper.Length; i++) _paper[i] = backgroundColor;
                _image.texture = null;
                _image.color = backgroundColor;
                return;
            }
            // 압축/읽기 불가 텍스처도 GPU 로 캔버스 크기에 맞춰 읽어 옴
            var rt = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active; // Blit 이 active 를 바꾸므로 먼저 저장
            Graphics.Blit(paper, rt);
            RenderTexture.active = rt;
            if (_shownPaper == null) _shownPaper = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false) { name = "PaperPixels" };
            _shownPaper.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0, false);
            _shownPaper.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _paper = _shownPaper.GetPixels32();
            for (int i = 0; i < _paper.Length; i++) _paper[i].a = 255;
            _image.texture = paper;
            _image.color = Color.white;
        }

        void LateUpdate()
        {
            if (!_dirty) return;
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
            _dirty = false;
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
            if (_shownPaper != null) Destroy(_shownPaper);
        }

        // ───────────── 공개 API ─────────────
        public void SetTool(PaintTool t) { tool = t; Notify(); }
        public void SetColor(Color c) { brushColor = c; if (tool == PaintTool.Eraser) tool = PaintTool.Brush; Notify(); }
        public void SetBrushSize(int s) { brushSize = Mathf.Clamp(s, 1, 40); Notify(); }

        public void Clear()
        {
            PushUndo();
            FillAll(Clear32);
            Notify();
        }

        /// <summary>새 체험 세션: 그림과 기록을 모두 지움 (되돌리기 불가).</summary>
        public void ResetCanvas()
        {
            _undo.Clear();
            _redo.Clear();
            _drawing = false;
            FillAll(Clear32);
            tool = PaintTool.Brush;
            Notify();
        }

        /// <summary>아무것도 그리지 않았는지 (도화지만 있는지).</summary>
        public bool IsEmpty()
        {
            for (int i = 0; i < _pixels.Length; i++)
                if (_pixels[i].a != 0) return false;
            return true;
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            _redo.Add((Color32[])_pixels.Clone());
            _pixels = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _dirty = true;
            Notify();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Add((Color32[])_pixels.Clone());
            _pixels = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _dirty = true;
            Notify();
        }

        /// <summary>
        /// (테스트용) 이미지를 비율 유지로 도화지 가운데에 그려 넣음. 남는 곳은 도화지. 취소로 되돌릴 수 있음.
        /// src 는 읽기 가능한 텍스처여야 함 (Texture2D.LoadImage 로 만든 것).
        /// </summary>
        public void LoadImage(Texture2D src)
        {
            if (src == null) return;
            PushUndo();
            int w = textureWidth, h = textureHeight;
            float scale = Mathf.Min(w / (float)src.width, h / (float)src.height);
            float dw = src.width * scale, dh = src.height * scale;
            float ox = (w - dw) / 2f, oy = (h - dh) / 2f;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f - oy) / dh;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f - ox) / dw;
                    if (u < 0 || u > 1 || v < 0 || v > 1) { _pixels[y * w + x] = Clear32; continue; }
                    Color32 c = src.GetPixelBilinear(u, v);
                    _pixels[y * w + x] = c.a < 8 ? Clear32 : Opaque(c);
                }
            }
            _dirty = true;
            Notify();
        }

        /// <summary>현재 그림(도화지 + 그린 내용)을 PNG 바이트로.</summary>
        public byte[] GetPNG()
        {
            var t = CreateSnapshot();
            byte[] png = t.EncodeToPNG();
            Destroy(t);
            return png;
        }

        /// <summary>도화지 + 그린 내용을 합친 복사본 텍스처 (보내기 / 결과 비교 화면용).</summary>
        public Texture2D CreateSnapshot()
        {
            var t = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            t.SetPixels32(Composite());
            t.Apply(false);
            return t;
        }

        Color32[] Composite()
        {
            var o = new Color32[_pixels.Length];
            for (int i = 0; i < o.Length; i++)
            {
                Color32 k = _pixels[i];
                if (k.a == 255) o[i] = k;
                else if (k.a == 0) o[i] = _paper[i];
                else { var c = Color32.Lerp(_paper[i], k, k.a / 255f); c.a = 255; o[i] = c; }
            }
            return o;
        }

        // ───────────── 입력 ─────────────
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (_drawing) return; // 이미 다른 손가락으로 그리는 중
            if (!ToPixel(e, out var p)) return;

            if (tool == PaintTool.Fill)
            {
                int fx = Mathf.Clamp((int)p.x, 0, textureWidth - 1), fy = Mathf.Clamp((int)p.y, 0, textureHeight - 1);
                Color32 t = _pixels[fy * textureWidth + fx];
                if (t.a == 255 && t.r == brushColor.r && t.g == brushColor.g && t.b == brushColor.b) return;
                PushUndo();
                FloodFill(fx, fy, brushColor);
                Notify();
                return;
            }
            PushUndo();
            _drawing = true;
            _pointerId = e.pointerId;
            _lastPos = p;
            Stamp(p);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_drawing || e.pointerId != _pointerId) return;
            ToPixel(e, out var p); // 캔버스 밖이어도 좌표는 계산 (가장자리까지 선이 이어지도록)
            DrawLine(_lastPos, p);
            _lastPos = p;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_drawing && e.pointerId == _pointerId) { _drawing = false; Notify(); }
        }

        bool ToPixel(PointerEventData e, out Vector2 pixel)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out var local);
            Rect r = _rect.rect;
            float u = (local.x - r.x) / r.width;
            float v = (local.y - r.y) / r.height;
            pixel = new Vector2(u * textureWidth, v * textureHeight);
            return u >= 0 && u <= 1 && v >= 0 && v <= 1;
        }

        // ───────────── 그리기 ─────────────
        Color32 CurrentColor => tool == PaintTool.Eraser ? Clear32 : Opaque(brushColor);
        static Color32 Opaque(Color32 c) { c.a = 255; return c; }

        void DrawLine(Vector2 a, Vector2 b)
        {
            float dist = Vector2.Distance(a, b);
            float step = Mathf.Max(0.5f, brushSize * 0.35f);
            int count = Mathf.CeilToInt(dist / step);
            for (int i = 1; i <= count; i++)
                Stamp(Vector2.Lerp(a, b, i / (float)count));
        }

        void Stamp(Vector2 center)
        {
            int r = brushSize;
            int cx = Mathf.RoundToInt(center.x), cy = Mathf.RoundToInt(center.y);
            int r2 = r * r;
            Color32 c = CurrentColor;
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= textureHeight) continue;
                int row = y * textureWidth;
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = cx + dx;
                    if (x < 0 || x >= textureWidth) continue;
                    _pixels[row + x] = c;
                }
            }
            _dirty = true;
        }

        void FillAll(Color32 c)
        {
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = c;
            _dirty = true;
        }

        bool Similar(Color32 a, Color32 b)
        {
            if (a.a == 0 || b.a == 0) return a.a == 0 && b.a == 0; // 빈 곳(도화지)끼리만 같은 영역
            return Mathf.Abs(a.r - b.r) <= fillTolerance &&
                   Mathf.Abs(a.g - b.g) <= fillTolerance &&
                   Mathf.Abs(a.b - b.b) <= fillTolerance;
        }

        /// <summary>스캔라인 방식 페인트통 채우기.</summary>
        void FloodFill(int sx, int sy, Color32 fill)
        {
            int w = textureWidth, h = textureHeight;
            if (sx < 0 || sy < 0 || sx >= w || sy >= h) return;
            Color32 target = _pixels[sy * w + sx];
            fill = Opaque(fill);
            if (target.a == 255 && target.r == fill.r && target.g == fill.g && target.b == fill.b) return;

            var visited = new bool[_pixels.Length];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(sx, sy));

            while (stack.Count > 0)
            {
                var p = stack.Pop();
                int x = p.x, y = p.y;
                int row = y * w;
                if (visited[row + x] || !Similar(_pixels[row + x], target)) continue;

                int left = x;
                while (left - 1 >= 0 && !visited[row + left - 1] && Similar(_pixels[row + left - 1], target)) left--;
                int right = x;
                while (right + 1 < w && !visited[row + right + 1] && Similar(_pixels[row + right + 1], target)) right++;

                for (int i = left; i <= right; i++)
                {
                    _pixels[row + i] = fill;
                    visited[row + i] = true;
                    if (y + 1 < h && !visited[row + w + i] && Similar(_pixels[row + w + i], target)) stack.Push(new Vector2Int(i, y + 1));
                    if (y - 1 >= 0 && !visited[row - w + i] && Similar(_pixels[row - w + i], target)) stack.Push(new Vector2Int(i, y - 1));
                }
            }
            _dirty = true;
        }

        // ───────────── 히스토리 ─────────────
        void PushUndo()
        {
            _undo.Add((Color32[])_pixels.Clone());
            if (_undo.Count > maxHistory) _undo.RemoveAt(0);
            _redo.Clear();
        }

        void Notify() => OnStateChanged?.Invoke();
    }
}
