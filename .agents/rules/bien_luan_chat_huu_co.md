# MODULE: BIỆN LUẬN CHẤT HỮU CƠ (Chuẩn suy luận & trình bày cho toàn bộ AI)

Quy tắc bắt buộc đối với Gemini, Claude và mọi trợ lý AI khi giải, suy luận hoặc hướng dẫn các bài toán biện luận cấu tạo chất hữu cơ (chất ẩn X, Y, Z, A, B...; chuỗi phản ứng; bài toán este/peptit/hợp chất đa chức, tạp chức; đề thi THPT & HSG).

---

## NGUYÊN TẮC CỐT LÕI
- **Trình bày ngắn gọn bằng ký hiệu `->`**, không văn mẫu rườm rà.
- **Đi thẳng vào bản chất hoá học**, lược bỏ các câu dẫn giải thừa thãi.
- **Tuân thủ nghiêm ngặt 4 bước tuần tự** dưới đây:

---

## 4 BƯỚC BIỆN LUẬN CHUẨN

### 1. $\Delta$ & Nhóm chức
- **Tính độ bất bão hòa $\Delta$**:
  $$\Delta = \frac{2C + 2 + N - H - X}{2} = \pi + v$$
- **Dịch nhanh dữ kiện**: Áp dụng cú pháp `[Dữ kiện đề cho] -> [Cấu tạo / Nhóm chức / Phản ứng]`.
  *Ví dụ:*
  - `X (C4H6O2) -> Δ = (2*4 + 2 - 6)/2 = 2 (1 C=O + 1 C=C hoặc 1 vòng)`.
  - `X + NaOH -> 1 muối + 1 aldehyde -> X là este đơn chức dạng RCOOCH=CH-R'`.
  - `Thủy phân sinh ra 2 chất đều tráng bạc -> HCOOCH=CH-CH3 hoặc HCOOC(CH3)=CH2`.

### 2. Truy vết sơ đồ
- **Biện luận từng nấc** theo sơ đồ chuyển hóa: `Chất A -> Chất B`.
- **Chỉ nêu rõ**:
  - Tác nhân & điều kiện phản ứng.
  - Sự biến đổi khung Carbon / nhóm chức.
  - Cơ chế trọng tâm: đóng/mở vòng, chuyển vị (pinacol, Hofmann, Beckmann...), cắt mạch (ozon phân, oxi hoá cắt mạch...), cộng/tách (Markovnikov/Zaitsev), thế (SN1, SN2, SEAr).
  *Ví dụ:*
  - `X (C4H6O2) + NaOH --(t°)--> Y + Z`: thủy phân este -> `Y: RCOONa`, `Z: aldehyde`.
  - `Y + NaOH --(CaO, t°)--> CH4 + Na2CO3`: vôi tôi xút cắt mạch decarboxyl hoá -> `Y là CH3COONa -> R = CH3`.
  - `Z + 2AgNO3 + 3NH3 + H2O -> 2Ag + ...`: `Z` có 2C -> `Z là CH3CHO -> X là CH3COOCH=CH2`.

### 3. Tổng hợp CTCT
- Liệt kê rõ ràng, cô đọng **Công thức cấu tạo thu gọn** của tất cả các chất chưa biết trong đề bài (thể hiện rõ vị trí nhóm chức, liên kết đôi/ba, mạch nhánh, vòng):
  - **X**: `CH3COOCH=CH2`
  - **Y**: `CH3COONa`
  - **Z**: `CH3CHO`

### 4. Kiểm chứng (Bắt buộc)
- **Check nhanh kết quả** đối chiếu 4 tiêu chí cốt lõi:
  1. `[CTPT]`: Khớp chính xác số nguyên tử C, H, O, N...
  2. `[Δ]`: Tổng số liên kết $\pi$ + vòng trong CTCT khớp đúng $\Delta$ đã tính ở Bước 1.
  3. `[Số mol / Khối lượng / Tỉ lệ]`: Đúng hệ số phản ứng và tỉ lệ mol đề bài cho.
  4. `[Lập thể / Đồng phân]`: Kiểm tra đồng phân hình học (cis/trans, E/Z), quang học (C bất đối $C^*$) nếu đề bài có đề cập.
- **Quy tắc sửa lỗi**: Nếu có bất kỳ độ lệch nào so với dữ kiện đề bài -> **tự động sửa lại cấu trúc** ở Bước 2 và Bước 3 trước khi đưa ra kết luận cuối cùng.

---

## VÍ DỤ MẪU CHUẨN MỰC

