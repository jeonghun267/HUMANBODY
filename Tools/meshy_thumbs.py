"""배치 로그의 refine task_id 로 썸네일을 받아 한 장의 contact sheet 로 만든다."""
import re, sys, urllib.request
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import meshy
log = Path("Tools/meshy_batch_fix.log").read_bytes().decode("cp949", errors="replace")
pairs = re.findall(r"==> (\w+): .*?refine=([0-9a-f-]+)", log)
out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
files = []
for name, tid in pairs:
    dest = out / f"{name}.png"
    if not dest.exists():
        t = meshy.api("GET", f"/openapi/v2/text-to-3d/{tid}")
        urllib.request.urlretrieve(t["thumbnail_url"], dest)
    files.append((name, dest))
try:
    from PIL import Image, ImageDraw
    cols = 5; size = 256
    rows = (len(files) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * size, rows * (size + 20)), "white")
    d = ImageDraw.Draw(sheet)
    for i, (name, f) in enumerate(files):
        im = Image.open(f).convert("RGB").resize((size, size))
        x, y = (i % cols) * size, (i // cols) * (size + 20)
        sheet.paste(im, (x, y)); d.text((x + 4, y + size + 2), name, fill="black")
    sheet.save(out / "_sheet.png"); print(out / "_sheet.png")
except ImportError:
    print("PIL 없음, 개별 파일:", [str(f) for _, f in files])
