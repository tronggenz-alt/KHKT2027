"""Chạy song song cho AI Riêng – giữ nguyên ngữ cảnh của yêu cầu (ContextVar: key AI `dung_khoa`, kế hoạch `dung_ke_hoach`) trong
mỗi luồng con (thread thường KHÔNG mang theo ContextVar). Dùng cho: làn Gemini đối chiếu khi giải, đọc nhiều trang scan, trích bài
từ nhiều cửa sổ tài liệu, nhúng nhiều lô vector, chạy nhiều câu của Bộ đề kiểm tra cùng lúc."""
from __future__ import annotations

import contextvars
import threading
from concurrent.futures import Future, ThreadPoolExecutor, wait as _wait
from typing import Any, Callable, Iterable

# làn Gemini đối chiếu: pool riêng, dùng chung cho mọi yêu cầu (một làn có thể chạy tiếp ở nền sau khi bài đã trả kết quả)
_LAN_NEN = ThreadPoolExecutor(max_workers=8, thread_name_prefix="ai-rieng-doi-chieu")


_dang_chay: set[Future] = set()
_khoa = threading.Lock()


def gui_nen(fn: Callable[..., Any], *a, **k) -> Future:
    ctx = contextvars.copy_context()
    f = _LAN_NEN.submit(ctx.run, fn, *a, **k)
    with _khoa:
        _dang_chay.add(f)
    f.add_done_callback(lambda x: _bo(x))
    return f


def _bo(f: Future) -> None:
    with _khoa:
        _dang_chay.discard(f)


def cho_nen_xong(toi_da: float = 30.0) -> bool:
    """Chờ mọi làn nền (Gemini đối chiếu) chạy xong – dùng khi tắt app / trong test (không để việc nền chạy sau khi test kết thúc)."""
    with _khoa:
        ds = list(_dang_chay)
    _wait(ds, timeout=toi_da)
    with _khoa:
        return not _dang_chay


def ban_do(fn: Callable[[Any], Any], ds: Iterable[Any], so_luong: int = 4) -> list[Any]:
    """[fn(x) for x in ds] chạy tối đa `so_luong` việc cùng lúc, giữ thứ tự; lỗi của một việc được trả về như kết quả (không làm
    hỏng các việc khác)."""
    ds = list(ds)
    if len(ds) <= 1 or so_luong <= 1:
        return [_an_toan(fn, x) for x in ds]
    with ThreadPoolExecutor(max_workers=min(so_luong, len(ds)), thread_name_prefix="ai-rieng") as ex:
        tuong_lai = [ex.submit(contextvars.copy_context().run, _an_toan, fn, x) for x in ds]
        return [f.result() for f in tuong_lai]


def _an_toan(fn: Callable[[Any], Any], x: Any) -> Any:
    try:
        return fn(x)
    except Exception as e:  # noqa: BLE001 – trả lỗi về cho người gọi xử lý
        return e
