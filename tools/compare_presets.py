r"""
프롬프트(프리셋) 비교: comfyui/presets/compare/*.json 을 같은 그림·같은 seed 로 모두 생성하고
결과를 한 화면(HTML)에 나란히 보여줍니다.

    .venv\Scripts\python.exe tools\compare_presets.py
    .venv\Scripts\python.exe tools\compare_presets.py --dir comfyui\presets\compare --seed 1000
    .venv\Scripts\python.exe tools\compare_presets.py --images C:\내그림폴더     (내 그림으로 비교)

결과: tools/out/compare_<시각>.html  (자동으로 브라우저에서 열림)
"""
import argparse, glob, html, json, os, subprocess, sys, time
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "out")


def newest_sdk_dir(after):
    dirs = [d for d in glob.glob(os.path.join(OUT, "*_sdk")) if os.path.getmtime(d) >= after - 1]
    return max(dirs, key=os.path.getmtime) if dirs else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=os.path.join(ROOT, "comfyui", "presets", "compare"))
    ap.add_argument("--images", default=os.path.join(ROOT, "shared", "test-drawings"))
    ap.add_argument("--workflow", default=os.path.join(ROOT, "comfyui", "workflows", "drawing_to_silkroad.api.json"))
    ap.add_argument("--seed", type=int, default=1000)
    a = ap.parse_args()

    presets = sorted(glob.glob(os.path.join(a.dir, "*.json")))
    if not presets:
        sys.exit("비교할 프리셋이 없어요: " + a.dir)
    images = sorted(x for x in os.listdir(a.images) if x.lower().endswith(".png"))
    print(f"프리셋 {len(presets)}개 × 그림 {len(images)}장 (seed {a.seed})\n")

    cols = []  # (preset 이름, 설명, 프롬프트, 결과 폴더)
    for p in presets:
        d = json.load(open(p, encoding="utf-8"))
        label = os.path.splitext(os.path.basename(p))[0]
        print(f"===== {label} =====")
        t0 = time.time()
        subprocess.run([sys.executable, os.path.join(ROOT, "tools", "comfy_sdk_test.py"),
                        "--preset", p, "--images", a.images, "--workflow", a.workflow,
                        "--seed-base", str(a.seed), "--seeds", "1"], cwd=ROOT)
        cols.append((label, d.get("_note", ""), d.get("positive", ""), d.get("negative", ""),
                     d.get("steps"), d.get("cfg"), newest_sdk_dir(t0)))

    # HTML 비교표
    page = os.path.join(OUT, "compare_" + datetime.now().strftime("%Y%m%d_%H%M%S") + ".html")
    rel = lambda path: os.path.relpath(path, OUT).replace("\\", "/")
    h = ["<!doctype html><meta charset='utf-8'><title>프롬프트 비교</title>",
         "<style>body{font-family:sans-serif;background:#222;color:#eee;margin:16px}"
         "table{border-collapse:collapse}td,th{padding:6px;vertical-align:top;border:1px solid #444}"
         "img{width:420px;display:block;cursor:zoom-in}th{background:#333;max-width:420px;text-align:left}"
         "details{font-weight:normal;font-size:12px;color:#bbb;white-space:pre-wrap}"
         "small{color:#999;font-weight:normal}</style>",
         "<h2>프롬프트 비교 (seed %d)</h2><p>이미지를 클릭하면 원본 크기로 열려요. 마음에 드는 열 이름을 알려 주세요.</p>" % a.seed,
         "<table><tr><th>원본 그림</th>"]
    for label, note, pos, neg, steps, cfg, _ in cols:
        h.append(f"<th>{html.escape(label)}<br><small>{html.escape(note)} · steps {steps} · cfg {cfg}</small>"
                 f"<details><summary>프롬프트</summary>{html.escape(pos)}\n\n[negative] {html.escape(neg)}</details></th>")
    h.append("</tr>")
    for img in images:
        h.append(f"<tr><td><a href='{rel(os.path.join(a.images, img))}' target='_blank'>"
                 f"<img src='{rel(os.path.join(a.images, img))}'></a><small>{html.escape(img)}</small></td>")
        stem = os.path.splitext(img)[0]
        for *_, folder in cols:
            f = os.path.join(folder, f"{stem}__seed{a.seed}.png") if folder else None
            if f and os.path.exists(f):
                h.append(f"<td><a href='{rel(f)}' target='_blank'><img src='{rel(f)}'></a></td>")
            else:
                h.append("<td>실패 (콘솔 확인)</td>")
        h.append("</tr>")
    h.append("</table>")
    open(page, "w", encoding="utf-8").write("\n".join(h))
    print("\n비교 화면:", page)
    try:
        os.startfile(page)  # Windows: 브라우저로 열기
    except Exception:
        pass


if __name__ == "__main__":
    main()
