using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Silkroad
{
    /// <summary>
    /// 두루마리 연출. 패널이 켜질 때
    ///  1) (선택) 종이가 왼쪽 롤러에서 오른쪽으로 펼쳐지고
    ///  2) 제목·버튼 같은 내용이 서서히 나타납니다.
    /// 패널에 RectMask2D 가 있어야 펼침이 보입니다 (메뉴 'Silkroad → 두루마리 연출 추가' 가 자동 설정).
    /// </summary>
    [RequireComponent(typeof(RectMask2D))]
    public class ScrollReveal : MonoBehaviour
    {
        [Header("펼침")]
        public bool unroll = true;
        public float unrollSeconds = 0.9f;
        [Tooltip("펼치기 시작 위치 (패널 왼쪽에서의 비율, 롤러 위치)")]
        [Range(0f, 0.5f)] public float startRatio = 0.08f;
        [Tooltip("(선택) 펼쳐지는 끝을 따라 움직이는 말린 종이 롤러")]
        public RectTransform travelRoller;
        [Tooltip("(선택) 펼쳐지는 종이(Frame). 있으면 롤러가 이 종이의 왼쪽 끝→오른쪽 끝으로 이동")]
        public RectTransform sheet;

        [Header("내용 나타나기")]
        public float fadeDelay = 0.15f;
        public float fadeSeconds = 0.6f;
        [Tooltip("하나씩 순서대로 나타나게 할 간격(초). 0 이면 동시에")]
        public float stagger = 0.05f;
        public GameObject[] fadeTargets;

        RectMask2D _mask;
        RectTransform _rt;
        CanvasGroup[] _groups;

        void Awake()
        {
            _mask = GetComponent<RectMask2D>();
            _rt = (RectTransform)transform;
            _groups = new CanvasGroup[fadeTargets != null ? fadeTargets.Length : 0];
            for (int i = 0; i < _groups.Length; i++)
            {
                if (fadeTargets[i] == null) continue;
                var g = fadeTargets[i].GetComponent<CanvasGroup>();
                if (g == null) g = fadeTargets[i].AddComponent<CanvasGroup>();
                _groups[i] = g;
            }
        }

        /// <summary>처음부터 다시 펼치기 (홈으로 돌아올 때 등).</summary>
        public void Replay()
        {
            if (!isActiveAndEnabled) return;
            OnEnable();
        }

        void OnEnable()
        {
            StopAllCoroutines();
            SetFade(0f);
            SetUnroll(unroll ? 0f : 1f);
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            yield return null; // 레이아웃 크기 확정 대기
            if (unroll)
            {
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, unrollSeconds))
                {
                    SetUnroll(1f - Mathf.Pow(1f - t, 3f)); // 끝에서 감속
                    yield return null;
                }
                SetUnroll(1f);
            }

            yield return new WaitForSecondsRealtime(fadeDelay);
            float total = fadeSeconds + stagger * Mathf.Max(0, _groups.Length - 1);
            for (float e = 0; e < total; e += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < _groups.Length; i++)
                    if (_groups[i]) _groups[i].alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((e - i * stagger) / fadeSeconds));
                yield return null;
            }
            SetFade(1f);
        }

        void SetUnroll(float k)
        {
            Rect r = _rt.rect;
            if (travelRoller != null && sheet != null)
            {
                // 종이 왼쪽 끝 → 오른쪽 끝으로 롤러가 굴러가며 펼쳐짐. 롤러 오른쪽은 아직 말려 있어 안 보임
                float left = sheet.anchoredPosition.x - sheet.rect.width * sheet.pivot.x;
                float right = left + sheet.rect.width;
                float edge = Mathf.Lerp(left + sheet.rect.width * startRatio, right, k);
                travelRoller.anchoredPosition = new Vector2(edge, travelRoller.anchoredPosition.y);
                travelRoller.gameObject.SetActive(k < 0.999f);
                float cut = r.xMax - (edge + travelRoller.rect.width * 0.5f);
                _mask.padding = new Vector4(0, 0, k >= 0.999f ? 0 : Mathf.Max(0, cut), 0);
                return;
            }
            float w = r.width;
            float visible = Mathf.Lerp(w * startRatio, w, k);
            _mask.padding = new Vector4(0, 0, Mathf.Max(0, w - visible), 0);
        }

        void SetFade(float a)
        {
            if (_groups == null) return;
            foreach (var g in _groups)
            {
                if (!g) continue;
                g.alpha = a;
                g.interactable = a >= 1f;
                g.blocksRaycasts = a >= 1f;
            }
        }

        void OnDisable() => SetUnroll(1f);
    }
}
