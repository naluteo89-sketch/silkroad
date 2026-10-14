using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 그림판 화면 디자인 적용 (씬 유지):
    ///  - 브러시/지우개/채우기/취소/복원, 보내기/지우기/메인 → 메달 아이콘 버튼 + 아래 글자
    ///  - 팔레트 → 붓 자국 모양, 굵기 → '&lt;' 쐐기 모양 슬라이더
    /// 아이콘은 Art/UI 폴더 PNG (tools/make_ui_icons.py 로 생성, 같은 이름으로 덮어쓰면 교체)
    /// </summary>
    public static class SilkroadPaintUISetup
    {
        const string UI = "Assets/_Project/Art/UI/";
        static readonly Color Ink = new Color32(70, 42, 18, 255);
        static readonly Color WedgeColor = new Color32(92, 62, 24, 255);

        [MenuItem("Silkroad/그림판 UI 디자인 적용 (씬 유지)", priority = 13)]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(UI.TrimEnd('/'), ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            var canvas = GameObject.Find("SilkroadCanvas");
            var pp = canvas ? canvas.transform.Find("PaintPanel") : null;
            if (pp == null) { EditorUtility.DisplayDialog("Silkroad", "PaintPanel 을 찾지 못했어요.", "확인"); return; }
            Undo.RegisterFullObjectHierarchyUndo(pp.gameObject, "Paint UI");

            var ring = S("ring_selected");
            // ── 상단 도구 (x, y 는 1920×1080 기준 UI 좌표) ──
            const float topY = 470f;
            Tool(pp, "BrushButton", "btn_brush", new Vector2(-830, topY), 96, ring);
            Tool(pp, "EraserButton", "btn_eraser", new Vector2(-724, topY), 96, ring);
            Tool(pp, "FillButton", "btn_fill", new Vector2(-618, topY), 96, ring);
            Tool(pp, "UndoButton", "btn_undo", new Vector2(-512, topY), 96, null);
            Tool(pp, "RedoButton", "btn_redo", new Vector2(-406, topY), 96, null);

            // ── 오른쪽 버튼 ──
            Tool(pp, "SendButton", "btn_send", new Vector2(770, 150), 150, null, 30);
            Tool(pp, "HomeButton", "btn_home", new Vector2(770, -130), 120, null, 28);

            // '모두 지우기' = 지우개 바로 아래에 지우개를 골랐을 때만 나타남 (PaintToolbar 가 표시/숨김)
            Tool(pp, "ClearButton", "btn_clear", new Vector2(-724, 345), 84, null, 22);
            var clear = pp.Find("ClearButton");
            if (clear)
            {
                var t = clear.GetComponentInChildren<Text>(true); if (t) t.text = "모두 지우기";
                clear.SetAsLastSibling(); // 캔버스 위에 떠 있도록
                clear.gameObject.SetActive(false);
            }

            // ── 굵기: '<' 쐐기 슬라이더 ──
            var title = pp.Find("SizeTitle") as RectTransform;
            if (title) { title.anchoredPosition = new Vector2(-320, 448); title.sizeDelta = new Vector2(70, 40); }
            var sl = pp.Find("SizeSlider") as RectTransform;
            if (sl)
            {
                sl.anchoredPosition = new Vector2(-165, 448); sl.sizeDelta = new Vector2(230, 56);
                var slider = sl.GetComponent<Slider>();
                var bg = sl.Find("Background") as RectTransform;
                if (bg)
                {
                    bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
                    var bi = bg.GetComponent<Image>(); bi.sprite = S("size_wedge"); bi.type = Image.Type.Simple; bi.color = WedgeColor;
                }
                var fill = sl.Find("Fill Area"); if (fill) fill.gameObject.SetActive(false);
                slider.fillRect = null;
                var ha = sl.Find("Handle Slide Area") as RectTransform;
                if (ha) { ha.offsetMin = new Vector2(14, 0); ha.offsetMax = new Vector2(-14, 0); }
                if (slider.handleRect)
                {
                    slider.handleRect.sizeDelta = new Vector2(48, 0);
                    var hi = slider.handleRect.GetComponent<Image>(); hi.sprite = S("size_knob"); hi.type = Image.Type.Simple; hi.preserveAspect = true; hi.color = Color.white;
                }
                EditorUtility.SetDirty(slider);
            }
            var val = pp.Find("SizeValue") as RectTransform;
            if (val) { val.anchoredPosition = new Vector2(-25, 448); val.sizeDelta = new Vector2(50, 40); }

            // ── 현재 색: 큰 붓 자국 ──
            var cur = pp.Find("CurrentColor") as RectTransform;
            if (cur)
            {
                cur.anchoredPosition = new Vector2(60, 448); cur.sizeDelta = new Vector2(120, 56);
                var ci = cur.GetComponent<Image>(); ci.sprite = S("palette_stroke"); ci.type = Image.Type.Simple; ci.preserveAspect = true;
            }

            // ── 팔레트: 붓 자국 7×3 ──
            var pal = pp.Find("Palette") as RectTransform;
            if (pal)
            {
                pal.anchoredPosition = new Vector2(380, 448); pal.sizeDelta = new Vector2(514, 100);
                var grid = pal.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(70, 30); grid.spacing = new Vector2(4, 5);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 7;
                var stroke = S("palette_stroke");
                foreach (Transform c in pal)
                {
                    var img = c.GetComponent<Image>();
                    img.sprite = stroke; img.type = Image.Type.Simple; img.preserveAspect = false;
                    var ol = c.GetComponent<Outline>(); if (ol) ol.enabled = false;
                    var btn = c.GetComponent<Button>();
                    var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1, 1, 1, 0.85f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1); btn.colors = cb;
                    btn.transition = Selectable.Transition.None; // 색 자체가 버튼 색이므로 틴트 끔
                }
            }

            // ── 선택 표시: 색 틴트 대신 금빛 테두리 ──
            var tb = pp.GetComponent<Silkroad.PaintToolbar>();
            if (tb) { tb.normalColor = Color.white; tb.selectedColor = Color.white; EditorUtility.SetDirty(tb); }

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            EditorUtility.DisplayDialog("Silkroad", "그림판 디자인을 적용했어요.\n아이콘은 Art/UI 의 PNG 를 같은 이름으로 바꾸면 교체됩니다.", "확인");
        }

        static Sprite S(string name)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(UI + name + ".png");
            if (sp == null) Debug.LogWarning("[Silkroad] 스프라이트 없음: " + UI + name + ".png");
            return sp;
        }

        static void Tool(Transform panel, string name, string icon, Vector2 pos, float size, Sprite ring, int fontSize = 24)
        {
            var rt = panel.Find(name) as RectTransform;
            if (!rt) return;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            var img = rt.GetComponent<Image>();
            img.sprite = S(icon); img.type = Image.Type.Simple; img.preserveAspect = true; img.color = Color.white;
            var btn = rt.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.97f, 0.9f); cb.pressedColor = new Color(0.82f, 0.78f, 0.7f);
            cb.selectedColor = Color.white; cb.disabledColor = new Color(1, 1, 1, 0.35f);
            btn.colors = cb;

            // 글자는 아이콘 아래
            var label = rt.GetComponentInChildren<Text>(true);
            if (label)
            {
                var lr = label.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0f); lr.pivot = new Vector2(0.5f, 1f);
                lr.anchoredPosition = new Vector2(0, -2); lr.sizeDelta = new Vector2(size + 60, fontSize + 12);
                label.fontSize = fontSize; label.color = Ink; label.alignment = TextAnchor.UpperCenter; label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            // 선택 테두리 (브러시/지우개/채우기만)
            var old = rt.Find("Ring");
            if (ring != null)
            {
                var r = old ? old.gameObject : new GameObject("Ring", typeof(RectTransform), typeof(Image));
                r.transform.SetParent(rt, false);
                var rr = (RectTransform)r.transform;
                rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
                rr.offsetMin = new Vector2(-10, -10); rr.offsetMax = new Vector2(10, 10);
                var ri = r.GetComponent<Image>(); ri.sprite = ring; ri.raycastTarget = false; ri.preserveAspect = true;
                r.transform.SetSiblingIndex(0);
                r.SetActive(name == "BrushButton");
            }
            else if (old) Object.DestroyImmediate(old.gameObject);
        }
    }
}
