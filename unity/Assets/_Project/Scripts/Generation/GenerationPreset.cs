using System;
using System.Globalization;

namespace Silkroad
{
    /// <summary>
    /// 생성 프리셋. comfyui/presets/*.json 과 같은 형식이며
    /// StreamingAssets/ComfyUI/ 로 복사되어 실행 시 읽힙니다 (tools/sync_comfyui.bat).
    /// </summary>
    [Serializable]
    public class GenerationPreset
    {
        public string name = "default";
        public string positive = "persian miniature painting, silk road caravan, intricate ornament, gold leaf, mineral pigments, aged paper";
        public string negative = "photo, 3d render, blurry, lowres, text, watermark, deformed hands";
        public float denoise = 0.75f;
        public int steps = 25;
        public float cfg = 6.5f;
        public float controlStrength = 0.6f;

        /// <summary>워크플로 템플릿의 자리표시자를 이 프리셋 값으로 치환.</summary>
        public string Apply(string workflow, Func<string, string> escape)
        {
            var ci = CultureInfo.InvariantCulture;
            return workflow
                .Replace("{{POSITIVE}}", escape(positive))
                .Replace("{{NEGATIVE}}", escape(negative))
                .Replace("{{DENOISE}}", denoise.ToString(ci))
                .Replace("{{STEPS}}", steps.ToString(ci))
                .Replace("{{CFG}}", cfg.ToString(ci))
                .Replace("{{CONTROL_STRENGTH}}", controlStrength.ToString(ci));
        }
    }
}
