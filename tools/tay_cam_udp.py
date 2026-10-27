"""Giả lập tay cầm qua UDP để thử game khi chưa có mạch, hoặc theo dõi tay cầm Wi-Fi.

Bộ joystick → Arduino Mega → ESP32 (gửi dòng chữ "X,Y,B" tới cổng 4210, docs/09):
    python tools/tay_cam_udp.py nghe                             # TẮT game trước; xem X, Y, B, số gói/s, tâm cần
    python tools/tay_cam_udp.py gia-lap --mega                   # giả lập bộ này khi chưa có mạch (MỞ game trước)

Giả lập tay cầm 1 cần (giống ESP32-CAM + 1 mạch joystick; MỞ game trước):
    python tools/tay_cam_udp.py gia-lap --mot-can                # W A S D = cần, Space = nhấn cần, B = giữ nút

Theo dõi tay cầm bản Wi-Fi (TẮT game trước, vì game cũng nghe cổng 4210):
    python tools/tay_cam_udp.py nghe
    python tools/tay_cam_udp.py nghe --lenh I --lenh P200,300     # hỏi thông tin, rung thử 0,3 s
    python tools/tay_cam_udp.py nghe --lenh J                     # căn tâm joystick (buông tay khỏi cần)
    python tools/tay_cam_udp.py nghe --ip 192.168.1.50            # mạng chặn gói quảng bá: gọi thẳng IP tay cầm

Giả lập tay cầm (MỞ game trước, game ở chế độ máy tính):
    python tools/tay_cam_udp.py gia-lap                          # điều khiển bằng bàn phím trong cửa sổ này
    python tools/tay_cam_udp.py gia-lap --kich-ban thu-nhanh       # tự đi, xoay, bóp cò rồi thoát
    python tools/tay_cam_udp.py gia-lap --toi 192.168.1.20         # game chạy ở máy khác

Phím khi giả lập (gõ trong cửa sổ dòng lệnh này, mỗi lần gõ giữ ~0,6 s, giữ phím để giữ lâu):
    W A S D  cần đi       I J K L  cần nhìn      Space  bóp cò 1 lần     B  bật / tắt giữ cò (rót, hút)
    Q  Căn lại            E  Hỏi AI             F  nhấn cần nhìn (lắc)   R  nhấn cần đi (đi nhanh)
    X  thoát
"""

from __future__ import annotations

import argparse
import os
import socket
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from khkt.protocol import (BTN_ASK_AI, BTN_J1, BTN_J2, BTN_RECENTER, BTN_TRIGGER,  # noqa: E402
                           FLAG_IMU_ERROR, FLAG_SINGLE_STICK, PacketParser)
from khkt.wireless import CONTROLLER_PORT, PC_PORT, FakeController, make_packet, make_xyb, parse_xyb  # noqa: E402

HOLD_S = 0.6  # gõ một phím = giữ chừng này giây (bàn phím dòng lệnh không báo lúc thả phím)

# Kịch bản: (thời điểm bắt đầu s, cần đi, cần nhìn, nút)
THU_NHANH = [
    (0.0, (0, 0), (0, 0), 0),
    (1.0, (0, 1), (0, 0), 0),             # đi tới 2 s
    (3.0, (0, 0), (1, 0), 0),             # xoay phải 1,5 s
    (4.5, (0, 0), (0, 0), 0),
    (5.0, (0, 0), (0, 0), BTN_TRIGGER),   # bóp cò
    (5.2, (0, 0), (0, 0), 0),
    (6.0, (0, 0), (0, 0), BTN_RECENTER),  # Căn lại (trả dụng cụ)
    (6.2, (0, 0), (0, 0), 0),
    (7.0, None, None, None),              # hết
]


def open_socket(port: int) -> socket.socket:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
    try:
        s.bind(("0.0.0.0", port))
    except OSError as e:
        sys.exit(f"Không mở được cổng UDP {port}: {e}\n"
                 f"Có thể game hoặc một cửa sổ tay_cam_udp.py khác đang dùng cổng này.")
    s.setblocking(False)
    return s


