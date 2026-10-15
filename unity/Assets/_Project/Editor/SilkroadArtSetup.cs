using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 아트 이미지 적용 (씬 유지). 결과 화면에 액자 이미지를 씌우고,
    /// 비교 영역을 액자 안쪽 투명 창(16:9)에 정확히 맞춥니다.
    /// </summary>
    public static class SilkroadArtSetup
    {
        const string FramePath = "Assets/_Project/Art/Frames/frame_16x9.png";

        // frame_16x9.png (2000×1327) 의 투명 창 위치 (픽셀)
        const float ImgW = 2000f, ImgH = 1327f;
        const float WinX0 = 206f, WinX1 = 1811f, WinY0 = 214f, WinY1 = 1117f; // 1605 × 903 (16:9)

        const float Scale = 0.76f;                 // 화면에서 액자 크기 (1520 × 1008)
        static readonly Vector2 WindowCenter = new Vector2(0, 40); // 비교 영역(창) 중심 위치
        const float ButtonsY = -515f;              // 하단 버튼 y

        [MenuItem("Silkroad/액자 이미지 적용 (씬 유지)", priority = 11)]
        public static void ApplyFrame()
        {
            if (PrepareSprite(FramePath) == null) { EditorUtility.DisplayDialog("Silkroad", FramePath + " 가 없어요.", "확인"); return; }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);

            var canvas = GameObject.Find("SilkroadCanvas");
            var result = canvas ? canvas.transform.Find("ResultPanel") : null;
            var area = result ? result.Find("CompareArea") as RectTransform : null;
            var rv = result ? result.GetComponent<Silkroad.ResultView>() : null;
            if (area == null || rv == null) { EditorUtility.DisplayDialog("Silkroad", "ResultPanel/CompareArea 를 찾지 못했어요. 먼저 '씬 자동 생성'을 해 주세요.", "확인"); return; }

            // 크기 계산: 비교 영역 = 액자 이미지 전체 크기, 그림은 액자 안쪽 창(16:9) 위치
            var areaSize = new Vector2(ImgW * Scale, ImgH * Scale);
            var winSize = new Vector2((WinX1 - WinX0) * Scale, (WinY1 - WinY0) * Scale);
            var winOffset = new Vector2(((WinX0 + WinX1) / 2f - ImgW / 2f) * Scale,
                                        -((WinY0 + WinY1) / 2f - ImgH / 2f) * Scale); // 영역 중심 → 창 중심

            Undo.RegisterFullObjectHierarchyUndo(result.gameObject, "Frame");

            // 1) 비교 영역
            area.sizeDelta = areaSize;
            area.anchoredPosition = WindowCenter - winOffset;

            // 2) 내 그림: 창 위치에만 (액자 없음)
            Place((RectTransform)area.Find("MyDrawing"), winSize, winOffset);

            // 3) AI 층: 마스크 안에 [AI 결과 + 액자] → 막대를 움직이면 액자도 같이 드러남
            var mask = (RectTransform)area.Find("AIMask");
            var layer = mask.Find("AILayer") as RectTransform;
            if (layer == null)
            {
                layer = new GameObject("AILayer", typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(mask, false);
            }
            layer.anchorMin = new Vector2(1, 0); layer.anchorMax = new Vector2(1, 1);
            layer.pivot = new Vector2(1, 0.5f);
            layer.anchoredPosition = Vector2.zero;
            layer.sizeDelta = new Vector2(areaSize.x, 0);

            var ai = rv.aiImage.rectTransform;
            ai.SetParent(layer, false);
            Place(ai, winSize, winOffset);

            var frameTr = layer.Find("FrameArt");
            var frame = frameTr ? frameTr.gameObject : new GameObject("FrameArt", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(layer, false);
            frame.transform.SetAsLastSibling();
            var frt = (RectTransform)frame.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var img = frame.GetComponent<Image>();
            img.sprite = sprite; img.color = Color.white; img.raycastTarget = false; img.preserveAspect = false;

            rv.aiLayer = layer;
            EditorUtility.SetDirty(rv);

            // 4) '내 그림' / 'AI 결과' 글씨 제거, 막대(Divider)는 맨 위 (드래그 받도록)
            foreach (var labelName in new[] { "MineLabel", "AILabel" })
            {
                var label = area.Find(labelName);
                if (label) Object.DestroyImmediate(label.gameObject);
            }
            var div = area.Find("Divider");
            if (div) div.SetAsLastSibling();

            // 5) 예전 버전에서 만든 바깥 액자 / 임시 테두리 정리
            var oldFrame = result.Find("FrameArt");
            if (oldFrame) Object.DestroyImmediate(oldFrame.gameObject);
            var border = result.Find("CompareBorder");
            if (border) border.gameObject.SetActive(false);

            // 6) 하단 버튼
            foreach (var name in new[] { "HomeButton", "RetryButton", "EditButton", "SaveButton" })
            {
                var b = result.Find(name) as RectTransform;
                if (!b) continue;
                b.anchoredPosition = new Vector2(b.anchoredPosition.x, ButtonsY);
                b.SetAsLastSibling();
            }

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            EditorUtility.DisplayDialog("Silkroad",
                "액자를 'AI 결과' 쪽에 넣었어요.\n\n- 결과가 오면 막대가 오른쪽→가운데로 오며 액자 그림이 드러나요\n- 막대를 좌우로 끌면 내 그림(액자 없음) ↔ AI 결과(액자) 비교\n- 저장 누르면 액자 그림 전체가 보여요", "확인");
        }

        static void Center(RectTransform r)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        }

        static void Place(RectTransform r, Vector2 size, Vector2 pos)
        {
            if (!r) return;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = pos;
        }

        /// <summary>PNG 를 UI 스프라이트로 설정 (고화질, 투명 유지)</summary>
        static Texture2D PrepareSprite(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            bool changed = imp.textureType != TextureImporterType.Sprite || imp.maxTextureSize < 2048 ||
                           imp.textureCompression != TextureImporterCompression.CompressedHQ || imp.mipmapEnabled;
            if (changed)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
