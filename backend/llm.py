"""Gọi AI (chỉ chạy ở server) – lớp điều phối dùng chung cho mọi tính năng.

- Code gọi từng hãng nằm ở backend/ai/providers/{claude,gemini,openai} (cùng giao diện AIProvider – backend/ai/types.py).
  Thứ tự AI + dự phòng cho TỪNG hệ thống (Giải chuỗi / Mô hình 3D / Cơ chế): backend/ai/router.py (routing.json, admin đổi ở
  /admin → Thứ tự AI). Tính năng chỉ dùng PhienHoiThoai – không cần biết đang gọi AI nào.
- Chọn mô hình theo BẬC (giảm chi phí): lite (Gemini Flash-Lite) → flash (Gemini Flash) → pro (Gemini Pro) → claude.
  Bậc do kế hoạch của yêu cầu quyết định (`dung_ke_hoach(KeHoachAI)` – pipeline đặt theo độ khó đề, xem do_kho.py);
  tên mô hình từng bậc đặt trong .env (GEMINI_MODEL_LITE / GEMINI_MODEL_FLASH / GEMINI_MODEL_PRO / CLAUDE_MODEL; ChatGPT: OPENAI_MODEL).
- MẶC ĐỊNH KHÔNG DÙNG CLAUDE: chỉ khi kế hoạch có quyền Claude (admin, Key Claude, mã Claude, API key riêng – kiểm tra ở backend)
  và bậc là "claude". Phiên đi theo chuỗi AI của hệ thống (mặc định claude → gemini → openai), BỎ QUA AI không dùng được
  (chưa có key, không có quyền Claude, người dùng dùng key Gemini riêng → không dùng key máy chủ). AI đang chạy lỗi / hết hạn mức /
  quá thời gian → tự chuyển cả hội thoại sang AI kế tiếp, báo người dùng, ghi nhật ký (ai_fallbacks). Lỗi tài khoản Claude
  (hết tiền / key / quyền / mô hình) làm Claude bị bỏ qua 10 phút cho mọi phiên sau.
- Mỗi lần gọi AI trong một kế hoạch được ghi vào bảng ai_usage (chi_phi_ai.py): người dùng, mức độ, mô hình, token vào/ra...
- Key dùng cho một yêu cầu do `dung_khoa(KhoaAI)` quyết định (auth.py chọn): người dùng có key Gemini riêng → CHỈ dùng
  Gemini bằng key đó (không Claude / ChatGPT / key của chủ app, kể cả khi key riêng lỗi/hết hạn mức); dùng thử → key của chủ app.
"""
from __future__ import annotations

import hashlib
import json
import logging
import re
import time
from contextlib import contextmanager
from contextvars import ContextVar
from dataclasses import dataclass, field
from typing import Any, Callable

from .ai import NHA_CUNG_CAP, router
from .ai.providers.claude.client import van_tay as _van_tay
from .ai.providers.gemini import client as _gemini_client
from .ai.providers.gemini.chat import kiem_tra_key_gemini  # noqa: F401 – tai_khoan.py dùng (Cài đặt → key Gemini)
from .ai.providers.gemini.config import MO_HINH_KIEM_TRA_KEY, mo_hinh_gemini  # noqa: F401
from .ai.types import GoiCongCu, LoiAI, PhanHoi  # noqa: F401 – các module tính năng import từ llm
from .config import settings

log = logging.getLogger(__name__)

# Loại lỗi khiến Claude bị tạm bỏ qua cho mọi phiên sau (đổi key/nạp tiền xong thì khởi động lại app)
_LOI_TAI_KHOAN = {"het_tien", "key", "quyen", "model", "thieu_key"}
THOI_GIAN_TAM_DUNG_CLAUDE = 600  # giây