def recv_all(s: socket.socket) -> list[tuple[bytes, tuple[str, int]]]:
    out = []
    while True:
        try:
            out.append(s.recvfrom(2048))
        except (BlockingIOError, ConnectionResetError):
            return out


# ----------------------------------------------------------------------------
# nghe: máy tính <- tay cầm thật
# ----------------------------------------------------------------------------

def listen(args: argparse.Namespace) -> None:
    s = open_socket(args.cong)
    print(f"Đang nghe tay cầm trên cổng UDP {args.cong}. Máy tính và tay cầm phải chung một Wi-Fi. Ctrl+C để dừng.")
    parser = PacketParser()
    controller: tuple[str, int] | None = (args.ip, CONTROLLER_PORT) if args.ip else None
    pending = list(args.lenh or [])
    last_hb, n_win, t_win, t0 = 0.0, 0, time.time(), time.time()
    pkt = None
    xyb_last, xyb_seen = None, []
    try:
        while args.thoi_gian <= 0 or time.time() - t0 < args.thoi_gian:
            now = time.time()
            for data, addr in recv_all(s):
                if controller != addr:
                    print(f"\nThấy tay cầm {addr[0]}:{addr[1]}")
                    controller = addr
                    parser = PacketParser()
                if data[:1] not in (b"\xa5", b"#"):  # dòng chữ "X,Y,B" của bộ Mega → ESP32
                    for line in data.decode("ascii", "replace").splitlines():
                        v = parse_xyb(line)
                        if v:
                            xyb_last = v
                            xyb_seen.append(v)
                            n_win += 1
                        elif line.strip():
                            print(f"\n[ESP32] {line.strip()}")
                    continue
                for p in parser.feed(data):
                    pkt = p
                    n_win += 1
            if controller and now - last_hb > 0.4:
                s.sendto(b"H\n", controller)  # để tay cầm biết máy tính, chuyển sang gửi thẳng
                last_hb = now
            if controller and pending:
                for cmd in pending:
                    s.sendto((cmd + "\n").encode("ascii"), controller)
                pending.clear()
            for m in parser.messages:
                print(f"\n[tay cầm] {m}")
            parser.messages.clear()
            if now - t_win >= 0.5:
                hz = n_win / (now - t_win)
                n_win, t_win = 0, now
                if xyb_last is not None:
                    x, y, b = xyb_last
                    first = xyb_seen[:15]
                    cx, cy = sum(v[0] for v in first) / len(first), sum(v[1] for v in first) / len(first)
                    xs, ys = [v[0] for v in xyb_seen], [v[1] for v in xyb_seen]
                    print(f"\r{hz:5.1f} goi/s  X={x:4d} Y={y:4d} nut={'NHAN' if b else '-   '}  tam=({cx:.0f},{cy:.0f})  "
                          f"X {min(xs)}..{max(xs)}  Y {min(ys)}..{max(ys)}   ", end="", flush=True)
                elif pkt is None:
                    print(f"\rChưa nhận được gói nào ({now - t0:4.0f} s)...", end="", flush=True)
                else:
                    j = pkt.joy
                    nut = [n for n, m in (("Co", BTN_TRIGGER), ("CanLai", BTN_RECENTER), ("HoiAI", BTN_ASK_AI),
                                          ("J1", BTN_J1), ("J2", BTN_J2)) if pkt.pressed(m)]
                    imu = " (khong IMU)" if pkt.pressed(FLAG_IMU_ERROR) else \
                        f" a=({pkt.accel_g[0]:+.2f},{pkt.accel_g[1]:+.2f},{pkt.accel_g[2]:+.2f})g"
                    print(f"\r{hz:5.1f} goi/s  mat={parser.lost_packets:4d} crc={parser.crc_errors:3d}  "
                          f"J1=({j[0]:+.2f},{j[1]:+.2f}) J2=({j[2]:+.2f},{j[3]:+.2f}) "
                          f"nut={'+'.join(nut) or '-':18}{imu}   ", end="", flush=True)
            time.sleep(0.005)
    except KeyboardInterrupt:
        pass
    finally:
        if controller:
            s.sendto(b"R0\nL0,0\n", controller)
        s.close()
        print()


