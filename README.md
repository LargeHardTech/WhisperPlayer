# WhisperPlayer

🎵 Terminal-based music player with ASCII video playback. BASS-powered, EQ, spectrum, LRC lyrics, script engine, and more.

**Like Winamp, but in your terminal.**

> 💡 This project was built with AI assistance (WorkBuddy + Deepseek).
> Code reviewed, tested, and directed by a human. AI handled the heavy lifting — architecture, implementation, debugging — under human supervision.

## Features

- 🎧 BASS audio engine — MP3, FLAC, WAV, OGG, AAC, M4A, APE, MIDI, .mp4 video audio
- 🎥 ASCII video playback — play .mp4 videos as text art in your terminal, powered by FFmpeg
- 🎚 10-band parametric EQ — Flat / Pop / Rock / Classical / Jazz / Bass Boost / Treble / Vocal presets
- 📊 Spectrum analyzer — 16-band real-time FFT visualization
- 🎤 8-bit chiptune mode — waveOut synth for retro vibes
- 📜 LRC lyrics — synced lyric display with highlight
- 🎚 Speed control — 0.5x ~ 2.0x with pitch preservation
- 🔄 Play modes — sequential / single loop / random
- 🔁 A-B loop — loop any segment
- 🎹 MIDI playback — built-in synthesizer
- 🚀 Zero dependency scripting — .whisp script engine with :exec
- 🎛 Command mode — :help, :seek, :vol, :info, :thanks and more
- 🖥 Terminal.Gui TUI — beautiful console UI, keyboard-driven
- 📦 Portable — compressed ffmpeg.exe embedded, extracts on first run
- 🎨 CharAnim engine — text-based animations for lyric-free playback

## Screenshots

```
┌─────────── WhisperPlayer ───────────────────────────┐
│  Artist - Song Title                   [Device ▾]   │
│ ▂▃▅▆███▆▅▃▂▁  00:42 / 03:15                        │
│                                                     │
│  .:=+*#%@%##*#@@@%#*+==-::...                       │
│  .:=*#%@%##*#@@@%#*+==-::...     ← ASCII Video MV  │
│  .:=*#%@%##*#@@@%#*+==-::...                        │
│  .:=*#%@%##*#@@@%#*+==-::...                        │
│  .:=*#%@%##*#@@@%#*+==-::...                        │
│                                                     │
│ Help │ ► ▐▐ █▌ │ EQ │ 8_Bit │ AB: 0:42 - 1:15     │
│ Vol: ████████░░ 1.00x Sequential   >>>              │
└─────────────────────────────────────────────────────┘
```

## Quick Start

1. Download the latest release
2. Extract anywhere
3. Drag music files onto `WhisperPlayer.exe`

Or from command line:

```bash
WhisperPlayer music.mp3
WhisperPlayer concert.mp4
WhisperPlayer playlist/
```

## Controls

| Key | Action |
|-----|--------|
| Space | Play / Pause |
| ← → | Seek ±5s |
| . / , | Prev / Next |
| ↑ ↓ | Volume |
| Delete | Remove from playlist |
| A / B | Set A-B loop |
| C | Clear playlist |
| Ctrl+O | Open files |
| : | Command mode |
| E | Equalizer |
| M / V | Toggle video |
| Q | Quit |

### Commands (type `:` then the command)

| Command | Description |
|---------|-------------|
| `:help` | List all commands |
| `:play` | Play |
| `:pause` | Pause |
| `:stop` | Stop |
| `:next` / `:prev` | Next / Previous |
| `:vol 0.5` | Set volume |
| `:seek +30` | Seek forward 30s |
| `:speed 1.5` | Set playback speed |
| `:mode random` | Play mode |
| `:exec script.whisp` | Run script |
| `:info` | About WhisperPlayer |
| `:thanks` | Open source credits |
| `:q` | Quit |

## Script Engine

`.whisp` files are simple text scripts — one command per line:

```whisp
play
wait s 5
next
wait done
vol 0.8
pause
```

Lines starting with `#` are comments.

## Building

```bash
git clone https://github.com/LargeHardTech/WhisperPlayer.git
cd WhisperPlayer
dotnet build
```

Requires .NET 8.0 SDK or later.

## Tech Stack

- [BASS Audio Library](https://www.un4seen.com/) — audio engine
- [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) — TUI framework
- [FFmpeg](https://ffmpeg.org/) — video decoding
- WaveOutSynth — custom 8-bit chiptune engine
- NativeAOT compiled — tiny binary, no runtime needed

## License

MIT © 2025-2026 LargeHardTech

This project includes FFmpeg, licensed under LGPL v2.1+.
See [ffmpeg/LICENSE.txt](ffmpeg/LICENSE.txt) for details.

---

*Made with 🎵 in Hebei, China*
