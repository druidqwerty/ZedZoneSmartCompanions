"""Drive ONLY an already-running --sc-ui-os-probe process via its exact PID.

Does not install, launch, focus windows, move the global cursor, or touch saves.
The parent rollback runner owns process startup and restoration. PostMessage
delivery alone never passes a check: each action requires observed UI state.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import time

user32 = ctypes.WinDLL("user32", use_last_error=True)
user32.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
user32.PostMessageW.restype = wintypes.BOOL
user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
user32.GetClientRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
user32.ClientToScreen.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.POINT)]
user32.IsWindowVisible.argtypes = [wintypes.HWND]
WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
user32.EnumWindows.argtypes = [WNDENUMPROC, wintypes.LPARAM]
WM_MOVE, WM_DOWN, WM_UP, WM_WHEEL = 0x200, 0x201, 0x202, 0x20A


def find_window(pid):
    windows = []

    @WNDENUMPROC
    def visit(hwnd, _):
        actual = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(actual))
        if actual.value == pid and user32.IsWindowVisible(hwnd):
            rect = wintypes.RECT()
            user32.GetClientRect(hwnd, ctypes.byref(rect))
            windows.append(((rect.right - rect.left) * (rect.bottom - rect.top), hwnd))
        return True

    user32.EnumWindows(visit, 0)
    return max(windows)[1] if windows else None


def point_param(x, y):
    return (round(x) & 0xFFFF) | ((round(y) & 0xFFFF) << 16)


def post(hwnd, message, x, y, buttons=0):
    if not user32.PostMessageW(hwnd, message, buttons, point_param(x, y)):
        raise ctypes.WinError(ctypes.get_last_error())


def wait_state(output, predicate, description, timeout=6):
    until = time.monotonic() + timeout
    last = None
    while time.monotonic() < until:
        try:
            last = json.loads((output / "state.json").read_text())
            if predicate(last):
                return last
        except (FileNotFoundError, json.JSONDecodeError, PermissionError):
            pass
        time.sleep(0.05)
    raise TimeoutError(f"{description}: no observed state change; last={last}. PostMessage may be unsupported by this Unity input mode.")


def physical(state, x, y):
    return ((state["x"] + x) * state["scale"], (state["y"] + y) * state["scale"])


def click(hwnd, point):
    post(hwnd, WM_MOVE, *point)
    post(hwnd, WM_DOWN, *point, buttons=1)
    time.sleep(0.08)
    post(hwnd, WM_UP, *point)


def capture(output, name):
    (output / "capture.txt").write_text(name)
    time.sleep(0.25)


def run(pid, output):
    user32.SetProcessDPIAware()
    report = {"pid": pid, "transport": "PostMessageW exact-PID window", "checks": [], "passed": False}
    hwnd = None
    try:
        state = wait_state(output, lambda s: s["open"] and s["tab"] == 0 and s["width"] == 1920 and s["height"] == 1080, "1920x1080 fixture ready", timeout=35)
        hwnd = find_window(pid)
        if not hwnd:
            raise RuntimeError("No visible window belonging to the exact requested PID.")
        report.update(width=state["width"], height=state["height"], effective_scale=state["scale"])
        capture(output, "os-start")

        # Header: x=14; four tabs each (w - 28 - 24)/4, with 8-unit gaps.
        tab_width = (state["w"] - 52) / 4
        click(hwnd, physical(state, 14 + 3 * (tab_width + 8) + tab_width / 2, 63))
        state = wait_state(output, lambda s: s["tab"] == 3, "settings tab click")
        report["checks"].append("actual tab switch to settings")

        x, y = physical(state, state["w"] / 2, 280)
        screen = wintypes.POINT(round(x), round(y))
        user32.ClientToScreen(hwnd, ctypes.byref(screen))
        before_scroll = state["scroll"]
        post(hwnd, WM_MOVE, x, y)
        post(hwnd, WM_WHEEL, screen.x, screen.y, buttons=((-360 & 0xFFFF) << 16))
        try:
            state = wait_state(output, lambda s: s["scroll"] > before_scroll + 1, "settings mouse wheel")
            report["checks"].append("actual wheel scrolling")
            report["wheel_verified"] = True
        except TimeoutError:
            report["wheel_verified"] = False
            report["limitations"] = ["Background WM_MOUSEWHEEL did not change scroll; not verified."]
            # Retained scrollbar's bottom arrow is a real clickable alternative.
            click(hwnd, physical(state, state["w"] - 22, state["h"] - 46))
            state = wait_state(output, lambda s: s["scroll"] > before_scroll + 1, "settings scrollbar arrow")
            report["checks"].append("actual scrollbar arrow scrolling (wheel unverified)")
        capture(output, "os-settings-scrolled")

        start = physical(state, 100, 24)
        original_x = state["x"]
        post(hwnd, WM_MOVE, *start)
        post(hwnd, WM_DOWN, *start, buttons=1)
        wait_state(output, lambda s: s["dragging"], "title drag capture")
        delta_x = -32 if state["width"] - (state["x"] + state["w"]) * state["scale"] < 40 else 32
        destination = (start[0] + delta_x, start[1])
        post(hwnd, WM_MOVE, *destination, buttons=1)
        state = wait_state(output, lambda s: abs(s["x"] - original_x - delta_x / s["scale"]) < 2 / s["scale"], "scaled title drag")
        post(hwnd, WM_UP, *destination)
        state = wait_state(output, lambda s: not s["dragging"], "title drag release")
        report["checks"].append("actual scaled title drag and release")
        capture(output, "os-dragged")

        click(hwnd, physical(state, state["w"] - 48, 24))
        wait_state(output, lambda s: not s["open"] and not s["dragging"], "close button")
        report["checks"].append("actual close button and released capture")
        report["passed"] = True
    except Exception as error:
        report["error"] = str(error)
    finally:
        if hwnd:
            try:
                post(hwnd, WM_UP, 0, 0)
            except OSError:
                pass  # Timeout may already have closed this exact-PID window.
        output.mkdir(parents=True, exist_ok=True)
        (output / "os-probe-result.json").write_text(json.dumps(report, indent=2))
        (output / "finish.txt").write_text("External driver completed; inspect os-probe-result.json.")
        print(json.dumps(report, ensure_ascii=False), flush=True)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    raise SystemExit(run(args.pid, args.output))
