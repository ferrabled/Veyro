"""Original score for the Veyro Run motion reel — synthesized, so there is no third-party music
licence to clear (Devpost rule: no copyrighted music). F major, cut to the picture:
every section change, jump, coin and slam in reel.html lands on a musical event here.
Foley comes from the game's own CC0 SFX (game/Assets/Resources/Audio/Sfx, see SOURCES.md there).

  .venv/Scripts/python music.py [out/events.json] [out/score.wav]

The arrangement below is written in the page's design time (120-BPM seconds). The page exports
its playback timescale with the cue list; K stretches every position and note length by it, so
the same score plays at 90 BPM when the picture does.
"""
import json, subprocess
import numpy as np
import scipy.signal as sg

import sys
EVENTS_PATH = sys.argv[1] if len(sys.argv) > 1 else 'out/events.json'
OUT_PATH = sys.argv[2] if len(sys.argv) > 2 else 'out/score.wav'
_cues = json.load(open(EVENTS_PATH))
if isinstance(_cues, dict): K = 1.0 / _cues['timescale']; ev = _cues['events']
else: K = 1.0; ev = _cues
SR = 48000
DUR = 20.0                      # design seconds
OUT = DUR * K                   # output seconds
N = int(SR * (OUT + 2.5))
rng = np.random.default_rng(7)
BEAT = 0.5

def mtof(m): return 440.0 * 2 ** ((m - 69) / 12)
NOTE = {n: i for i, n in enumerate(['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'])}
def nm(s):  # "Bb4" -> midi
    name, octv = s[:-1], int(s[-1]); return 12 * (octv + 1) + NOTE[name]

def tt(n): return np.arange(n) / SR
def lp(x, f, order=2): return sg.sosfilt(sg.butter(order, min(f, SR * .45), 'low', fs=SR, output='sos'), x)
def hp(x, f, order=2): return sg.sosfilt(sg.butter(order, f, 'high', fs=SR, output='sos'), x)
def bp(x, lo, hi, order=2): return sg.sosfilt(sg.butter(order, [lo, hi], 'band', fs=SR, output='sos'), x)

class Bus:
    def __init__(self): self.x = np.zeros((2, N))
    def add(self, sig, t, gain=1.0, pan=0.0):
        i = int(round(t * K * SR))
        if i >= N: return
        if sig.ndim == 1: sig = np.stack([sig * np.sqrt(.5 * (1 - pan)), sig * np.sqrt(.5 * (1 + pan))]) * np.sqrt(2)
        n = min(sig.shape[1], N - i)
        if i < 0: sig = sig[:, -i:]; n = min(sig.shape[1], N); i = 0
        self.x[:, i:i + n] += sig[:, :n] * gain

drums, bass, pad, arp, lead, fx, sfx, verb_send = (Bus() for _ in range(8))

# ------------------------------------------------------------------ drums
def kick(big=False):
    n = int(.5 * SR); t = tt(n)
    f = 44 + (150 if big else 120) * np.exp(-t * 30)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * (5 if big else 8))
    x += rng.standard_normal(n) * np.exp(-t * 700) * .35
    return np.tanh(x * 1.8) * .9
def clap():
    n = int(.4 * SR); t = tt(n); nz = bp(rng.standard_normal(n), 900, 5200)
    env = np.zeros(n)
    for k, d in enumerate([0, .012, .024]):
        i = int(d * SR); env[i:] += np.exp(-(t[:n - i]) * (160 if k < 2 else 16))
    body = np.sin(2 * np.pi * 185 * t) * np.exp(-t * 30) * .4
    return (nz * env * 1.3 + body) * .7
def snare():
    n = int(.25 * SR); t = tt(n)
    return (bp(rng.standard_normal(n), 1500, 7000) * np.exp(-t * 26) + np.sin(2 * np.pi * 200 * t) * np.exp(-t * 40) * .6) * .6
def hat(open_=False):
    n = int((.3 if open_ else .06) * SR); t = tt(n)
    return hp(rng.standard_normal(n), 7500, 4) * np.exp(-t * (11 if open_ else 75)) * .38
