"""인체탐험 효과음·배경음 만들기 (외부 소리 파일 없이 합성 → 라이선스 걱정 없음).

결과: Assets/Resources/Sfx/{이름}.wav  — Sfx.Get(이름) 이 같은 이름의 파일을 먼저 쓴다.
사용: python Tools/make_sfx.py
"""
import wave
from pathlib import Path
import numpy as np

RATE = 44100
OUT = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Sfx"
rng = np.random.default_rng(11)


def t(sec):
    return np.arange(int(sec * RATE)) / RATE


def lowpass(x, cutoff):
    """한 번 걸러 주는 간단한 저역 통과 (반복하면 더 부드럽다)."""
    a = np.exp(-2 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def lp(x, cutoff, passes=2):
    for _ in range(passes):
        x = lowpass(x, cutoff)
    return x


def hp(x, cutoff):
    return x - lp(x, cutoff, 1)


def env(n, attack, release, total=None):
    """붙임-풀림 모양의 크기 변화."""
    total = total or n / RATE
    tt = np.arange(n) / RATE
    e = np.minimum(1, tt / max(attack, 1e-4)) * np.exp(-np.maximum(0, tt - attack) / max(release, 1e-4))
    return e


def norm(x, peak=0.8):
    m = np.max(np.abs(x)) or 1
    return x / m * peak


def loopify(x, fade=0.25):
    """끝과 처음을 겹쳐 이음매 없는 반복 소리로."""
    n = int(fade * RATE)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    w = np.linspace(0, 1, n)
    return np.concatenate([body, tail * (1 - w) + head * w])


def save(name, x):
    OUT.mkdir(parents=True, exist_ok=True)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())
    print(f"{name}.wav  {len(x) / RATE:.2f}s")


def thump(sec, freq, decay):
    tt = t(sec)
    f = freq * (1 + 0.6 * np.exp(-tt / 0.03))  # 첫 순간 음이 살짝 높다가 내려간다
    return np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-tt / decay)


def heartbeat():
    """쿵-쿵 (분당 70회), 몸 안에서 들리는 둔한 소리."""
    beat = 60 / 70
    total = beat * 4
    x = np.zeros(int(total * RATE))
    for k in range(4):
        for off, amp, f in ((0.0, 1.0, 52), (0.28, 0.7, 64)):
            s = thump(0.25, f, 0.07) * amp
            i = int((k * beat + off) * RATE)
            x[i:i + len(s)] += s[: len(x) - i]
    x = lp(x, 180)
    return norm(loopify(x, 0.1), 0.7)


def gurgle():
    """위·장의 꾸르륵: 낮은 웅웅거림 위로 크고 작은 거품이 터진다."""
    sec = 8
    base = lp(rng.normal(0, 1, int(sec * RATE)), 120) * 0.6
    x = base.copy()
    for _ in range(70):
        start = rng.uniform(0, sec - 0.4)
        dur = rng.uniform(0.05, 0.25)
        tt = t(dur)
        f0 = rng.uniform(150, 600)
        chirp = np.sin(2 * np.pi * (f0 * tt + rng.uniform(200, 900) * tt ** 2)) * np.sin(np.pi * tt / dur) ** 2
        i = int(start * RATE)
        x[i:i + len(chirp)] += chirp * rng.uniform(0.15, 0.5)
    x = lp(x, 900, 1)
    return norm(loopify(x), 0.6)


def flow():
    """관 속을 흐르는 액체의 낮은 쏴아 소리."""
    sec = 6
    n = rng.normal(0, 1, int(sec * RATE))
    x = lp(n, 400) * (0.7 + 0.3 * np.sin(2 * np.pi * 0.25 * t(sec)))
    return norm(loopify(x), 0.5)


def subhum():
    """잠수정 엔진의 낮은 윙 소리."""
    sec = 4
    tt = t(sec)
    x = (np.sin(2 * np.pi * 55 * tt) * 0.6 + np.sin(2 * np.pi * 110 * tt) * 0.25 + np.sin(2 * np.pi * 165.5 * tt) * 0.12)
    x *= 0.85 + 0.15 * np.sin(2 * np.pi * 0.5 * tt)
    x += lp(rng.normal(0, 1, len(tt)), 300) * 0.25
    return norm(loopify(x, 0.2), 0.45)


def clinic():
    """병원 실내: 은은한 공조음과 가끔 들리는 모니터 삐 소리."""
    sec = 8
    tt = t(sec)
    x = lp(rng.normal(0, 1, len(tt)), 250) * 0.5
    for k in range(3):
        i = int((1.2 + k * 2.6) * RATE)
        b = np.sin(2 * np.pi * 1000 * t(0.12)) * env(int(0.12 * RATE), 0.005, 0.05) * 0.25
        x[i:i + len(b)] += b
    return norm(loopify(x), 0.4)


