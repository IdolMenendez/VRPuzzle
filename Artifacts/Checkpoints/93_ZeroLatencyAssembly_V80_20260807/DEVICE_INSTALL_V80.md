# V80 Swan install and binary identity

- Date: 2026-08-07
- Physical device serial: `PB321XKGL4210087B`
- Device model / codename: `B3110 / swan`
- Package: `com.bytedance.personallypuzzled.xr`
- Install command: `adb -s PB321XKGL4210087B install -r -g`
- Install result: `Success`
- Installed package path:
  `/data/app/~~QpXDngYr6fljM3UtxiN6Zg==/com.bytedance.personallypuzzled.xr-_rBGnYMhWGEZVBDg5PremQ==/base.apk`
- Local / device bytes: `443233558 / 443233558`
- Local / device SHA-256:
  `a13a1c5c4d99d0011341c407211b3930efd1245100a7d20c0b19ec6640ccfe7a`
- Binary identity: `PASS - exact byte count and hash match`
- Application PID after replacement: empty
- Device wear state: `far`
- Automatic launch: no

The physical Swan and `emulator-5554` were both connected. Installation
explicitly targeted the physical serial; the emulator was not modified.

No device property was written. Pre/post values remained
`persist.pvr.sleep_by_static=1`, empty `persist.psensor.sleep.delay`,
`persist.psensor.screenoff.delay=10`, `stay_on_while_plugged_in=2`, empty
`key_et_enable`, empty `persist.pvr.record_vision`, and empty
`service.adb.root`.

User-worn V80 responsiveness acceptance remains pending.