def crash(dur=2.4):
    n = int(dur * SR); t = tt(n)
    metal = sum(np.sign(np.sin(2 * np.pi * f * t)) for f in (527, 811, 1187, 1693, 2317)) * .12
    x = hp(rng.standard_normal(n) + metal, 3500, 2) * np.exp(-t * 2.0)
    return np.stack([x, np.roll(x, 37)]) * .5
def impact(short=False):
    n = int((.45 if short else 1.6) * SR); t = tt(n)
    f = 32 + 60 * np.exp(-t * 6)
    boom = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * (9 if short else 2.6))
    nz = lp(rng.standard_normal(n), 900) * np.exp(-t * 9) * .6
    return np.tanh((boom + nz) * 1.5) * .9
def riser(dur):
    dur *= K; n = int(dur * SR); t = tt(n); p = t / dur
    nz = rng.standard_normal(n)
    out = np.zeros(n); seg = 2048
    for i in range(0, n, seg):                       # band sweeps 300 Hz -> 9 kHz
        c = 300 * (30 ** p[i]); lo, hi = c * .7, min(c * 1.4, SR * .45)
        out[i:i + seg] = bp(nz[i:i + seg], lo, hi, 1)
    sweep = np.sin(2 * np.pi * np.cumsum(180 * (7 ** p)) / SR) * .25
    return (out * 1.4 + sweep) * p ** 2.2 * .6
def reverse_crash(dur=1.0):
    c = crash(dur)[0][::-1]
    return c * .9
def whoosh(dur=.45, up=True):
    dur *= K; n = int(dur * SR); t = tt(n); p = t / dur
    nz = rng.standard_normal(n); out = np.zeros(n); seg = 1024
    for i in range(0, n, seg):
        c = (400 * 12 ** p[i]) if up else (5000 * (1 / 12) ** p[i])
        out[i:i + seg] = bp(nz[i:i + seg], c * .6, min(c * 1.6, SR * .45), 1)
    return out * np.sin(np.pi * p) ** 1.5 * .9

# ------------------------------------------------------------------ tonal voices
def pulse(freq, n, duty=.25, vib=0.0, slide=0.0):
    t = tt(n)
    f = freq * 2 ** ((vib * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - .09) * 8, 0, 1) - slide * np.exp(-t * 60)) / 12)
    ph = np.cumsum(f) / SR % 1.0
    return np.where(ph < duty, 1.0, -1.0) - (2 * duty - 1)
def saw(freq, n, detune=0.0):
    ph = (np.arange(n) * freq * 2 ** (detune / 1200) / SR) % 1.0
    return 2 * ph - 1
def env(n, a=.005, d=.1, s=.6, r=.05, hold=None):
    t = tt(n); hold = hold if hold is not None else n / SR - r
    e = np.where(t < a, t / a, s + (1 - s) * np.exp(-(t - a) / max(d, 1e-4)))
    rel = np.clip(1 - (t - hold) / r, 0, 1)
    return e * np.where(t > hold, rel, 1)

def lead_note(m, dur, gain=1.0):
    dur *= K; n = int((dur + .25) * SR)
    x = pulse(mtof(m), n, .25, vib=.18, slide=1.0) * .55 + pulse(mtof(m) * 1.003, n, .125) * .25
    return lp(x, 5200) * env(n, .003, .18, .55, .12, hold=dur) * gain
def bass_note(m, dur, gain=1.0):
    dur *= K; n = int((dur + .05) * SR)
    x = pulse(mtof(m), n, .5) * .6 + np.sin(2 * np.pi * mtof(m) * tt(n)) * .7
    return lp(x, 1100) * env(n, .003, .12, .75, .03, hold=dur) * gain
def pad_chord(ms, dur, gain=1.0, rel=.5, cutoff=2200):
    dur *= K; rel *= K; n = int((dur + rel) * SR); x = np.zeros(n)
    for m in ms:
        for dt in (-9, 0, 8): x += saw(mtof(m), n, dt)
    x = lp(x / (len(ms) * 3), cutoff, 2)
    return x * env(n, .12, .5, .8, rel, hold=dur) * gain
