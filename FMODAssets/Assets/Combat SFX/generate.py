# Combat SFX generator. Sources: only files already in FMODAssets/Assets.
# Run: python generate.py   (needs ffmpeg on PATH and numpy)
import os
import subprocess
import wave

import numpy as np

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.dirname(HERE)
U = "UI Soundpack/MP3/"
R = "RPG Sound Pack/"


def load(src, rate=1.0, reverse=False, extra=None):
    """Decode to stereo float, trim silence, peak-normalize to 1.0."""
    filters = []
    if rate != 1.0:
        filters.append(f"asetrate={SR * rate:.2f}")
    filters.append(f"aresample={SR}")
    if extra:
        filters.append(extra)
    cmd = ["ffmpeg", "-v", "error", "-i", os.path.join(ASSETS, src), "-ac", "2", "-ar", str(SR),
           "-af", ",".join(filters), "-f", "f32le", "-"]
    raw = subprocess.run(cmd, capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).astype(np.float64)
    peak = np.abs(x).max()
    idx = np.where(np.abs(x).max(axis=1) > peak * 0.01)[0]
    x = x[idx[0]:idx[-1] + 1] / peak
    if reverse:
        x = x[::-1].copy()
    return x


def fade(x, fade_in=0.0, fade_out=0.0):
    x = x.copy()
    n = int(fade_in * SR)
    if n:
        x[:n] *= np.linspace(0, 1, n)[:, None]
    n = min(int(fade_out * SR), len(x))
    if n:
        x[-n:] *= np.linspace(1, 0, n)[:, None]
    return x


def cut(x, seconds, fade_out=0.03):
    return fade(x[:int(seconds * SR)], 0, fade_out)


def tail(x, seconds, fade_in=0.15):
    return fade(x[-int(seconds * SR):], fade_in, 0.005)


def mix(layers, peak_db=-3.0, max_len=None):
    """layers: (samples, start seconds, gain dB)."""
    end = max(int(t * SR) + len(x) for x, t, _ in layers)
    out = np.zeros((end, 2))
    for x, t, g in layers:
        s = int(t * SR)
        out[s:s + len(x)] += x * 10 ** (g / 20)
    if max_len:
        out = cut(out, max_len, 0.25)
    out = fade(out, 0.002, 0.01)
    return out / np.abs(out).max() * 10 ** (peak_db / 20)


def save(name, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(os.path.join(HERE, name + ".wav"), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{name}.wav {len(x) / SR:.2f}s")


def main():
    beep = cut(load(U + "Retro5.mp3"), 0.13)
    save("warning", mix([(beep, 0, 0), (beep, 0.17, 0)]))

    hi = cut(load(U + "Retro5.mp3", 1.19), 0.12)
    low = cut(load(U + "Retro11.mp3"), 0.12)
    save("swarm_warning", mix([(h, t, g) for t in (0, 0.15, 0.30) for h, g in ((hi, 0), (low, -3))]))

    a = load(U + "Retro12.mp3", 0.5)
    b = load(U + "Retro11.mp3", 0.5)
    save("boss_warning", mix([(a, 0, 0), (b, 0.35, 0), (a, 0.70, 0), (b, 1.05, 0)]))

    save("boss_appear", mix([(load(R + "misc/random1.wav", 0.7), 0, 0),
                             (load(R + "NPC/giant/giant2.wav", 0.75), 0.05, -4)]))

    chord = [(load(U + "Modern16.mp3", r), 0.12, -8) for r in (1.0, 1.26, 1.498)]
    save("boss_kill", mix([(load(R + "misc/random1.wav", 0.85), 0, 0)] + chord
                          + [(load(U + "Modern16.mp3", 2.0), 0.30, -8)]))

    blips = [("Modern11.mp3", 1.0), ("Modern10.mp3", 1.0), ("Modern12.mp3", 1.0),
             ("Modern11.mp3", 1.335), ("Modern10.mp3", 1.335), ("Modern12.mp3", 1.335),
             ("Modern11.mp3", 1.78), ("Modern10.mp3", 1.78)]
    times = (0, 0.16, 0.30, 0.42, 0.52, 0.61, 0.69, 0.76)
    layers = [(load(U + f, r), t, -10 + 10 * i / 7) for i, ((f, r), t) in enumerate(zip(blips, times))]
    swell = load(U + "Modern16.mp3", reverse=True)
    layers.append((swell, 0.85 - len(swell) / SR, -6))
    save("telegraph_charge", mix(layers))

    save("telegraph_fire", mix([(load(R + "battle/swing3.wav", 0.8), 0, 0),
                                (load(R + "interface/interface6.wav", 0.8), 0, -3)]))

    save("player_hit", mix([(load(R + "interface/interface6.wav", 0.9), 0, 0),
                            (load(R + "inventory/cloth-heavy.wav"), 0, -4)]))

    save("player_death", mix([(load(U + "Retro10.mp3", 0.75), 0, 0),
                              (load(U + "Retro10.mp3", 0.5), 0.35, -3)]))

    save("heal", mix([(load(U + "Modern13.mp3"), 0, 0), (load(U + "Modern13.mp3", 1.5), 0.12, -4)]))

    save("freeze", mix([(load(R + "inventory/metal-ringing.wav"), 0, 0),
                        (load(R + "inventory/metal-ringing.wav", 0.5), 0, -3)]))

    save("magnet_pull", mix([(tail(load(R + "inventory/coin2.wav", reverse=True), 0.6), 0, 0)]))

    arp = [(load(U + "Modern15.mp3", r), t, 0) for r, t in ((1.0, 0), (1.26, 0.16), (1.498, 0.32))]
    final = [(load(U + "Modern16.mp3", 1.012 * r), 0.48, g)
             for r, g in ((2.0, 0), (1.498, -5), (1.26, -5), (1.0, -5))]
    save("victory", mix(arp + final))

    steps = ((1.0, 0), (0.841, 0.28), (0.667, 0.56), (0.5, 0.84))
    save("defeat", mix([(load(U + "Modern16.mp3", r), t, 0) for r, t in steps], max_len=1.9))

    trem = "tremolo=f=45:d=0.95"
    save("zap1", mix([(load(R + "battle/sword-unsheathe2.wav", 1.3, extra=trem), 0, 0)]))
    save("zap2", mix([(load(R + "battle/sword-unsheathe4.wav", 1.3, extra=trem), 0, 0)]))

    save("splash1", mix([(load(R + "inventory/bottle.wav"), 0, 0),
                         (load(R + "NPC/slime/slime8.wav"), 0.03, -2)]))
    save("splash2", mix([(load(R + "inventory/bottle.wav", 1.15), 0, 0),
                         (load(R + "NPC/slime/slime9.wav"), 0.03, -2),
                         (load(R + "inventory/bubble3.wav"), 0.03, -6)]))


if __name__ == "__main__":
    main()