# ======================================================================= Chọn key cho từng yêu cầu
@dataclass(frozen=True)
class KhoaAI:
    """Key AI của một yêu cầu.

    nguon: "chu_app" = key trong .env của chủ app (Claude → Gemini như cũ); "ca_nhan" = key Gemini riêng của người dùng;
    "khong_co" = không được dùng AI (vd chưa đăng nhập ở mục 3D).
    """

    nguon: str = "chu_app"
    gemini_api_key: str = field(default="", repr=False)  # không bao giờ in/ghi log
    cuoi: str = ""  # 4 ký tự cuối để hiển thị

    @property
    def ma(self) -> str:
        """Mã băm ngắn của key (để tách trạng thái hạn mức theo từng key, không lộ key)."""
        return hashlib.sha256(self.gemini_api_key.encode()).hexdigest()[:16] if self.gemini_api_key else ""

    @property
    def mo_ta(self) -> str:
        if self.nguon == "ca_nhan":
            return f"key Gemini của bạn (…{self.cuoi})" if self.cuoi else "key Gemini của bạn"
        return "key của chủ app" if self.nguon == "chu_app" else "không dùng AI"


KHOA_CHU_APP = KhoaAI("chu_app")
KHOA_KHONG_CO = KhoaAI("khong_co")
_khoa_hien_tai: ContextVar[KhoaAI] = ContextVar("khoa_ai", default=KHOA_CHU_APP)


@contextmanager
def dung_khoa(khoa: KhoaAI):
    """Trong khối `with`, mọi PhienHoiThoai mới tạo dùng key này (đặt trong chính thread chạy bài giải)."""
    tok = _khoa_hien_tai.set(khoa)
    try:
        yield khoa
    finally:
        _khoa_hien_tai.reset(tok)


def khoa_hien_tai() -> KhoaAI:
    return _khoa_hien_tai.get()


def _key_rieng(quyen: Any) -> str:
    """API key Claude RIÊNG của người dùng trong quyền Claude của yêu cầu (claude_rieng.QuyenRieng); "" = dùng key máy chủ."""
    return getattr(quyen, "api_key", "") or ""



def gemini_san_sang() -> bool:
    return _gemini_client.san_sang(khoa_hien_tai())


def ai_san_sang() -> bool:
    """Có gọi được AI với key hiện tại không (Gemini, ChatGPT của máy chủ, hoặc Claude nếu yêu cầu có quyền)."""
    k = khoa_hien_tai()
    if k.nguon != "chu_app":
        return k.nguon == "ca_nhan" and bool(k.gemini_api_key)
    kh = _ke_hoach.get()
    return (gemini_san_sang() or NHA_CUNG_CAP["openai"].co_key()
            or (kh is not None and kh.claude is not None and (bool(settings.api_key) or bool(_key_rieng(kh.claude)))))


# ======================================================================= Kế hoạch dùng AI của một yêu cầu
BAC = ("lite", "flash", "pro", "claude")
TEN_BAC = {"lite": "Gemini Flash-Lite", "flash": "Gemini Flash", "pro": "Gemini Pro", "claude": "Claude"}


@dataclass
class KeHoachAI:
    """Kế hoạch dùng AI của MỘT yêu cầu (một bài giải, một lần vẽ cơ chế, mục 3D...).

    bac: bậc mô hình cho các phiên mới (pipeline đổi theo từng bước); claude: quyền dùng Claude do BACKEND cấp
    (ma_claude.QuyenClaude – admin / mã hợp lệ, hoặc key_claude.QuyenKey – Key Claude dùng chung; người dùng bật "Dùng Claude"),
    None = không được dùng Claude.
    """

    loai: str = "khac"                 # giai | co_che | 3d | khac
    user_id: int | None = None
    job_id: str = ""
    muc_do: str = ""                   # de | trung_binh | hsg ("" = chưa phân loại)
    bac: str = "flash"
    buoc: str = ""                     # bước đang chạy (ghi chi phí): phan_tich | phan_loai | tra_cuu | giai | sua...
    nang_cap: bool = False             # đã nâng bậc vì lời giải sai / JSON hỏng
    claude: Any = None
    claude_thanh_cong: bool = False    # có lượt Claude trả lời thành công (để trừ lượt mã khi bài giải xong)
    thong_bao: list[str] = field(default_factory=list)  # thông báo về Claude (không dùng được / lỗi → Gemini) để trả cho người dùng
    uu_tien_ai: str = ""               # người dùng chọn AI (gemini | openai | claude) → đưa lên đầu chuỗi của yêu cầu này (lỗi vẫn chuyển tiếp)