def beam():
    """치료 광선: 높은 음에서 빠르게 내려가는 지잉."""
    tt = t(0.28)
    f = 1800 * np.exp(-tt * 9) + 300
    x = (np.sin(2 * np.pi * np.cumsum(f) / RATE) + 0.4 * np.sin(4 * np.pi * np.cumsum(f) / RATE)) * env(len(tt), 0.004, 0.09)
    return norm(x, 0.7)


def pop():
    """세균이 터지는 퐁 + 물기."""
    tt = t(0.35)
    f = 900 * np.exp(-tt * 18) + 120
    x = np.sin(2 * np.pi * np.cumsum(f) / RATE) * env(len(tt), 0.002, 0.06)
    x += hp(rng.normal(0, 1, len(tt)), 2000) * env(len(tt), 0.001, 0.03) * 0.5
    return norm(x, 0.8)


def fizz():
    """위산 중화: 칙 하고 거품이 꺼진다."""
    tt = t(0.7)
    x = hp(rng.normal(0, 1, len(tt)), 3000) * env(len(tt), 0.01, 0.25)
    x += np.sin(2 * np.pi * 420 * tt) * env(len(tt), 0.002, 0.05) * 0.3
    return norm(x, 0.6)


def cure():
    """치료광이 비추는 동안의 부드러운 윙 (반복)."""
    sec = 1.6
    tt = t(sec)
    x = np.sin(2 * np.pi * 660 * tt) * 0.4 + np.sin(2 * np.pi * 990 * tt) * 0.2
    x *= 0.8 + 0.2 * np.sin(2 * np.pi * 6 * tt)
    return norm(loopify(x, 0.1), 0.35)


def notes(freqs, each=0.13, tail=0.5, peak=0.6):
    out = np.zeros(int((each * len(freqs) + tail) * RATE))
    for k, f in enumerate(freqs):
        tt = t(each + tail)
        s = (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(4 * np.pi * f * tt)) * env(len(tt), 0.005, 0.25)
        i = int(k * each * RATE)
        out[i:i + len(s)] += s[: len(out) - i]
    return norm(out, peak)


def wrong():
    tt = t(0.35)
    x = (np.sign(np.sin(2 * np.pi * 160 * tt)) * 0.3 + np.sin(2 * np.pi * 120 * tt)) * env(len(tt), 0.005, 0.15)
    return norm(lp(x, 1200, 1), 0.5)


def click():
    tt = t(0.06)
    return norm(np.sin(2 * np.pi * 1400 * tt) * env(len(tt), 0.001, 0.015), 0.5)


def gulp():
    """꿀꺽: 낮게 떨어지는 덩어리 소리 + 물기."""
    tt = t(0.6)
    f = 260 * np.exp(-tt * 4) + 70
    x = np.sin(2 * np.pi * np.cumsum(f) / RATE) * env(len(tt), 0.02, 0.18)
    x += lp(rng.normal(0, 1, len(tt)), 700) * env(len(tt), 0.01, 0.12) * 0.6
    return norm(x, 0.85)


def crunch():
    """아삭: 짧은 잡음 덩어리 여러 개."""
    x = np.zeros(int(0.32 * RATE))
    for _ in range(9):
        i = int(rng.uniform(0, 0.22) * RATE)
        n = int(rng.uniform(0.01, 0.04) * RATE)
        burst = hp(rng.normal(0, 1, n), 1500) * env(n, 0.001, 0.008)
        x[i:i + n] += burst[: len(x) - i]
    return norm(x, 0.7)


def whoosh():
    tt = t(1.0)
    x = lp(rng.normal(0, 1, len(tt)), 900) * np.sin(np.pi * tt) ** 2
    return norm(x, 0.6)


if __name__ == "__main__":
    for name, fn in [("pulse", heartbeat), ("heartbeat", heartbeat), ("gurgle", gurgle), ("flow", flow), ("subhum", subhum),
                     ("clinic", clinic), ("beam", beam), ("pop", pop), ("fizz", fizz), ("cure", cure),
                     ("success", lambda: notes([523, 659, 784, 1046])), ("correct", lambda: notes([659, 880], 0.1, 0.4)),
                     ("wrong", wrong), ("incorrect", wrong), ("click", click), ("ting", lambda: notes([1318], 0.05, 0.6, 0.5)),
                     ("gulp", gulp), ("crunch", crunch), ("whoosh", whoosh)]:
        save(name, fn())
