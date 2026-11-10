"""인체탐험 효과음·배경음 만들기 (외부 소리 파일 없이 합성 → 라이선스 걱정 없음).

결과: Assets/Resources/Sfx/{이름}.wav  — Sfx.Get(이름) 이 같은 이름의 파일을 먼저 쓴다.
사용: python Tools/make_sfx.py            (모두 다시 만든다)
      python Tools/make_sfx.py step bgm   (이름이 그것이거나 그것_ 으로 시작하는 소리만)
"""
import sys
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
    """x 가 (2, n) 이면 좌우 소리(배경 음악), 아니면 한 줄 소리."""
    OUT.mkdir(parents=True, exist_ok=True)
    x = np.asarray(x)
    channels = 2 if x.ndim == 2 else 1
    data = (np.clip(x.T if channels == 2 else x, -1, 1) * 32767).astype(np.int16)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(channels); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())
    print(f"{name}.wav  {x.shape[-1] / RATE:.2f}s{'  (좌우)' if channels == 2 else ''}")


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


def place(buf, sig, start):
    """sig 를 buf 의 start 초 자리에 더한다. 끝을 넘친 꼬리는 처음으로 돌아가 이어진다 (반복 음악의 이음매가 맞물린다)."""
    i = (int(start * RATE) + np.arange(len(sig))) % len(buf)
    np.add.at(buf, i, sig)


