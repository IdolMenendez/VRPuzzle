#!/usr/bin/env python3
"""Prepare a Unity 6000.0 Gradle export for Android-only build servers.

Run after building the unmodified export with :launcher:assembleRelease.
The game's IL2CPP runtime stays IL2CPP: only its host-specific compiler is
removed. All packaged native libraries come from that same export's APK.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile


def prepare(source, destination, wrapper):
    apk = source / 'launcher/build/outputs/apk/release/launcher-release.apk'
    if not apk.is_file():
        raise SystemExit('Build the raw Unity export before preparing delivery.')
    if destination.exists():
        raise SystemExit('Destination must not exist; never overwrite a delivery.')
    for file in ('gradlew', 'gradlew.bat', 'gradle/wrapper/gradle-wrapper.jar',
                 'gradle/wrapper/gradle-wrapper.properties'):
        if not (wrapper / file).is_file():
            raise SystemExit('Missing official Gradle wrapper file: ' + file)

    def ignore(directory, names):
        excluded = {'build', '.gradle', '.cxx', 'local.properties', 'symbols',
                    'Il2CppOutputProject', 'jniStaticLibs', 'cpp', '.DS_Store'}
        return [n for n in names if n in excluded or 'DoNotShip' in n]

    shutil.copytree(source, destination, ignore=ignore)
    for file in destination.rglob('*'):
        if file.is_symlink():
            raise SystemExit('Export must contain real files, not symlinks: ' + str(file))
    native = destination / 'unityLibrary/src/main/jniLibs/arm64-v8a'
    # Libraries already provided by AAR dependencies must not be duplicated.
    aar_libs = set()
    for aar in (destination / 'unityLibrary/libs').glob('*.aar'):
        with zipfile.ZipFile(aar) as archive:
            aar_libs.update(Path(n).name for n in archive.namelist()
                            if n.startswith('jni/arm64-v8a/') and n.endswith('.so'))
    wanted = {p.name for p in native.glob('*.so')
              if not p.name.endswith(('.sym.so', '.dbg.so', '.usym.so'))}
    wanted.update({'libil2cpp.so', 'libswappywrapper.so', 'libc++_shared.so'} - aar_libs)
    shutil.rmtree(native)
    native.mkdir()
    with zipfile.ZipFile(apk) as archive:
        for name in sorted(wanted):
            data = archive.read('lib/arm64-v8a/' + name)
            if data[:4] != b'\x7fELF':
                raise SystemExit('Expected Android ELF library: ' + name)
            (native / name).write_bytes(data)

    lib = destination / 'unityLibrary/build.gradle'
    text = lib.read_text()
    if text.count('\ndef getSdkDir()') != 1:
        raise SystemExit('Unexpected Unity IL2CPP export format; inspect before adapting.')
    text = text.split('\ndef getSdkDir()')[0]
    text, count = re.subn(r'\n        externalNativeBuild \{\n            cmake \{.*?\n            \}\n        \}\n',
                          '\n', text, flags=re.S)
    if count != 1:
        raise SystemExit('Unexpected Unity CMake configuration.')
    lib.write_text(text + '\n// Native libraries were compiled from this same Unity export.\n')
    for gradle in destination.rglob('*.gradle'):
        text = gradle.read_text()
        text = re.sub(r'^\s*ndkPath\s+.*\n', '', text, flags=re.M)
        gradle.write_text(text)
    launcher = destination / 'launcher/build.gradle'
    text = launcher.read_text()
    signing = '''
// Supply the platform/upload certificate through environment variables only.
def signingKeys = ['PICO_KEYSTORE_FILE', 'PICO_KEYSTORE_PASSWORD', 'PICO_KEY_ALIAS', 'PICO_KEY_PASSWORD']
def signingValues = signingKeys.collect { System.getenv(it) }
def releaseSigningReady = signingValues.every { it != null && !it.isEmpty() }
if (signingValues.any { it != null && !it.isEmpty() } && !releaseSigningReady) {
    throw new GradleException('Provide all four PICO signing environment variables, or none for an unsigned release.')
}
'''
    text = text.replace('\nandroid {', signing + '\nandroid {', 1)
    text = text.replace('    buildTypes {', '''    signingConfigs {
        if (releaseSigningReady) {
            releaseUpload {
                storeFile file(signingValues[0])
                storePassword signingValues[1]
                keyAlias signingValues[2]
                keyPassword signingValues[3]
            }
        }
    }

    buildTypes {''', 1)
    before, release = text.split('        release {', 1)
    if release.count('signingConfig signingConfigs.debug') != 1:
        raise SystemExit('Unexpected release signing configuration.')
    release = release.replace('signingConfig signingConfigs.debug',
                              'if (releaseSigningReady) signingConfig signingConfigs.releaseUpload')
    launcher.write_text(before + '        release {' + release)
    properties = destination / 'gradle.properties'
    removed = {'unityProjectPath', 'unity.projectPath', 'unity.androidSdkPath',
               'unity.androidNdkPath', 'unity.jdkPath'}
    properties.write_text('\n'.join(line for line in properties.read_text().splitlines()
                                   if line.split('=', 1)[0] not in removed) + '\n')
    for file in ('gradlew', 'gradlew.bat', 'gradle/wrapper/gradle-wrapper.jar',
                 'gradle/wrapper/gradle-wrapper.properties'):
        target = destination / file
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(wrapper / file, target)
    (destination / 'gradlew').chmod(0o755)
    properties = destination / 'gradle/wrapper/gradle-wrapper.properties'
    properties.write_text(properties.read_text().replace('networkTimeout=10000', 'networkTimeout=60000')
                         + 'distributionSha256Sum=20f1b1176237254a6fc204d8434196fa11a4cfb387567519c61556e8710aed78\n')
    (destination / '.gitignore').write_text('.gradle/\n**/build/\n**/.cxx/\nlocal.properties\n.idea/\n*.iml\n*.jks\n*.keystore\n.DS_Store\n')
    # This branch intentionally stores real build inputs, not LFS pointers.
    (destination / '.gitattributes').write_text('* -text\n*.gradle text eol=lf\n*.properties text eol=lf\n*.md text eol=lf\n*.xml text eol=lf\n*.java text eol=lf\n*.yml text eol=lf\ngradlew text eol=lf\ngradlew.bat text eol=crlf\n')
    report = {
        'unity_version': '6000.0.77f1', 'runtime': 'IL2CPP', 'abi': 'arm64-v8a',
        'raw_export_apk_sha256': hashlib.sha256(apk.read_bytes()).hexdigest(),
        'native_libraries': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                             for p in sorted(native.glob('*.so'))},
        'precompiled_native_runtime': True, 'requires_unity_on_build_server': False,
    }
    (destination / 'DELIVERY.json').write_text(json.dumps(report, indent=2) + '\n')
    for file in destination.rglob('*'):
        if file.is_file() and file.suffix in {'.gradle', '.properties', '.xml', '.json'}:
            text = file.read_text(errors='replace')
            if '/Users/' in text or '/Volumes/' in text:
                raise SystemExit('Workstation path remains: ' + str(file))
    print('PORTABLE GRADLE PREPARATION PASSED:', destination)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('export', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--wrapper', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.export.resolve(), args.destination.resolve(), args.wrapper.resolve())
