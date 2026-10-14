"""
ComfyUI 연결 점검: 서버 접속 / 워크플로에 필요한 노드 / 모델 파일 이름이 서버에 있는지 확인.
    python tools/check_comfyui.py --server http://127.0.0.1:8188
"""
import argparse, json, os, re, sys, urllib.error, urllib.request

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

KEY = load_key()

def get(url):
    h = {"X-API-Key": KEY, "Authorization": "Bearer " + KEY} if KEY else {}
    with urllib.request.urlopen(urllib.request.Request(url, headers=h), timeout=20) as r:
        return json.loads(r.read())

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", default="https://cloud.comfy.org", help="Comfy Cloud: https://cloud.comfy.org (환경변수 COMFY_API_KEY 필요)")
    ap.add_argument("--workflow", default=os.path.join(ROOT, "comfyui", "workflows", "drawing_to_silkroad.api.json"))
    a = ap.parse_args()
    s = a.server.rstrip("/")
    ok = True
    cloud = "comfy.org" in s or "comfy.app" in s
    if cloud:
        if not KEY:
            sys.exit("[실패] 키를 못 찾음: C:\\keydata\\comfy_key.py 의 comfy_key 변수 확인")
        # 키 모양 진단 (키 자체는 출력하지 않음)
        print(f"키: {len(KEY)}자, 앞부분 '{KEY[:8]}…', 공백/따옴표 포함: {any(c in KEY for c in ' \t\"\'')}")
        results = {}
        for label, path, hdr in (
            ("v2 Bearer   GET /api/v2/jobs", "/api/v2/jobs?limit=1", {"Authorization": "Bearer " + KEY}),
            ("v2 X-API-Key GET /api/v2/jobs", "/api/v2/jobs?limit=1", {"X-API-Key": KEY}),
            ("v1 X-API-Key GET /api/user", "/api/user", {"X-API-Key": KEY}),
        ):
            try:
                with urllib.request.urlopen(urllib.request.Request(s + path, headers=hdr), timeout=20) as r:
                    results[label] = (r.status, "")
            except urllib.error.HTTPError as e:
                results[label] = (e.code, e.read().decode("utf-8", "replace")[:200])
            except Exception as e:
                results[label] = ("ERR", str(e)[:200])
            code, body = results[label]
            print(f"  {label}: {code} {body}")
        if not any(c == 200 for c, _ in results.values()):
            sys.exit("\n[실패] 모든 인증 방식 실패.\n"
                     " - 401: 키가 틀렸거나 삭제됨 → platform.comfy.org 에서 새 키 발급 후 comfy_key.py 교체\n"
                     " - 402/403/429: Comfy Cloud 구독(Standard 이상) 또는 크레딧 확인\n"
                     " 위 결과(키는 앞 8자만 표시됨)를 Claude 에게 보내 주세요.")
        print(f"[OK] Comfy Cloud 인증 성공: {s}")
        s_info = s + "/api"
    else:
        s_info = s

    if not cloud:
      try:
        st = get(s + "/system_stats")
        dev = st.get("devices", [{}])[0]
        print(f"[OK] 서버 접속: {s}  ComfyUI {st.get('system', {}).get('comfyui_version', '?')}")
        print(f"     GPU: {dev.get('name', '?')}  VRAM {dev.get('vram_total', 0) / 1024**3:.1f} GB")
      except Exception as e:
        sys.exit(f"[실패] 서버 접속 안 됨: {e}\n  → ComfyUI 실행 중인지, 주소/포트, --listen 0.0.0.0, 방화벽 확인")

    text = open(a.workflow, encoding="utf-8").read()
    wf = json.loads(re.sub(r"\{\{[A-Z_]+\}\}", "0", text))
    try:
        info = get(s_info + "/object_info")
    except Exception as e:
        sys.exit(f"[실패] 노드 목록(object_info) 조회 실패: {e}")
    for nid, node in wf.items():
        ct = node["class_type"]
        if ct not in info:
            ok = False
            print(f"[실패] 노드 {nid} '{ct}' 가 서버에 없음 (커스텀 노드 설치 필요)")
            continue
        req = info[ct].get("input", {}).get("required", {})
        for key, val in node["inputs"].items():
            spec = req.get(key)
            if spec and isinstance(spec[0], list) and isinstance(val, str) and val not in spec[0]:
                if key in ("ckpt_name", "control_net_name", "lora_name", "vae_name"):
                    ok = False
                    print(f"[실패] 노드 {nid} {ct}.{key} = '{val}' 파일이 서버에 없음")
                    print("       서버에 있는 파일: " + (", ".join(spec[0][:10]) or "(없음)"))
    print("[OK] 워크플로 점검 끝" if ok else "\n→ 모델 파일을 넣거나, 워크플로 JSON 의 파일 이름을 서버에 있는 이름으로 바꾸세요 (comfyui/dependencies.md)")
    sys.exit(0 if ok else 1)

if __name__ == "__main__":
    main()
