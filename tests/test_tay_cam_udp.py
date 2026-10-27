"""Kiểm thử tay cầm Wi-Fi ESP32-CAM: gói tin, lệnh, logic tìm máy tính, và khớp hằng số giữa firmware / Unity / Python."""

import os
import re
import socket
import sys
import time
import unittest

ROOT = os.path.join(os.path.dirname(__file__), "..")
sys.path.insert(0, ROOT)

from khkt import protocol, wireless  # noqa: E402

FW_DIR = os.path.join(ROOT, "firmware", "tay_cam_esp32cam")
UNITY_INPUT = os.path.join(ROOT, "Phong thi nghiem ao", "Assets", "Scripts", "Input")


def read(path: str) -> str:
    with open(path, encoding="utf-8") as f:
        return f.read()


class TestPacket(unittest.TestCase):
    def test_axis_mapping(self):
        self.assertEqual(wireless.axis_to_raw(0), 512)
        self.assertEqual(wireless.axis_to_raw(1), 1023)
        self.assertEqual(wireless.axis_to_raw(-1), 0)
        self.assertEqual(wireless.axis_to_raw(5), 1023)  # bị kẹp

    def test_roundtrip(self):
        data = wireless.make_packet(7, 123456, j1=(0.5, -1), j2=(0, 1), buttons=protocol.BTN_TRIGGER | protocol.BTN_J2)
        self.assertEqual(len(data), protocol.PACKET_LEN)
        pkts = protocol.PacketParser().feed(data)
        self.assertEqual(len(pkts), 1)
        p = pkts[0]
        self.assertEqual((p.seq, p.t_ms), (7, 123456))
        self.assertEqual(p.joy_raw, (768, 0, 512, 1023))
        self.assertTrue(p.pressed(protocol.BTN_TRIGGER) and p.pressed(protocol.BTN_J2))
        self.assertFalse(p.pressed(protocol.BTN_RECENTER))
        self.assertAlmostEqual(p.accel_g[2], 1.0)

    def test_text_line_between_packets(self):
        # Firmware gửi dòng '#...' thành gói UDP riêng; bộ đọc vẫn tách được khi dồn chung luồng byte
        parser = protocol.PacketParser()
        pkts = parser.feed(wireless.make_packet(1, 0) + b"#INFO KHKT-TAYCAM-ESP32 v1 ip=192.168.4.1\n" + wireless.make_packet(2, 10))
        self.assertEqual(len(pkts), 2)
        self.assertEqual(parser.messages, ["INFO KHKT-TAYCAM-ESP32 v1 ip=192.168.4.1"])


class TestXyb(unittest.TestCase):
    """Dòng chữ "X,Y,B" của bộ joystick → Mega → ESP32."""

    def test_doc_dong(self):
        self.assertEqual(wireless.parse_xyb("409,387,0"), (409, 387, False))
        self.assertEqual(wireless.parse_xyb(" 0,1023,1\r"), (0, 1023, True))
        self.assertEqual(wireless.parse_xyb("2048,1990,0"), (2048, 1990, False))  # ESP32 đọc thẳng 12 bit
        for bad in ("NO_MEGA_DATA", "409,387", "409,387,2", "4,0,9,1", "-5,10,0", "4a9,387,0", ""):
            self.assertIsNone(wireless.parse_xyb(bad), bad)

    def test_tao_dong(self):
        self.assertEqual(wireless.make_xyb((0, 0), False, (409, 387)), b"409,387,0")
        self.assertEqual(wireless.make_xyb((1, -1), True, (409, 387)), b"1023,0,1")
        self.assertEqual(wireless.parse_xyb(wireless.make_xyb((0.5, 0.5), False).decode()), (768, 768, False))

    def test_unity_doc_cung_dang(self):
        cs = read(os.path.join(UNITY_INPUT, "XybDecoder.cs"))
        self.assertIn("Split(',')", cs)
        self.assertIn("x > 4095", cs)
        udp = read(os.path.join(UNITY_INPUT, "UdpControllerSource.cs"))
        self.assertIn('"NO_MEGA_DATA"', udp)


