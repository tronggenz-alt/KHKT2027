"""Tay cầm qua Wi-Fi UDP: phần chung cho công cụ giả lập / theo dõi (tools/tay_cam_udp.py) và kiểm thử.

Game (UdpControllerSource) nghe gói tay cầm qua UDP. Firmware hiện tại (firmware/tay_cam_esp32cam, ESP32-CAM + 1 joystick)
gửi qua cáp USB / Bluetooth chứ không qua Wi-Fi (chân analog của ESP32-CAM không đọc được khi Wi-Fi bật), nên đường UDP
dùng để giả lập tay cầm khi chưa có mạch, và cho bản Wi-Fi sau này (cần thêm module ADS1115). Quy ước UDP:
  - tay cầm gửi gói 29 byte (docs/02-giao-thuc-tay-cam.md) tới cổng PC_PORT; chưa biết máy tính nào thì phát quảng bá;
  - máy tính trả lời bằng lệnh chữ (H, R, P, L, C, I, J) tới địa chỉ đã gửi gói, tay cầm nhớ địa chỉ đó và gửi thẳng;
  - quá 2 s không nghe máy tính: tắt rung; quá 5 s: quên máy tính, phát quảng bá lại.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from khkt.protocol import Packet, encode

PC_PORT = 4210          # CONG_PC trong firmware/tay_cam_esp32cam/cau_hinh.h
CONTROLLER_PORT = 4211  # CONG_TAY_CAM
FAILSAFE_S = 2.0        # HET_HAN_PC_MS
FORGET_S = 5.0          # QUEN_PC_MS
VERSION = "KHKT-TAYCAM-GIALAP v1"


def axis_to_raw(x: float) -> int:
    """-1..1 sang 0..1023 (tâm 512), cùng thang với analogRead của Mega và hàm joystick() của firmware ESP32."""
    x = max(-1.0, min(1.0, x))
    return int(round(512 + x * 511)) if x >= 0 else int(round(512 + x * 512))


def make_packet(seq: int, t_ms: int, j1: tuple[float, float] = (0.0, 0.0), j2: tuple[float, float] = (0.0, 0.0),
                buttons: int = 0, accel_raw: tuple[int, int, int] = (0, 0, 16384),
                gyro_raw: tuple[int, int, int] = (0, 0, 0)) -> bytes:
    """Gói 29 byte. Mặc định tay cầm nằm yên, mặt phẳng ngang (gia tốc 1 g theo trục z)."""
    joy = (axis_to_raw(j1[0]), axis_to_raw(j1[1]), axis_to_raw(j2[0]), axis_to_raw(j2[1]))
    return encode(Packet(seq=seq, t_ms=t_ms, accel_raw=accel_raw, gyro_raw=gyro_raw, joy_raw=joy, buttons=buttons))


def parse_xyb(line: str) -> tuple[int, int, bool] | None:
    """Dòng "X,Y,B" của bộ joystick → Mega → ESP32 (firmware/joystick_qua_mega). None nếu dòng hỏng."""
    parts = (line or "").strip().split(",")
    if len(parts) != 3:
        return None
    try:
        x, y, b = (int(p) for p in parts)
    except ValueError:
        return None
    if not (0 <= x <= 4095 and 0 <= y <= 4095 and b in (0, 1)):
        return None
    return x, y, b == 1


def make_xyb(j: tuple[float, float], pressed: bool, center: tuple[int, int] = (512, 512)) -> bytes:
    """Gói chữ giống ESP32 gửi: cần -1..1 → analogRead 0..1023 quanh tâm."""
    def raw(v: float, c: int) -> int:
        v = max(-1.0, min(1.0, v))
        return int(round(c + v * (1023 - c if v > 0 else c)))
    return f"{raw(j[0], center[0])},{raw(j[1], center[1])},{1 if pressed else 0}".encode("ascii")


def split_commands(data: bytes) -> list[str]:
    """Một gói UDP có thể chứa nhiều dòng lệnh, ví dụ b"R0\\nL0,0\\n"."""
    text = data.decode("ascii", errors="replace")
    return [line.strip() for line in text.replace("\r", "\n").split("\n") if line.strip()]


def _int(s: str, lo: int, hi: int) -> int:
    try:
        return max(lo, min(hi, int(s)))
    except ValueError:
        return 0


@dataclass
class FakeController:
    """Trạng thái phía tay cầm: máy tính nào đang nối, mức rung, đèn. `now` tính bằng giây."""

    pc: tuple[str, int] | None = None
    last_heard: float = -1e9
    vibration: int = 0
    pulse_level: int = 0
    pulse_until: float = 0.0
    led_green: bool = False
    led_red: bool = False
    log: list[str] = field(default_factory=list)

    def pc_alive(self, now: float) -> bool:
        return self.pc is not None and now - self.last_heard < FAILSAFE_S

    def destination(self, now: float, broadcast: tuple[str, int]) -> tuple[str, int]:
        """Gửi gói tới đâu: máy tính đã biết (gửi thẳng) hoặc địa chỉ quảng bá."""
        if self.pc is not None and now - self.last_heard > FORGET_S:
            self.pc = None
            self.log.append("mat may tinh, phat quang ba")
        return self.pc if self.pc is not None else broadcast

    def motor(self, now: float) -> int:
        """Mức rung thực tế lúc này: 0 nếu mất máy tính (an toàn), xung ưu tiên hơn rung liên tục."""
        if not self.pc_alive(now):
            return 0
        return self.pulse_level if now < self.pulse_until else self.vibration

    def handle(self, data: bytes, sender: tuple[str, int], now: float) -> list[bytes]:
        """Xử lý một gói lệnh từ máy tính, trả về các dòng '#...' cần gửi lại."""
        if self.pc != sender:
            self.log.append(f"noi voi may tinh {sender[0]}:{sender[1]}")
        self.pc = sender
        self.last_heard = now
        replies: list[bytes] = []
        for cmd in split_commands(data):
            c, rest = cmd[0], cmd[1:]
            if c == "R":
                self.vibration = _int(rest, 0, 255)
            elif c == "P" and "," in rest:
                lvl, ms = rest.split(",", 1)
                self.pulse_level = _int(lvl, 0, 255)
                self.pulse_until = now + _int(ms, 0, 2000) / 1000.0
            elif c == "L" and "," in rest:
                g, r = rest.split(",", 1)
                self.led_green, self.led_red = _int(g, 0, 1) != 0, _int(r, 0, 1) != 0
            elif c == "C":
                replies.append(b"#CAL bias=0,0,0\n")
            elif c == "J":
                replies.append(b"#JOY tam=13200,13200,13200,13200\n")
            elif c == "I":
                replies.append(f"#INFO {VERSION} ads=OK pcf=OK imu=OK\n".encode("ascii"))
            # H (nhịp tim) và lệnh lạ: chỉ cập nhật thời điểm nghe thấy máy tính
        return replies