def fft_lp(x, cutoff):
    """주파수 영역에서 부드럽게 거른다. 긴 음악에도 빠르고, 둥글게 이어 붙인 반복 소리의 이음매를 해치지 않는다."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    spec *= 1 / (1 + (f / cutoff) ** 4)
    return np.fft.irfft(spec, len(x))


def midi(m):
    return 440 * 2 ** ((m - 69) / 12)


# ───────── 발소리 (걷기) ─────────
def step_hard(seed):
    """병원 바닥(비닐 타일) 위 고무 밑창: 뒤꿈치 툭 + 조금 뒤 앞꿈치 톡 + 짧은 마찰."""
    r = np.random.default_rng(seed)
    x = np.zeros(int(0.3 * RATE))
    heel = thump(0.12, r.uniform(85, 105), 0.022) * 0.9
    n = int(0.05 * RATE)
    heel[:n] += lp(hp(r.normal(0, 1, n), 300), r.uniform(1400, 1900)) * env(n, 0.001, 0.01) * 0.7
    toe = thump(0.08, r.uniform(115, 140), 0.016) * 0.45
    m = int(0.035 * RATE)
    toe[:m] += lp(hp(r.normal(0, 1, m), 500), 2200) * env(m, 0.001, 0.007) * 0.45
    place(x, heel, 0.005)
    place(x, toe, r.uniform(0.06, 0.09))
    return norm(x, 0.7)


def step_wet(seed):
    """몸속 말랑하고 축축한 바닥: 둔한 눌림 + 질척 + 끈적하게 떨어지는 물기."""
    r = np.random.default_rng(seed)
    x = np.zeros(int(0.45 * RATE))
    place(x, thump(0.18, r.uniform(58, 72), 0.05) * 0.8, 0.0)
    n = int(0.28 * RATE)
    place(x, lp(r.normal(0, 1, n), r.uniform(650, 950)) * env(n, 0.02, 0.07) * 1.4, 0.01)
    for _ in range(r.integers(2, 4)):
        dur = r.uniform(0.03, 0.07)
        tt = t(dur)
        f0 = r.uniform(300, 650)
        chirp = np.sin(2 * np.pi * (f0 * tt + r.uniform(900, 2200) * tt ** 2)) * np.sin(np.pi * tt / dur) ** 2
        place(x, chirp * r.uniform(0.12, 0.25), r.uniform(0.07, 0.26))
    return norm(lp(x, 2600, 1), 0.65)


# ───────── 버튼·모니터·이동 ─────────
def hover():
    """버튼을 가리킬 때: 아주 짧고 작은 틱."""
    tt = t(0.045)
    x = np.sin(2 * np.pi * 2400 * tt) * env(len(tt), 0.001, 0.008)
    x += hp(rng.normal(0, 1, len(tt)), 4000) * env(len(tt), 0.0005, 0.003) * 0.3
    return norm(x, 0.35)


def beeps(freqs, each=0.075, gap=0.045, tail=0.05, peak=0.5):
    """진찰 모니터의 전자음: 각진 소리가 아니라 둥근 삐 (사인 + 조금 섞은 3배음)."""
    x = np.zeros(int(((each + gap) * len(freqs) + tail) * RATE))
    for k, f in enumerate(freqs):
        tt = t(each)
        b = (np.sin(2 * np.pi * f * tt) + 0.25 * np.sin(6 * np.pi * f * tt)) * np.clip(tt / 0.004, 0, 1) * np.clip((each - tt) / 0.01, 0, 1)
        i = int(k * (each + gap) * RATE)
        x[i:i + len(b)] += b
    return norm(x, peak)


def monitor_plan():
    """치료 계획이 뜰 때: 올라가는 삐삐삐 + 맑게 남는 울림."""
    x = beeps([1046, 1318, 1568], 0.08, 0.04, 0.0)
    ring = notes([2093], 0.05, 0.7, 0.35)
    return norm(np.concatenate([x, ring]), 0.55)


def travel():
    """잠수정 이동(장면 넘어갈 때): 엔진이 올라가며 물살이 부풀었다가 잦아든다 + 거품."""
    r = np.random.default_rng(5)
    sec = 3.2
    tt = t(sec)
    shape = np.sin(np.pi * tt / sec) ** 1.5
    f = 48 + 42 * tt / sec
    hum = np.sin(2 * np.pi * np.cumsum(f) / RATE) * 0.6 + np.sin(4 * np.pi * np.cumsum(f) / RATE) * 0.2
    noise = r.normal(0, 1, len(tt))
    rush = fft_lp(noise, 350) * (1 - shape) + fft_lp(noise, 1600) * shape
    x = hum * shape * 0.5 + rush / np.max(np.abs(rush)) * shape
    for _ in range(26):
        dur = r.uniform(0.03, 0.09)
        bt = t(dur)
        chirp = np.sin(2 * np.pi * (r.uniform(250, 700) * bt + r.uniform(600, 1800) * bt ** 2)) * np.sin(np.pi * bt / dur) ** 2
        i = int(r.uniform(0.5, sec - 0.6) * RATE)
        x[i:i + len(chirp)] += chirp * r.uniform(0.08, 0.22) * shape[i]
    return norm(x, 0.7)


# ───────── 배경 음악 (반복) ─────────
def pad_note(f, sec, attack, release, detune, bright):
    tt = t(sec + release)
    e = np.minimum(1, tt / attack) * np.clip((sec + release - tt) / release, 0, 1)
    out = []
    for d in (1 - detune, 1 + detune):  # 왼쪽·오른쪽 귀에 아주 조금 다른 음 → 넓게 퍼지는 소리
        ph = 2 * np.pi * f * d * tt + 0.6 * np.sin(2 * np.pi * 0.21 * tt)
        out.append((np.sin(ph) + bright * 0.35 * np.sin(2 * ph) + bright * 0.12 * np.sin(3 * ph)) * e)
    return np.array(out)


def bell_note(f, decay, kind):
    tt = t(decay * 4)
    if kind == "marimba":  # 병원: 나무 건반처럼 동글동글
        x = np.sin(2 * np.pi * f * tt) * np.exp(-tt / decay) + 0.25 * np.sin(2 * np.pi * 4 * f * tt) * np.exp(-tt / (decay * 0.25))
    else:  # 몸속: 유리 종처럼 맑고 길게
        x = (np.sin(2 * np.pi * f * tt) * np.exp(-tt / decay) + 0.35 * np.sin(2 * np.pi * 2.76 * f * tt) * np.exp(-tt / (decay * 0.4))
             + 0.15 * np.sin(2 * np.pi * 5.4 * f * tt) * np.exp(-tt / (decay * 0.2)))
    return x * np.minimum(1, tt / 0.003)


def space(lr, delays, gain, cutoff):
    """둥글게 이어지는 메아리 몇 겹 (좌우가 다른 간격) → 방 울림."""
    out = lr.copy()
    for k, d in enumerate(delays):
        for ch in (0, 1):
            n = int(d * (1.0 if ch == 0 else 1.13) * RATE)
            out[ch] += fft_lp(np.roll(lr[ch], n), cutoff) * gain ** (k + 1)
    return out


def music(bpm, chords, bass, melody, kind, pad_cut, pad_bright, echo_beats, peak):
    beat = 60 / bpm
    bar = beat * 4
    n = int(round(bar * len(chords) * RATE))
    pads, bells, low = np.zeros((2, n)), np.zeros((2, n)), np.zeros(n)
    for b, chord in enumerate(chords):
        for m in chord:
            sig = pad_note(midi(m), bar, beat * 1.2, beat * 1.5, 0.0018, pad_bright) * 0.22
            for ch in (0, 1):
                place(pads[ch], sig[ch], b * bar)
        tt = t(bar * 0.95)
        f = midi(bass[b])
        low_sig = (np.sin(2 * np.pi * f * tt) + 0.2 * np.sin(4 * np.pi * f * tt)) * np.minimum(1, tt / 0.03) * np.exp(-tt / (bar * 0.6))
        place(low, low_sig * 0.5, b * bar)
    for k, (at, m, vel) in enumerate(melody):
        sig = bell_note(midi(m), 0.35 if kind == "marimba" else 1.4, kind) * vel
        pan = 0.5 + (0.22 if k % 2 else -0.22)
        place(bells[0], sig * (1 - pan) * 1.4, at * beat)
        place(bells[1], sig * pan * 1.4, at * beat)
    pads = np.array([fft_lp(pads[0], pad_cut), fft_lp(pads[1], pad_cut)])
    echo = np.zeros_like(bells)
    for k in range(1, 4):  # 종소리 메아리 (좌우 번갈아)
        d = int(echo_beats * beat * k * RATE)
        echo[k % 2] += np.roll(bells[0] + bells[1], d) * 0.42 ** k * 0.5
    mix = pads + bells + echo + low * 0.5
    mix = space(mix, [0.031, 0.047, 0.071, 0.113], 0.55, 2500)
    return np.tanh(mix / np.max(np.abs(mix)) * 1.2) / np.tanh(1.2) * peak


def melody_from(chords, beats_per_bar, pattern, octave, rng_seed, vel=(0.55, 0.85)):
    """화음 구성음으로 이어지는 선율 (같은 음 반복을 피하고, 다음 음은 가까운 음을 고르는 편)."""
    r = np.random.default_rng(rng_seed)
    out, prev = [], None
    for b, chord in enumerate(chords):
        tones = sorted({(m % 12) + octave for m in chord} | {(m % 12) + octave + 12 for m in chord[:2]})
        for slot in pattern[b % len(pattern)]:
            if prev is None:
                m = tones[len(tones) // 2]
            else:
                near = sorted(tones, key=lambda q: abs(q - prev) + r.uniform(0, 4))
                m = near[0] if near[0] != prev else near[1]
            out.append((b * beats_per_bar + slot, m, r.uniform(*vel)))
            prev = m
    return out


def bgm_clinic():
    """병원 진료실: 밝고 차분한, 궁금해지는 느낌 (다장조, 88박). 16마디 ≈ 44초 반복."""
    C, Am, F, G = [60, 64, 67, 71], [57, 60, 64, 67], [57, 60, 65, 69], [59, 62, 67, 69]
    Em, Dm, Gs = [59, 62, 64, 67], [57, 60, 62, 65], [60, 62, 67, 65]
    chords = [C, Am, F, G, C, Am, F, G, F, Em, Dm, Gs, C, Am, F, G]
    bass = [36, 45, 41, 43, 36, 45, 41, 43, 41, 40, 38, 43, 36, 45, 41, 43]
    pattern = [[0, 1.5, 2.5, 3], [0, 1, 2.5], [0.5, 1.5, 2, 3.5], [0, 2, 3]]
    melody = melody_from(chords, 4, pattern, 72, 3)
    for b in range(0, 16, 2):  # 두 마디마다 높은 반짝임
        melody.append((b * 4 + 3.5, chords[b][-1] + 24, 0.25))
    return music(88, chords, bass, melody, "marimba", 1600, 1.0, 0.75, 0.6)


def bgm_body():
    """몸속 탐험: 신비롭고 잔잔한 (D 도리안, 66박). 12마디 ≈ 44초 반복."""
    Dm9, Bb, F, Cs = [50, 53, 57, 60, 64], [46, 50, 53, 57], [53, 57, 60, 64], [48, 50, 55, 62]
    chords = [Dm9, Bb, F, Cs] * 3
    bass = [26, 34, 29, 36] * 3
    pattern = [[0, 2.5], [1, 3], [0.5, 2], [1.5]]
    melody = melody_from(chords, 4, pattern, 74, 8, (0.35, 0.6))
    return music(66, chords, bass, melody, "glass", 900, 0.5, 1.5, 0.55)


SOUNDS = [("pulse", heartbeat), ("heartbeat", heartbeat), ("gurgle", gurgle), ("flow", flow), ("subhum", subhum),
          ("clinic", clinic), ("beam", beam), ("pop", pop), ("fizz", fizz), ("cure", cure),
          ("success", lambda: notes([523, 659, 784, 1046])), ("correct", lambda: notes([659, 880], 0.1, 0.4)),
          ("wrong", wrong), ("incorrect", wrong), ("click", click), ("ting", lambda: notes([1318], 0.05, 0.6, 0.5)),
          ("gulp", gulp), ("crunch", crunch), ("whoosh", whoosh)]
SOUNDS += [(f"step_clinic_{k}", lambda k=k: step_hard(100 + k)) for k in range(1, 5)]
SOUNDS += [(f"step_wet_{k}", lambda k=k: step_wet(200 + k)) for k in range(1, 5)]
SOUNDS += [("hover", hover), ("monitor_found", lambda: beeps([1568, 2093])), ("monitor_plan", monitor_plan), ("travel", travel),
           ("bgm_clinic", bgm_clinic), ("bgm_body", bgm_body)]


if __name__ == "__main__":
    wanted = sys.argv[1:]
    for name, fn in SOUNDS:
        if not wanted or any(name == w or name.startswith(w + "_") for w in wanted):
            save(name, fn())
