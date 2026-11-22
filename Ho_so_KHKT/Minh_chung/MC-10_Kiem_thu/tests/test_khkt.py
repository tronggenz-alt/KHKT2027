import math
import os
import random
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from khkt import fusion, pour, protocol, skill, titration  # noqa: E402


class ProtocolTest(unittest.TestCase):
    def sample(self, seq=0):
        return protocol.Packet(seq=seq, t_ms=123456, accel_raw=(0, -100, 16384), gyro_raw=(655, -655, 0),
                               joy_raw=(512, 0, 1023, 511), buttons=protocol.BTN_TRIGGER | protocol.BTN_ASK_AI)

    def test_roundtrip_and_scaling(self):
        p = protocol.PacketParser()
        out = p.feed(protocol.encode(self.sample()))
        self.assertEqual(len(out), 1)
        pkt = out[0]
        self.assertEqual(pkt.t_ms, 123456)
        self.assertAlmostEqual(pkt.accel_g[2], 1.0)
        self.assertAlmostEqual(pkt.gyro_dps[0], 10.0)
        self.assertTrue(pkt.pressed(protocol.BTN_ASK_AI))
        self.assertFalse(pkt.pressed(protocol.BTN_RECENTER))
        self.assertEqual(pkt.joy[0], 0.0)   # tâm nằm trong vùng chết
        self.assertEqual(pkt.joy[2], 1.0)

    def test_packet_size_matches_firmware(self):
        self.assertEqual(len(protocol.encode(self.sample())), 29)

    def test_resync_text_and_corruption(self):
        good1 = protocol.encode(self.sample(1))
        bad = bytearray(protocol.encode(self.sample(2)))
        bad[10] ^= 0xFF
        good3 = protocol.encode(self.sample(3))
        stream = b"\x00\x13#KHKT-TAYCAM v1 imu=OK\n" + good1 + bytes(bad) + good3
        p = protocol.PacketParser()
        pkts = []
        for i in range(0, len(stream), 7):  # nhận từng mẩu nhỏ như cổng COM thật
            pkts += p.feed(stream[i:i + 7])
        self.assertEqual([k.seq for k in pkts], [1, 3])
        self.assertEqual(p.crc_errors, 1)
        self.assertEqual(p.lost_packets, 1)
        self.assertIn("KHKT-TAYCAM v1 imu=OK", p.messages)

    def test_seq_wraparound_is_not_loss(self):
        p = protocol.PacketParser()
        p.feed(protocol.encode(self.sample(255)) + protocol.encode(self.sample(0)))
        self.assertEqual(p.lost_packets, 0)


class FusionTest(unittest.TestCase):
    def test_converges_to_gravity_tilt(self):
        # tay cầm chúc đầu xuống 30 độ, đứng yên
        th = math.radians(30)
        acc = (-math.sin(th), 0.0, math.cos(th))
        f = fusion.Mahony()
        for _ in range(1500):
            f.update((0.0, 0.0, 0.0), acc, 0.01)
        roll, pitch, _ = f.euler_deg()
        self.assertAlmostEqual(pitch, 30.0, delta=0.5)
        self.assertAlmostEqual(roll, 0.0, delta=0.5)
        self.assertAlmostEqual(f.tilt_deg(), 30.0, delta=0.5)

    def test_reset_from_accel_matches(self):
        th = math.radians(40)
        f = fusion.Mahony()
        f.reset_from_accel((0.0, math.sin(th), math.cos(th)))
        roll, pitch, yaw = f.euler_deg()
        self.assertAlmostEqual(roll, 40.0, delta=1e-6)
        self.assertAlmostEqual(pitch, 0.0, delta=1e-6)

    def test_gyro_integrates_yaw(self):
        f = fusion.Mahony()
        for _ in range(100):  # 90 °/s trong 1 s quanh trục Z
            f.update((0.0, 0.0, 90.0), (0.0, 0.0, 1.0), 0.01)
        self.assertAlmostEqual(f.euler_deg()[2], 90.0, delta=1.0)