_ke_hoach: ContextVar[KeHoachAI | None] = ContextVar("ke_hoach_ai", default=None)


@contextmanager
def dung_ke_hoach(kh: KeHoachAI):
    """Trong khối `with`, mọi PhienHoiThoai mới tạo theo kế hoạch này (đặt trong chính thread xử lý yêu cầu)."""
    tok = _ke_hoach.set(kh)
    try:
        yield kh
    finally:
        _ke_hoach.reset(tok)


def ke_hoach_hien_tai() -> KeHoachAI | None:
    return _ke_hoach.get()


def ten_ai(bac: str) -> str:
    """Tên AI sẽ trả lời TRƯỚC cho bậc `bac` của yêu cầu hiện tại, theo thứ tự AI của hệ thống (để tiến trình hiện đúng khi admin đổi
    thứ tự, hoặc chủ app chỉ có key ChatGPT). Thứ tự mặc định Claude → Gemini → ChatGPT cho kết quả như TEN_BAC[bac] trước đây."""
    khoa, kh = khoa_hien_tai(), _ke_hoach.get()
    co_gemini = _gemini_client.san_sang(khoa)
    co_claude = kh is not None and kh.claude is not None and (bac == "claude" or not co_gemini)
    thu_tu = router.thu_tu(router.he_thong_cua(kh.loai if kh else ""))
    if kh is not None and kh.uu_tien_ai in thu_tu:
        thu_tu = [kh.uu_tien_ai] + [t for t in thu_tu if t != kh.uu_tien_ai]
    for t in thu_tu:
        if t == "claude" and co_claude:
            return TEN_BAC["claude"]
        if t == "gemini" and co_gemini:
            return TEN_BAC["pro" if bac == "claude" else bac]
        if t == "openai" and khoa.nguon == "chu_app" and NHA_CUNG_CAP["openai"].co_key():
            return "ChatGPT"
    return TEN_BAC[bac]


# ======================================================================= Claude: trạng thái tạm dừng (chính sách – dùng chung mọi phiên)
_claude_tam_dung_den = 0.0
_claude_ly_do_tam_dung = ""
_tam_dung_rieng: dict[str, tuple[float, str]] = {}   # vân tay key riêng → (tạm dừng đến, lý do) – không ảnh hưởng key máy chủ


def _claude_kha_dung() -> bool:
    return bool(settings.api_key) and time.time() >= _claude_tam_dung_den


def _tam_dung_claude(loi: LoiAI) -> None:
    global _claude_tam_dung_den, _claude_ly_do_tam_dung
    if loi.loai in _LOI_TAI_KHOAN:
        _claude_tam_dung_den = time.time() + THOI_GIAN_TAM_DUNG_CLAUDE
        _claude_ly_do_tam_dung = str(loi)


