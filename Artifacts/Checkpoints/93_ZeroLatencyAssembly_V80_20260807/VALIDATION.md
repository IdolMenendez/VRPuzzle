# V80 zero-latency assembly - Validation

Status: `SCENE / STATIC / FOCUSED PLAYMODE / 53-PART REGRESSION / GPU VISUAL / FINAL VALIDATOR / PICO APK / SWAN INSTALL+HASH PASS - USER-WORN RESPONSIVENESS PENDING`.

## Root causes and fixes

- Eiffel Part 02 was not changed by V79 Part 10 work. It still used
  `0.145 m / 135 degrees`; its authored four-leg foundation pose can be almost
  `180 degrees` from the socket, so close releases were rejected by rotation.
  V80 uses a scoped `0.180 m / 180 degrees` symmetric profile after Part 01.
- Rendering was the larger performance cost. V79 Ship drew approximately
  `4.96M` triangles because the full solved blueprint duplicated all thirteen
  high-poly Parts. V79 Eiffel also had 22 active lights and three soft-shadow
  lights.
- CPU had deterministic waste: both persistent hand interactors repeatedly
  searched the whole scene, idle Parts rewrote hidden ghosts, and guidance,
  status and prompt components repeatedly scanned unchanged state.

V80 hides the full blueprint at rest and shows one exact module only during a
physical Hint, caches per-stage interaction objects, uses event-driven status
and idle fast paths, keeps 4 lights per ordinary level / 5 for Eiffel, removes
all real-time shadows, uses an independent 2x-MSAA/no-shadow/four-light URP
asset, and enables low fixed foveation on PICO.

## Evidence

| Surface | Result | Evidence |
|---|---|---|
| Independent scenes / backups | PASS, 10 / 10 byte matched; V79 preserved | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV80_ZeroLatencyAssembly` and `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/Backups/RuntimeV80_ZeroLatencyAssembly` |
| V79 performance baseline | PASS, measured triangles/lights/shadows/meshes | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V79_ASSEMBLY_PERFORMANCE_BASELINE.txt` |
| V80 GPU budget | PASS, triangles reduced 16.2%-41.4%, steady lights 4/4/5/4/4, shadow lights/renderers 0 | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_ASSEMBLY_PERFORMANCE_RESULT.txt` |
| Eiffel Part 02 | PASS, real release at `0.1656 m / 176.4 degrees` | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_FOCUSED_PLAYMODE_SMOKE_R05.log` |
| On-demand blueprint | PASS, 0 modules at rest / exactly 1 during Hint / returns to 0 | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_FOCUSED_PLAYMODE_SMOKE_R05.log` |
| Idle CPU window | PASS, 120 frames average `0.97 ms`, worst `4.11 ms`, main-thread allocation `0 bytes` in Editor test | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_FOCUSED_PLAYMODE_SMOKE_R05.log` |
| Complete interaction | PASS, Cottage `13/13`, Ship `13/13`, Eiffel `10/10`, Pagoda `9/9`, Windmill `8/8` with grab/move/bad-return/valid-snap | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_COMPLETE_INTERACTION_SMOKE.log` |
| Visual review | PASS, five front and five left-eye originals plus Part 02 evidence | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_FIVE_ZERO_LATENCY_LEVELS_FINAL.png` |
| Overlay / difference | PASS | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_VS_V79_COTTAGE_PERFORMANCE_OVERLAY.png` and `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_VS_V79_COTTAGE_PERFORMANCE_DIFFERENCE_X4.png` |
| Audio | PASS | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_AUDIO_EVIDENCE.md` |
| Final validator | PASS | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_VALIDATE_FINAL.log` |
| External Android toolchain | PASS | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/PICO_APK_SHA256_V80.txt` |
| APK / Swan identity | PASS, `443233558` bytes and exact local/device hash | `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/DEVICE_INSTALL_V80.md` |
| User-worn responsiveness | PENDING, device was `far`; automated Editor timing is not headset feel | This record |

## Rejected diagnostics

- Initial compile was rejected before scene generation by an ambiguous
  `FoveatedRenderingMode` enum; the PICO type is now explicit.
- Focused smoke R01-R04 used an over-broad runtime shadow/light assertion.
  Diagnosis proved the sixth light was only the short-lived V05 arrival glow;
  the accepted R05 requires five steady Eiffel lights plus at most that one
  transient light, all with no shadows.

Final screenshot:

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/93_ZeroLatencyAssembly_V80_20260807/V80_FIVE_ZERO_LATENCY_LEVELS_FINAL.png`
