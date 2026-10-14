using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>
    /// 한글이 □□ 로 깨지지 않도록 실행 시 OS의 한글 폰트(맑은 고딕 등)를 모든 Text 에 적용.
    /// 나중에 직접 만든 폰트를 쓰고 싶으면 customFont 에 넣으세요.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class KoreanFontFixer : MonoBehaviour
    {
        public Font customFont;
        public string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular", "NanumGothic", "Noto Sans CJK KR" };

        void Awake()
        {
            Font f = customFont != null ? customFont : Font.CreateDynamicFontFromOSFont(osFontNames, 32);
            if (f == null) return;
            foreach (var t in GetComponentsInChildren<Text>(true)) t.font = f;
        }
    }
}