# ======================================================================= Phiên hội thoại trung lập (đi theo chuỗi AI của router)
class PhienHoiThoai:
    """Một cuộc hội thoại với AI. Lịch sử trung lập `_lich_su` (để dựng lại khi chuyển nhà cung cấp giữa phiên); mỗi nhà cung cấp
    giữ lịch sử riêng (Claude: `_claude_msgs` kể cả khối thinking; Gemini: `_gemini_contents` kể cả thought signature; ChatGPT:
    gửi lại từ `_lich_su`). AI lỗi → chuyển AI kế tiếp trong `chuoi` (thứ tự của hệ thống, đã bỏ AI không dùng được).
    """

    def __init__(
        self,
        system: str,
        *,
        tools: list[dict] | None = None,
        output_schema: dict | None = None,
        effort: str | None = None,
        max_tokens: int = 16000,
        bao: Callable[[str], None] | None = None,
        gemini_uu_tien: str = "",
        claude_model: str = "",
        bac: str | None = None,
        suy_nghi: int | None = None,
    ):
        self.system = system
        self.suy_nghi = suy_nghi  # ngân sách suy nghĩ Gemini theo mức độ thông minh (None = mặc định); Claude / ChatGPT dùng `effort`
        self._khi_co_chu: Callable[[str | None], None] | None = None
        self.claude_model = claude_model  # mô hình Claude riêng cho phiên (trống = CLAUDE_MODEL)
        self.ke_hoach = _ke_hoach.get()
        self.bac = bac or (self.ke_hoach.bac if self.ke_hoach else "flash")  # lite | flash | pro | claude
        self.tools = tools
        self.output_schema = output_schema
        self.effort = effort
        self.max_tokens = max_tokens
        self.bao = bao or (lambda _m: None)
        self._lich_su: list[dict] = []
        self._claude_msgs: list[dict] | None = None
        self._gemini_contents: list | None = None
        self._cac_loi: list[tuple[str, LoiAI]] = []
        self.loi_claude = ""
        self.khoa = khoa_hien_tai()  # key của yêu cầu tạo phiên (dùng suốt phiên)
        self.he_thong = router.he_thong_cua(self.ke_hoach.loai if self.ke_hoach else "")
        self.claude_duoc_dung = False
        if self.khoa.nguon != "khong_co":
            if self.bac == "claude":
                ly_do = self._ly_do_khong_dung_claude()
                if ly_do is None:
                    self.claude_duoc_dung = True
                else:  # không có quyền / mã hết lượt / Claude đang tạm dừng → Gemini Pro (hoặc AI kế tiếp)
                    self.bac, self.loi_claude = "pro", ly_do
            if (not self.claude_duoc_dung and not gemini_san_sang() and self.ke_hoach is not None
                    and self.ke_hoach.claude is not None and self._ly_do_khong_dung_claude() is None):
                self.claude_duoc_dung, self.bac = True, "claude"  # chủ app chưa có GEMINI_API_KEY: người được phép dùng Claude
        # mô hình Gemini thử trước: theo bậc (phiên giữ nguyên mô hình đã trả lời – thought signature)
        self.gemini_uu_tien = gemini_uu_tien or mo_hinh_gemini("pro" if self.bac == "claude" else self.bac)
        # chuỗi AI của phiên = thứ tự của hệ thống, bỏ AI không dùng được (không AI nào → giữ Gemini để báo lỗi như cũ)
        if self.khoa.nguon == "khong_co":
            self.chuoi, self.nha_cung_cap = [], "khong_co"
        else:
            self.chuoi = [t for t in router.thu_tu(self.he_thong) if NHA_CUNG_CAP[t].ly_do_bo_qua(self) is None] or ["gemini"]
            uu_tien = self.ke_hoach.uu_tien_ai if self.ke_hoach else ""
            if uu_tien in self.chuoi:  # người dùng chọn AI cho yêu cầu này (không dùng được → giữ thứ tự của hệ thống)
                self.chuoi.remove(uu_tien)
                self.chuoi.insert(0, uu_tien)
            self.nha_cung_cap = self.chuoi[0]

    @property
    def key_claude_rieng(self) -> str:
        return _key_rieng(self.ke_hoach.claude if self.ke_hoach else None)

    def _ly_do_khong_dung_claude(self) -> str | None:
        """None = được dùng Claude cho phiên này (đã giữ 1 lượt mã cho yêu cầu); ngược lại là lý do (tiếng Việt)."""
        kh = self.ke_hoach
        if kh is None or kh.claude is None:
            return "tài khoản chưa mở Claude"
        rieng = _key_rieng(kh.claude)
        if rieng:  # key Claude riêng của người dùng: không phụ thuộc key / trạng thái tạm dừng của máy chủ
            den, ly_do = _tam_dung_rieng.get(_van_tay(rieng), (0.0, ""))
            if time.time() < den:
                return f"key Claude của bạn đang lỗi ({ly_do.rstrip('.')})"
        elif not settings.api_key:
            return "chủ app chưa có ANTHROPIC_API_KEY"
        elif not _claude_kha_dung():
            return f"Claude đang tạm dừng ({_claude_ly_do_tam_dung.rstrip('.')})"
        if not kh.claude.giu():
            return getattr(kh.claude, "ly_do_het", "") or "hết lượt Claude"
        return None

    def _bao_claude(self, thong_diep: str) -> None:
        """Báo người dùng việc AI không dùng được / lỗi / chuyển AI (qua `bao` + lưu vào kế hoạch cho API không có luồng tiến trình)."""
        self.bao(thong_diep)
        if self.ke_hoach is not None and thong_diep not in self.ke_hoach.thong_bao:
            self.ke_hoach.thong_bao.append(thong_diep)

    def _ghi_chi_phi(self, nha_cung_cap: str, model: str, ph: PhanHoi | None = None, loi: Exception | None = None) -> None:
        """Ghi một lần gọi AI vào bảng chi phí (chỉ khi có kế hoạch – yêu cầu thật của người dùng)."""
        if self.ke_hoach is None:
            return
        try:
            from . import chi_phi_ai

            nguon = "claude_rieng" if nha_cung_cap == "claude" and self.key_claude_rieng else self.khoa.nguon
            chi_phi_ai.ghi(self.ke_hoach, nha_cung_cap=nha_cung_cap, model=model, bac=self.bac, nguon_key=nguon,
                           tokens_in=ph.tokens_in if ph else 0, tokens_out=ph.tokens_out if ph else 0,
                           tokens_think=ph.tokens_think if ph else 0, loi=loi)
        except Exception:  # noqa: BLE001 – ghi chi phí lỗi không được làm hỏng bài giải
            log.exception("Không ghi được chi phí AI")

    # ------------------------------------------------------------ chuyển AI dự phòng
    def _ai_ke_tiep(self) -> str | None:
        i = self.chuoi.index(self.nha_cung_cap) if self.nha_cung_cap in self.chuoi else len(self.chuoi)
        return next((t for t in self.chuoi[i + 1:] if NHA_CUNG_CAP[t].ly_do_bo_qua(self) is None), None)

    def _khi_claude_loi(self, e: LoiAI) -> None:
        rieng = self.key_claude_rieng
        if rieng and e.loai in _LOI_TAI_KHOAN:
            _tam_dung_rieng[_van_tay(rieng)] = (time.time() + THOI_GIAN_TAM_DUNG_CLAUDE, str(e))
            from . import claude_rieng  # admin thấy "Key lỗi" trong danh sách người dùng
            claude_rieng.ghi_loi(self.ke_hoach.user_id if self.ke_hoach else None, str(e))
        elif not rieng:
            _tam_dung_claude(e)
        self.loi_claude = str(e)

    def _khi_claude_xong(self) -> None:
        if self.ke_hoach is not None:
            self.ke_hoach.claude_thanh_cong = True
            if self.key_claude_rieng:
                from . import claude_rieng
                claude_rieng.xoa_loi(self.ke_hoach.user_id)  # key riêng lại chạy được (vd đã nạp tiền) → bỏ nhãn "Key lỗi"

    def _thong_bao_chuyen(self, tu: str, sang: str, e: LoiAI) -> str:
        if tu == "claude" and sang == "gemini":  # (giữ nguyên câu cũ)
            return (f"Key Claude của bạn lỗi: {e} → chuyển sang Gemini (không dùng key Claude của máy chủ)." if self.key_claude_rieng
                    else f"Claude lỗi: {e} → tự chuyển sang Gemini Pro (không trừ lượt Claude).")
        thua = " (không trừ lượt Claude)" if tu == "claude" else ""
        return f"{router.TEN_AI[tu]} lỗi: {e} → tự chuyển sang {router.TEN_AI[sang]}{thua}."

    def _loi_cuoi(self) -> LoiAI:
        (ten1, e1), *sau = self._cac_loi
        if not sau:
            if ten1 == "claude":
                goi_y = "" if settings.gemini_api_key else " (Có thể thêm GEMINI_API_KEY vào .env để tự chuyển sang Gemini.)"
                return LoiAI(str(e1) + goi_y, e1.loai)
            return e1
        thong_diep = str(e1) + "".join(f" {router.TEN_AI[t]} dự phòng cũng lỗi: {e}" for t, e in sau)
        return LoiAI(thong_diep, sau[-1][1].loai)

    # ------------------------------------------------------------ API công khai
    def gui(
        self,
        text: str | None = None,
        anh: tuple[str, str] | list[tuple[str, str]] | None = None,
        ket_qua_cong_cu: list[tuple[GoiCongCu, dict]] | None = None,
        khi_co_chu: Callable[[str | None], None] | None = None,
    ) -> PhanHoi:
        """Gửi một lượt người dùng: văn bản (+ ảnh base64) hoặc kết quả công cụ.
        khi_co_chu: nhận từng đoạn chữ của câu trả lời ngay khi mô hình viết ra (streaming); None = bắt đầu lại (thử lại / đổi mô hình)."""
        self._khi_co_chu = khi_co_chu
        if ket_qua_cong_cu is not None:
            luot = {"vai": "tool", "ket_qua": ket_qua_cong_cu}
        else:
            luot = {"vai": "user", "text": text or "", "anh": anh}
        self._lich_su.append(luot)
        if self.nha_cung_cap == "khong_co":
            raise LoiAI("Cần đăng nhập và có key Gemini (hoặc còn lượt dùng thử) để dùng AI. Vào Cài đặt để thêm key.", "thieu_key")
        self._cac_loi = []
        while True:
            ncc = NHA_CUNG_CAP[self.nha_cung_cap]
            if ncc.ten == "gemini" and self._gemini_contents is None and self.loi_claude and len(self._lich_su) == 1:
                self._bao_claude(f"Dùng {TEN_BAC.get(self.bac, 'Gemini')} thay Claude – {self.loi_claude.rstrip('.')}")
            try:
                ph = ncc.chat(self, luot)
            except LoiAI as e:
                self._ghi_chi_phi(ncc.ten, ncc.mo_hinh(self), loi=e)
                if ncc.ten == "claude":
                    self._khi_claude_loi(e)
                self._cac_loi.append((ncc.ten, e))
                sau = self._ai_ke_tiep()
                if sau is None:
                    raise self._loi_cuoi() from e
                router.ghi_du_phong(self.he_thong, ncc.ten, sau, str(e), self.ke_hoach.user_id if self.ke_hoach else None,
                                    self.ke_hoach.job_id if self.ke_hoach else "")
                self._bao_claude(self._thong_bao_chuyen(ncc.ten, sau, e))
                if ncc.ten == "claude" and sau == "gemini":
                    self.bac, self.gemini_uu_tien = "pro", mo_hinh_gemini("pro")
                self.nha_cung_cap = sau
                continue
            if ncc.ten == "claude":
                self._khi_claude_xong()
            self._ghi_chi_phi(ncc.ten, ph.model, ph)
            return ph


def mo_ta_mo_hinh(ph: PhanHoi) -> str:
    """Tên hiển thị của mô hình đã trả lời (dùng cho chân ảnh đáp án)."""
    if ph.nha_cung_cap == "gemini":
        return f"Gemini ({ph.model})"
    if ph.nha_cung_cap == "openai":
        return f"ChatGPT ({ph.model or settings.openai_model})"
    return f"Claude ({ph.model or settings.model})"


def doc_json(text: str) -> Any:
    """Đọc JSON từ câu trả lời (bỏ khung ```json nếu có)."""
    t = text.strip()
    m = re.search(r"```(?:json)?\s*(.*?)```", t, re.S)
    if m:
        t = m.group(1).strip()
    if not t.startswith(("{", "[")):
        start, end = t.find("{"), t.rfind("}")
        if start != -1 and end > start:
            t = t[start : end + 1]
    return json.loads(t)
