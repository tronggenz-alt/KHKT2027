"""Sao lưu / khôi phục dữ liệu qua repo GitHub riêng tư (Render Free không có ổ đĩa lưu) + cấp admin theo CAP_ADMIN_EMAIL."""
import base64
import sqlite3

from backend import sao_luu


class GitHubGia:
    """Giả lập API Git Data của GitHub: nhớ blob cuối cùng được đẩy lên nhánh main."""
    def __init__(self):
        self.blob, self.goi, self.ref = {}, [], None

    def phien(self):
        gh = self

        class Resp:
            def __init__(self, code=201, js=None, content=b""):
                self.status_code, self._js, self.content = code, js or {}, content

            def json(self):
                return self._js

            def raise_for_status(self):
                if self.status_code >= 400:
                    import requests
                    raise requests.HTTPError(response=self)

        class Phien:
            headers = {}

            def post(self, url, json=None, timeout=None):
                gh.goi.append(("POST", url.rsplit("/git/", 1)[1]))
                if url.endswith("/git/blobs"):
                    gh.blob["b1"] = base64.b64decode(json["content"])
                    return Resp(js={"sha": "b1"})
                if url.endswith("/git/trees"):
                    assert json["tree"][0]["path"] == sao_luu.TEN_TEP
                    return Resp(js={"sha": "t1"})
                if url.endswith("/git/commits"):
                    assert json["parents"] == []          # commit không cha → repo không phình to
                    return Resp(js={"sha": "c1"})
                return Resp()

            def patch(self, url, json=None, timeout=None):
                gh.goi.append(("PATCH", "refs"))
                assert json["force"] is True
                gh.ref = json["sha"]
                return Resp(200)

        def get(url, headers=None, timeout=None):
            return Resp(200, content=gh.blob["b1"]) if gh.ref else Resp(404)

        return Phien, get, Resp


def _tao_db(thu_muc, so):
    thu_muc.mkdir(parents=True, exist_ok=True)
    c = sqlite3.connect(thu_muc / "app.db")
    c.execute("CREATE TABLE IF NOT EXISTS t (x INTEGER)")
    c.execute("INSERT INTO t VALUES (?)", (so,))
    c.commit()
    c.close()


def test_tat_khi_chua_cau_hinh(monkeypatch, tmp_path):
    monkeypatch.delenv("SAO_LUU_GITHUB_REPO", raising=False)
    monkeypatch.delenv("SAO_LUU_GITHUB_TOKEN", raising=False)
    assert not sao_luu.bat() and not sao_luu.sao_luu_ngay() and not sao_luu.khoi_phuc_neu_can(tmp_path)


def test_sao_luu_roi_khoi_phuc(monkeypatch, tmp_path):
    gh = GitHubGia()
    Phien, get, _ = gh.phien()
    monkeypatch.setenv("SAO_LUU_GITHUB_REPO", "ai-do/du-lieu")
    monkeypatch.setenv("SAO_LUU_GITHUB_TOKEN", "token-gia")
    monkeypatch.setattr(sao_luu.requests, "Session", Phien)
    monkeypatch.setattr(sao_luu.requests, "get", get)
    monkeypatch.setitem(sao_luu._ban_cuoi, "bam", "")
    may_cu = tmp_path / "cu" / "data"
    _tao_db(may_cu, 42)
    (may_cu / "ai_rieng").mkdir()
    (may_cu / "ai_rieng" / "tai_lieu.txt").write_text("hoá hữu cơ", encoding="utf-8")
    (may_cu / "app_secret.key").write_text("khoa-bi-mat-khong-duoc-sao-luu", encoding="utf-8")
    monkeypatch.setattr(sao_luu, "_goi_tin", lambda thu_muc=may_cu, f=sao_luu._goi_tin: f(thu_muc))
    # cùng dữ liệu → đóng gói giống hệt (băm ổn định) → lần 2 không gửi lại
    assert sao_luu._goi_tin() == sao_luu._goi_tin()
    assert sao_luu.sao_luu_ngay() and len(gh.goi) == 4
    assert sao_luu.sao_luu_ngay() and len(gh.goi) == 4
    # máy mới (ổ đĩa trống) → khôi phục đúng CSDL + tài liệu, KHÔNG có khoá chính
    may_moi = tmp_path / "moi" / "data"
    assert sao_luu.khoi_phuc_neu_can(may_moi)
    assert sqlite3.connect(may_moi / "app.db").execute("SELECT x FROM t").fetchall() == [(42,)]
    assert (may_moi / "ai_rieng" / "tai_lieu.txt").read_text(encoding="utf-8") == "hoá hữu cơ"
    assert not (may_moi / "app_secret.key").exists()
    # đã có CSDL → không ghi đè
    assert not sao_luu.khoi_phuc_neu_can(may_moi)


def test_loi_mang_khong_lam_hong_app(monkeypatch, tmp_path):
    import requests

    class PhienLoi:
        headers = {}

        def post(self, *a, **k):
            raise requests.ConnectionError("mất mạng")

    monkeypatch.setenv("SAO_LUU_GITHUB_REPO", "ai-do/du-lieu")
    monkeypatch.setenv("SAO_LUU_GITHUB_TOKEN", "token-gia")
    monkeypatch.setattr(sao_luu.requests, "Session", PhienLoi)
    monkeypatch.setitem(sao_luu._ban_cuoi, "bam", "")
    _tao_db(tmp_path / "d", 1)
    monkeypatch.setattr(sao_luu, "_goi_tin", lambda f=sao_luu._goi_tin: f(tmp_path / "d"))
    assert sao_luu.sao_luu_ngay() is False


def test_cap_admin_chi_khi_da_xac_thuc_email(monkeypatch):
    from backend import auth, db
    from backend.config import settings
    monkeypatch.setattr(settings, "cap_admin_email", "chu@vi-du.vn")
    uid = db.tao_tai_khoan_email("chu@vi-du.vn", "Chủ app", "băm-giả").id
    assert auth.cap_admin_theo_cau_hinh() == 0 and not db.lay_nguoi_dung(uid).la_admin      # chưa xác thực → không cấp
    db.dat_xac_thuc_email(uid)
    assert auth.cap_admin_theo_cau_hinh() == 1 and db.lay_nguoi_dung(uid).la_admin