def pluck(m, gain=1.0, cutoff=3000):
    n = int(.28 * SR); t = tt(n)
    x = saw(mtof(m), n) * .6 + pulse(mtof(m), n, .5) * .3
    return lp(x, cutoff) * np.exp(-t * 16) * gain
def blip(f0, f1, dur=.08, gain=.4):
    n = int(dur * SR); t = tt(n)
    return np.sin(2 * np.pi * np.cumsum(np.geomspace(f0, f1, n)) / SR) * np.exp(-t * (6 / dur)) * gain

CH = {'Fmaj7': [53, 57, 60, 64], 'F': [53, 57, 60, 65], 'Dm': [50, 53, 57, 62], 'Bb': [46, 50, 53, 58], 'C': [48, 52, 55, 60]}
ROOT = {'Fmaj7': 41, 'F': 41, 'Dm': 38, 'Bb': 34, 'C': 36}

# ------------------------------------------------------------------ arrangement
FOUR = [(2.0, 5.5), (6.0, 7.0), (11.0, 13.5), (15.0, 17.5)]           # four-on-the-floor sections
KICKS = []
for a, b in FOUR:
    k = a
    while k < b - 1e-6: KICKS.append(k); k += BEAT
KICKS += [7.0, 7.75, 9.0, 9.75, 10.0, 13.5, 14.0, 14.5, 18.0]           # half-time + stops + the slam
KICKS = sorted(set(round(k, 4) for k in KICKS))
for k in KICKS: drums.add(kick(big=k in (2.0, 11.0, 18.0, 7.0)), k, 1.0)
for c in [2.5, 3.5, 4.5, 6.5, 11.5, 12.5, 15.5, 16.5]: drums.add(clap(), c, .8, .05); verb_send.add(clap(), c, .25)
drums.add(snare(), 7.0, .9); drums.add(clap(), 8.0, .9); verb_send.add(clap(), 8.0, .35)
for a, b in FOUR:
    s = a
    while s < b - 1e-6:
        ph = round((s - a) / .125) % 4
        if ph == 2: drums.add(hat(True), s, .5, .3)
        else: drums.add(hat(), s, [.55, .3, 0, .35][ph], -.3)
        s += .125
s = 7.0
while s < 10.0 - 1e-6:                                                   # half-time hats
    drums.add(hat(), s, .28 if round((s - 7) / .125) % 2 else .45, -.2); s += .125
s = 1.0
while s < 2.0 - 1e-6: drums.add(hat(), s, .08 + .3 * (s - 1.0), -.2); s += .125
def roll(a, b, g0=.15, g1=.9):
    s = a
    while s < b - 1e-6:
        p = (s - a) / (b - a); step = .125 if p < .5 else (.0625 if p < .8 else .03125)
        drums.add(snare(), s, g0 + (g1 - g0) * p ** 1.5, .1); s += step
roll(1.5, 2.0); roll(10.0, 11.0, .08, .8); roll(17.5, 18.0, .2, 1.0)
for c in [2.0, 7.0, 11.0, 15.0, 18.0]: fx.add(crash(3.0 if c == 18.0 else 2.4), c, 1.0 if c != 15.0 else .6)
for c in [2.0, 11.0, 18.0]: fx.add(impact(), c, 1.0 if c != 18.0 else 1.25)
for c, g in [(13.5, .7), (14.0, .8), (14.5, 1.0)]: fx.add(impact(True), c, g); fx.add(crash(.6), c, .45)
fx.add(riser(1.0), 1.0, .8); fx.add(riser(1.0), 10.0, .9); fx.add(riser(.7), 17.3, 1.0)
for c in [2.0, 11.0, 18.0]:
    rc = reverse_crash(.9 * K); fx.add(rc, c - len(rc) / SR / K, .7)

# pad
PADS = [(0.0, 2.0, 'Fmaj7', .55, 1400), (2.0, 2.0, 'F', .5, 2200), (4.0, 1.5, 'Dm', .5, 2200), (5.5, .5, 'Dm', .35, 900),
        (6.0, 2.0, 'Bb', .5, 2000), (8.0, 2.0, 'C', .55, 1800), (10.0, 1.0, 'Dm', .55, 2400), (11.0, 1.0, 'Dm', .5, 2600),
        (12.0, 1.5, 'Bb', .5, 2600), (13.5, .3, 'Bb', .8, 3000), (14.0, .3, 'C', .8, 3000), (14.5, .35, 'Dm', .9, 3200),
        (15.0, 1.0, 'F', .5, 2400), (16.0, 1.0, 'Bb', .5, 2400), (17.0, 1.0, 'C', .55, 2600), (18.0, 1.3, 'F', .9, 3200)]
