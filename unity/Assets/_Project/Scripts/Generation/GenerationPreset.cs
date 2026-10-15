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
        /// <summary>결과를 가져올 SaveImage 노드 ID. 비어 있으면 ComfyUIClient.outputNodeId 사용.</summary>
        public string outputNode = "";
        public string positive = "persian miniature painting, silk road caravan, intricate ornament, gold leaf, mineral pigments, aged paper";
        public string negative = "photo, 3d render, blurry, lowres, text, watermark, deformed hands";
        public float denoise = 0.75f;
        public int steps = 25;
        public float cfg = 6.5f;
        public float controlStrength = 0.6f;
        /// <summary>0 보다 크면 고정 seed: 새 그림 = seed, 재요청 = seed+1, +2 … / 0 이면 매번 랜덤.</summary>
        public long seed = 0;

        [Serializable]
        public class Theme
        {
            public string name;
            [UnityEngine.TextArea(2, 6)] public string prompt;
        }

        /// <summary>재요청 때 랜덤으로 하나 골라 positive 의 {{THEME}} 자리에 넣는 테마들. 처음 보내기는 빈칸(기본 테마).</summary>
        public Theme[] themes = new Theme[0];
        /// <summary>{{THEME}} 자리가 비었을 때(처음 보내기) 넣는 기본 테마 문장.</summary>
        public string defaultTheme = "";

        /// <summary>positive 의 {{THEME}} 자리에 테마 문장 삽입 (빈칸이면 기본 테마). 자리가 없으면 끝에 붙임.</summary>
        public string BuildPositive(string theme)
        {
            string pos = positive ?? "";
            theme = (theme ?? "").Trim();
            if (theme.Length == 0) theme = (defaultTheme ?? "").Trim();
            if (pos.Contains("{{THEME}}")) pos = pos.Replace("{{THEME}}", theme);
            else if (theme.Length > 0) pos = pos.TrimEnd() + " " + theme;
            return System.Text.RegularExpressions.Regex.Replace(pos, "[ ]{2,}", " ").Trim();
        }

        public string OutputNodeOr(string fallback) => string.IsNullOrEmpty(outputNode) ? fallback : outputNode;

        /// <summary>워크플로 템플릿의 자리표시자를 이 프리셋 값으로 치환.</summary>
        public string Apply(string workflow, Func<string, string> escape, string theme = "")
        {
            var ci = CultureInfo.InvariantCulture;
            return workflow
                .Replace("{{POSITIVE}}", escape(BuildPositive(theme)))
                .Replace("{{NEGATIVE}}", escape(negative))
                .Replace("{{DENOISE}}", denoise.ToString(ci))
                .Replace("{{STEPS}}", steps.ToString(ci))
                .Replace("{{CFG}}", cfg.ToString(ci))
                .Replace("{{CONTROL_STRENGTH}}", controlStrength.ToString(ci));
        }
    }
}
