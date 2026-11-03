"""journey_content.json 의 모든 대사를 edge-tts 로 음성 파일로 만든다.
파일명 규칙은 JourneyBuilder.FindVoice 와 동일: {stageId}_{key}_{n}.mp3
  intro/outro/churn/game/complete → _1, _2 ...   absorbed → {stageId}_absorbed_{id}
사용: python Tools/make_narration.py [--voice ko-KR-SunHiNeural] [--rate +5%] [--pitch +20Hz]
"""
import argparse, asyncio, json
from pathlib import Path
import edge_tts

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Sounds" / "Narration"

# 씬 빌더가 직접 넣는 라이드 정차/구역 대사도 같은 규칙으로 뽑는다
EXTRA = {
    "Mouth_chew_1": "이가 음식을 꼭꼭 씹는 모습을 함께 살펴보자!",
    "Esophagus_ride_1": "벽이 차례로 조여지는 모습이 보이지? 이게 연동 운동이야. 근육이 위에서 아래로 수축하면서 음식을 밀어내고 있어.",
    "Esophagus_ride_2": "식도 아래쪽에 도착했어. 저기 닫혀 있는 문이 보이지? 위로 들어가는 분문이야.",
    "SmallIntestine_ride_1": "이 벽 전체가 융털로 덮여 있어. 융털 하나하나 안에는 아주 가는 혈관이 지나가서 영양소를 실어 날라.",
    "LargeIntestine_ride_1": "초록색 친구들이 유익균이야. 우리가 먹은 채소의 식이섬유를 먹고 비타민을 만들어 줘.",
    "LargeIntestine_ride_2": "보라색은 유해균. 평소엔 조용하지만, 인스턴트만 먹거나 잠을 못 자면 숫자가 확 늘어나서 배가 아파.",
}


def collect() -> dict[str, str]:
    data = json.loads((ROOT / "Assets/Data/journey_content.json").read_text(encoding="utf-8"))
    lines: dict[str, str] = {}
    for s in data["stages"]:
        sid = s["stageId"]
        for key in ("intro", "outro", "churn", "game", "complete"):
            for i, text in enumerate(s.get(key, []), 1):
                lines[f"{sid}_{key}_{i}"] = text
        for nid, text in s.get("absorbed", {}).items():
            lines[f"{sid}_absorbed_{nid}"] = text
        for part in s.get("parts", []):
            lines[f"{sid}_part_{part['id']}"] = part["line"]
    lines.update(EXTRA)
    return lines


async def synth(name: str, text: str, voice: str, rate: str, pitch: str, force: bool):
    dest = OUT / f"{name}.mp3"
    if dest.exists() and not force:
        return f"{name}: 있음"
    await edge_tts.Communicate(text, voice, rate=rate, pitch=pitch).save(str(dest))
    return f"{name}: 생성 ({dest.stat().st_size // 1024} KB)"


async def main(a):
    OUT.mkdir(parents=True, exist_ok=True)
    lines = collect()
    print(f"{len(lines)}줄, voice={a.voice} rate={a.rate} pitch={a.pitch}")
    sem = asyncio.Semaphore(4)
    async def run(n, t):
        async with sem:
            print(await synth(n, t, a.voice, a.rate, a.pitch, a.force), flush=True)
    await asyncio.gather(*(run(n, t) for n, t in lines.items()))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default="ko-KR-SunHiNeural")
    ap.add_argument("--rate", default="+3%")
    ap.add_argument("--pitch", default="+25Hz", help="병아리 느낌으로 살짝 높게")
    ap.add_argument("--force", action="store_true", help="기존 파일 덮어쓰기")
    asyncio.run(main(ap.parse_args()))
