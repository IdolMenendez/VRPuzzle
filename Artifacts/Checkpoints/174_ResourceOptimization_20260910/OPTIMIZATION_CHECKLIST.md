# V82 execution checklist (before resource changes)

Baseline APK: 280,115,601 bytes. Audit: 25 referenced models, 5,970 serialized references.

- [x] Immutable V82 scene/model copies, original V80 scenes preserved.
- [x] Font subset: original 15.7 MiB font referenced only by seven runtime scenes; verify every scanned character supported by original remains supported.
- [x] Trial Medium mesh compression on copied models; retain vertex/triangle/submesh identities, audit vertex error, render and run all 53 pieces. Roll back candidates that fail.
- [x] Inspect and compress three embedded Biplane material textures (about 2.7 MiB each).
- [x] Build with exact required external SDK/JDK/NDK; measure APK bytes, signature, CRC and original-resolution stereo captures.

Deferred based on evidence: old 16-part Eiffel is actively referenced by Completion/Collection, so not garbage; unifying display models needs a separate semantic assembly change. PICO audio libraries have native call declarations and no connected headset for load validation. Leave Read/Write, topology, managed stripping and shader stripping unchanged in this trial.

## Final disposition

Executed: 23 model copies Medium, 2 workbench copies original Low after UV visual rejection; 544-codepoint font subset with build-time coverage guard; 3 biplane texture copies ASTC 6x6 / 1K. All 25 FBX source copies are byte-identical; topology unchanged. Final APK 230,554,388 bytes, down 17.6931% from V81. Deferred items above remain unchanged.
