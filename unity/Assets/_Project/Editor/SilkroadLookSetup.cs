using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 시작하기 버튼 / 로딩(낙타 + 도는 고리) / 공통 두루마리 적용 (씬 유지, 여러 번 눌러도 안전).
    /// 두루마리: 모든 화면 뒤에 '종이 한 장(ScrollStage)'을 두고, 메인 화면에서 왼쪽→오른쪽으로 펼침.
    /// 다 펼쳐진 종이가 그대로 그림판·로딩·결과 화면의 바탕이 됨 → 시작하기를 눌러도 틀이 움직이지 않음.
    /// 이미지: Art/UI/btn_start, loading_ring, loading_camel / Art/Scroll/scroll_sheet, scroll_roller
    /// </summary>
    public static class SilkroadLookSetup
    {
        static readonly string[] Panels = { "MainPanel", "PaintPanel", "LoadingPanel", "ResultPanel" };
        static readonly string[] OldBackground = { "Frame", "Roller", "RollerMoving" };

        [MenuItem("Silkroad/시작·로딩·두루마리 디자인 적용 (씬 유지)", priority = 14)]
        public static void Apply()
        {
            AssetDatabase.ImportAsset("Assets/_Project/Art", ImportAssetOptions.ImportRecursive);
            var canvas = GameObject.Find("SilkroadCanvas");
            if (canvas == null) { EditorUtility.DisplayDialog("Silkroad", "SilkroadCanvas 를 찾지 못했어요.", "확인"); return; }
            Undo.RegisterFullObjectHierarchyUndo(canvas, "Silkroad Look");
            var sheetSprite = S("Scroll/scroll_sheet");
            var rollerSprite = S("Scroll/scroll_roller");

            // ── 기준 크기: 그림판 화면의 Frame (그림판 배치가 이 종이에 맞춰져 있음) ──
            Vector2 sheetPos = Vector2.zero, sheetSize = new Vector2(1800, 1020);
            var paintFrame = canvas.transform.Find("PaintPanel/Frame") as RectTransform;
            if (paintFrame) { sheetPos = paintFrame.anchoredPosition; sheetSize = paintFrame.sizeDelta; }

            // ── 공통 두루마리 (모든 패널 뒤) ──
            var stage = canvas.transform.Find("ScrollStage") as RectTransform;
            if (!stage)
            {
                stage = new GameObject("ScrollStage", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(stage.gameObject, "ScrollStage");
                stage.SetParent(canvas.transform, false);
            }
            // 화면 전체 배경색(Background) 바로 위, 모든 패널 아래
            var bgLayer = canvas.transform.Find("Background");
            stage.SetAsLastSibling(); // 먼저 맨 뒤로 빼서 Background 순서가 흔들리지 않게
            stage.SetSiblingIndex(bgLayer ? bgLayer.GetSiblingIndex() + 1 : 0);
            stage.anchorMin = Vector2.zero; stage.anchorMax = Vector2.one; stage.offsetMin = stage.offsetMax = Vector2.zero;

            var sheet = Child(stage, "Sheet");
            Center(sheet, sheetPos, sheetSize);
            if (sheetSprite) SetImage(sheet, sheetSprite);

            var rollerSize = new Vector2(120, sheetSize.y + 90);
            var left = Child(stage, "RollerLeft");
            Center(left, sheetPos + new Vector2(-sheetSize.x / 2f, 0), rollerSize);
            if (rollerSprite) SetImage(left, rollerSprite);
            var moving = Child(stage, "RollerMoving");
            Center(moving, sheetPos + new Vector2(sheetSize.x / 2f, 0), rollerSize);
            if (rollerSprite) SetImage(moving, rollerSprite);
            left.SetAsLastSibling(); moving.SetAsLastSibling();

            var sr = stage.GetComponent<ScrollReveal>();
            if (!sr) sr = Undo.AddComponent<ScrollReveal>(stage.gameObject);
            sr.unroll = true;
            sr.unrollSeconds = 1.4f;
            sr.startRatio = 0.04f;
            sr.sheet = sheet;
            sr.travelRoller = moving;
            sr.fadeTargets = new GameObject[0];
            EditorUtility.SetDirty(sr);

            // ── 각 화면의 예전 배경(Frame/Roller)은 끄고, 화면 자체는 펼침 없이 내용만 나타나게 ──
            foreach (var name in Panels)
            {
                var p = canvas.transform.Find(name);
                if (!p) continue;
                foreach (var bg in OldBackground)
                {
                    var t = p.Find(bg);
                    if (t) { Undo.RecordObject(t.gameObject, "Hide"); t.gameObject.SetActive(false); }
                }
                var psr = p.GetComponent<ScrollReveal>();
                if (psr)
                {
                    psr.unroll = false;
                    psr.travelRoller = null;
                    psr.sheet = null;
                    if (psr.fadeTargets != null) psr.fadeTargets = psr.fadeTargets.Where(g => g && !OldBackground.Contains(g.name)).ToArray();
                    if (name == "MainPanel") psr.fadeDelay = sr.unrollSeconds; // 종이가 다 펼쳐진 뒤 제목·버튼 등장
                    EditorUtility.SetDirty(psr);
                }
            }

            var app = Object.FindAnyObjectByType<AppController>(FindObjectsInactive.Include);
            if (app) { app.scrollStage = sr; EditorUtility.SetDirty(app); }

            // ── 시작하기 버튼 ──
            var start = canvas.transform.Find("MainPanel/StartButton") as RectTransform;
            var startSprite = S("UI/btn_start");
            if (start && startSprite)
            {
                SetImage(start, startSprite);
                start.sizeDelta = new Vector2(560, 171);
                var t = start.GetComponentInChildren<Text>(true);
                if (t)
                {
                    t.color = new Color32(250, 236, 200, 255);
                    t.fontSize = 56;
                    t.fontStyle = FontStyle.Bold;
                    if (!t.GetComponent<Shadow>()) t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
                }
                var b = start.GetComponent<Button>();
                var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.95f, 0.85f); cb.pressedColor = new Color(0.8f, 0.72f, 0.6f); b.colors = cb;
            }

            // ── 로딩: 도는 장식 고리 + 걷는 낙타 ──
            var loading = canvas.transform.Find("LoadingPanel");
            var lv = loading ? loading.GetComponent<LoadingView>() : null;
            if (lv)
            {
                var ringSprite = S("UI/loading_ring");
                var camel = S("UI/loading_camel");
                var icon = lv.icon;
                if (icon && camel) { SetImage(icon, camel); icon.sizeDelta = new Vector2(280, 210); icon.anchoredPosition = new Vector2(0, 110); }
                if (ringSprite)
                {
                    var ring = Child(loading, "Ring");
                    Center(ring, new Vector2(0, 110), new Vector2(420, 420));
                    SetImage(ring, ringSprite);
                    if (icon) ring.SetSiblingIndex(icon.GetSiblingIndex());
                    lv.ring = ring;
                }
                if (lv.messageText) lv.messageText.rectTransform.anchoredPosition = new Vector2(0, -150);
                EditorUtility.SetDirty(lv);
            }

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            EditorUtility.DisplayDialog("Silkroad",
                "적용했어요.\n\n- 모든 화면이 같은 두루마리 종이 한 장 위에 놓여요\n- 메인 화면: 종이가 왼쪽→오른쪽으로 펼쳐진 뒤 제목·버튼 등장\n- 시작하기: 종이는 그대로, 그림판 도구만 나타남\n- 홈으로 돌아오면 다시 펼쳐짐", "확인");
        }

        static RectTransform Child(Transform parent, string name)
        {
            var t = parent.Find(name) as RectTransform;
            if (t) return t;
            t = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(t.gameObject, name);
            t.SetParent(parent, false);
            return t;
        }

        static void Center(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/" + path + ".png");

        static void SetImage(RectTransform r, Sprite s)
        {
            var img = r.GetComponent<Image>();
            if (!img) return;
            img.sprite = s;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.preserveAspect = false;
            img.raycastTarget = r.GetComponent<Button>() != null;
            EditorUtility.SetDirty(img);
        }
    }
}
