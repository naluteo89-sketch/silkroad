using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>"생성 중..." 화면. 아이콘이 통통 튀고 점이 늘어납니다.</summary>
    public class LoadingView : MonoBehaviour
    {
        public RectTransform icon;
        [Tooltip("(선택) 아이콘 뒤에서 천천히 도는 장식 고리")]
        public RectTransform ring;
        public Text messageText;
        public Text errorText;
        public Button backButton;

        public string message = "생성 준비 중";

        bool _loading;
        Vector2 _iconBase;

        void Awake()
        {
            if (icon) _iconBase = icon.anchoredPosition;
        }

        /// <summary>단계 표시: 업로드 중 / 대기 중 / 생성 중 / 결과 받는 중</summary>
        public void SetStatus(string status) { message = status; }

        public void ShowLoading()
        {
            message = "생성 준비 중";
            _loading = true;
            messageText.gameObject.SetActive(true);
            if (icon) icon.gameObject.SetActive(true);
            if (ring) ring.gameObject.SetActive(true);
            errorText.gameObject.SetActive(false);
            backButton.GetComponentInChildren<Text>().text = "취소";
        }

        public void ShowError(string msg, string backLabel = "돌아가기")
        {
            _loading = false;
            messageText.gameObject.SetActive(false);
            if (icon) icon.gameObject.SetActive(false);
            if (ring) ring.gameObject.SetActive(false);
            errorText.gameObject.SetActive(true);
            errorText.text = "생성에 실패했어요.\n\n" + msg;
            backButton.GetComponentInChildren<Text>().text = backLabel;
        }

        void Update()
        {
            if (!_loading) return;
            float t = Time.unscaledTime;
            if (icon)
            {
                // 낙타가 걷는 것처럼 살짝 위아래로 흔들림
                icon.anchoredPosition = _iconBase + new Vector2(0, Mathf.Abs(Mathf.Sin(t * 4f)) * 10f);
                icon.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 4f) * 3f);
            }
            if (ring) ring.localRotation = Quaternion.Euler(0, 0, -t * 60f); // 밝은 구슬이 시계 방향으로 돎
            int dots = (int)(t * 2f) % 4;
            messageText.text = message + new string('.', dots);
        }
    }
}