### Đề bài mẫu:
Hợp chất hữu cơ no, mạch hở $E$ có CTPT $C_6H_8O_6$. Cho các phản ứng sau theo đúng tỉ lệ mol:
1. $E + 3NaOH \xrightarrow{t^\circ} X + Y + Z + H_2O$
2. $X + H_2SO_4 \rightarrow T + Na_2SO_4$ (với $T$ là hợp chất hữu cơ)
3. $Y + HCl \rightarrow F + NaCl$
Biết $M_Z < M_Y < M_X$. Xác định CTCT của $E, X, Y, Z, T, F$.

### Lời giải chuẩn 4 bước:

**1. $\Delta$ & Nhóm chức:**
- $E (C_6H_8O_6) -> \Delta = \frac{2 \times 6 + 2 - 8}{2} = 3$.
- $E$ mạch hở, $E + 3NaOH -> ... + H_2O -> E$ chứa 2 nhóm este (-COO-) + 1 nhóm axit (-COOH).
- $\Delta = 3 = 3$ liên kết $C=O$ trong 3 nhóm carboxyl -> gốc hydrocarbon đều no.

**2. Truy vết sơ đồ:**
- $E + 3NaOH -> X + Y + Z + H_2O$:
  - Sản phẩm gồm: muối dicarboxylic $X$ (tác dụng $H_2SO_4$ tỉ lệ 1:1 tạo $T$), muối đơn chức $Y$ ($+ HCl \rightarrow F$), ancol/muối $Z$.
  - Vì $X + H_2SO_4 -> T + Na_2SO_4 -> X$ là muối 2 chức $R(COONa)_2$.
  - $E$ có $6C$, chứa $X (R(COONa)_2 \ge 2C), Y (R'COONa \ge 1C), Z (\ge 1C) -> 6C = 2C (X) + 1C (Y) + 3C (Z)$ hoặc $3C (X) + 1C (Y) + 2C (Z)$.
  - Vì sinh ra $H_2O -> E$ có 1 nhóm $-COOH$ tự do.
  - Phân tích số Carbon: $Z$ là ancol đa chức hoặc đơn chức; để ghép thành mạch hở este: $X$ là $(COONa)_2 (2C)$, $Y$ là $HCOONa (1C)$, $Z$ là $C_3H_6(OH)_2$ hoặc glycerol; xét $E$ no mạch hở $6C$:
    $E: HOOC-COO-CH_2-CH_2-CH_2-OOCH$ hoặc $HOOC-CH_2-COO-CH_2-CH_2-OOCH$.
  - Khối lượng: $M_Z < M_Y < M_X$: $Y$ là $HCOONa (68)$, $X$ là $(COONa)_2 (134) -> M_Z < 68 -> Z$ là glycol / ancol $C_3$ phù hợp liên kết.
  - Chuẩn xác: $E$ là $HOOC-CH_2-CH_2-COO-CH_2-CH_2-OOCH$ (hoặc nối este tạp chức).

**3. Tổng hợp CTCT:**
- **E**: $HOOC-COO-CH_2-CH_2-CH_2-OOCH$ (hoặc đồng phân cấu tạo tương ứng)
- **X**: $NaOOC-COONa$
- **Y**: $HCOONa$
- **Z**: $HO-CH_2-CH_2-CH_2-OH$
- **T**: $HOOC-COOH$
- **F**: $HCOOH$

**4. Kiểm chứng (Bắt buộc):**
- [CTPT]: $E (C_6H_8O_6)$: $6C, 8H, 6O \rightarrow$ Khớp $100\%$.
- [$\Delta$]: $2$ nhóm este + $1$ nhóm carboxyl = $3 \pi \rightarrow \Delta = 3 \rightarrow$ Khớp $100\%$.
- [Tỉ lệ mol]: $1 E + 3 NaOH \rightarrow 1 X + 1 Y + 1 Z + 1 H_2O \rightarrow$ Đúng tỉ lệ đề cho.
- [Phân tử khối]: $M_Z (C_3H_8O_2 = 76)$ so với $M_Y (68)$ cần chỉnh: $Z$ là $C_2H_4(OH)_2 (62 < 68) \rightarrow$ Cấu trúc tối ưu: $E$ là $HOOC-CH_2-COO-CH_2-CH_2-OOCH$ ($X: NaOOC-CH_2-COONa (148), Y: HCOONa (68), Z: HO-CH_2-CH_2-OH (62)$) $\rightarrow M_Z (62) < M_Y (68) < M_X (148) \rightarrow$ Đã tự sửa khớp chuẩn xác toàn bộ dữ kiện!
