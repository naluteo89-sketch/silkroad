using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>
    /// 태블릿에서 직접 ComfyUI 서버 주소 / 더미 모드를 바꾸는 설정 창.
    /// (다시 빌드하지 않아도 PC 의 IP 가 바뀌면 여기서 고치면 됨)
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        public ComfyUIClient comfy;
        public Button openButton;     // 메인 화면의 "설정" 버튼
        public InputField urlInput;
        public Toggle dummyToggle;
        public Button testButton;
        public Button saveButton;
        public Button closeButton;
        public Text statusText;

        void Awake()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            saveButton.onClick.AddListener(Save);
            testButton.onClick.AddListener(Test);
        }

        void Start() => gameObject.SetActive(false);

        public void Open()
        {
            gameObject.SetActive(true);
            urlInput.text = comfy.serverUrl;
            dummyToggle.isOn = comfy.useDummy;
            statusText.text = "";
        }

        void Close() => gameObject.SetActive(false);

        void Save()
        {
            comfy.SaveSettings(urlInput.text, dummyToggle.isOn);
            statusText.text = "저장했어요. (" + (comfy.useDummy ? "더미 모드" : comfy.serverUrl) + ")";
        }

        void Test()
        {
            statusText.text = "확인 중...";
            comfy.TestConnection(urlInput.text, (ok, msg) => { if (statusText) statusText.text = msg; });
        }
    }
}
