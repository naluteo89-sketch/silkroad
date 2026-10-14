using System;
using UnityEngine;

namespace Silkroad
{
    /// <summary>
    /// 6단계(XR 연동) 자리. 체험자가 결과를 확정하면 이 이벤트로 텍스처가 전달됩니다.
    /// XR 전시 면(프로젝터/HMD/LED 등)이 정해지면 이 이벤트를 구독하는 쪽을 구현하세요.
    ///   예) XRResultBridge.OnResultConfirmed += (mine, ai) => wallMaterial.mainTexture = ai;
    /// 네트워크로 다른 기기에 보내야 한다면 여기서 업로드/소켓 전송을 붙이면 됩니다.
    /// </summary>
    public static class XRResultBridge
    {
        public static event Action<Texture2D, Texture2D> OnResultConfirmed;

        /// <summary>마지막으로 확정된 결과 (늦게 구독한 쪽이 가져갈 수 있도록 보관).</summary>
        public static Texture2D LastAiResult { get; private set; }

        public static void Publish(Texture2D mine, Texture2D ai)
        {
            LastAiResult = ai;
            OnResultConfirmed?.Invoke(mine, ai);
        }
    }
}
