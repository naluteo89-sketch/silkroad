using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 그림판 화면 가로 배치 (씬 유지). 숫자만 바꾸고 메뉴를 다시 누르면 됨 (몇 번 눌러도 같은 결과).
    /// 기준: 두루마리 종이 x -900 ~ 900, 왼쪽 롤러가 -840 까지 덮음.
    /// </summary>
    public static class SilkroadPaintLayout
    {
        const float CanvasX = -60f;   // 그림 그리는 영역(+테두리, 안내 문구) 가운데 x  (예전 -110)
        const float RightButtonsX = 800f; // 보내기 / 지우기 / 메인 버튼 x  (예전 770)

        static readonly string[] CanvasParts = { "CanvasBorder", "DrawingCanvas", "Toast" };
        static readonly string[] RightButtons = { "SendButton", "ClearButton", "HomeButton" };

        [MenuItem("Silkroad/그림판 배치 조정 (씬 유지)", priority = 15)]
        public static void Apply()
        {
            var pp = GameObject.Find("SilkroadCanvas")?.transform.Find("PaintPanel");
            if (pp == null) { EditorUtility.DisplayDialog("Silkroad", "PaintPanel 을 찾지 못했어요.", "확인"); return; }
            Undo.RegisterFullObjectHierarchyUndo(pp.gameObject, "Paint Layout");
            foreach (var n in CanvasParts) SetX(pp, n, CanvasX);
            foreach (var n in RightButtons) SetX(pp, n, RightButtonsX);
            EditorSceneManager.MarkSceneDirty(pp.gameObject.scene);
            EditorSceneManager.SaveScene(pp.gameObject.scene);
            EditorUtility.DisplayDialog("Silkroad", "그림판 영역 x = " + CanvasX + ", 오른쪽 버튼 x = " + RightButtonsX + " 로 옮겼어요.\n더 옮기려면 Editor/SilkroadPaintLayout.cs 의 숫자를 바꾸고 다시 누르세요.", "확인");
        }

        static void SetX(Transform parent, string name, float x)
        {
            var r = parent.Find(name) as RectTransform;
            if (r == null) return;
            r.anchoredPosition = new Vector2(x, r.anchoredPosition.y);
            EditorUtility.SetDirty(r);
        }
    }
}
