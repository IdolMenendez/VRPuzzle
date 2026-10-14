# V15 open-platform music source and license

## Source

- Pack: `PLAYFUL PIANO | Free Music Pack (WAV / OGG, Loopable + Full)`
- Composer / publisher: Dylann Taylor
- Official download page: https://dylanntaylor.itch.io/playful-piano
- Downloaded archive: `/Users/bytedance/Downloads/PLAYFUL PIANO by DYLANN TAYLOR.zip`
- Archive size: `155487792` bytes
- Archive SHA-256: `de4f920096c6adeae74384ac385b500c4069c51b9017e65698b37cc71759693d`
- Download date: `2026-07-24`

The publisher describes the pack as a cohesive set of lighthearted,
jazz-inspired piano variants supplied in seamless loop and full forms. The
download page explicitly marks all included files as CC0, including commercial
use, modification and distribution inside a finished project.

## License

- License: Creative Commons CC0 1.0 Universal
- License URL: https://creativecommons.org/publicdomain/zero/1.0/
- Attribution: not required by CC0; this project still records and credits the
  composer for provenance.

## Imported loop masters

The Unity assets are the unmodified 48 kHz / 24-bit stereo WAV loop masters.
Unity imports them as streaming Vorbis while preserving their 48 kHz sample
rate. No loudness normalization or re-encoding was applied before Unity import,
so the authored loop boundary remains intact.

| Unity asset | Role | Integrated loudness | SHA-256 |
| --- | --- | ---: | --- |
| `BGM_Open_01_Atmos_DylannTaylor_V15.wav` | default random pool; calm/airy | -19.6 LUFS | `cc893a377e2bb972d261e6188546b14dae743cf8837cdac5e40b9b16cbe7a02f` |
| `BGM_Open_02_Original_DylannTaylor_V15.wav` | default random pool; neutral workshop | -20.5 LUFS | `6dd2d49f92c1c73380f1c04716a4012e76cc82a3c85911d4d5c3de637b4aa603` |
| `BGM_Open_03_Melody_DylannTaylor_V15.wav` | default random pool; rewarding/bright | -18.4 LUFS | `8cfac22efd7ff2b6e3e5a5ba2c730cb7beffbb1a7d9c8e77c3754eef21b5e147` |
| `BGM_Open_04_Strings_DylannTaylor_V15.wav` | default random pool; warm/soft | -18.7 LUFS | `b0b628a7b8d08e7ed9c896e7f4701e071bd740a688bd14268266e0c7f2dedb7f` |
| `BGM_Open_05_JazzTrio_DylannTaylor_V15.wav` | imported opt-in candidate; excluded from default pool | -17.9 LUFS | `a33521d8c89c02bf8e3df8b5029c5e2105a797b4d7a612f2481f6663054dc168` |

## Runtime policy

- Puzzle stages randomly select among the four restrained default variants.
- The conductor prevents an immediate repeat when multiple clips are available.
- Crossfades and the existing user music-volume preference remain unchanged.
- `JazzTrio` is intentionally not in the default pool because its more explicit
  rhythm can compete with spatial assembly concentration.
- V15 is an audio-only scene version. It does not alter V14 scene composition,
  puzzle Parts, snap tolerances, physical lamp hint interaction, cameras,
  PICO/OpenXR wiring, or desktop controls.
