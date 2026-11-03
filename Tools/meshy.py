"""Meshy AI 클라이언트 - 인체탐험 VR 프로젝트용 3D 모델 생성 도구.

API 키: 환경변수 MESHY_API_KEY 또는 바탕화면의 msyapi.txt 에서 읽음.

사용법:
  python Tools/meshy.py balance
  python Tools/meshy.py text  "realistic human heart, anatomical" --name Heart
  python Tools/meshy.py image path/to/ref.png --name Lung
  python Tools/meshy.py status <task_id>
"""
import argparse
import base64
import json
import os
import sys
import time
import urllib.request
import urllib.error
from pathlib import Path

BASE = "https://api.meshy.ai"
PROJECT = Path(__file__).resolve().parent.parent
OUT_DIR = PROJECT / "Assets" / "Models" / "Meshy"


def load_key() -> str:
    key = os.environ.get("MESHY_API_KEY", "").strip()
    if not key:
        p = Path.home() / "Desktop" / "msyapi.txt"
        if p.exists():
            key = p.read_text(encoding="utf-8").strip()
    if not key:
        sys.exit("MESHY_API_KEY 환경변수 또는 ~/Desktop/msyapi.txt 가 필요합니다.")
    return key


def api(method: str, path: str, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Authorization", f"Bearer {load_key()}")
    data = None
    if body is not None:
        req.add_header("Content-Type", "application/json")
        data = json.dumps(body).encode()
    try:
        with urllib.request.urlopen(req, data, timeout=60) as r:
            return json.loads(r.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        sys.exit(f"HTTP {e.code} {path}: {e.read().decode()[:500]}")


def wait_task(kind: str, task_id: str, poll=10):
    """kind: 'text-to-3d' (v2) 또는 'image-to-3d' (v1)"""
    ver = "v2" if kind == "text-to-3d" else "v1"
    while True:
        t = api("GET", f"/openapi/{ver}/{kind}/{task_id}")
        st, prog = t.get("status"), t.get("progress", 0)
        print(f"  [{task_id[:8]}] {st} {prog}%", flush=True)
        if st == "SUCCEEDED":
            return t
        if st in ("FAILED", "CANCELED", "EXPIRED"):
            sys.exit(f"작업 실패: {t.get('task_error')}")
        time.sleep(poll)


def download(task: dict, name: str) -> Path:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    urls = task.get("model_urls", {})
    url = urls.get("glb") or urls.get("fbx") or urls.get("obj")
    if not url:
        sys.exit(f"다운로드 URL 없음: {json.dumps(task)[:300]}")
    ext = ".glb" if "glb" in urls else (".fbx" if "fbx" in urls else ".obj")
    dest = OUT_DIR / f"{name}{ext}"
    urllib.request.urlretrieve(url, dest)
    print(f"저장됨: {dest.relative_to(PROJECT)} ({dest.stat().st_size // 1024} KB)")
    return dest


def cmd_balance(_):
    print(json.dumps(api("GET", "/openapi/v1/balance"), indent=2))


def cmd_text(a):
    print("1/2 preview 생성 중...")
    pv = api("POST", "/openapi/v2/text-to-3d", {
        "mode": "preview", "prompt": a.prompt,
        "art_style": a.style, "ai_model": "meshy-5",
        "should_remesh": True, "topology": "triangle",
        "target_polycount": a.polycount,
    })["result"]
    wait_task("text-to-3d", pv)
    if a.no_texture:
        return download(api("GET", f"/openapi/v2/text-to-3d/{pv}"), a.name)
    print("2/2 refine(텍스처) 생성 중...")
    rf = api("POST", "/openapi/v2/text-to-3d", {
        "mode": "refine", "preview_task_id": pv, "enable_pbr": True,
    })["result"]
    download(wait_task("text-to-3d", rf), a.name)


def cmd_image(a):
    p = Path(a.image)
    mime = "image/png" if p.suffix.lower() == ".png" else "image/jpeg"
    data_uri = f"data:{mime};base64," + base64.b64encode(p.read_bytes()).decode()
    print("image-to-3d 생성 중...")
    tid = api("POST", "/openapi/v1/image-to-3d", {
        "image_url": data_uri, "ai_model": "meshy-5", "enable_pbr": True,
        "should_remesh": True, "topology": "triangle",
        "target_polycount": a.polycount,
    })["result"]
    download(wait_task("image-to-3d", tid), a.name)


def gen_one(item: dict) -> str:
    """매니페스트 항목 하나를 text-to-3d(preview+refine)로 생성 후 저장."""
    name = item["name"]
    if (OUT_DIR / f"{name}.glb").exists():
        return f"{name}: 이미 존재, 건너뜀"
    pv = api("POST", "/openapi/v2/text-to-3d", {
        "mode": "preview", "prompt": item["prompt"],
        "art_style": item.get("style", "realistic"), "ai_model": "meshy-5",
        "should_remesh": True, "topology": "triangle",
        "target_polycount": item.get("polycount", 30000),
    })["result"]
    wait_task("text-to-3d", pv, poll=15)
    rf = api("POST", "/openapi/v2/text-to-3d", {
        "mode": "refine", "preview_task_id": pv, "enable_pbr": True,
    })["result"]
    t = wait_task("text-to-3d", rf, poll=15)
    download(t, name)
    return f"{name}: 완료 (preview={pv}, refine={rf})"


def cmd_batch(a):
    from concurrent.futures import ThreadPoolExecutor, as_completed
    items = json.loads(Path(a.manifest).read_text(encoding="utf-8"))
    if a.only:
        items = [i for i in items if i["name"] in a.only.split(",")]
    print(f"{len(items)}개 항목, 동시 {a.parallel}개. 시작 잔액: {api('GET', '/openapi/v1/balance')['balance']}")
    with ThreadPoolExecutor(a.parallel) as ex:
        futs = {ex.submit(gen_one, it): it["name"] for it in items}
        for f in as_completed(futs):
            try:
                print("==>", f.result(), flush=True)
            except SystemExit as e:
                print("==> 실패", futs[f], e, flush=True)
    print(f"종료 잔액: {api('GET', '/openapi/v1/balance')['balance']}")


def cmd_status(a):
    kind = "image-to-3d" if a.image else "text-to-3d"
    print(json.dumps(api("GET", f"/openapi/{'v1' if a.image else 'v2'}/{kind}/{a.task_id}"), indent=2))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("balance").set_defaults(fn=cmd_balance)

    t = sub.add_parser("text"); t.set_defaults(fn=cmd_text)
    t.add_argument("prompt"); t.add_argument("--name", required=True)
    t.add_argument("--style", default="realistic", choices=["realistic", "sculpture"])
    t.add_argument("--polycount", type=int, default=30000)
    t.add_argument("--no-texture", action="store_true")

    i = sub.add_parser("image"); i.set_defaults(fn=cmd_image)
    i.add_argument("image"); i.add_argument("--name", required=True)
    i.add_argument("--polycount", type=int, default=30000)

    s = sub.add_parser("status"); s.set_defaults(fn=cmd_status)
    s.add_argument("task_id"); s.add_argument("--image", action="store_true")

    b = sub.add_parser("batch"); b.set_defaults(fn=cmd_batch)
    b.add_argument("manifest"); b.add_argument("--parallel", type=int, default=3)
    b.add_argument("--only", help="쉼표로 구분한 name 목록만 생성")

    args = ap.parse_args()
    args.fn(args)