# ----------------------------------------------------------------------------
# gia-lap: tay cầm giả -> game
# ----------------------------------------------------------------------------

class KeyboardPad:
    """Đọc phím trong cửa sổ dòng lệnh Windows (msvcrt). Mỗi lần gõ giữ trạng thái HOLD_S giây."""

    def __init__(self) -> None:
        try:
            import msvcrt  # noqa: F401
            self.ok = True
        except ImportError:
            self.ok = False
        self.until: dict[str, float] = {}
        self.hold_trigger = False
        self.quit = False

    def poll(self, now: float) -> None:
        if not self.ok:
            return
        import msvcrt
        while msvcrt.kbhit():
            ch = msvcrt.getwch().lower()
            if ch in ("\x00", "\xe0"):  # phím đặc biệt: bỏ mã thứ hai
                msvcrt.getwch()
                continue
            if ch == "x":
                self.quit = True
            elif ch == "b":
                self.hold_trigger = not self.hold_trigger
                print(f"\nGiữ cò: {'BẬT' if self.hold_trigger else 'tắt'}")
            elif ch == " ":
                self.until[" "] = now + 0.15
            else:
                self.until[ch] = now + HOLD_S

    def held(self, ch: str, now: float) -> bool:
        return self.until.get(ch, 0) > now

    def state(self, now: float) -> tuple[tuple[float, float], tuple[float, float], int]:
        h = lambda c: 1.0 if self.held(c, now) else 0.0  # noqa: E731
        j2 = (h("d") - h("a"), h("w") - h("s"))
        j1 = (h("l") - h("j"), h("i") - h("k"))
        b = 0
        if self.held(" ", now) or self.hold_trigger:
            b |= BTN_TRIGGER
        if self.held("q", now):
            b |= BTN_RECENTER
        if self.held("e", now):
            b |= BTN_ASK_AI
        if self.held("f", now):
            b |= BTN_J1
        if self.held("r", now):
            b |= BTN_J2
        return j1, j2, b


def scripted_state(script, elapsed: float):
    cur = script[0]
    for step in script:
        if elapsed >= step[0]:
            cur = step
    if cur[1] is None:
        return None
    return cur[2], cur[1], cur[3]  # j1 (nhìn), j2 (đi), nút


