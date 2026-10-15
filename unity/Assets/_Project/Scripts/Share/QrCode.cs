using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Silkroad
{
    /// <summary>
    /// 외부 라이브러리 없이 쓰는 작은 QR 코드 생성기.
    /// 바이트 모드, 오류 정정 M, 버전 1~9 (URL 약 180바이트까지). 마스크는 벌점 규칙으로 자동 선택.
    /// </summary>
    public static class QrCode
    {
        static readonly int[] EccPerBlock = { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22 };
        static readonly int[] NumBlocks = { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5 };

        /// <summary>QR 모듈 배열 [y, x] (true = 검정). 너무 길면 null.</summary>
        public static bool[,] Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? "");
            int ver = -1;
            for (int v = 1; v <= 9; v++)
                if (4 + 8 + 8 * data.Length <= DataCodewords(v) * 8) { ver = v; break; }
            if (ver < 0) return null;

            // 1) 데이터 비트
            var bits = new List<bool>();
            void App(int val, int n) { for (int i = n - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0); }
            App(4, 4);
            App(data.Length, 8);
            foreach (byte b in data) App(b, 8);
            int cap = DataCodewords(ver) * 8;
            App(0, Math.Min(4, cap - bits.Count));
            App(0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < cap; pad ^= 0xEC ^ 0x11) App(pad, 8);
            var cw = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++) if (bits[i]) cw[i >> 3] |= (byte)(1 << (7 - (i & 7)));

            // 2) 블록 나누기 + 오류 정정 + 섞기
            int nb = NumBlocks[ver], ecl = EccPerBlock[ver], raw = RawModules(ver) / 8;
            int nShort = nb - raw % nb, shortLen = raw / nb;
            byte[] div = RsDivisor(ecl);
            var blocks = new List<byte[]>();
            for (int i = 0, k = 0; i < nb; i++)
            {
                int dl = shortLen - ecl + (i < nShort ? 0 : 1);
                var dat = new byte[dl];
                Array.Copy(cw, k, dat, 0, dl);
                k += dl;
                byte[] ecc = RsRemainder(dat, div);
                var block = new byte[shortLen + 1];
                Array.Copy(dat, 0, block, 0, dl);
                // 짧은 블록은 dl 자리에 0 을 끼워 길이를 맞춤 (섞을 때 건너뜀)
                Array.Copy(ecc, 0, block, (i < nShort ? dl + 1 : dl), ecl);
                blocks.Add(block);
            }
            var res = new List<byte>();
            for (int i = 0; i < shortLen + 1; i++)
                for (int j = 0; j < blocks.Count; j++)
                    if (i != shortLen - ecl || j >= nShort) res.Add(blocks[j][i]);

            // 3) 기능 패턴 + 데이터 배치
            int size = ver * 4 + 17;
            var mod = new bool[size, size];
            var fn = new bool[size, size];
            void SetF(int x, int y, bool d) { mod[y, x] = d; fn[y, x] = true; }

            for (int i = 0; i < size; i++) { SetF(6, i, i % 2 == 0); SetF(i, 6, i % 2 == 0); }
            void Finder(int cx, int cy)
            {
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int d = Math.Max(Math.Abs(dx), Math.Abs(dy)), xx = cx + dx, yy = cy + dy;
                        if (xx >= 0 && xx < size && yy >= 0 && yy < size) SetF(xx, yy, d != 2 && d != 4);
                    }
            }
            Finder(3, 3); Finder(size - 4, 3); Finder(3, size - 4);
            if (ver >= 2)
            {
                int n = ver / 7 + 2;
                int step = (ver * 8 + n * 3 + 5) / (n * 4 - 4) * 2;
                var pos = new int[n];
                pos[0] = 6;
                for (int i = n - 1, p = size - 7; i >= 1; i--, p -= step) pos[i] = p;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)) continue;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                                SetF(pos[i] + dx, pos[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                    }
            }
            DrawFormat(mod, fn, size, 0);
            if (ver >= 7)
            {
                int rem = ver;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int b = (ver << 12) | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bt = ((b >> i) & 1) != 0;
                    int a = size - 11 + i % 3, c = i / 3;
                    SetF(a, c, bt); SetF(c, a, bt);
                }
            }

            int bitIdx = 0, total = res.Count * 8;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool up = ((right + 1) & 2) == 0;
                        int y = up ? size - 1 - vert : vert;
                        if (!fn[y, x] && bitIdx < total)
                        {
                            mod[y, x] = ((res[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                            bitIdx++;
                        }
                    }
            }

            // 4) 마스크 선택 (벌점이 가장 낮은 것)
            bool[,] best = null;
            int bestPenalty = int.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                var t = (bool[,])mod.Clone();
                ApplyMask(t, fn, size, m);
                DrawFormat(t, fn, size, m);
                int p = Penalty(t, size);
                if (p < bestPenalty) { bestPenalty = p; best = t; }
            }
            return best;
        }

        /// <summary>QR 텍스처 (흰 여백 4칸 포함, 칸당 scale 픽셀, 선명하게 Point 필터).</summary>
        public static Texture2D ToTexture(bool[,] m, int scale = 8)
        {
            int size = m.GetLength(0), border = 4, w = (size + border * 2) * scale;
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "QR" };
            var px = new Color32[w * w];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);
            for (int py = 0; py < w; py++)
                for (int pxl = 0; pxl < w; pxl++)
                {
                    int mx = pxl / scale - border, my = py / scale - border;
                    bool dark = mx >= 0 && my >= 0 && mx < size && my < size && m[size - 1 - my, mx]; // 텍스처는 아래쪽부터
                    px[py * w + pxl] = dark ? black : white;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        static int RawModules(int v)
        {
            int r = (16 * v + 128) * v + 64;
            if (v >= 2)
            {
                int n = v / 7 + 2;
                r -= (25 * n - 10) * n - 55;
                if (v >= 7) r -= 36;
            }
            return r;
        }

        static int DataCodewords(int v) => RawModules(v) / 8 - EccPerBlock[v] * NumBlocks[v];

        static int GfMul(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = ((z << 1) ^ ((z >> 7) * 0x11D)) & 0xFF;
                z ^= ((y >> i) & 1) * x;
            }
            return z;
        }

        static byte[] RsDivisor(int deg)
        {
            var r = new int[deg];
            r[deg - 1] = 1;
            int root = 1;
            for (int i = 0; i < deg; i++)
            {
                for (int j = 0; j < deg; j++)
                {
                    r[j] = GfMul(r[j], root);
                    if (j + 1 < deg) r[j] ^= r[j + 1];
                }
                root = GfMul(root, 0x02);
            }
            var o = new byte[deg];
            for (int i = 0; i < deg; i++) o[i] = (byte)r[i];
            return o;
        }

        static byte[] RsRemainder(byte[] data, byte[] div)
        {
            var r = new int[div.Length];
            foreach (byte b in data)
            {
                int f = b ^ r[0];
                Array.Copy(r, 1, r, 0, r.Length - 1);
                r[r.Length - 1] = 0;
                for (int i = 0; i < r.Length; i++) r[i] ^= GfMul(div[i], f);
            }
            var o = new byte[r.Length];
            for (int i = 0; i < r.Length; i++) o[i] = (byte)r[i];
            return o;
        }

        static void DrawFormat(bool[,] mod, bool[,] fn, int size, int mask)
        {
            int data = (0 << 3) | mask; // 오류 정정 M = 00
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int b = ((data << 10) | rem) ^ 0x5412;
            void S(int x, int y, bool d) { mod[y, x] = d; fn[y, x] = true; }
            for (int i = 0; i <= 5; i++) S(8, i, ((b >> i) & 1) != 0);
            S(8, 7, ((b >> 6) & 1) != 0);
            S(8, 8, ((b >> 7) & 1) != 0);
            S(7, 8, ((b >> 8) & 1) != 0);
            for (int i = 9; i < 15; i++) S(14 - i, 8, ((b >> i) & 1) != 0);
            for (int i = 0; i < 8; i++) S(size - 1 - i, 8, ((b >> i) & 1) != 0);
            for (int i = 8; i < 15; i++) S(8, size - 15 + i, ((b >> i) & 1) != 0);
            S(8, size - 8, true);
        }

        static void ApplyMask(bool[,] mod, bool[,] fn, int size, int mask)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (fn[y, x]) continue;
                    bool inv;
                    switch (mask)
                    {
                        case 0: inv = (x + y) % 2 == 0; break;
                        case 1: inv = y % 2 == 0; break;
                        case 2: inv = x % 3 == 0; break;
                        case 3: inv = (x + y) % 3 == 0; break;
                        case 4: inv = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: inv = x * y % 2 + x * y % 3 == 0; break;
                        case 6: inv = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: inv = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (inv) mod[y, x] = !mod[y, x];
                }
        }

        static readonly bool[] Pat1 = { true, false, true, true, true, false, true, false, false, false, false };

        static int Penalty(bool[,] m, int s)
        {
            int r = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < s; a++)
                {
                    int cnt = 1;
                    for (int i = 1; i <= s; i++)
                    {
                        bool same = i < s && Get(m, pass, a, i) == Get(m, pass, a, i - 1);
                        if (same) cnt++;
                        else { if (cnt >= 5) r += 3 + (cnt - 5); cnt = 1; }
                    }
                    for (int i = 0; i + 11 <= s; i++)
                    {
                        bool f = true, b = true;
                        for (int k = 0; k < 11; k++)
                        {
                            bool v = Get(m, pass, a, i + k);
                            if (v != Pat1[k]) f = false;
                            if (v != Pat1[10 - k]) b = false;
                        }
                        if (f) r += 40;
                        if (b) r += 40;
                    }
                }
            for (int y = 0; y < s - 1; y++)
                for (int x = 0; x < s - 1; x++)
                {
                    bool c = m[y, x];
                    if (c == m[y, x + 1] && c == m[y + 1, x] && c == m[y + 1, x + 1]) r += 3;
                }
            int dark = 0;
            foreach (bool v in m) if (v) dark++;
            int total = s * s;
            r += Math.Abs(dark * 20 - total * 10) / total * 10;
            return r;
        }

        // pass 0 = 가로줄(a = y), pass 1 = 세로줄(a = x)
        static bool Get(bool[,] m, int pass, int a, int i) => pass == 0 ? m[a, i] : m[i, a];
    }
}
