"""One-shot Windows Graphics Capture; no mouse, keyboard, or UI automation."""
import json
import sys
import time
from pathlib import Path

import cv2
import psutil
import win32gui
import win32process
from windows_capture import WindowsCapture


def capture_window(hwnd: int, process_id: int, output: Path) -> dict:
    if not win32gui.IsWindow(hwnd) or win32process.GetWindowThreadProcessId(hwnd)[1] != process_id:
        raise RuntimeError("ArcGIS Pro window changed. Refresh the connection before capturing.")
    if psutil.Process(process_id).name().lower() != "arcgispro.exe":
        raise RuntimeError("Capture target is not ArcGIS Pro.")
    if win32gui.IsIconic(hwnd):
        raise RuntimeError("ArcGIS Pro is minimized. Restore its window before capturing.")
    capture = WindowsCapture(window_hwnd=hwnd, cursor_capture=False)
    metadata = {}

    @capture.event
    def on_frame_arrived(frame, capture_control):
        ok, png = cv2.imencode(".png", frame.frame_buffer)
        if not ok:
            raise RuntimeError("Failed to encode captured window.")
        output.write_bytes(png.tobytes())
        metadata.update(path=str(output), width=frame.width, height=frame.height,
                        window_title=win32gui.GetWindowText(hwnd), method="Windows.Graphics.Capture")
        capture_control.stop()

    @capture.event
    def on_closed():
        pass

    control = capture.start_free_threaded()
    deadline = time.monotonic() + 15
    while not control.is_finished() and time.monotonic() < deadline:
        time.sleep(0.1)
    if not control.is_finished():
        control.stop()
    control.wait()
    if not metadata:
        raise RuntimeError("No window frame was received within 15 seconds.")
    return metadata


if __name__ == "__main__":
    print(json.dumps(capture_window(int(sys.argv[1]), int(sys.argv[2]), Path(sys.argv[3])), ensure_ascii=True))
