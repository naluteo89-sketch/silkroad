using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 이미 만든 씬(직접 꾸민 것 포함)에 두루마리 연출만 추가합니다. 씬을 다시 만들지 않음.
    /// 여러 번 눌러도 안전 (이미 있으면 갱신).
    /// </summary>
    public static class SilkroadScrollSetup
    {
        static readonly HashSet<string> Background = new HashSet<string> { "Frame", "Roller", "CanvasBorder", "CompareBorder", "DrawingCanvas", "CompareArea", "Window" };

        [MenuItem("Silkroad/두루마리 연출 추가 (씬 유지)", priority = 10)]
        public static void Apply()
        {
            var canvas = GameObject.Find("SilkroadCanvas");
            if (canvas == null) { EditorUtility.DisplayDialog("Silkroad", "SilkroadCanvas 가 없어요. 먼저 '씬 자동 생성'을 해 주세요.", "확인"); return; }

            // 시작 화면: 펼침 + 제목/버튼 순서대로 등장
            Setup(canvas.transform.Find("MainPanel"), unroll: true);
            // 그림판: 도구들이 나타남 (영상처럼)
            Setup(canvas.transform.Find("PaintPanel"), unroll: false);
            Setup(canvas.transform.Find("ResultPanel"), unroll: false);

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            EditorUtility.DisplayDialog("Silkroad",
                "두루마리 연출을 추가했어요.\n\n- 시작 화면: 종이 펼침 + 제목·버튼 등장\n- 그림판/결과: 버튼 등장\n- 각 화면 왼쪽 'Roller' = 두루마리 막대 자리 (이미지 교체)\n\n속도는 각 Panel 의 ScrollReveal 에서 조절.", "확인");
        }

        static void Setup(Transform panel, bool unroll)
        {
            if (panel == null) return;
            if (!panel.GetComponent<RectMask2D>()) Undo.AddComponent<RectMask2D>(panel.gameObject);
            var sr = panel.GetComponent<ScrollReveal>();
            if (!sr) sr = Undo.AddComponent<ScrollReveal>(panel.gameObject);
            sr.unroll = unroll;

            var frame = panel.Find("Frame") as RectTransform;
            if (frame && !panel.Find("Roller"))
            {
                // 두루마리 막대 자리 (임시 갈색 막대 → 나중에 이미지로 교체)
                var go = new GameObject("Roller", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Roller");
                go.transform.SetParent(panel, false);
                go.transform.SetSiblingIndex(frame.GetSiblingIndex() + 1);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(90, frame.sizeDelta.y + 50);
                rt.anchoredPosition = frame.anchoredPosition + new Vector2(-frame.sizeDelta.x / 2f + 20, 0);
                var img = go.GetComponent<Image>();
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                img.type = Image.Type.Sliced;
                img.color = new Color(0.62f, 0.45f, 0.26f);
                img.raycastTarget = false;
            }

            var list = new List<GameObject>();
            foreach (Transform c in panel)
                if (!Background.Contains(c.name)) list.Add(c.gameObject);
            sr.fadeTargets = list.ToArray();
            EditorUtility.SetDirty(sr);
        }
    }
}
