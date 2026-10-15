"""
1단계(생성 품질 검증) / 5단계(성능 실측)용 배치 테스트.
Unity 없이 shared/test-drawings 의 그림들을 ComfyUI 에 보내 결과와 시간을 기록합니다.

사용법 (파이썬 3.9+, 추가 설치 없음):
    로컬 ComfyUI : python tools/comfy_batch_test.py --server http://127.0.0.1:8188 --seeds 3
    Comfy Cloud  : set COMFY_API_KEY=발급받은키
                   python tools/comfy_batch_test.py --server https://cloud.comfy.org --seeds 3
                   (주소에 comfy.org / comfy.app 이 있으면 자동으로 Cloud API v2 사용)

옵션:
    --images   테스트 그림 폴더 (기본: shared/test-drawings)
    --workflow 워크플로 (기본: comfyui/workflows/drawing_to_silkroad.api.json)
    --preset   프리셋 (기본: comfyui/presets/silkroad_style.json)
    --seeds    그림당 seed 개수 (기본 3) / --seed-base 고정 seed 시작값 (기본 1000, 같은 값이면 재현 가능)
               프리셋에 "seed" 가 있으면 그 값부터 시작 (앱의 새 그림 = seed, 재요청 = seed+1, +2 …)
    --set denoise=0.6  프리셋 값 덮어쓰기 (여러 번 가능) → 한 번에 한 변수만 바꿔 비교
    --warmup   본 측정 전에 1회 실행해 모델 로딩 시간을 분리 (기본 켜짐)

결과: tools/out/<시각>/ 에 결과 PNG, results.csv, summary.txt (중앙값/95백분위/실패율)
"""
import argparse, csv, json, os, statistics, sys, time, uuid, urllib.error, urllib.parse, urllib.request
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_key():
    """API 키: 환경변수 COMFY_API_KEY → 없으면 C:\\keydata\\comfy_key.py 의 comfy_key 변수 (화면에 출력하지 않음)"""
    k = os.environ.get("COMFY_API_KEY", "")
    if k:
        return k.strip()
    path = os.environ.get("COMFY_KEY_FILE", r"C:\keydata\comfy_key.py")
    if os.path.exists(path):
        import runpy
        return str(runpy.run_path(path).get("comfy_key", "")).strip()
    return ""


def http(method, url, data=None, headers=None, timeout=30):
    req = urllib.request.Request(url, data=data, method=method, headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.read()
    except urllib.error.HTTPError as e:
        if e.code in (301, 302, 303, 307, 308):
            raise
        body = e.read().decode("utf-8", "replace")[:400]
        hint = {401: "API 키 인증 실패 → python tools\\check_comfyui.py 로 진단",
                402: "크레딧/결제 필요", 403: "권한 없음(구독 플랜 확인)", 429: "구독 비활성 또는 요청 한도"}.get(e.code, "")
        raise RuntimeError(f"HTTP {e.code} {method} {url.split('?')[0]} {hint}\n  응답: {body}") from None


def upload(server, path):
    boundary = uuid.uuid4().hex
    name = "batch_" + uuid.uuid4().hex[:8] + "_" + os.path.basename(path)
    with open(path, "rb") as f:
        img = f.read()
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n"
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\n"
            f"Content-Type: image/png\r\n\r\n").encode() + img + f"\r\n--{boundary}--\r\n".encode()
    res = json.loads(http("POST", server + "/upload/image", body,
                          {"Content-Type": f"multipart/form-data; boundary={boundary}"}))
    return res["name"]


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


def is_cloud(server):
    return "comfy.org" in server or "comfy.app" in server


def cloud_req(method, url, key, data=None, headers=None, timeout=60):
    h = {"Authorization": "Bearer " + key, "User-Agent": "silkroad-tools/1.0"}
    h.update(headers or {})
    return http(method, url, data, h, timeout)


