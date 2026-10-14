r"""
공식 comfy-sdk 로 Comfy Cloud 생성 테스트 (인증/업로드/다운로드를 SDK 가 처리).
    pip install comfy-sdk
    python tools/comfy_sdk_test.py                 # 테스트 그림 전부 × seed 1
    python tools/comfy_sdk_test.py --seeds 3       # 품질 검수용 (그림 × 3)
API 키: 환경변수 COMFY_API_KEY → 없으면 C:\keydata\comfy_key.py 의 comfy_key
결과: tools/out/<시각>_sdk/ 에 PNG + results.csv + summary.txt
"""
import argparse, csv, json, os, statistics, sys, tempfile, time
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_key():
    k = os.environ.get("COMFY_API_KEY", "")
    if k:
        return k.strip()
    path = os.environ.get("COMFY_KEY_FILE", r"C:\keydata\comfy_key.py")
    if os.path.exists(path):
        import runpy
        return str(runpy.run_path(path).get("comfy_key", "")).strip()
    return ""


def print_job_error(e):
    """JobFailed 의 실제 원인(노드 오류 메시지 등)을 최대한 출력"""
    job = getattr(e, "job", None)
    for obj in (job, e):
        if obj is None:
            continue
        err = getattr(obj, "error", None)
        if err:
            print("       ▶ error:", (json.dumps(err, ensure_ascii=False, default=str) if not isinstance(err, str) else err)[:1500])
            return
    try:
        print("       ▶ 상세:", json.dumps(vars(e), ensure_ascii=False, default=str)[:1500])
    except Exception:
        print("       ▶ 상세:", repr(e)[:1500])
    jid = getattr(job, "id", None)
    if jid:
        print(f"       ▶ 로그 보기: python tools\\job_info.py {jid}")


def main():
    try:
        from comfy_sdk import Comfy
    except ImportError:
        sys.exit("먼저 설치하세요:  pip install comfy-sdk")

    ap = argparse.ArgumentParser()
    ap.add_argument("--images", default=os.path.join(ROOT, "shared", "test-drawings"))
    ap.add_argument("--workflow", default=os.path.join(ROOT, "comfyui", "workflows", "drawing_to_silkroad.api.json"))
    ap.add_argument("--preset", default=os.path.join(ROOT, "comfyui", "presets", "silkroad_style.json"))
    ap.add_argument("--mapping", default=os.path.join(ROOT, "comfyui", "node-mapping.json"))
    ap.add_argument("--seeds", type=int, default=1)
    ap.add_argument("--seed-base", type=int, default=1000)
    ap.add_argument("--set", action="append", default=[], help="프리셋 덮어쓰기 key=value")
    a = ap.parse_args()

    key = load_key()
    if not key:
        sys.exit(r"API 키를 못 찾았어요: C:\keydata\comfy_key.py 의 comfy_key 확인")
    os.environ.setdefault("COMFY_BASE_URL", "https://cloud.comfy.org")

    mapping = json.load(open(a.mapping, encoding="utf-8"))
    in_node = mapping["placeholders"]["{{INPUT_IMAGE}}"]["node"]
    out_node = mapping["output"]["node"]
    preset = json.load(open(a.preset, encoding="utf-8"))
    for kv in a.set:
        k, v = kv.split("=", 1)
        preset[k] = type(preset[k])(v) if k in preset and not isinstance(preset[k], str) else v
    wf_template = open(a.workflow, encoding="utf-8").read()

    def build_file(seed):
        t = wf_template
        rep = {"{{POSITIVE}}": json.dumps(preset["positive"])[1:-1], "{{NEGATIVE}}": json.dumps(preset["negative"])[1:-1],
               "{{DENOISE}}": str(preset["denoise"]), "{{STEPS}}": str(int(preset["steps"])), "{{CFG}}": str(preset["cfg"]),
               "{{CONTROL_STRENGTH}}": str(preset["controlStrength"]), "{{SEED}}": str(seed),
               "{{INPUT_IMAGE}}": "placeholder.png"}  # SDK set_input 으로 실제 업로드 이미지로 교체됨
        for k, v in rep.items():
            t = t.replace(k, v)
        f = tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8")
        f.write(t); f.close()
        return f.name

    client = Comfy(api_key=key)
    images = sorted(x for x in os.listdir(a.images) if x.lower().endswith(".png"))
    out = os.path.join(ROOT, "tools", "out", datetime.now().strftime("%Y%m%d_%H%M%S") + "_sdk")
    os.makedirs(out, exist_ok=True)
    json.dump({"preset": preset, "workflow": os.path.relpath(a.workflow, ROOT)},
              open(os.path.join(out, "settings.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"[comfy-sdk] {os.environ['COMFY_BASE_URL']}  그림 {len(images)}장 × seed {a.seeds} → {out}")

    rows, times = [], []
    for img in images:
        for i in range(a.seeds):
            seed = a.seed_base + i
            t0 = time.time()
            try:
                wf = client.workflows.from_file(build_file(seed))
                wf.set_input(in_node, "image", client.assets.from_file(os.path.join(a.images, img)))
                job = client.run(wf)
                outs = job.get_outputs(out_node) or job.outputs
                fn = f"{os.path.splitext(img)[0]}__seed{seed}.png"
                outs[0].to_file(os.path.join(out, fn))
                dt = time.time() - t0
                times.append(dt)
                rows.append([img, seed, "ok", f"{dt:.2f}", getattr(job, "id", ""), fn, ""])
                print(f"  OK   {img} seed={seed} {dt:.1f}s")
            except Exception as e:
                name = type(e).__name__
                hint = {"Unauthorized": "→ 키가 틀렸거나 삭제됨 (platform.comfy.org 에서 재발급)",
                        "InsufficientCredits": "→ 크레딧/구독 확인", "QueueFull": "→ 잠시 후 재시도",
                        "JobFailed": "→ 워크플로/모델 이름 확인"}.get(name, "")
                rows.append([img, seed, "fail", f"{time.time() - t0:.2f}", "", "", f"{name}: {str(e)[:300]}"])
                print(f"  FAIL {img} seed={seed}: {name} {hint}\n       {str(e)[:300]}")
                if name == "JobFailed":
                    print_job_error(e)
                if name in ("Unauthorized", "InsufficientCredits"):
                    break
        else:
            continue
        break

    with open(os.path.join(out, "results.csv"), "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(["image", "seed", "status", "seconds", "job_id", "output", "error",
                    "판정: 대상·배치 유지(O/X)", "색 반영(O/X)", "화풍(O/X)", "형태 훼손(O/X)"])
        w.writerows(rows)
    if times:
        st = sorted(times)
        p95 = st[min(len(st) - 1, int(round(0.95 * (len(st) - 1))))]
        msg = f"성공 {len(times)}/{len(rows)}  중앙값 {statistics.median(st):.1f}초 / 95백분위 {p95:.1f}초"
        print(msg)
        open(os.path.join(out, "summary.txt"), "w", encoding="utf-8").write(msg + "\n")


if __name__ == "__main__":
    main()
