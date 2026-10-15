using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 결과 비교 막대(Divider)에 막대 이미지 적용 (씬 유지, 여러 번 눌러도 안전).
    /// 이미지: Art/UI/compare_bar.png (막대 + 가운데 손잡이 한 장, 같은 이름으로 덮어쓰면 교체)
    /// </summary>
    public static class SilkroadCompareBar
    {
        const float BarHeight = 760f;   // 화면에서 막대 높이 (그림 창 약 687 보다 조금 길게)

        [MenuItem("Silkroad/비교 막대 이미지 적용 (씬 유지)", priority = 16)]
        public static void Apply()
        {
            const string path = "Assets/_Project/Art/UI/compare_bar.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var div = GameObject.Find("SilkroadCanvas")?.transform.Find("ResultPanel/CompareArea/Divider");
            var slider = div ? div.GetComponent<Slider>() : null;
            if (sprite == null || slider == null || slider.handleRect == null)
            { EditorUtility.DisplayDialog("Silkroad", "compare_bar.png 또는 ResultPanel/CompareArea/Divider 를 찾지 못했어요.", "확인"); return; }

            Undo.RegisterFullObjectHierarchyUndo(div.gameObject, "Compare Bar");

            // 손잡이 = 막대 이미지 (그림 창 높이에 맞춰 가운데)
            var h = slider.handleRect;
            float w = BarHeight * sprite.rect.width / sprite.rect.height;
            h.anchorMin = new Vector2(h.anchorMin.x, 0.5f);
            h.anchorMax = new Vector2(h.anchorMax.x, 0.5f);
            h.pivot = new Vector2(0.5f, 0.5f);
            h.sizeDelta = new Vector2(w, BarHeight);
            h.anchoredPosition = new Vector2(h.anchoredPosition.x, 0);
            var img = h.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Simple; img.color = Color.white; img.preserveAspect = true; img.raycastTarget = true;
            var area = div.Find("Handle Slide Area") as RectTransform;
            if (area) { area.offsetMin = Vector2.zero; area.offsetMax = Vector2.zero; }

            // 예전 가는 선/바탕은 투명하게 (바탕은 터치 영역으로만 남김)
            foreach (var n in new[] { "Background", "Fill Area/Fill" })
            {
                var t = div.Find(n);
                var g = t ? t.GetComponent<Image>() : null;
                if (g) { g.color = new Color(1, 1, 1, 0); EditorUtility.SetDirty(g); }
            }
            var cb = slider.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.95f, 0.85f); cb.pressedColor = new Color(0.85f, 0.8f, 0.7f); slider.colors = cb;
            div.SetAsLastSibling();
            EditorUtility.SetDirty(slider); EditorUtility.SetDirty(img); EditorUtility.SetDirty(h);

            EditorSceneManager.MarkSceneDirty(div.gameObject.scene);
            EditorSceneManager.SaveScene(div.gameObject.scene);
            EditorUtility.DisplayDialog("Silkroad", "비교 막대 이미지를 적용했어요 (높이 " + BarHeight + ", 폭 " + Mathf.RoundToInt(w) + ").\n크기는 Editor/SilkroadCompareBar.cs 의 BarHeight 로 조절.", "확인");
        }
    }
}