class TitrationTest(unittest.TestCase):
    def test_strong_acid_points(self):
        self.assertAlmostEqual(titration.ph(0.1, 25, 0.1, 0), 1.0, places=3)
        self.assertAlmostEqual(titration.ph(0.1, 25, 0.1, 25), 7.0, places=3)
        # thừa 1 ml NaOH 0,1 M trong 51 ml
        self.assertAlmostEqual(titration.ph(0.1, 25, 0.1, 26), 14 + math.log10(0.1 / 51), places=3)

    def test_matches_design_formula_before_equivalence(self):
        ca, va, cb, vb = 0.1, 20.0, 0.1, 15.0
        h = (ca * va - cb * vb) / (va + vb)
        self.assertAlmostEqual(titration.ph(ca, va, cb, vb), -math.log10(h), places=3)

    def test_weak_acid_half_equivalence_is_pka(self):
        ka = 1.75e-5
        self.assertAlmostEqual(titration.ph(0.1, 25, 0.1, 12.5, ka=ka), -math.log10(ka), delta=0.02)
        self.assertGreater(titration.ph(0.1, 25, 0.1, 25, ka=ka), 8.0)  # điểm tương đương có tính bazơ

    def test_curve_monotonic(self):
        c = titration.curve(0.1, 25, 0.1, 40)
        self.assertTrue(all(b[1] >= a[1] for a, b in zip(c, c[1:])))

    def test_indicator(self):
        self.assertEqual(titration.indicator_fraction("phenolphtalein", 7.0), 0.0)
        self.assertEqual(titration.indicator_fraction("phenolphtalein", 11.0), 1.0)

    def test_parallax_sign_and_size(self):
        e = titration.parallax_error_ml(eye_above_m=0.10, eye_distance_m=0.30)
        self.assertLess(e, 0)
        self.assertTrue(0.1 < abs(e) < 0.4)
        self.assertEqual(titration.parallax_error_ml(0.0, 0.3), 0.0)


class PourTest(unittest.TestCase):
    def test_no_pour_when_upright(self):
        self.assertEqual(pour.Cup().flow_ml_s(0), 0.0)

    def test_emptier_cup_needs_more_tilt(self):
        self.assertGreater(pour.Cup(volume_ml=20).start_angle_deg(), pour.Cup(volume_ml=80).start_angle_deg())

    def test_flow_starts_at_start_angle_and_grows(self):
        c = pour.Cup(volume_ml=50)
        a = c.start_angle_deg()
        self.assertEqual(c.flow_ml_s(a - 1), 0.0)
        self.assertGreater(c.flow_ml_s(a + 10), c.flow_ml_s(a + 3) > 0)

    def test_holding_angle_stops_by_itself(self):
        c = pour.Cup(volume_ml=60)
        tilt = c.start_angle_deg() + 5
        poured = sum(c.step(tilt, 0.01) for _ in range(2000))
        self.assertGreater(poured, 0)
        self.assertEqual(c.flow_ml_s(tilt), 0.0)  # mực nước hạ đến khi ngừng chảy


class SkillTest(unittest.TestCase):
    def test_tremor_picks_band(self):
        fs = 100
        t = np.arange(0, 10, 1 / fs)
        slow = 40 * np.sin(2 * np.pi * 0.5 * t)  # cử động rót chậm: không phải run
        shake = 3 * np.sin(2 * np.pi * 9 * t)    # run 9 Hz biên độ 3 °/s
        gyro = np.column_stack([slow + shake, np.zeros_like(t), np.zeros_like(t)])
        self.assertAlmostEqual(skill.tremor_dps(gyro, fs), 3 / math.sqrt(2), delta=0.1)

    def test_dtw_identity_and_shift(self):
        a = np.sin(np.linspace(0, 6, 80))
        self.assertAlmostEqual(skill.dtw_distance(a, a), 0.0)
        shifted = np.concatenate([np.zeros(10), a])[:80]
        self.assertLess(skill.dtw_distance(a, shifted), np.mean(np.abs(a - shifted)))

    def test_rate_near_endpoint_and_slowdown(self):
        # 1 ml/s tới 18 ml, rồi 0,1 ml/s tới 20 ml
        t = np.concatenate([np.linspace(0, 18, 181), 18 + np.linspace(0.1, 20, 200)])
        v = np.concatenate([np.linspace(0, 18, 181), 18 + np.linspace(0.01, 2, 200)])
        self.assertAlmostEqual(skill.rate_near_endpoint(t, v, 20.0), 0.1, delta=0.01)
        self.assertLess(skill.slowdown_ratio(t, v, 20.0), 0.2)
        self.assertAlmostEqual(skill.overshoot_drops(20.1, 20.0), 2.0)

    def test_summarize_csv_roundtrip(self):
        import tempfile
        rnd = random.Random(1)
        path = os.path.join(tempfile.mkdtemp(), "s.csv")
        with open(path, "w", encoding="utf-8") as f:
            f.write("t_ms,gx,gy,gz,v_buret_ml,opening\n")
            for i in range(1000):
                v = min(20.2, i * 0.03)
                f.write(f"{i * 10},{rnd.gauss(0, 1)},{rnd.gauss(0, 1)},{rnd.gauss(0, 1)},{v},{0.5}\n")
        d = skill.load_csv(path)
        s = skill.summarize(d, v_eq_ml=20.0, reference=d)
        self.assertAlmostEqual(s["tan_so_mau_hz"], 100.0, delta=0.1)
        self.assertAlmostEqual(s["so_giot_vuot"], 4.0, delta=0.01)
        self.assertAlmostEqual(s["lech_so_voi_chuan"], 0.0)


if __name__ == "__main__":
    unittest.main()