def run_cloud(server, key, img_path, wf_text, preset, seed, output_node, timeout):
    boundary = uuid.uuid4().hex
    with open(img_path, "rb") as f:
        img = f.read()
    name = os.path.basename(img_path)
    parts = []
    for k, v in (("content_type", "image/png"), ("file_path", name)):
        parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n".encode())
    parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{name}\"\r\nContent-Type: image/png\r\n\r\n".encode() + img + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    asset = json.loads(cloud_req("POST", server + "/api/v2/assets", key, b"".join(parts),
                                 {"Content-Type": f"multipart/form-data; boundary={boundary}", "Idempotency-Key": uuid.uuid4().hex}))
    t = wf_text.replace('"{{INPUT_IMAGE}}"', json.dumps({"__type": "core/ASSET", "info": {"id": asset["id"]}}))
    wf = build(t, preset, "", seed)
    body = json.dumps({"workflow": wf}).encode()
    job = json.loads(cloud_req("POST", server + "/api/v2/jobs", key, body,
                               {"Content-Type": "application/json", "Idempotency-Key": uuid.uuid4().hex}))
    jid = job["id"]
    t0 = time.time()
    while time.time() - t0 < timeout:
        time.sleep(1.5)
        job = json.loads(cloud_req("GET", server + "/api/v2/jobs/" + jid, key))
        st = job.get("status")
        if st in ("failed", "canceled", "expired"):
            raise RuntimeError(f"{st}: " + json.dumps(job.get("error"), ensure_ascii=False)[:500])
        if st == "succeeded":
            outs = [o for o in job.get("outputs", []) if o.get("type") == "image"]
            pick = next((o for o in outs if o.get("node_id") == output_node), outs[0] if outs else None)
            if not pick:
                raise RuntimeError("이미지 출력 없음")
            opener = urllib.request.build_opener(_NoRedirect)
            req = urllib.request.Request(server + f"/api/v2/assets/{pick['id']}/content", headers={"Authorization": "Bearer " + key, "User-Agent": "silkroad-tools/1.0"})
            try:
                with opener.open(req, timeout=60) as r:
                    return jid, r.read()
            except urllib.error.HTTPError as e:
                if e.code in (301, 302, 303, 307, 308):
                    return jid, http("GET", e.headers["Location"], timeout=120)
                raise
    raise TimeoutError(f"{timeout}초 초과 (job={jid})")


def esc(s):
    return json.dumps(s)[1:-1]


def build(workflow_text, preset, image_name, seed):
    t = workflow_text
    rep = {
        "{{INPUT_IMAGE}}": esc(image_name),
        "{{POSITIVE}}": esc(preset["positive"]),
        "{{NEGATIVE}}": esc(preset["negative"]),
        "{{DENOISE}}": str(preset["denoise"]),
        "{{STEPS}}": str(int(preset["steps"])),
        "{{CFG}}": str(preset["cfg"]),
        "{{CONTROL_STRENGTH}}": str(preset["controlStrength"]),
        "{{SEED}}": str(seed),
    }
    for k, v in rep.items():
        t = t.replace(k, v)
    return json.loads(t)