for a, d, ch, g, cut in PADS:
    x = pad_chord(CH[ch] + ([65, 69] if a == 18.0 else []), d, g, rel=1.4 if a == 18.0 else .35, cutoff=cut)
    if a == 0.0: x *= np.clip(tt(len(x)) / 1.2, 0, 1)
    pad.add(x, a); verb_send.add(x, a, .3)

# bass
def octaves(a, b, root, g=.9):
    s = a; k = 0
    while s < b - 1e-6: bass.add(bass_note(root + (12 if k % 2 else 0), .2), s, g); s += .25; k += 1
octaves(2.0, 4.0, 41); octaves(4.0, 5.5, 38); bass.add(bass_note(38, .5, .6), 5.5)
octaves(6.0, 7.0, 34); bass.add(bass_note(34, .95), 7.0); bass.add(bass_note(46, .2), 7.75)
bass.add(bass_note(36, .95), 8.0); bass.add(bass_note(36, .7), 9.0); bass.add(bass_note(48, .2), 9.75)
s = 10.0
while s < 11.0 - 1e-6: bass.add(bass_note(38, .1, .4 + .6 * (s - 10)), s); s += .125
octaves(11.0, 12.0, 38); octaves(12.0, 13.5, 34)
for c, r in [(13.5, 34), (14.0, 36), (14.5, 38)]: bass.add(bass_note(r, .3, 1.1), c); bass.add(bass_note(r + 12, .3, .5), c)
octaves(15.0, 16.0, 41); octaves(16.0, 17.0, 34); octaves(17.0, 17.5, 36)
s = 17.5
while s < 18.0 - 1e-6: bass.add(bass_note(36, .06, .4 + (s - 17.5)), s); s += .0625
bass.add(bass_note(29, 1.6, 1.1), 18.0); bass.add(bass_note(41, 1.2, .6), 18.0)

# lead: 8th-note phrases ('.' rest, '-' tie)
def phrase(a, notes, g=1.0):
    toks = notes.split(); i = 0
    while i < len(toks):
        tk = toks[i]
        if tk in '.-': i += 1; continue
        j = i + 1
        while j < len(toks) and toks[j] == '-': j += 1
        x = lead_note(nm(tk), .25 * (j - i) - .03, g); lead.add(x, a + .25 * i, 1.0, .1); verb_send.add(x, a + .25 * i, .25)
        i = j
phrase(2.0, 'C5 . A4 C5 D5 . C5 A4')
phrase(4.0, 'A4 . F4 A4 C5 - . .')
phrase(6.0, 'Bb4 . D5 F5')
phrase(11.0, 'D5 . C5 D5 F5 . D5 C5')
phrase(13.0, 'A4 C5')
for c, ms in [(13.5, ['D5', 'F5', 'Bb5']), (14.0, ['E5', 'G5', 'C6']), (14.5, ['F5', 'A5', 'D6'])]:
    for m in ms: x = lead_note(nm(m), .28, .6); lead.add(x, c); verb_send.add(x, c, .4)
phrase(15.0, 'C5 . A4 C5 D5 . F5 D5')
phrase(17.0, 'E5 . G5 .')
for m in ['F5', 'A5', 'C6', 'F6']:
    x = lead_note(nm(m), 1.1, .55); lead.add(x, 18.0); verb_send.add(x, 18.0, .6)

# arps: 16th plucks over chord tones
def arpeggio(a, b, ch, g=.5, cut0=600, cut1=4000):
    tones = CH[ch] + [m + 12 for m in CH[ch]]; s = a; k = 0
    while s < b - 1e-6:
        p = (s - a) / max(b - a, 1e-6); m = tones[[0, 1, 2, 3, 4, 3, 2, 1][k % 8]]
        x = pluck(m + 12, g, cut0 * (cut1 / cut0) ** p); arp.add(x, s, 1.0, .35 if k % 2 else -.35); verb_send.add(x, s, .2)
        s += .125; k += 1
