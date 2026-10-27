"""Gói tin tay cầm v1. Đặc tả: docs/02-giao-thuc-tay-cam.md."""

from __future__ import annotations

import struct
from dataclasses import dataclass

SYNC = b"\xa5\x5a"
PACKET_LEN = 29
_BODY = struct.Struct("<BI3h3h4HB")  # byte 2..27
assert _BODY.size == 26

ACC_LSB_PER_G = 16384.0
GYRO_LSB_PER_DPS = 65.5

BTN_TRIGGER = 1 << 0
BTN_RECENTER = 1 << 1
BTN_ASK_AI = 1 << 2
BTN_J1 = 1 << 3
BTN_J2 = 1 << 4
FLAG_SINGLE_STICK = 1 << 6  # tay cầm 1 cần (ESP32-CAM + 1 mạch joystick): cần ở j2, nút ở bit 4
FLAG_IMU_ERROR = 1 << 7


def crc8(data: bytes) -> int:
    crc = 0
    for b in data:
        crc ^= b
        for _ in range(8):
            crc = ((crc << 1) ^ 0x07) & 0xFF if crc & 0x80 else (crc << 1) & 0xFF
    return crc


@dataclass
class Packet:
    seq: int
    t_ms: int
    accel_raw: tuple[int, int, int]
    gyro_raw: tuple[int, int, int]
    joy_raw: tuple[int, int, int, int]  # j1x j1y j2x j2y, 0..1023
    buttons: int

    @property
    def accel_g(self) -> tuple[float, float, float]:
        return tuple(v / ACC_LSB_PER_G for v in self.accel_raw)  # type: ignore[return-value]

    @property
    def gyro_dps(self) -> tuple[float, float, float]:
        return tuple(v / GYRO_LSB_PER_DPS for v in self.gyro_raw)  # type: ignore[return-value]

    @property
    def joy(self) -> tuple[float, float, float, float]:
        """Joystick chuẩn hóa về -1..1, có vùng chết 6% quanh tâm."""
        out = []
        for v in self.joy_raw:
            x = (v - 511.5) / 511.5
            out.append(0.0 if abs(x) < 0.06 else max(-1.0, min(1.0, x)))
        return tuple(out)  # type: ignore[return-value]

    def pressed(self, mask: int) -> bool:
        return bool(self.buttons & mask)


def encode(p: Packet) -> bytes:
    body = _BODY.pack(p.seq & 0xFF, p.t_ms & 0xFFFFFFFF, *p.accel_raw, *p.gyro_raw, *p.joy_raw, p.buttons)
    return SYNC + body + bytes([crc8(body)])


class PacketParser:
    """Nhận byte rời rạc từ cổng COM, trả về các gói hợp lệ.

    Byte nằm ngoài gói (dòng chữ '#...' của firmware, rác khi mới cắm) được gom
    vào `messages`; gói sai CRC bị bỏ và đếm trong `crc_errors`.
    """

    def __init__(self) -> None:
        self._buf = bytearray()
        self._text = bytearray()
        self.crc_errors = 0
        self.lost_packets = 0
        self.messages: list[str] = []
        self._last_seq: int | None = None

    def feed(self, data: bytes) -> list[Packet]:
        self._buf += data
        out: list[Packet] = []
        while True:
            i = self._buf.find(SYNC)
            if i < 0:
                # giữ lại byte cuối phòng khi nó là nửa đầu của mã đồng bộ
                keep = 1 if self._buf[-1:] == SYNC[:1] else 0
                self._skip(len(self._buf) - keep)
                break
            if i > 0:
                self._skip(i)
            if len(self._buf) < PACKET_LEN:
                break
            body = bytes(self._buf[2:28])
            if crc8(body) != self._buf[28]:
                self.crc_errors += 1
                self._skip(1)
                continue
            f = _BODY.unpack(body)
            pkt = Packet(seq=f[0], t_ms=f[1], accel_raw=f[2:5], gyro_raw=f[5:8], joy_raw=f[8:12], buttons=f[12])
            if self._last_seq is not None:
                self.lost_packets += (pkt.seq - self._last_seq - 1) % 256
            self._last_seq = pkt.seq
            del self._buf[:PACKET_LEN]
            out.append(pkt)
        return out

    def _skip(self, n: int) -> None:
        if n <= 0:
            return
        self._text += self._buf[:n]
        del self._buf[:n]
        while b"\n" in self._text:
            line, _, rest = self._text.partition(b"\n")
            self._text = bytearray(rest)
            s = line.decode("ascii", errors="replace").strip()
            k = s.find("#")  # lúc mới cắm có thể dính rác trước dấu '#'
            if k >= 0:
                self.messages.append(s[k + 1:])
        if len(self._text) > 256:  # rác không có xuống dòng: bỏ
            self._text.clear()
