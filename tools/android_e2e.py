#!/usr/bin/env python3
"""Runs the samples on an Android emulator or device, as the WebGL end-to-end runs do in a browser.

Installs the APK, starts it with deep-link intent extras, and prints the [sample] and [suite] lines from
logcat until the run finishes. Exits 0 when every run reached its outcome with the expected records.

Examples:
  python3 tools/android_e2e.py --suite 02-assessments,03-objectives --pace fast
  python3 tools/android_e2e.py --sample 02-assessments --autopilot pass --screenshot pass.png

Calls the SDK's platform-tools/adb directly (ANDROID_HOME, or ~/Library/Android/sdk), not a shell wrapper.
"""
import argparse
import json
import os
import queue
import re
import shutil
import subprocess
import sys
import threading
import time

PACKAGE = "com.arborxr.insightssamples"
EXTRAS = ("sample", "autopilot", "suite", "pace", "module", "stream_log", "pairing")
LOG_LINE = re.compile(r"\[(sample|suite)\] (\{.*\})")


def adb_path():
    sdk = os.environ.get("ANDROID_HOME") or os.path.expanduser("~/Library/Android/sdk")
    bundled = os.path.join(sdk, "platform-tools", "adb")
    return bundled if os.path.exists(bundled) else shutil.which("adb") or "adb"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apk", default="Builds/Android-Flat-local/InsightsSamples.apk")
    parser.add_argument("--serial", help="the device to use, when more than one is connected")
    parser.add_argument("--no-install", action="store_true", help="run what's installed")
    parser.add_argument("--timeout", type=float, default=1800, help="seconds to wait for the run to finish")
    parser.add_argument("--screenshot", help="save a screenshot here when the run ends")
    parser.add_argument("--clear-data", action="store_true", help="clear the app's data (pairing, storage) first")
    for key in EXTRAS:
        parser.add_argument("--" + key)
    args = parser.parse_args()
    if not args.sample and not args.suite:
        parser.error("pass --sample or --suite")

    adb = [adb_path()] + (["-s", args.serial] if args.serial else [])

    def run(*command):
        return subprocess.run(adb + list(command), check=True, capture_output=True, text=True).stdout

    if not args.no_install:
        print(f"Installing {args.apk}", flush=True)
        run("install", "-r", args.apk)
    run("shell", "am", "force-stop", PACKAGE)
    if args.clear_data:
        run("shell", "pm", "clear", PACKAGE)
    run("logcat", "-c")

    activity = run("shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE).strip().splitlines()[-1]
    extras = []
    for key in EXTRAS:
        value = getattr(args, key)
        if value:
            extras += ["--es", key, value]
    # Release builds log the [sample] and [suite] lines this script waits for only with stream_log.
    if not args.stream_log:
        extras += ["--es", "stream_log", "1"]
    run("shell", "am", "start", "-n", activity, *extras)

    lines = queue.Queue()
    logcat = subprocess.Popen(adb + ["logcat", "-v", "brief", "-s", "Unity:I"], stdout=subprocess.PIPE, text=True)
    threading.Thread(target=lambda: [lines.put(line) for line in logcat.stdout], daemon=True).start()

    expected_runs = None if args.suite else 1
    finished, failures = 0, []
    deadline = time.time() + args.timeout
    try:
        while time.time() < deadline:
            try:
                line = lines.get(timeout=1)
            except queue.Empty:
                continue
            match = LOG_LINE.search(line)
            if not match:
                continue
            print(match.group(0), flush=True)
            record = json.loads(match.group(2))
            if match.group(1) == "suite":
                failures += record.get("failures", [])
                expected_runs = finished = record.get("runs", finished)
                break
            if record.get("state") == "finished":
                finished += 1
                if record.get("result") != record.get("outcome") or record.get("records") != "ok":
                    failures.append(f"{record.get('id')} {record.get('outcome')}")
                if expected_runs is not None and finished >= expected_runs:
                    break
        else:
            failures.append(f"timed out after {args.timeout:.0f} s")
    finally:
        logcat.terminate()
        if args.screenshot:
            with open(args.screenshot, "wb") as image:
                subprocess.run(adb + ["exec-out", "screencap", "-p"], stdout=image, check=True)

    print(f"{finished} run(s) finished, {len(failures)} failure(s)" + (": " + "; ".join(failures) if failures else ""))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