arpeggio(0.0, 2.0, 'Fmaj7', .5, 500, 5000)
arpeggio(7.0, 8.0, 'Bb', .35, 1500, 2500); arpeggio(8.0, 10.0, 'C', .35, 1500, 3000); arpeggio(10.0, 11.0, 'Dm', .45, 1500, 7000)
arpeggio(15.0, 16.0, 'F', .22, 2500, 2500); arpeggio(16.0, 17.0, 'Bb', .22, 2500, 2500); arpeggio(17.0, 17.5, 'C', .3, 2500, 5000)
for i, m in enumerate(['C6', 'F6', 'A6', 'C7']):                     # final sparkle
    x = pluck(nm(m), .35, 6000); arp.add(x, 18.25 + i * .0625, 1, (-.5 + i / 3)); verb_send.add(x, 18.25 + i * .0625, .5)

# ------------------------------------------------------------------ foley from the game (CC0)
def ogg(name):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', f'sfx/{name}.ogg', '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'], capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)
def repitch(x, ratio): return np.interp(np.arange(0, len(x) - 1, ratio), np.arange(len(x)), x)
S = {k: ogg(k) for k in ['coin', 'jump', 'lane_swoosh', 'streak', 'countdown_tick', 'new_best', 'slide', 'ui_tap']}
PENTA = [nm(n) for n in ['F6', 'G6', 'A6', 'C7', 'D7', 'F7']]
coins = [e['t'] for e in ev if e['type'] == 'coin']
run = 0; last = -1
for c in coins:
    run = run + 1 if c - last < .12 else 0; last = c
    sfx.add(repitch(S['coin'], mtof(PENTA[min(run, 5)]) / 1990.0), c, .42, .2); verb_send.add(repitch(S['coin'], mtof(PENTA[min(run, 5)]) / 1990.0), c, .15)
def jump_boing(t0):                                   # pitch follows the real arc: v0 7.5 m/s, g 22 m/s^2
    air = 2 * 7.5 / 22; n = int(air * K * SR); t = tt(n) / K; y = 7.5 * t - 11 * t * t
    f = 420 * 2 ** (y / 1.28 * 1.0)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 2.2) * .22
    return x * np.clip(t / .01, 0, 1)
tick_i = 0
for e in ev:
    t, k = e['t'], e['type']
    if k == 'swoosh': sfx.add(S['lane_swoosh'], t, .9, -.2 if round(t * 2) % 2 else .2)
    elif k == 'stamp': sfx.add(S['streak'], t, .9); sfx.add(kick() * .5, t, .6)
    elif k == 'whoosh': x = whoosh(.45 if t != 12.9 else .6); sfx.add(x, t, .7); verb_send.add(x, t, .2)
    elif k == 'tap': sfx.add(S['ui_tap'], t, 1.0); sfx.add(blip(1800, 900, .05, .25), t)
    elif k == 'jump': sfx.add(S['jump'], t, .9); sfx.add(jump_boing(t), t, 1.0)
    elif k == 'tick': sfx.add(repitch(S['countdown_tick'], 2 ** (tick_i / 12 * 1.5)), t, .8, -.4 + tick_i * .1); tick_i += 1
    elif k == 'type':
        s = t
        while s < t + .55: sfx.add(blip(4200 + rng.random() * 800, 3000, .012, .12), s, 1.0, rng.random() - .5); s += .045
    elif k == 'print':
        n = int(.58 * K * SR); tp = tt(n)
        gate = (np.sin(2 * np.pi * 42 * tp) > 0).astype(float)
        x = bp(rng.standard_normal(n), 1200, 6000) * gate * .22 + np.sin(2 * np.pi * np.cumsum(np.linspace(700, 1400, n)) / SR) * .05
        sfx.add(x * np.clip(tp / .02, 0, 1) * np.clip((.58 * K - tp) / .05, 0, 1), t)
    elif k == 'deal': sfx.add(S['streak'], t, .9, [-.4, 0, .4][int(round((t - 15.3) / .2)) % 3]); sfx.add(S['slide'][:int(.2 * SR)], t - .12, .35)
    elif k == 'flip': sfx.add(S['slide'], t, .6, [-.4, 0, .4][int(round((t - 16.3) / .09)) % 3])
    elif k == 'pop': sfx.add(blip(300, 1400, .12, .5), t)
    elif k == 'pop2': sfx.add(S['new_best'], t - .15, .55); verb_send.add(S['new_best'], t - .15, .3)
    elif k == 'slam': sfx.add(S['streak'], t, .7)
    elif k == 'slam_big': sfx.add(S['streak'], t, 1.0)
