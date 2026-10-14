using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>"생성 중..." 화면. 아이콘이 통통 튀고 점이 늘어납니다.</summary>
    public class LoadingView : MonoBehaviour
    {
        public RectTransform icon;
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
            errorText.gameObject.SetActive(false);
            backButton.GetComponentInChildren<Text>().text = "취소";
        }

        public void ShowError(string msg, string backLabel = "돌아가기")
        {
            _loading = false;
            messageText.gameObject.SetActive(false);
            if (icon) icon.gameObject.SetActive(false);
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
                icon.anchoredPosition = _iconBase + new Vector2(0, Mathf.Abs(Mathf.Sin(t * 3f)) * 20f);
                icon.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3f) * 8f);
            }
            int dots = (int)(t * 2f) % 4;
            messageText.text = message + new string('.', dots);
        }
    }
}
