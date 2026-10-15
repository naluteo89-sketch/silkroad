using System;
using System.Collections.Generic;
using UnityEngine;

namespace Silkroad
{
    /// <summary>
    /// 결과 화면에 씌울 액자 목록. Assets/_Project/Art/Frames/ 에 PNG 를 넣으면
    /// 에디터가 자동으로 이 목록을 갱신합니다 (메뉴: Silkroad → 액자 목록 갱신).
    /// 각 액자의 '가운데 투명 창' 위치는 자동으로 찾아서 저장됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "FrameLibrary", menuName = "Silkroad/Frame Library")]
    public class FrameLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public Sprite sprite;
            [Tooltip("원본 이미지 크기 (픽셀)")]
            public Vector2Int size;
            [Tooltip("가운데 투명 창 (픽셀, 왼쪽 아래 기준)")]
            public RectInt window;
            [Tooltip("체크 해제하면 랜덤 후보에서 빠짐")]
            public bool enabled = true;
        }

        [Tooltip("체크: 결과마다 랜덤 액자 / 해제: 첫 번째 액자만")]
        public bool random = true;
        public List<Entry> frames = new List<Entry>();

        /// <summary>사용 가능한 액자 중 하나를 고름. 직전과 같은 액자는 가능하면 피함.</summary>
        public Entry Pick(ref int last)
        {
            var pool = new List<int>();
            for (int i = 0; i < frames.Count; i++)
                if (frames[i] != null && frames[i].enabled && frames[i].sprite != null && frames[i].window.width > 0) pool.Add(i);
            if (pool.Count == 0) return null;
            int pick = pool[0];
            if (random && pool.Count > 1)
            {
                pool.Remove(last);
                pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            last = pick;
            return frames[pick];
        }
    }
}