# viewfinder boot blips
for i, t in enumerate([7.72, 7.8, 7.88, 7.96, 8.04]): sfx.add(blip(1200 + i * 300, 1200 + i * 300, .05, .18), t, 1.0, .5)
for t in [8.0, 8.5, 9.0, 9.5, 10.0]: sfx.add(blip(2400, 2400, .03, .12), t + .04, 1.0, .6)      # tracker lock-on

# ------------------------------------------------------------------ mix
def sidechain(x, depth=.55, rel=.14):
    g = np.ones(N); t_all = np.arange(N) / SR
    for k in KICKS:
        i = int(k * K * SR); n = int(.5 * SR); tloc = np.arange(min(n, N - i)) / SR
        g[i:i + len(tloc)] = np.minimum(g[i:i + len(tloc)], 1 - depth * np.exp(-tloc / rel))
    return x * g
ir_n = int(1.8 * SR); tir = tt(ir_n)
ir = np.stack([lp(rng.standard_normal(ir_n), 5000) * np.exp(-tir * 3.2), lp(rng.standard_normal(ir_n), 5000) * np.exp(-tir * 3.2)])
ir /= np.sqrt((ir ** 2).sum(axis=1, keepdims=True))                     # unit-energy IR
wet = np.stack([sg.fftconvolve(verb_send.x[c], ir[c])[:N] for c in range(2)])
# dotted-eighth ping-pong on the lead
dl = int(.375 * K * SR); dly = np.zeros((2, N))
for k in range(1, 5):
    g = .32 ** k; c = k % 2
    dly[c, k * dl:] += lead.x[c, :N - k * dl] * g + lead.x[1 - c, :N - k * dl] * g * .3
def rms(x): return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)
for name, b in [('drums', drums.x), ('bass', bass.x), ('pad', pad.x), ('arp', arp.x), ('lead', lead.x), ('fx', fx.x), ('sfx', sfx.x), ('wet', wet), ('dly', dly)]:
    print(f'{name:6s} rms {rms(b):6.1f} dB  peak {20*np.log10(np.abs(b).max()+1e-9):6.1f} dB')
# balance by target RMS per bus (dB), so the mix does not depend on each voice's raw scale
TARGET = {'drums': -15, 'bass': -21, 'pad': -24, 'arp': -27, 'lead': -20, 'fx': -22, 'sfx': -21, 'wet': -27, 'dly': -29}
BUSES = {'drums': drums.x, 'bass': sidechain(bass.x), 'pad': sidechain(pad.x, .45), 'arp': sidechain(arp.x, .35), 'lead': lead.x, 'fx': fx.x, 'sfx': sfx.x, 'wet': wet, 'dly': dly}
mix = sum(x * 10 ** ((TARGET[k] - rms(x)) / 20) for k, x in BUSES.items())
mix = hp(mix, 28, 2)
# gentle master: glue + soft clip, fade the tail into the last frame
mix /= np.abs(mix).max() / 1.25
mix = np.tanh(mix * 1.1) / np.tanh(1.1)
t_all = np.arange(N) / SR
mix *= np.clip((OUT - t_all) / (.7 * K), 0, 1) ** 1.5
mix = mix[:, :int(OUT * SR)]
mix /= np.max(np.abs(mix)) / 0.89
import wave
pcm = (np.clip(mix.T, -1, 1) * 32767).astype('<i2')
with wave.open(OUT_PATH, 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
print(OUT_PATH, mix.shape[1] / SR, 's', f'({120 / K:.0f} BPM)')