def simulate(args: argparse.Namespace) -> None:
    s = open_socket(args.cong_tay_cam)
    target = (args.toi, args.cong)
    fake = FakeController()
    pad = KeyboardPad()
    script = THU_NHANH if args.kich_ban == "thu-nhanh" else None
    if script is None and not pad.ok:
        sys.exit("Chế độ bàn phím chỉ chạy trên Windows. Dùng --kich-ban thu-nhanh.")
    print(f"Giả lập tay cầm: gửi 100 gói/s tới {target[0]}:{target[1]}, nghe lệnh ở cổng {args.cong_tay_cam}.")
    if script is None:
        print(__doc__.split("Phím khi giả lập", 1)[1].split("\n", 1)[1])
    t0 = time.time()
    seq, next_t, last_print = 0, t0, 0.0
    sent_since_print = 0
    try:
        while True:
            now = time.time()
            for data, addr in recv_all(s):
                for reply in fake.handle(data, addr, now - t0):
                    s.sendto(reply, addr)
            while fake.log:
                print(f"\n[giả lập] {fake.log.pop(0)}")
            if script is not None:
                st = scripted_state(script, now - t0)
                if st is None:
                    break
                j1, j2, buttons = st
            else:
                pad.poll(now)
                if pad.quit:
                    break
                j1, j2, buttons = pad.state(now)
            if args.mot_can:
                # giống firmware ESP32-CAM + 1 joystick: chỉ có cần ở j2, nút nhấn cần ở bit 4, cờ 1 cần + không IMU
                pressed = buttons & (BTN_TRIGGER | BTN_J2)
                j1, buttons = (0.0, 0.0), FLAG_SINGLE_STICK | FLAG_IMU_ERROR | (BTN_J2 if pressed else 0)
            if now >= next_t:
                next_t += 0.01
                if now - next_t > 0.1:
                    next_t = now + 0.01
                dest = fake.destination(now - t0, target)
                if args.mega:  # dòng chữ "X,Y,B" như ESP32 nhận từ Mega; tâm lệch như mạch thật
                    pkt = make_xyb(j2, bool(buttons & (BTN_TRIGGER | BTN_J2)), center=(409, 387))
                else:
                    pkt = make_packet(seq, int((now - t0) * 1000) & 0xFFFFFFFF, j1=j1, j2=j2, buttons=buttons)
                try:
                    s.sendto(pkt, dest)
                    sent_since_print += 1
                except OSError as e:
                    print(f"\nLỗi gửi tới {dest}: {e}")
                    time.sleep(0.5)
                seq = (seq + 1) & 0xFF
            if now - last_print > 0.5:
                el = now - t0
                game = f"game {fake.pc[0]}" if fake.pc_alive(el) else "chưa thấy game"
                print(f"\r{sent_since_print / max(0.001, now - last_print) if last_print else 0:5.0f} gói/s  {game:24} "
                      f"rung={fake.motor(el):3d}  đèn xanh={'BẬT' if fake.led_green else 'tắt'} "
                      f"đỏ={'BẬT' if fake.led_red else 'tắt'}  J1=({j1[0]:+.0f},{j1[1]:+.0f}) J2=({j2[0]:+.0f},{j2[1]:+.0f}) "
                      f"nút={buttons:05b}   ", end="", flush=True)
                last_print, sent_since_print = now, 0
            time.sleep(0.002)
    except KeyboardInterrupt:
        pass
    finally:
        s.close()
        print()


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="che_do", required=True)
    n = sub.add_parser("nghe", help="theo dõi tay cầm thật (tắt game trước)")
    n.add_argument("--cong", type=int, default=PC_PORT, help="cổng máy tính nghe (CONG_PC)")
    n.add_argument("--ip", help="IP tay cầm, nếu mạng chặn gói quảng bá")
    n.add_argument("--lenh", action="append", help="lệnh gửi khi thấy tay cầm: I, C, J, R<0-255>, P<mức>,<ms>, L<x>,<đ>")
    n.add_argument("--thoi-gian", type=float, default=0, help="tự dừng sau N giây (0 = chạy mãi)")
    g = sub.add_parser("gia-lap", help="giả lập tay cầm gửi vào game")
    g.add_argument("--toi", default="127.0.0.1", help="IP máy chạy game (255.255.255.255 = phát quảng bá)")
    g.add_argument("--cong", type=int, default=PC_PORT, help="cổng game nghe (CONG_PC)")
    g.add_argument("--cong-tay-cam", type=int, default=CONTROLLER_PORT, help="cổng tay cầm giả nghe lệnh")
    g.add_argument("--kich-ban", choices=["ban-phim", "thu-nhanh"], default="ban-phim")
    g.add_argument("--mot-can", action="store_true", help="giả lập tay cầm 1 cần (ESP32-CAM + 1 mạch joystick)")
    g.add_argument("--mega", action="store_true", help="giả lập bộ joystick → Mega → ESP32: gửi dòng chữ X,Y,B")
    args = ap.parse_args()
    if args.che_do == "nghe":
        listen(args)
    else:
        simulate(args)


if __name__ == "__main__":
    main()