def run_one(server, wf, output_node, timeout):
    client_id = uuid.uuid4().hex
    res = json.loads(http("POST", server + "/prompt",
                          json.dumps({"prompt": wf, "client_id": client_id}).encode(),
                          {"Content-Type": "application/json"}))
    if "prompt_id" not in res:
        raise RuntimeError("prompt 거부: " + json.dumps(res, ensure_ascii=False)[:500])
    pid = res["prompt_id"]
    t0 = time.time()
    while time.time() - t0 < timeout:
        time.sleep(1)
        h = json.loads(http("GET", server + "/history/" + pid)).get(pid)
        if not h:
            continue
        st = h.get("status", {})
        if st.get("status_str") == "error":
            raise RuntimeError("실행 오류: " + json.dumps(st, ensure_ascii=False)[:500])
        imgs = h.get("outputs", {}).get(output_node, {}).get("images", [])
        if imgs:
            im = imgs[0]
            q = urllib.parse.urlencode({"filename": im["filename"], "subfolder": im["subfolder"], "type": im["type"]})
            return pid, http("GET", server + "/view?" + q, timeout=60)
    raise TimeoutError(f"{timeout}초 초과 (prompt_id={pid})")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", default="https://cloud.comfy.org")
    ap.add_argument("--images", default=os.path.join(ROOT, "shared", "test-drawings"))
    ap.add_argument("--workflow", default=os.path.join(ROOT, "comfyui", "workflows", "drawing_to_silkroad.api.json"))
    ap.add_argument("--preset", default=os.path.join(ROOT, "comfyui", "presets", "silkroad_style.json"))
    ap.add_argument("--mapping", default=os.path.join(ROOT, "comfyui", "node-mapping.json"))
    ap.add_argument("--seeds", type=int, default=3)
    ap.add_argument("--seed-base", type=int, default=1000)
    ap.add_argument("--set", action="append", default=[], help="프리셋 덮어쓰기 key=value")
    ap.add_argument("--timeout", type=int, default=300)
    ap.add_argument("--no-warmup", action="store_true")
    ap.add_argument("--api-key", default=None, help="Comfy Cloud 키 (기본: COMFY_API_KEY 또는 C:\\keydata\\comfy_key.py)")
    a = ap.parse_args()
    server = a.server.rstrip("/")

    wf_text = open(a.workflow, encoding="utf-8").read()
    preset = json.load(open(a.preset, encoding="utf-8"))
    for kv in a.set:
        k, v = kv.split("=", 1)
        preset[k] = type(preset.get(k, ""))(v) if k in preset and not isinstance(preset[k], str) else v
    output_node = json.load(open(a.mapping, encoding="utf-8"))["output"]["node"]

    images = sorted(f for f in os.listdir(a.images) if f.lower().endswith(".png"))
    if not images:
        sys.exit("테스트 그림이 없습니다: " + a.images)
    out = os.path.join(ROOT, "tools", "out", datetime.now().strftime("%Y%m%d_%H%M%S"))
    os.makedirs(out, exist_ok=True)
    json.dump({"preset": preset, "workflow": os.path.relpath(a.workflow, ROOT), "server": server},
              open(os.path.join(out, "settings.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    if not a.api_key:
        a.api_key = load_key()
    cloud = is_cloud(server)
    if cloud and not a.api_key:
        sys.exit("Comfy Cloud 키를 못 찾았어요: C:\\keydata\\comfy_key.py 의 comfy_key 변수 확인")

    def one(path, seed):
        if cloud:
            return run_cloud(server, a.api_key, path, wf_text, preset, seed, output_node, a.timeout)
        name = upload(server, path)
        return run_one(server, build(wf_text, preset, name, seed), output_node, a.timeout)

    print(("[Comfy Cloud v2] " if cloud else "[로컬 ComfyUI] ") + f"서버 {server} / 그림 {len(images)}장 × seed {a.seeds}개 → {out}")
    if not a.no_warmup:
        t = time.time()
        one(os.path.join(a.images, images[0]), 1)
        print(f"워밍업(모델 로딩 포함): {time.time() - t:.1f}초")

    rows, times = [], []
    for img in images:
        for i in range(a.seeds):
            # 프리셋에 seed 가 있으면 앱과 같게 preset.seed + i (i = 재요청 횟수), 없으면 --seed-base + i
            seed = int(preset.get("seed") or a.seed_base) + i
            t = time.time()
            try:
                pid, data = one(os.path.join(a.images, img), seed)
                dt = time.time() - t
                fn = f"{os.path.splitext(img)[0]}__seed{seed}.png"
                open(os.path.join(out, fn), "wb").write(data)
                times.append(dt)
                rows.append([img, seed, "ok", f"{dt:.2f}", pid, fn, ""])
                print(f"  OK  {img} seed={seed} {dt:.1f}s")
            except Exception as e:
                rows.append([img, seed, "fail", f"{time.time() - t:.2f}", "", "", str(e)[:300]])
                print(f"  FAIL {img} seed={seed}: {e}")

    with open(os.path.join(out, "results.csv"), "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(["image", "seed", "status", "seconds", "prompt_id", "output", "error",
                    "판정: 대상·배치 유지(O/X)", "색 반영(O/X)", "화풍(O/X)", "형태 훼손(O/X)"])
        w.writerows(rows)
    fails = sum(1 for r in rows if r[2] != "ok")
    lines = [f"총 {len(rows)}건, 실패 {fails}건 ({fails / len(rows) * 100:.0f}%)"]
    if times:
        st = sorted(times)
        p95 = st[min(len(st) - 1, int(round(0.95 * (len(st) - 1))))]
        lines.append(f"중앙값 {statistics.median(st):.1f}초 / 95백분위 {p95:.1f}초 / 최대 {st[-1]:.1f}초 (목표: 20초 / 40초)")
    open(os.path.join(out, "summary.txt"), "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("\n".join(lines))
    print("results.csv 의 판정 칸을 채워 품질 검수(30건 중 24건 이상 목표)에 사용하세요.")


if __name__ == "__main__":
    main()
