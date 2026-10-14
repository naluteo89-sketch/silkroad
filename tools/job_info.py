r"""
Comfy Cloud 작업(job) 상세 + 로그 출력. 실패 원인 확인용.
    .venv\Scripts\python.exe tools\job_info.py <job_id>
"""
import json, os, sys, urllib.error, urllib.request

def load_key():
    k = os.environ.get("COMFY_API_KEY", "")
    if k:
        return k.strip()
    path = os.environ.get("COMFY_KEY_FILE", r"C:\keydata\comfy_key.py")
    if os.path.exists(path):
        import runpy
        return str(runpy.run_path(path).get("comfy_key", "")).strip()
    return ""

def get(url, key):
    req = urllib.request.Request(url, headers={"Authorization": "Bearer " + key, "User-Agent": "silkroad-tools/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")

if len(sys.argv) < 2:
    sys.exit("사용법: python tools\\job_info.py <job_id>")
key, jid = load_key(), sys.argv[1]
base = os.environ.get("COMFY_BASE_URL", "https://cloud.comfy.org").rstrip("/")
code, body = get(f"{base}/api/v2/jobs/{jid}", key)
print(f"== job ({code}) ==")
try:
    j = json.loads(body)
    print("status:", j.get("status"))
    print("error :", json.dumps(j.get("error"), ensure_ascii=False, indent=2))
except Exception:
    print(body[:2000])
code, body = get(f"{base}/api/v2/jobs/{jid}/logs", key)
print(f"\n== logs ({code}) ==")
print(body[-4000:])