class TestFakeController(unittest.TestCase):
    PC = ("192.168.1.20", 4210)
    BCAST = ("255.255.255.255", 4210)

    def test_commands(self):
        f = wireless.FakeController()
        self.assertEqual(f.handle(b"R120\nL1,0\n", self.PC, 0.0), [])
        self.assertEqual((f.vibration, f.led_green, f.led_red), (120, True, False))
        f.handle(b"P200,300\n", self.PC, 1.0)
        self.assertEqual(f.motor(1.1), 200)   # xung ưu tiên
        self.assertEqual(f.motor(1.35), 120)  # hết xung: về rung liên tục
        self.assertTrue(f.handle(b"I\n", self.PC, 1.4)[0].startswith(b"#INFO"))
        f.handle(b"R999\n", self.PC, 1.5)
        self.assertEqual(f.vibration, 255)    # kẹp như constrain() của firmware

    def test_discovery_and_failsafe(self):
        f = wireless.FakeController()
        self.assertEqual(f.destination(0.0, self.BCAST), self.BCAST)  # chưa biết máy tính: quảng bá
        f.handle(b"H\n", self.PC, 0.0)
        f.handle(b"R100\n", self.PC, 0.1)
        self.assertEqual(f.destination(0.5, self.BCAST), self.PC)     # đã nghe: gửi thẳng
        self.assertEqual(f.motor(1.0), 100)
        self.assertEqual(f.motor(2.5), 0)                             # > 2 s im lặng: tắt rung
        self.assertEqual(f.destination(4.0, self.BCAST), self.PC)
        self.assertEqual(f.destination(5.5, self.BCAST), self.BCAST)  # > 5 s: quên, quảng bá lại

    def test_udp_loopback(self):
        """Tay cầm giả và 'game' giả qua socket thật trên 127.0.0.1: tìm nhau rồi chuyển sang gửi thẳng."""
        pc = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        pad = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            pc.bind(("127.0.0.1", 0))
            pad.bind(("127.0.0.1", 0))
            pc.settimeout(1.0)
            pad.settimeout(1.0)
            pc_addr = pc.getsockname()
            fake = wireless.FakeController()
            # 1) tay cầm chưa biết máy tính: gửi tới địa chỉ "quảng bá" (ở đây là cổng của pc)
            pad.sendto(wireless.make_packet(0, 0, j2=(0, 1)), fake.destination(0.0, pc_addr))
            data, pad_addr = pc.recvfrom(64)
            self.assertEqual(protocol.PacketParser().feed(data)[0].joy_raw[3], 1023)
            # 2) máy tính trả nhịp tim + lệnh đèn tới đúng địa chỉ vừa gửi gói
            pc.sendto(b"H\nL1,1\n", pad_addr)
            cmd, sender = pad.recvfrom(64)
            fake.handle(cmd, sender, 0.1)
            self.assertTrue(fake.led_green and fake.led_red)
            self.assertEqual(fake.destination(0.2, ("255.255.255.255", 9)), pc_addr)
            # 3) hỏi thông tin: trả lời dòng '#INFO' về máy tính
            pc.sendto(b"I\n", pad_addr)
            cmd, sender = pad.recvfrom(64)
            for r in fake.handle(cmd, sender, 0.3):
                pad.sendto(r, sender)
            data, _ = pc.recvfrom(256)
            parser = protocol.PacketParser()
            parser.feed(data)
            self.assertTrue(parser.messages and parser.messages[0].startswith("INFO"))
        finally:
            pc.close()
            pad.close()


class TestConstantsMatch(unittest.TestCase):
    """Cổng UDP, cờ nút, bố cục gói phải giống nhau ở firmware ESP32-CAM 1 cần, Unity và Python."""

    @classmethod
    def setUpClass(cls):
        cls.ino = read(os.path.join(FW_DIR, "tay_cam_esp32cam.ino"))
        cls.udp = read(os.path.join(UNITY_INPUT, "UdpControllerSource.cs"))
        cls.state = read(os.path.join(UNITY_INPUT, "ControllerState.cs"))

    def test_ports(self):
        self.assertIn(f"listenPort = {wireless.PC_PORT};", self.udp)
        self.assertIn(f"controllerPort = {wireless.CONTROLLER_PORT};", self.udp)

    def test_packet_layout(self):
        self.assertIn("uint8_t g[29]", self.ino)
        self.assertIn("g[0] = 0xA5;", self.ino)
        self.assertIn("g[1] = 0x5A;", self.ino)
        self.assertIn("g[28] = crc8(g + 2, 26);", self.ino)
        self.assertIn("ghiU16(g + 23, x);", self.ino)  # cần duy nhất nằm ở j2 (cần đi)
        self.assertIn("ghiU16(g + 25, y);", self.ino)
        self.assertIn("g[27] = nut;", self.ino)

    def test_flags(self):
        self.assertIn(f"NUT_NHAN_CAN = 1 << {protocol.BTN_J2.bit_length() - 1};", self.ino)
        self.assertIn(f"CO_MOT_CAN = 1 << {protocol.FLAG_SINGLE_STICK.bit_length() - 1};", self.ino)
        self.assertIn(f"CO_KHONG_IMU = 1 << {protocol.FLAG_IMU_ERROR.bit_length() - 1};", self.ino)
        self.assertIn(f"SingleStick = 1 << {protocol.FLAG_SINGLE_STICK.bit_length() - 1},", self.state)

    def test_strapping_pins_unused(self):
        # IO0 (chế độ nạp), IO2 / IO12 (cấu hình lúc khởi động), IO16 (PSRAM), IO1/IO3 (cổng USB) không được dùng
        pins = {int(p) for p in re.findall(r"PIN_\w+\s*=\s*(\d+)", self.ino)}
        self.assertTrue(pins.isdisjoint({0, 1, 2, 3, 12, 16}), pins)
        self.assertEqual(pins, {13, 14, 15, 33, 4})
        self.assertNotIn("WiFi", self.ino)  # chân analog ADC2 không đọc được khi Wi-Fi bật


if __name__ == "__main__":
    unittest.main()
