# GIÁO TRÌNH CHUẨN: TRỢ LÝ GIẢI PHẪU & SINH LÝ HỌC THPT
### (Chương trình Giáo dục phổ thông 2018 – Sinh học 11 & Bám sát Đề thi Tốt nghiệp THPT)

Tài liệu này được biên soạn bởi **Trợ lý Giải phẫu** dành cho học sinh THPT và hệ thống web Giải phẫu 3D. Mỗi bài học về từng cơ quan đều tuân thủ nghiêm ngặt chuẩn cấu trúc 7 phần:
1. **Tổng quan**
2. **Cấu tạo liên quan đến chức năng**
3. **Đường đi (có mũi tên "→")**
4. **Cơ chế hoạt động chi tiết**
5. **Lưu ý thi THPT (gắn nhãn [Nhận biết], [Thông hiểu], [Vận dụng])**
6. **Câu hỏi tự luyện (trắc nghiệm 4 đáp án + giải thích)**
7. **Dữ liệu mũi tên cho mô hình 3D (khối JSON)**

---

## CHUYÊN ĐỀ 1: TRÁI TIM (HEART) – HỆ TUẦN HOÀN

### 1. Tổng quan
Tim là cơ quan nòng cốt của hệ tuần hoàn, nằm trong khoang lồng ngực giữa hai lá phổi và hơi chếch sang bên trái. Tim đóng vai trò như một chiếc máy bơm hút và đẩy máu liên tục, tạo động lực chính để lưu thông máu trong toàn bộ hệ mạch, vận chuyển dưỡng chất, khí ($O_2, CO_2$) và hormone đi khắp các cơ quan trong cơ thể.

### 2. Cấu tạo liên quan đến chức năng
- **Bốn buồng tim riêng biệt (2 tâm nhĩ, 2 tâm thất):** Tâm nhĩ ở phía trên có thành mỏng chuyên tiếp nhận máu từ các tĩnh mạch trở về tim; tâm thất ở phía dưới có thành cơ dày chuyên co bóp tống máu vào các động mạch lớn.
- **Độ dày không đều của thành cơ tâm thất:** Thành tâm thất trái dày gấp khoảng 3 lần thành tâm thất phải → giúp tạo lực co bóp cực lớn thắng sức cản hệ tuần hoàn lớn để đẩy máu đi nuôi toàn thân (trong khi thất phải chỉ cần bơm máu lên phổi ở cự ly gần).
- **Hệ thống van tim một chiều:**
  - *Van nhĩ - thất (van 3 lá ở tim phải, van 2 lá ở tim trái):* Chỉ cho máu chảy từ tâm nhĩ xuống tâm thất, đóng chặt khi tâm thất co để ngăn máu trào ngược về tâm nhĩ.
  - *Van bán nguyệt / van tổ chim (van động mạch chủ, van động mạch phổi):* Chỉ cho máu chảy từ tâm thất vào động mạch, đóng chặt khi tâm thất dãn để ngăn máu dội ngược lại tâm thất.
- **Tế bào cơ tim có cấu trúc đĩa xen (intercalated discs):** Tế bào cơ tim phân nhánh và liên kết chặt chẽ qua cầu liên bào → giúp xung điện lan truyền cực nhanh, làm các buồng tim co đồng bộ theo quy luật "tất cả hoặc không có gì".
- **Hệ dẫn truyền tim tự động:** Gồm nút xoang nhĩ, nút nhĩ thất, bó His và mạng Purkinje → tự phát xung điện nhịp nhàng, đảm bảo tim đập liên tục mà không cần sự điều khiển trực tiếp của vỏ não.

### 3. Đường đi (có mũi tên)

**Vòng tuần hoàn lớn:**
Tâm thất trái (máu giàu $O_2$, đỏ tươi)
→ van động mạch chủ
→ động mạch chủ
→ các động mạch nhỏ → mao mạch cơ quan (trao đổi chất: nhả $O_2$, nhận $CO_2$)
→ các tĩnh mạch nhỏ (máu nghèo $O_2$, đỏ thẫm)
→ tĩnh mạch chủ trên + tĩnh mạch chủ dưới
→ tâm nhĩ phải

*Tóm tắt vòng lớn:* Xuất phát từ **tâm thất trái** (máu giàu $O_2$) và kết thúc tại **tâm nhĩ phải** (máu nghèo $O_2$).

**Vòng tuần hoàn nhỏ (phổi):**
Tâm thất phải (máu nghèo $O_2$, đỏ thẫm)
→ van động mạch phổi
→ động mạch phổi
→ mao mạch phổi (thải $CO_2$, nhận $O_2$)
→ tĩnh mạch phổi (máu giàu $O_2$, đỏ tươi)
→ tâm nhĩ trái → van hai lá → tâm thất trái

*Tóm tắt vòng nhỏ:* Xuất phát từ **tâm thất phải** (máu nghèo $O_2$) và kết thúc tại **tâm nhĩ trái** (máu giàu $O_2$).

### 4. Cơ chế hoạt động chi tiết
1. **Tính tự động và hệ dẫn truyền tim:**
   - Xung điện được phát nhịp tự động từ **Nút xoang nhĩ (SA)** nằm ở thành tâm nhĩ phải.
   - Xung điện lan truyền khắp hai tâm nhĩ gây co cơ nhĩ, đồng thời truyền đến **Nút nhĩ thất (AV)**.
   - Từ nút nhĩ thất, xung truyền dọc theo **Bó His** (vách liên thất) rồi tỏa vào **Mạng Purkinje** nằm trong thành cơ tâm thất.
   - Xung điện kích hoạt hai tâm thất co đồng thời từ đỉnh (mỏm tim) ngược lên đáy tim để đẩy máu lên các động mạch.
2. **Chu kì hoạt động của tim (người trưởng thành ~75 nhịp/phút, chu kì $T = 0,8\text{ s}$):**
   - *Pha nhĩ co ($0,1\text{ s}$):* Hai tâm nhĩ co bóp, áp lực buồng nhĩ tăng đẩy nốt 30% lượng máu còn lại xuống tâm thất qua van nhĩ thất mở. Van bán nguyệt đóng. Tâm nhĩ dãn nghỉ $0,7\text{ s}$ sau đó.
   - *Pha thất co ($0,3\text{ s}$):* Cơ tâm thất co mạnh. Thời kì đầu, áp lực thất vượt nhĩ làm **van nhĩ thất đóng chặt** (tạo tiếng tim T1 "bùm"). Tiếp đó, áp lực thất vượt động mạch làm **van bán nguyệt mở ra**, máu tống vào động mạch chủ và động mạch phổi. Tâm thất dãn nghỉ $0,5\text{ s}$ sau đó.
   - *Pha dãn chung ($0,4\text{ s}$):* Cả nhĩ và thất cùng dãn. Áp lực buồng thất hạ thấp hơn động mạch làm dòng máu dội ngược đóng sập **van bán nguyệt** (tạo tiếng tim T2 "tặc"). Áp suất hạ thấp hơn tĩnh mạch giúp hút máu từ tĩnh mạch về nhĩ và làm mở van nhĩ thất để máu tràn tự do xuống thất (chiếm 70% lượng máu của chu kì).
   - *Quy luật tim không mỏi:* Thời gian nghỉ ngơi của tâm nhĩ ($0,7\text{ s}$) và tâm thất ($0,5\text{ s}$) đều dài hơn thời gian co ($0,1\text{ s}$ và $0,3\text{ s}$), giúp cơ tim kịp phục hồi năng lượng.
3. **Quy luật huyết áp và vận tốc máu:**
   - *Huyết áp:* Giảm liên tục từ động mạch chủ $\to$ tiểu động mạch $\to$ mao mạch $\to$ tiểu tĩnh mạch $\to$ tĩnh mạch chủ (thấp nhất gần bằng $0\text{ mmHg}$).
   - *Vận tốc máu:* Tỉ lệ nghịch với tổng diện tích tiết diện lòng mạch: cao nhất ở động mạch chủ ($\sim 500\text{ mm/s}$) $\to$ thấp nhất ở mao mạch ($\sim 0,5\text{ mm/s}$ do tổng tiết diện mao mạch lớn nhất, tối ưu cho trao đổi chất) $\to$ tăng dần trở lại ở hệ tĩnh mạch gom về tim.

### 5. Lưu ý thi THPT
- **[Nhận biết]** Động mạch phổi chứa máu NGHÈO $O_2$, tĩnh mạch phổi chứa máu GIÀU $O_2$ (ngoại lệ kinh điển cần nhớ, không được mặc định hễ động mạch là máu đỏ tươi).
- **[Nhận biết]** Thứ tự dẫn truyền xung điện tim: *Nút xoang nhĩ $\to$ Nút nhĩ thất $\to$ Bó His $\to$ Mạng Purkinje*.
- **[Thông hiểu]** Vận tốc máu chậm nhất ở mao mạch KHÔNG PHẢI do đường kính 1 mao mạch nhỏ, mà do **tổng diện tích tiết diện của toàn bộ hệ mao mạch là lớn nhất** trong hệ mạch.
- **[Thông hiểu]** Huyết áp tâm thu (huyết áp tối đa) ứng với thời điểm tâm thất co; huyết áp tâm trương (huyết áp tối thiểu) ứng với thời điểm tâm thất dãn.
- **[Vận dụng]** Thành tâm thất trái dày nhất vì phải tạo lực co bóp lớn nhất để thắng sức cản ngoại vi của vòng tuần hoàn lớn.
- **[Vận dụng - Dạng tính toán]:** Chu kì tim $T = \frac{60}{\text{Nhịp tim}}$ (s). Tỉ lệ chuẩn 3 pha: Nhĩ co ($0,125 \times T$), Thất co ($0,375 \times T$), Dãn chung ($0,5 \times T$). Khi nhịp tim tăng, pha dãn chung bị rút ngắn nhiều nhất.
- **[Vận dụng - Dạng tính toán]:** Cung lượng tim $Q = \text{Thể tích tâm thu} \times \text{Nhịp tim}$ (mL/phút hoặc L/phút).

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Ở người khỏe mạnh, tiếng tim thứ nhất (tiếng T1 "bùm") nghe được qua ống nghe là do hiện tượng nào sau đây gây ra?  
A. Van động mạch chủ và van động mạch phổi đóng lại.  
B. Van nhĩ - thất (van 2 lá và van 3 lá) đóng lại khi tâm thất bắt đầu co.  
C. Dòng máu va đập mạnh vào thành động mạch chủ khi tâm thất tống máu.  
D. Van nhĩ - thất mở ra khi tâm nhĩ co bóp đẩy máu xuống tâm thất.  

**Câu 2 [Vận dụng]:** Một người có nhịp tim đo được là 80 nhịp/phút. Thể tích máu tâm thất trái tống đi trong mỗi nhát bóp là $75\text{ mL}$. Giả sử tỉ lệ thời gian các pha trong chu kì tim không đổi, thời gian pha co tâm thất và lượng máu tim bơm trong 1 phút lần lượt là:  
A. $0,281\text{ s}$ và $6,0\text{ lít/phút}$.  
B. $0,300\text{ s}$ và $5,6\text{ lít/phút}$.  
C. $0,250\text{ s}$ và $6,0\text{ lít/phút}$.  
D. $0,375\text{ s}$ và $4,8\text{ lít/phút}$.  

--- Đáp án ---  
**Câu 1:** **B**. Giải thích: Tiếng tim thứ nhất T1 xuất hiện ở đầu pha co tâm thất do áp lực buồng thất tăng cao làm van nhĩ thất đóng sập lại, ngăn máu trào ngược về nhĩ.  
**Câu 2:** **A**. Giải thích: Chu kì tim $T = \frac{60}{80} = 0,75\text{ s}$. Thời gian pha thất co = $0,375 \times 0,75 = 0,28125\text{ s} \approx 0,281\text{ s}$. Cung lượng tim = $75\text{ mL} \times 80\text{ nhịp/phút} = 6000\text{ mL/phút} = 6,0\text{ lít/phút}$.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "heart",
  "flows": [
    {
      "name": "Vòng tuần hoàn lớn",
      "color": "#E53935",
      "path": ["left_ventricle", "aorta", "systemic_capillaries", "vena_cava", "right_atrium"],
      "labels": ["Máu giàu O2", "", "Trao đổi chất", "Máu nghèo O2", ""]
    },
    {
      "name": "Vòng tuần hoàn nhỏ",
      "color": "#1E63D6",
      "path": ["right_ventricle", "pulmonary_artery", "lungs", "pulmonary_veins", "left_atrium", "left_ventricle"],
      "labels": ["Máu nghèo O2", "", "Thải CO2, nhận O2", "Máu giàu O2", "", ""]
    }
  ],
  "highlight": ["left_ventricle", "right_ventricle", "aorta", "pulmonary_artery"]
}
```

---

## CHUYÊN ĐỀ 2: HỆ MẠCH MÁU (BLOOD VESSELS) – ĐỘNG MẠCH, TĨNH MẠCH, MAO MẠCH

### 1. Tổng quan
Hệ mạch máu là mạng lưới đường ống kín dẫn máu từ tim đến khắp các tế bào trong cơ thể rồi quay trở về tim, bao gồm động mạch, mao mạch và tĩnh mạch. Hệ mạch đảm bảo máu lưu thông liên tục dưới áp lực thích hợp để vận chuyển chất dinh dưỡng, khí, hormone và duy trì áp suất thẩm thấu nội môi.

### 2. Cấu tạo liên quan đến chức năng
- **Động mạch:** Thành mạch gồm 3 lớp với lớp cơ trơn và sợi đàn hồi rất dày → chịu được áp lực máu rất cao và co giãn nhịp nhàng (tạo sóng xung huyết áp) để đẩy máu đi xa.
- **Mao mạch:** Thành cực mỏng chỉ gồm một lớp tế bào nội mô duy nhất, đường kính siêu nhỏ ($\sim 7\text{–}10\,\mu\text{m}$, vừa đủ một hồng cầu đi qua) → khuếch tán dễ dàng các chất khí, glucose, amino acid giữa máu và dịch mô.
- **Tĩnh mạch:** Thành mỏng hơn động mạch, lớp cơ trơn và sợi đàn hồi mỏng hơn nhưng lòng mạch rộng → giảm sức cản dòng máu hồi lưu về tim.
- **Hệ thống van tĩnh mạch (ở các chi dưới):** Các nếp gấp hình bán nguyệt hướng về tim → ngăn máu chảy ngược do tác dụng của trọng lực khi người đứng thẳng.
- **Cơ vòng trước mao mạch (tiểu động mạch tận):** Điều hòa lưu lượng máu vào từng vùng mô tùy theo nhu cầu chuyển hóa tại chỗ (ví dụ mở rộng khi cơ bắp hoạt động).

### 3. Đường đi (có mũi tên)

**Dòng chảy liên tục trong hệ mạch:**
Động mạch chủ (áp lực và vận tốc cao nhất)
→ Các động mạch cơ quan (phân nhánh nhỏ dần)
→ Tiểu động mạch (điều hòa phân phối máu)
→ Hệ thống mao mạch cơ quan (vận tốc chậm nhất, diễn ra trao đổi chất và khí)
→ Tiểu tĩnh mạch
→ Tĩnh mạch lớn (có van tĩnh mạch hỗ trợ)
→ Tĩnh mạch chủ trên và tĩnh mạch chủ dưới
→ Tâm nhĩ phải

*Tóm tắt đường đi:* Xuất phát từ **động mạch chủ** (áp lực cao nhất) và kết thúc tại **tĩnh mạch chủ** (huyết áp thấp nhất).

### 4. Cơ chế hoạt động chi tiết
1. **Sự biến đổi huyết áp trong hệ mạch:**
   - Huyết áp cao nhất ở động mạch chủ ($\sim 120\text{ mmHg}$) rồi giảm dần qua các động mạch lớn $\to$ động mạch nhỏ $\to$ mao mạch ($20\text{–}40\text{ mmHg}$) $\to$ tĩnh mạch $\to$ tĩnh mạch chủ ($\approx 0\text{ mmHg}$).
   - Nguyên nhân: Ma sát giữa các phần tử máu với nhau và ma sát giữa máu với thành mạch làm tiêu hao năng lượng áp suất.
2. **Sự biến đổi vận tốc máu trong hệ mạch:**
   - Vận tốc máu tỉ lệ nghịch với tổng diện tích tiết diện lòng mạch: $v = \frac{Q}{S}$ (với $Q$ là lưu lượng tim, $S$ là tổng tiết diện).
   - Vận tốc nhanh nhất ở động mạch chủ ($\sim 500\text{ mm/s}$, do tổng tiết diện nhỏ nhất $\sim 3\text{–}5\text{ cm}^2$).
   - Vận tốc giảm mạnh ở tiểu động mạch và **chậm nhất ở mao mạch** ($\sim 0,5\text{ mm/s}$, do tổng tiết diện mao mạch lên tới $\sim 4500\text{ cm}^2$, gấp hàng trăm lần động mạch chủ).
   - Vận tốc tăng dần trở lại ở hệ tĩnh mạch ($\sim 100\text{–}200\text{ mm/s}$) do các mạch nhỏ hợp lưu thành tĩnh mạch lớn, làm tổng diện tích tiết diện giảm dần.
3. **Cơ chế đưa máu từ tĩnh mạch về tim:**
   - Dù huyết áp trong tĩnh mạch rất thấp, máu vẫn về được tim nhờ:
     + Sự co bóp của các cơ bao quanh tĩnh mạch (bơm cơ) ép vào thành tĩnh mạch.
     + Van tĩnh mạch một chiều chỉ cho máu hướng về tim.
     + Áp suất âm trong khoang lồng ngực khi hít vào hút máu về tĩnh mạch chủ và tim.
     + Sức hút của tâm nhĩ khi dãn nở trong pha dãn chung.

### 5. Lưu ý thi THPT
- **[Nhận biết]** Huyết áp giảm liên tục trong suốt chiều dài hệ mạch: Động mạch chủ > Động mạch > Mao mạch > Tĩnh mạch > Tĩnh mạch chủ.
- **[Nhận biết]** Mao mạch là nơi duy nhất diễn ra trao đổi khí và chất dinh dưỡng với dịch mô do thành mạch chỉ có 1 lớp tế bào nội mô.
- **[Thông hiểu]** Phân biệt đồ thị huyết áp và vận tốc máu: Đồ thị huyết áp dốc xuống một chiều từ ĐM đến TM; đồ thị vận tốc máu có dạng "hình đáy chữ V" (thấp nhất ở mao mạch và tăng dần ở tĩnh mạch).
- **[Thông hiểu]** Vì sao máu trong tĩnh mạch chi dưới không bị ứ đọng khi đứng thẳng? Nhờ sự phối hợp của van tĩnh mạch và sự co bóp của các cơ vân xung quanh.
- **[Vận dụng]** Khi ăn mặn (nhiều ion $Na^+$), nồng độ thẩm thấu trong máu tăng $\to$ giữ nước trong lòng mạch và tăng thể tích máu $\to$ gây tăng huyết áp.
- **[Vận dụng]** Xơ vữa động mạch làm giảm tính đàn hồi và hẹp lòng động mạch $\to$ sức cản ngoại vi tăng $\to$ tâm thất trái phải tăng lực co bóp dẫn đến phì đại cơ tim và cao huyết áp.

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Vận tốc máu chảy trong hệ mao mạch là nhỏ nhất trong toàn bộ hệ mạch. Ý nghĩa sinh học chủ yếu của đặc điểm này là gì?  
A. Giảm thiểu nguy cơ vỡ mao mạch do thành mạch rất mỏng.  
B. Tạo đủ thời gian để quá trình trao đổi khí và chất giữa máu với tế bào đạt hiệu quả tối đa.  
C. Giúp máu kịp nhận nhiệt lượng từ các cơ quan để ổn định thân nhiệt.  
D. Giảm ma sát của các hồng cầu khi đi qua lòng mao mạch hẹp.  

**Câu 2 [Vận dụng]:** Ở một người bị suy tim giai đoạn muộn, tâm thất co bóp yếu khiến áp lực máu dồn ứ ở tĩnh mạch ngoại vi tăng cao. Hiện tượng nào sau đây sẽ xảy ra tại các mô cơ thể?  
A. Tăng lượng nước từ dịch mô thấm trở lại vào mao mạch, làm khô mô.  
B. Giảm áp lực lọc ở mao mạch, dẫn đến hạ huyết áp mô.  
C. Tăng lượng dịch thoát ra khỏi mao mạch vào dịch mô, gây phù nề.  
D. Tăng tốc độ tuần hoàn máu trong các tĩnh mạch sâu.  

--- Đáp án ---  
**Câu 1:** **B**. Giải thích: Vận tốc máu ở mao mạch rất chậm ($\sim 0,5\text{ mm/s}$) giúp các phân tử oxy, CO2, glucose, amino acid có đủ thời gian khuếch tán qua thành nội mô mao mạch.  
**Câu 2:** **C**. Giải thích: Ứ trệ máu tĩnh mạch làm tăng áp suất thủy tĩnh trong mao mạch, đẩy nhiều dịch từ lòng mạch thoát ra khoang gian bào (dịch mô), gây ứ nước và phù nề cơ thể.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "blood_vessels",
  "flows": [
    {
      "name": "Dòng động mạch tống máu",
      "color": "#E53935",
      "path": ["aorta", "iliac_artery", "arterioles", "systemic_capillaries"],
      "labels": ["Động mạch chủ (Huyết áp cao)", "Động mạch chậu", "Tiểu động mạch", "Mao mạch (Vận tốc chậm nhất)"]
    },
    {
      "name": "Dòng tĩnh mạch hồi lưu",
      "color": "#1E63D6",
      "path": ["systemic_capillaries", "venules", "iliac_vein", "vena_cava"],
      "labels": ["Trao đổi chất", "Tiểu tĩnh mạch", "Tĩnh mạch có van", "Tĩnh mạch chủ (Huyết áp thấp nhất)"]
    }
  ],
  "highlight": ["aorta", "vena_cava", "mach_chau"]
}
```

---

## CHUYÊN ĐỀ 3: PHỔI & HỆ HÔ HẤP (LUNGS & RESPIRATION)

### 1. Tổng quan
Phổi là cơ quan trao đổi khí chủ yếu của hệ hô hấp, gồm hai lá phổi (phổi phải 3 thùy, phổi trái 2 thùy) nằm trong lồng ngực phía trên cơ hoành. Chức năng chính của phổi là lấy khí $O_2$ từ không khí ngoài môi trường cung cấp cho máu và đào thải khí $CO_2$ sinh ra từ quá trình hô hấp tế bào ra khỏi cơ thể, đồng thời góp phần điều hòa pH máu.

### 2. Cấu tạo liên quan đến chức năng
- **Bề mặt trao đổi khí khổng lồ:** Phổi chứa khoảng 300–500 triệu phế nang với tổng diện tích bề mặt lên tới $70\text{–}100\text{ m}^2$ (gấp ~50 lần diện tích da) → khuếch tán khí cực nhanh và tối đa.
- **Hàng rào phế nang - mao mạch siêu mỏng:** Thành phế nang và thành mao mạch bao quanh chỉ dày khoảng $0,5\,\mu\text{m}$ → quãng đường khuếch tán khí cực ngắn, tăng tốc độ khuếch tán theo định luật Fick.
- **Màng ẩm ướt bao phủ phế nang:** Bề mặt trong phế nang luôn có một lớp dịch mỏng chứa chất hoạt diện (surfactant) → làm giảm sức căng bề mặt phế nang, ngăn phế nang bị xẹp khi thở ra và giúp khí $O_2, CO_2$ dễ hòa tan để khuếch tán.
- **Hệ thống mao mạch dày đặc bao quanh từng phế nang:** Tạo mạng lưới lưới ôm khít từng túi khí → lượng máu tiếp xúc với không khí phế nang trong mỗi giây luôn dồi dào.
- **Đường dẫn khí phân nhánh hình cây (cây phế quản):** Khí quản có 16–20 vòng sụn chữ C chống xẹp; lớp niêm mạc có nhiều tế bào tiết chất nhầy và lông rung chuyển động hướng lên hầu → lọc sạch bụi bẩn, làm ẩm và sưởi ấm không khí trước khi vào phế nang.

### 3. Đường đi (có mũi tên)

**Đường dẫn khí khi hít vào (thông khí phổi):**
Không khí ngoài môi trường (giàu $O_2$, nghèo $CO_2$)
→ Khoang mũi (lọc bụi, làm ấm, làm ẩm)
→ Hầu (ngã ba hô hấp - tiêu hóa)
→ Thanh quản (cơ quan phát âm, có nắp thanh môn đậy kín khi nuốt)
→ Khí quản (ống sụn chữ C)
→ Phế quản chính (trái và phải)
→ Các nhánh phế quản thùy và tiểu phế quản
→ Phế nang (nơi trao đổi khí với máu mao mạch)

*Tóm tắt đường hít vào:* Xuất phát từ **khoang mũi** và kết thúc tại **các túi phế nang**.

**Đường khuếch tán khí tại phế nang:**
Khí $O_2$ trong lòng phế nang ($pO_2 \approx 100\text{ mmHg}$)
→ Lớp dịch surfactant
→ Biểu mô phế nang $\to$ màng đáy $\to$ nội mô mao mạch
→ Huyết tương $\to$ màng hồng cầu $\to$ liên kết với Hemoglobin ($Hb + O_2 \to HbO_2$)
(Ngược lại: Khí $CO_2$ từ máu mao mạch phổi [$pCO_2 \approx 45\text{ mmHg}$] khuếch tán vào lòng phế nang [$pCO_2 \approx 40\text{ mmHg}$] để thở ra)

### 4. Cơ chế hoạt động chi tiết
1. **Cơ chế thông khí ở phổi (hít vào - thở ra):**
   - *Hít vào bình thường (chủ động):* Cơ hoành co (vòm hoành hạ thấp xuống) + cơ liên sườn ngoài co (nâng xương sườn lên và ra ngoài) $\to$ thể tích lồng ngực tăng lên $\to$ áp suất màng phổi giảm sâu hơn áp suất khí quyển $\to$ phổi dãn rộng ra $\to$ không khí bị hút tràn vào phế nang.
   - *Thở ra bình thường (thụ động):* Cơ hoành và cơ liên sườn ngoài dãn $\to$ xương sườn hạ xuống, cơ hoành vồng lên $\to$ thể tích lồng ngực giảm $\to$ tính đàn hồi của mô phổi ép không khí trong phế nang ra ngoài.
   - *Hít vào và thở ra gắng sức:* Cần thêm sự tham gia của các cơ phụ trợ như cơ ức đòn chũm, cơ ngực, cơ chéo bụng co mạnh để ép sâu lồng ngực.
2. **Cơ chế trao đổi khí tại phế nang và tế bào:**
   - Hoàn toàn tuân theo nguyên lý **khuếch tán thụ động** từ nơi có phân áp cao sang nơi có phân áp thấp:
     + *Tại phổi:* Phân áp $O_2$ ở phế nang ($100\text{ mmHg}$) cao hơn trong máu mao mạch đến phổi ($40\text{ mmHg}$) $\to O_2$ khuếch tán vào máu. Phân áp $CO_2$ trong máu ($45\text{ mmHg}$) cao hơn ở phế nang ($40\text{ mmHg}$) $\to CO_2$ khuếch tán vào phế nang để thở ra.
     + *Tại tế bào mô:* Phân áp $O_2$ trong máu mao mạch ($100\text{ mmHg}$) cao hơn trong tế bào mô ($< 40\text{ mmHg}$) $\to O_2$ nhả khỏi Hb khuếch tán vào tế bào. $CO_2$ từ tế bào mô ($> 45\text{ mmHg}$) khuếch tán vào máu mao mạch.
3. **Cơ chế điều hòa hô hấp:**
   - Trung khu hô hấp nằm ở **hành não và cầu não**.
   - Tác nhân hóa học kích thích chủ yếu lên thụ thể hóa học là **nồng độ ion $H^+$ và phân áp $CO_2$ trong máu** (không phải phân áp $O_2$ trong điều kiện bình thường). Khi $pCO_2$ trong máu tăng $\to H^+$ tăng (pH máu giảm) $\to$ kích thích thụ thể $\to$ trung khu hô hấp phát xung lệnh tăng nhịp và độ sâu của nhịp thở để thải nhanh $CO_2$.

### 5. Lưu ý thi THPT
- **[Nhận biết]** Phổi phải có 3 thùy, phổi trái chỉ có 2 thùy (vì nhường chỗ cho khuyết tim).
- **[Nhận biết]** Trung khu điều hòa hô hấp tự động nằm ở hành não và cầu não (thuộc thân não).
- **[Thông hiểu]** Động lực hít vào bình thường là chủ động (tiêu tốn ATP do co cơ hoành và cơ liên sườn ngoài); thở ra bình thường là thụ động (không tiêu tốn năng lượng do dãn cơ).
- **[Thông hiểu]** Tác nhân trực tiếp kích thích trung khu hô hấp tăng nhịp thở là **nồng độ ion $H^+$ (pH giảm) và phân áp $CO_2$ tăng**, không phải do nồng độ $O_2$ giảm.
- **[Thông hiểu]** Bốn đặc điểm của bề mặt trao đổi khí hiệu quả: Diện tích bề mặt lớn; mỏng ẩm ướt; nhiều mao mạch có sắc tố hô hấp; có sự lưu thông khí tạo chênh lệch phân áp.
- **[Vận dụng]** Khí $CO$ (carbon monoxide) cực kì nguy hiểm vì có ái lực với Hemoglobin cao gấp hơn 200 lần so với $O_2$, chiếm vị trí liên kết làm $Hb$ mất khả năng vận chuyển $O_2$, dẫn đến ngạt tế bào nhanh chóng.
- **[Vận dụng]** Khi lên vùng núi cao, áp suất không khí giảm $\to$ phân áp $O_2$ giảm $\to$ cơ thể thích nghi bằng cách tăng nhịp thở, tăng nhịp tim và tủy xương kích thích sản sinh thêm hồng cầu (qua hormone erythropoietin do thận tiết ra).

### 6. Câu hỏi tự luyện
**Câu 1 [Nhận biết]:** Ở động vật có phổi, sự trao đổi khí giữa phế nang với máu diễn ra theo cơ chế nào sau đây?  
A. Vận chuyển chủ động qua các kênh protein xuyên màng tiêu tốn ATP.  
B. Khuếch tán thụ động từ nơi có phân áp cao sang nơi có phân áp thấp.  
C. Thẩm thấu theo độ chênh lệch nồng độ chất tan giữa dịch mô và máu.  
D. Thực bào và ẩm bào qua màng tế bào nội mô mao mạch.  

**Câu 2 [Thông hiểu]:** Khi một người nín thở trong khoảng 45 giây, diễn biến sinh lý nào sau đây trong cơ thể người đó là ĐÚNG?  
A. Nồng độ $O_2$ trong máu tăng, kích thích thụ thể xoang động mạch cảnh.  
B. Nồng độ $CO_2$ trong máu tăng, làm pH máu giảm và kích thích trung khu hô hấp ở hành não.  
C. Cơ hoành bị tê liệt tạm thời khiến thể tích lồng ngực không thể thay đổi.  
D. Nút xoang nhĩ ngừng phát xung khiến nhịp tim giảm xuống gần bằng 0.  

--- Đáp án ---  
**Câu 1:** **B**. Giải thích: Quá trình trao đổi khí $O_2$ và $CO_2$ ở phế nang hoàn toàn tuân theo định luật khuếch tán thụ động dựa trên sự chênh lệch phân áp khí, không tiêu tốn ATP.  
**Câu 2:** **B**. Giải thích: Nín thở làm $CO_2$ tích tụ trong máu, tạo thành $H_2CO_3 \rightleftharpoons H^+ + HCO_3^-$, làm tăng $H^+$ (giảm pH máu), tác động mạnh mẽ lên thụ thể hóa học ở xoang cảnh và thụ thể trung ương ở hành não, kích thích phản xạ thở gấp không thể kìm nén.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "lungs",
  "flows": [
    {
      "name": "Luồng khí hít vào",
      "color": "#1E63D6",
      "path": ["khi_quan", "phe_quan", "phoi_phai", "phoi_trai"],
      "labels": ["Khí quản", "Cây phế quản", "Phế nang (Nhận O2)", "Phế nang (Thải CO2)"]
    },
    {
      "name": "Mao mạch trao đổi khí",
      "color": "#E53935",
      "path": ["dong_mach_phoi", "phoi_trai", "phoi_phai", "tinh_mach_phoi"],
      "labels": ["Máu nghèo O2 đến", "", "Trao đổi O2/CO2 tại phế nang", "Máu giàu O2 về tim"]
    }
  ],
  "highlight": ["khi_quan", "phe_quan", "phoi_trai", "phoi_phai", "co_hoanh"]
}
```

---

## CHUYÊN ĐỀ 4: THẬN & HỆ TIẾT NIỆU (KIDNEYS & URINARY SYSTEM)

### 1. Tổng quan
Thận là cơ quan bài tiết quan trọng nhất của hệ tiết niệu, gồm hai quả thận hình hạt đậu nằm ở khoang bụng sau phúc mạc hai bên cột sống thắt lưng. Thận đảm nhiệm chức năng lọc máu đào thải các sản phẩm cặn bã của quá trình chuyển hóa (như urê, axit uric, creatinine) và các độc tố ra ngoài dưới dạng nước tiểu, đồng thời đóng vai trò then chốt trong việc cân bằng nội môi (duy trì thể tích máu, áp suất thẩm thấu, huyết áp và nồng độ các ion điện giải).

### 2. Cấu tạo liên quan đến chức năng
- **Đơn vị cấu tạo chức năng Nephron (mỗi thận có ~1 triệu nephron):** Đảm bảo quá trình lọc máu diễn ra phân tán trên quy mô vi thể khổng lồ, tổng diện tích lọc cực lớn.
- **Cầu thận (bọc Bowman ôm búi mao mạch Malpighi):** Búi mao mạch có áp lực máu rất cao do đường kính tiểu động mạch đến lớn hơn tiểu động mạch đi; màng lọc cầu thận có các lỗ lọc kích thước $\sim 7\text{–}8\text{ nm}$ với lớp màng đáy tích điện âm → cho nước, glucose, ion, axit amin và urê đi qua tự do, nhưng ngăn chặn hoàn toàn hồng cầu và các protein huyết tương lớn.
- **Ống lượn gần:** Biểu mô có bờ bàn chải gồm hàng triệu vi nhung mao dày đặc và chứa rất nhiều ty thể → thực hiện tái hấp thu tích cực 100% glucose, axit amin và khoảng 65–70% nước và ion $Na^+, Cl^-$.
- **Quai Henle (nhánh xuống mỏng, nhánh lên dày):** Chạy sâu vào tủy thận tạo hệ thống nhân nồng độ ngược dòng → nhánh xuống thấm nước nhưng không thấm muối, nhánh lên bơm tích cực $Na^+, Cl^-$ nhưng không thấm nước → tạo môi trường tủy thận ưu trương cao, giúp cô đặc nước tiểu.
- **Ống lượn xa và ống góp:** Nằm dưới sự kiểm soát của các hormone (ADH, Aldosterone) → tinh chỉnh lượng nước và ion tái hấp thu lần cuối trước khi đổ vào bể thận.

### 3. Đường đi (có mũi tên)

**Đường lưu chuyển của dịch lọc tạo nước tiểu trong Nephron:**
Máu trong tiểu động mạch đến (áp lực thủy tĩnh cao $\sim 60\text{ mmHg}$)
→ Búi mao mạch cầu thận (quá trình lọc huyết tương)
→ Nang Bowman (thu nhận nước tiểu đầu: $\sim 170\text{–}180\text{ lít/ngày}$)
→ Ống lượn gần (tái hấp thu 100% glucose, 65% $Na^+, H_2O$)
→ Quai Henle (nhánh xuống mất $H_2O$, nhánh lên bơm $Na^+/Cl^-$)
→ Ống lượn xa (tái hấp thu có chọn lọc dưới tác dụng của Aldosterone, bài tiết $K^+, H^+$)
→ Ống góp (tái hấp thu nước dưới tác dụng của ADH)
→ Bể thận (thu nhận nước tiểu chính thức: chỉ khoảng $1,5\text{–}2\text{ lít/ngày}$)

*Tóm tắt đường dịch lọc:* Xuất phát từ **nang Bowman** và kết thúc tại **bể thận**.

**Đường bài xuất nước tiểu ra ngoài:**
Bể thận
→ Niệu quản (nhu động cơ trơn đẩy nước tiểu từng đợt)
→ Bàng quang (túi cơ trơn lưu trữ $400\text{–}600\text{ mL}$)
→ Niệu đạo (có cơ thắt trơn trong và cơ thắt vân ngoài)
→ Môi trường ngoài

### 4. Cơ chế hoạt động chi tiết
Quá trình tạo nước tiểu ở thận gồm 3 giai đoạn kế tiếp nhau:

1. **Giai đoạn lọc máu ở cầu thận (quá trình thụ động):**
   - Động lực lọc là **áp suất lọc hữu hiệu** ($P_L$):
     $$P_L = P_H - (P_K + P_B)$$
     Trong đó:
     - $P_H$: Áp suất thủy tĩnh trong mao mạch cầu thận ($\sim 60\text{ mmHg}$, đẩy nước và chất tan ra ngoài).
     - $P_K$: Áp suất keo của protein trong huyết tương ($\sim 32\text{ mmHg}$, giữ nước lại).
     - $P_B$: Áp suất thủy tĩnh của dịch trong nang Bowman ($\sim 18\text{ mmHg}$, chống lại dòng lọc).
     $\Rightarrow P_L = 60 - (32 + 18) = 10\text{ mmHg} > 0 \to$ dịch lọc liên tục tràn vào nang Bowman.
   - Thành phần nước tiểu đầu: Giống dịch huyết tương nhưng **không có tế bào máu và không có protein có khối lượng phân tử lớn**.

2. **Giai đoạn tái hấp thu (chủ động và thụ động):**
   - Diễn ra dọc ống lượn gần, quai Henle, ống lượn xa và ống góp.
   - Khoảng 99% lượng nước và các chất có ích (100% glucose, 100% axit amin, phần lớn ion $Na^+, HCO_3^-, Cl^-$) được tái hấp thu trở lại mạng lưới mao mạch quanh ống thận để đưa về tĩnh mạch thận.

3. **Giai đoạn bài tiết tiếp:**
   - Các tế bào thành ống thận chủ động bài tiết các chất cặn bã, ion dư thừa ($H^+, K^+, NH_4^+$), thuốc và độc tố từ máu vào lòng ống thận.
   - Nước tiểu chính thức chỉ còn lại khoảng 1% thể tích dịch lọc ($\sim 1,5\text{ L/ngày}$), đậm đặc urê, axit uric và muối khoáng dư thừa.

4. **Cơ chế điều hòa nội môi của thận:**
   - **Khi cơ thể thiếu nước (áp suất thẩm thấu máu tăng):** Vùng dưới đồi kích thích thùy sau tuyến yên tiết **ADH (hormone chống bài niệu)** $\to$ ADH làm tăng tính thấm của ống lượn xa và ống góp với nước $\to$ tăng tái hấp thu nước về máu $\to$ lượng nước tiểu giảm và đậm đặc, áp suất thẩm thấu máu trở về bình thường.
   - **Khi huyết áp hoặc lượng $Na^+$ máu giảm:** Phức hợp cận cầu thận tiết enzyme **Renin** kích hoạt hệ thống Renin - Angiotensin - Aldosterone (RAAS) $\to$ vỏ tuyến thượng thận tiết **Aldosterone** $\to$ tăng tái hấp thu $Na^+$ và nước ở ống lượn xa $\to$ thể tích máu tăng và huyết áp hồi phục.

### 5. Lưu ý thi THPT
- **[Nhận biết]** Đơn vị chức năng của thận là **Nephron** (gồm cầu thận, nang Bowman và hệ thống ống thận).
- **[Nhận biết]** Nước tiểu đầu được tạo ra ở cầu thận có thể tích $\sim 170\text{–}180\text{ lít/ngày}$, nhưng nước tiểu chính thức chỉ khoảng $\sim 1,5\text{ lít/ngày}$ (do 99% lượng nước đã được tái hấp thu).
- **[Thông hiểu]** Nước tiểu đầu khác huyết tương ở chỗ: Nước tiểu đầu không chứa tế bào máu và hầu như không chứa protein huyết tương kích thước lớn.
- **[Thông hiểu]** Phân biệt vai trò của 2 hormone điều hòa chức năng thận:
  - **ADH (tuyến yên sau):** Tăng tái hấp thu $H_2O$ khi áp suất thẩm thấu tăng (khát nước).
  - **Aldosterone (vỏ thượng thận):** Tăng tái hấp thu $Na^+$ (kèm $H_2O$) khi huyết áp giảm hoặc thiếu $Na^+$.
- **[Vận dụng]** Bệnh đái tháo đường (tiểu đường): Nồng độ glucose trong máu vượt quá "ngưỡng thận" ($\sim 180\text{ mg/dL}$) $\to$ ống thận không thể tái hấp thu hết glucose $\to$ glucose xuất hiện trong nước tiểu, kéo theo nước do áp suất thẩm thấu (gây triệu chứng tiểu nhiều, khát nhiều).
- **[Vận dụng]** Rượu bia ức chế tuyến yên tiết ADH $\to$ ống lượn xa và ống góp giảm tái hấp thu nước $\to$ người uống bia đi tiểu nhiều lần dẫn đến mất nước và háo nước sau đó.

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Nếu một người bị tổn thương cầu thận làm tăng kích thước các lỗ màng lọc, xét nghiệm nước tiểu của người này nhiều khả năng sẽ xuất hiện bất thường nào sau đây?  
A. Nồng độ urê và axit uric giảm về mức 0.  
B. Xuất hiện protein huyết tương (albumin) và hồng cầu trong nước tiểu.  
C. Nước tiểu hoàn toàn không còn ion $Na^+$ và $Cl^-$.  
D. Lượng nước tiểu giảm nghiêm trọng dưới $0,2\text{ lít/ngày}$.  

**Câu 2 [Vận dụng]:** Sau khi ăn một bữa ăn có nhiều muối, cơ chế cân bằng nội môi nào sau đây sẽ được cơ thể kích hoạt?  
A. Tăng tiết hormone Aldosterone để tăng đào thải nước qua thận.  
B. Giảm tiết hormone ADH làm tăng lượng nước tiểu thải ra ngoài.  
C. Tăng tiết hormone ADH để tăng tái hấp thu nước ở ống góp và kích thích trung khu cảm giác khát.  
D. Giảm nhịp tim và dãn toàn bộ hệ mạch ngoại vi để hạ huyết áp ngay lập tức.  

--- Đáp án ---  
**Câu 1:** **B**. Giải thích: Lỗ lọc cầu thận bình thường chỉ rộng $\sim 7\text{–}8\text{ nm}$ ngăn protein và hồng cầu. Khi màng lọc bị viêm loét hoặc rách lỗ, hồng cầu và albumin sẽ lọt vào nang Bowman và không thể tái hấp thu, xuất hiện trong nước tiểu (tiểu ra máu, tiểu đạm).  
**Câu 2:** **C**. Giải thích: Ăn nhiều muối làm tăng áp suất thẩm thấu dịch ngoại bào và máu, kích thích vùng dưới đồi truyền tín hiệu lên thùy sau tuyến yên giải phóng ADH để tăng tái hấp thu nước ở ống góp, đồng thời gây cảm giác khát nước để bổ sung nước vào cơ thể.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "kidneys",
  "flows": [
    {
      "name": "Dòng cấp máu lọc",
      "color": "#E53935",
      "path": ["dong_mach_chu", "mach_than", "than_phai", "than_trai"],
      "labels": ["Máu từ ĐM chủ bụng", "Động mạch thận", "Lọc tại cầu thận", "Lọc tại cầu thận"]
    },
    {
      "name": "Dòng bài xuất nước tiểu",
      "color": "#8E44AD",
      "path": ["than_trai", "than_phai", "nieu_quan", "bang_quang", "nieu_dao"],
      "labels": ["Bể thận trái", "Bể thận phải", "Niệu quản dẫn nước tiểu", "Bàng quang lưu trữ", "Niệu đạo bài xuất"]
    }
  ],
  "highlight": ["than_trai", "than_phai", "mach_than", "nieu_quan", "bang_quang"]
}
```

---

## CHUYÊN ĐỀ 5: DẠ DÀY (STOMACH) – HỆ TIÊU HÓA

### 1. Tổng quan
Dạ dày là đoạn phình to nhất của ống tiêu hóa, hình chữ J, nằm ở vùng hạ sườn trái và thượng vị ổ bụng ngay dưới cơ hoành. Chức năng chính của dạ dày là chứa đựng thức ăn, thực hiện tiêu hóa cơ học mạnh mẽ (nhào trộn, nghiền nát) kết hợp tiêu hóa hóa học sơ bộ (phân giải protein thành chuỗi polypeptide ngắn) và chuyển thức ăn thành dạng dưỡng trấp bán lỏng trước khi đưa xuống tá tràng.

### 2. Cấu tạo liên quan đến chức năng
- **Thành cơ dày gồm 3 lớp cơ trơn (cơ dọc, cơ vòng và cơ chéo đặc thù):** Sự có mặt của lớp cơ chéo ở trong cùng giúp dạ dày co bóp và vặn xoắn đa chiều → nhào trộn thức ăn cực kì triệt để với dịch vị và nghiền nát cơ học các mảnh thức ăn lớn.
- **Niêm mạc gấp nếp nhăn nheo và có độ dãn nở lớn:** Thể tích dạ dày khi rỗng chỉ $\sim 50\text{ mL}$ nhưng có thể dãn chứa tới $1,5\text{–}2,5\text{ lít}$ thức ăn sau bữa ăn mà áp lực trong lòng dạ dày không tăng quá mức.
- **Các tuyến vị giàu tế bào chuyên biệt:**
  - *Tế bào chính (Chief cells):* Tiết ra tiền enzyme **pepsinogen** bất hoạt.
  - *Tế bào viền (Parietal cells):* Chứa nhiều bơm proton $H^+/K^+$-ATPase tiết ra **axit $HCl$** đậm đặc (pH dịch vị từ $1,5\text{–}2,5$) và yếu tố nội sinh (giúp hấp thu vitamin B12).
  - *Tế bào nhầy (Mucous cells):* Tiết ra lớp chất nhầy giàu $HCO_3^-$ kiềm tính dày $\sim 1\text{–}1,5\text{ mm}$ phủ kín niêm mạc → tạo hàng rào bảo vệ vững chắc chống lại sự tự tiêu hóa của pepsin và ăn mòn của axit $HCl$.
- **Cơ thắt tâm vị và cơ thắt môn vị:**
  - *Cơ thắt tâm vị (đầu vào):* Đóng kín ngăn dịch vị trào ngược lên thực quản.
  - *Cơ thắt môn vị (đầu ra):* Đóng mở nhịp nhàng điều hòa từng đợt dưỡng trấp nhỏ từ dạ dày xuống tá tràng theo cơ chế phản xạ thần kinh - thể dịch.

### 3. Đường đi (có mũi tên)

**Đường di chuyển và biến đổi của thức ăn qua dạ dày:**
Viên thức ăn từ thực quản (được làm ẩm bởi nước bọt)
→ Qua cơ thắt tâm vị mở ra
→ Đáy vị và thân vị (chứa đựng, ngấm dần dịch vị)
→ Hang vị và môn vị (co bóp nghiền nát, nhào trộn mạnh mẽ với axit $HCl$ và enzyme Pepsin)
→ Dưỡng trấp (hỗn hợp bán lỏng, protein bị cắt thành chuỗi peptide ngắn)
→ Từng đợt qua cơ thắt môn vị
→ Tá tràng (đoạn đầu ruột non)

*Tóm tắt đường đi:* Xuất phát từ **tâm vị** và kết thúc tại **môn vị**.

### 4. Cơ chế hoạt động chi tiết
1. **Tiêu hóa cơ học:**
   - Khi thức ăn vào, các sóng co thắt nhu động xuất phát từ thân vị lan dần xuống môn vị với tần số $\sim 3\text{ lần/phút}$.
   - Khi sóng nhu động đến môn vị, cơ thắt môn vị đóng chặt khiến khối thức ăn bị dội ngược trở lại thân vị $\to$ tạo lực cắt xé và va đập cực mạnh làm nghiền nát thức ăn thành các hạt mịn đường kính $< 2\text{ mm}$.
2. **Tiêu hóa hóa học (vai trò của $HCl$ và Pepsin):**
   - Axit clohiđric ($HCl$) do tế bào viền tiết ra tạo môi trường $pH \approx 1,5\text{–}2$:
     $$\text{Pepsinogen (tiền enzyme bất hoạt)} \xrightarrow{HCl / \text{Pepsin}} \text{Pepsin (enzyme hoạt tính)}$$
   - $HCl$ làm biến tính cấu trúc bậc 3 và bậc 4 của protein trong thức ăn $\to$ bộc lộ các liên kết peptide.
   - Enzyme **Pepsin** thủy phân các liên kết peptide nội sinh giữa các axit amin thơm $\to$ cắt protein phức tạp thành các chuỗi **polypeptide ngắn (peptone, proteose)**.
   - $HCl$ tiêu diệt hầu hết các vi khuẩn có hại theo thức ăn vào dạ dày và đóng mở cơ thắt môn vị.
3. **Cơ chế đóng mở môn vị:**
   - Dưỡng trấp mang tính axit chạm vào niêm mạc tá tràng $\to$ kích thích phản xạ đóng chặt cơ thắt môn vị.
   - Khi dịch tụy và dịch mật mang tính kiềm ($NaHCO_3$) trung hòa axit tại tá tràng $\to$ môn vị mở ra cho đợt dưỡng trấp tiếp theo đi xuống.
4. **Hấp thu ở dạ dày:**
   - Dạ dày **hầu như không hấp thu chất dinh dưỡng** (vì niêm mạc có lớp nhầy dày và diện tích hấp thu nhỏ). Dạ dày chỉ hấp thu một lượng rất nhỏ nước, muối khoáng, đường đơn và các chất tan trong lipid như **rượu (ethanol) và thuốc aspirin** (giải thích tại sao uống rượu có tác dụng say rất nhanh).

### 5. Lưu ý thi THPT
- **[Nhận biết]** Ở dạ dày, sự tiêu hóa hóa học chỉ diễn ra chủ yếu với **Protein** (nhờ enzyme Pepsin và môi trường axit $HCl$). Tinh bột chín chỉ được tiêu hóa dở dang ở giai đoạn đầu trong lòng viên thức ăn khi amylase của nước bọt chưa bị $HCl$ vô hoạt.
- **[Nhận biết]** Tế bào viền tiết $HCl$, tế bào chính tiết Pepsinogen, tế bào nhầy tiết chất nhầy kiềm tính bảo vệ niêm mạc.
- **[Thông hiểu]** Vì sao dạ dày không bị chính dịch vị (pepsin và axit $HCl$) phá hủy? Nhờ hàng rào chất nhầy giàu ion $HCO_3^-$ bảo vệ niêm mạc, sự liên kết chặt chẽ giữa các tế bào biểu mô và khả năng tái sinh tế bào niêm mạc cực nhanh (thay mới mỗi 3–5 ngày).
- **[Thông hiểu]** Vi khuẩn *Helicobacter pylori (HP)* có thể sống được trong môi trường axit cực mạnh của dạ dày vì nó tiết ra enzyme urease biến đổi urê thành amoniac ($NH_3$) có tính kiềm bao quanh trung hòa axit.
- **[Vận dụng]** Căng thẳng kéo dài (stress) kích thích dây thần kinh phế vị (dây X) tăng tiết $HCl$ đồng thời làm co mạch nuôi dưỡng niêm mạc $\to$ suy giảm lớp màng nhầy bảo vệ $\to$ gây viêm loét dạ dày - tá tràng.
- **[Vận dụng]** Thuốc ức chế bơm proton (Omeprazole, Nexium) chữa đau dạ dày bằng cách gắn vào bơm $H^+/K^+$-ATPase ở màng tế bào viền, ngăn cản tiết ion $H^+ \to$ làm tăng pH dịch vị và giúp vết loét mau lành.

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Trong dịch vị của người, axit clohiđric ($HCl$) KHÔNG có vai trò nào sau đây?  
A. Hoạt hóa tiền enzyme pepsinogen thành enzyme pepsin hoạt động.  
B. Tiêu diệt phần lớn các vi sinh vật gây hại đi theo thức ăn vào dạ dày.  
C. Trực tiếp thủy phân hoàn toàn các chuỗi protein thành các axit amin tự do.  
D. Tạo môi trường pH tối ưu để enzyme pepsin phân giải protein.  

**Câu 2 [Vận dụng]:** Hiện tượng trào ngược dạ dày - thực quản thường gây cảm giác nóng rát vùng sau xương ức ("ợ nóng"). Nguyên nhân chính của hiện tượng này là do:  
A. Cơ thắt tâm vị đóng không kín, khiến axit $HCl$ và pepsin trào lên làm tổn thương niêm mạc thực quản không có lớp nhầy bảo vệ dày.  
B. Cơ thắt môn vị mở quá rộng khiến dịch mật từ tá tràng trào ngược lên tận thực quản.  
C. Dạ dày tiết quá nhiều chất nhầy làm tăng áp suất đẩy ngược thức ăn lên họng.  
D. Quá trình tiêu hóa tinh bột ở thực quản diễn ra quá mạnh sinh nhiều khí $CO_2$.  

--- Đáp án ---  
**Câu 1:** **C**. Giải thích: $HCl$ chỉ biến tính protein và hoạt hóa pepsinogen; enzyme pepsin cắt protein thành chuỗi polypeptide ngắn (peptone). Việc thủy phân hoàn toàn polypeptide thành các axit amin tự do là do các enzyme của dịch tụy và dịch ruột thực hiện ở ruột non.  
**Câu 2:** **A**. Giải thích: Cơ thắt tâm vị bị suy yếu khiến axit $HCl$ trào ngược lên thực quản. Niêm mạc thực quản không có lớp nhầy bảo vệ dày như dạ dày nên bị axit ăn mòn gây bỏng rát.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "stomach",
  "flows": [
    {
      "name": "Dòng thức ăn vào dạ dày",
      "color": "#43A047",
      "path": ["thuc_quan", "da_day", "ta_trang"],
      "labels": ["Thực quản (Nuốt thức ăn)", "Dạ dày (Nghiền nát, Pepsin/HCl)", "Tá tràng (Dưỡng trấp axit)"]
    }
  ],
  "highlight": ["thuc_quan", "da_day", "ta_trang"]
}
```

---

## CHUYÊN ĐỀ 6: RUỘT (INTESTINES) – RUỘT NON & ĐẠI TRÀNG

### 1. Tổng quan
Ruột là phần dài nhất của ống tiêu hóa, chia làm ruột non (gồm tá tràng, hỗng tràng, hồi tràng dài $5\text{–}6\text{ m}$) và ruột già / đại tràng (dài $\sim 1,5\text{ m}$). Ruột non là **trung tâm tiêu hóa hóa học triệt để và hấp thu hơn 90% các chất dinh dưỡng** của cơ thể; ruột già có nhiệm vụ tái hấp thu nước, một số khoáng chất, tổng hợp một số vitamin nhờ hệ vi khuẩn đường ruột và cô đặc cặn bã để tạo khuôn phân bài xuất ra ngoài.

### 2. Cấu tạo liên quan đến chức năng
- **Diện tích bề mặt ruột non khổng lồ ($\sim 250\text{–}300\text{ m}^2$):** Bề mặt ruột non có 3 cấp gấp nếp đặc thù:
  1. *Các nếp gấp niêm mạc (van Kerckring):* Tăng diện tích lên 3 lần.
  2. *Hàng triệu lông ruột (nhung mao):* Tăng diện tích lên 10 lần.
  3. *Hàng tỷ vi nhung mao (microvilli) ở bờ bàn chải tế bào biểu mô:* Tăng diện tích lên thêm 20 lần.
  $\Rightarrow$ Tăng tổng diện tích tiếp xúc và hấp thu lên tới $\sim 600\text{ lần}$ so với một ống trơn nhẵn cùng kích thước.
- **Hệ thống mạch máu và mạch bạch huyết dày đặc bên trong từng nhung mao:** Mỗi nhung mao đều có một quai mao mạch máu bao quanh một ống bạch huyết trung tâm (mao mạch dưỡng trấp) → tiếp nhận nhanh chóng các chất dinh dưỡng vừa thấm qua biểu mô đưa vào hệ tuần hoàn.
- **Hệ thống cơ trơn đại tràng có các dải cơ dọc (taenia coli) và các ngấn phồng (haustra):** Nhu động chậm rãi giật cục giúp lưu giữ khối phân lâu từ 12–24 giờ để hút kiệt nước.
- **Ruột thừa (đính ở đáy manh tràng ruột già):** Chứa nhiều nang lympho có vai trò miễn dịch và là nơi "trú ẩn", nuôi dưỡng dự phòng hệ vi khuẩn có lợi đường ruột.

### 3. Đường đi (có mũi tên)

**Đường di chuyển và hấp thu dinh dưỡng ở ruột non:**
Dưỡng trấp từ dạ dày xuống tá tràng (được trung hòa bởi $NaHCO_3$ từ dịch tụy và dịch mật)
→ Hỗn hợp enzyme tiêu hóa (tụy + ruột) phân giải triệt để:
  * Tinh bột $\to$ Glucose
  * Protein $\to$ Axit amin
  * Lipid $\to$ Axit béo và Glycerol
→ Hỗng tràng và hồi tràng (hấp thu qua vi nhung mao):
  * **Con đường máu:** Glucose, axit amin, nước, ion khoáng, vitamin tan trong nước $\to$ Mao mạch nhung mao $\to$ Tĩnh mạch cửa gan $\to$ **Gan (điều hòa, khử độc)** $\to$ Tĩnh mạch chủ dưới $\to$ Tim
  * **Con đường bạch huyết:** Axit béo, glycerol, vitamin tan trong dầu (A, D, E, K) $\to$ Ống bạch huyết trung tâm $\to$ Ống ngực $\to$ Tĩnh mạch dưới đòn trái $\to$ Tim

**Đường đi của chất cặn bã qua đại tràng:**
Hồi tràng (qua van hồi manh tràng)
→ Manh tràng (có ruột thừa đính ở đáy)
→ Kết tràng lên $\to$ Kết tràng ngang $\to$ Kết tràng xuống $\to$ Kết tràng sigma
→ Hút lại nước và muối khoáng, phân đặc dần
→ Trực tràng (tích trữ phân)
→ Ống hậu môn $\to$ Ra ngoài

### 4. Cơ chế hoạt động chi tiết
1. **Tiêu hóa hóa học triệt để ở ruột non:**
   - Dịch tụy chứa đầy đủ các enzyme mạnh:
     + *Amylase:* Tinh bột $\to$ Maltose $\xrightarrow{\text{Maltase}} 2\text{ Glucose}$.
     + *Trypsin, Chymotrypsin, Carboxypeptidase:* Cắt nhỏ chuỗi polypeptide $\xrightarrow{\text{Peptidase ruột}} \text{Axit amin}$.
     + *Lipase tụy:* Thủy phân lipid (sau khi được muối mật nhũ tương hóa) $\to$ Axit béo + Glycerol.
     + *Nuclease:* Phân giải ADN, ARN $\to$ Nucleotide.
   - Dịch mật từ gan: Không chứa enzyme tiêu hóa, nhưng chứa **muối mật** giúp nhũ tương hóa lipid (chia nhỏ giọt mỡ lớn thành các hạt micelle siêu nhỏ) làm tăng diện tích tiếp xúc cho enzyme lipase hoạt động gấp hàng ngàn lần.
2. **Cơ chế hấp thu dưỡng chất qua biểu mô nhung mao:**
   - *Glucose và Axit amin:* Vận chuyển tích cực thứ phát đồng vận chuyển với ion $Na^+$ (nhờ bơm $Na^+/K^+$-ATPase ở màng đáy) từ lòng ruột vào tế bào biểu mô, sau đó khuếch tán thuận hóa vào mao mạch máu.
   - *Axit béo và Glycerol:* Khuếch tán qua màng lipid kép của tế bào biểu mô $\to$ được tái tổng hợp thành triglyceride trong lưới nội chất $\to$ đóng gói cùng protein thành các hạt **Chylomicron** $\to$ xuất bào đi vào mạch bạch huyết trung tâm.
3. **Hoạt động của vi sinh vật ở đại tràng:**
   - Hàng nghìn tỷ vi khuẩn sống cộng sinh (như *E. coli, Bifidobacterium*):
     + Lên men phần chất xơ (cellulose) không tiêu hóa được, sinh ra một số axit béo chuỗi ngắn nuôi dưỡng niêm mạc đại tràng.
     + Tổng hợp một số vitamin quan trọng cho cơ thể, đặc biệt là **Vitamin K** (cần thiết cho quá trình đông máu) và một số vitamin nhóm B.

### 5. Lưu ý thi THPT
- **[Nhận biết]** Ruột non là cơ quan quan trọng nhất diễn ra quá trình tiêu hóa hóa học triệt để và hấp thu dinh dưỡng.
- **[Nhận biết]** Dịch mật KHÔNG chứa enzyme tiêu hóa, vai trò cốt lõi của dịch mật là **nhũ tương hóa lipid** nhờ các muối mật.
- **[Thông hiểu]** Phân biệt 2 con đường vận chuyển chất dinh dưỡng sau khi hấp thu:
  - *Đường máu:* Đi qua **Gan** trước khi về tim (để gan kiểm soát nồng độ và khử độc).
  - *Đường bạch huyết:* Đổ trực tiếp vào tĩnh mạch dưới đòn rồi về tim, **không qua gan**.
- **[Thông hiểu]** Vì sao người bị bệnh tắc ống dẫn mật lại đi ngoài phân sống (phân mỡ) và cơ thể thiếu hụt các vitamin A, D, E, K? Vì thiếu muối mật làm lipid không được nhũ tương hóa $\to$ lipase không tiêu hóa được mỡ $\to$ các vitamin tan trong chất béo không được hấp thu.
- **[Vận dụng]** Kháng sinh phổ rộng đường uống dài ngày có thể tiêu diệt hệ vi khuẩn đường ruột có lợi $\to$ giảm tổng hợp vitamin K dẫn đến nguy cơ rối loạn đông máu, đồng thời gây loạn khuẩn tiêu chảy.

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Các chất dinh dưỡng sau khi được hấp thu qua niêm mạc ruột non được vận chuyển theo con đường máu và con đường bạch huyết. Điểm khác biệt CƠ BẢN giữa hai con đường này là gì?  
A. Con đường máu chỉ vận chuyển chất vô cơ, con đường bạch huyết chỉ vận chuyển chất hữu cơ.  
B. Các chất đi theo con đường máu đều phải đi qua gan để điều hòa và khử độc trước khi về tim, còn con đường bạch huyết thì không qua gan.  
C. Con đường máu không tiêu tốn ATP, con đường bạch huyết tiêu tốn nhiều ATP.  
D. Con đường máu đưa chất về tâm nhĩ trái, con đường bạch huyết đưa chất về tâm thất phải.  

**Câu 2 [Vận dụng]:** Hiện tượng tiêu chảy kéo dài ở trẻ nhỏ do nhiễm vi khuẩn tả thường làm tăng bài tiết nước và chất điện giải vào lòng ruột với tốc độ rất lớn. Hậu quả nguy hiểm nhất cần được can thiệp cấp cứu ngay là gì?  
A. Dạ dày bị rỗng dẫn đến thủng niêm mạc do axit $HCl$ tăng cao.  
B. Giảm thể tích tuần hoàn máu nghiêm trọng, dẫn đến tụt huyết áp và sốc mất nước.  
C. Gan bị ngộ độc do không nhận đủ lượng glucose từ ruột chuyển về.  
D. Quá trình hô hấp bị tê liệt do thiếu hụt lượng vitamin K tổng hợp từ ruột.  

--- Đáp án ---  
**Câu 1:** **B**. Giải thích: Tất cả các chất dinh dưỡng đi theo con đường mao mạch máu đều tập trung về tĩnh mạch cửa gan để gan xử lý, điều hòa và giải độc trước khi vào vòng tuần hoàn chung. Con đường bạch huyết đổ thẳng vào tĩnh mạch dưới đòn và về tim mà không qua gan.  
**Câu 2:** **B**. Giải thích: Mất nước và điện giải ồ ạt qua phân làm sụt giảm nhanh chóng thể tích huyết tương trong lòng mạch $\to$ huyết áp tụt dốc nghiêm trọng $\to$ trụy tim mạch và sốc giảm thể tích có thể gây tử vong nhanh chóng nếu không được bù nước và điện giải (Oresol) kịp thời.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "intestines",
  "flows": [
    {
      "name": "Hấp thu dưỡng chất đường máu qua Gan",
      "color": "#E53935",
      "path": ["ruot_non", "gan", "tinh_mach_chu"],
      "labels": ["Mao mạch ruột non", "Tĩnh mạch cửa qua Gan", "Về tim"]
    },
    {
      "name": "Dòng cặn bã qua đại tràng",
      "color": "#43A047",
      "path": ["ruot_non", "ruot_thua", "dai_trang", "truc_trang"],
      "labels": ["Hồi tràng", "Manh tràng / Ruột thừa", "Đại tràng (Hút nước)", "Trực tràng (Tạo phân)"]
    }
  ],
  "highlight": ["ruot_non", "dai_trang", "ruot_thua", "truc_trang", "ta_trang"]
}
```

---

## CHUYÊN ĐỀ 7: GAN & TÚI MẬT (LIVER & GALLBLADDER)

### 1. Tổng quan
Gan là tạng đặc lớn nhất trong cơ thể người (nặng khoảng $1,4\text{–}1,6\text{ kg}$), nằm ở vùng hạ sườn phải ngay dưới cơ hoành; túi mật là túi nhỏ hình quả lê nằm ở mặt dưới của gan. Gan được ví như "nhà máy hóa chất trung tâm" của cơ thể, đảm nhiệm hơn 500 chức năng sinh học phức tạp bao gồm chuyển hóa đường, đạm, mỡ, khử độc các chất ngoại lai và nội sinh, dự trữ vitamin và sắt, sản xuất dịch mật cùng các protein huyết tương (albumin, fibrinogen).

### 2. Cấu tạo liên quan đến chức năng
- **Hệ thống mạch máu kép độc nhất vô nhị:**
  - *Động mạch gan:* Cung cấp máu giàu $O_2$ từ động mạch chủ nuôi dưỡng tế bào gan ($\sim 25\%$ lưu lượng).
  - *Tĩnh mạch cửa gan:* Đưa toàn bộ dòng máu giàu dưỡng chất và độc chất hấp thu từ ruột, dạ dày, lách về gan ($\sim 75\%$ lưu lượng) → giúp gan giám sát và xử lý toàn bộ dưỡng chất trước khi hòa vào tuần hoàn chung.
- **Tiểu thùy gan (đơn vị chức năng lục lăng):** Gồm các bè tế bào gan (bè Remak) xếp hình nan hoa quanh tĩnh mạch trung tâm tiểu thùy, kẹp giữa là các xoang mạch máu (sinusoids) có lỗ lọc rộng và vận tốc máu chậm → tối đa hóa sự tiếp xúc giữa máu và tế bào gan.
- **Tế bào Kupffer (đại thực bào cố định trong xoang gan):** Thực bào hồng cầu già, vi khuẩn và kháng nguyên lọt vào từ đường ruột → bảo vệ cơ thể khỏi nhiễm trùng máu.
- **Túi mật có thành cơ trơn đàn hồi:** Lưu trữ và cô đặc dịch mật gấp 5–10 lần bằng cách tái hấp thu nước; co bóp đẩy mật qua ống mật chủ vào tá tràng dưới tác động của hormone Cholecystokinin (CCK) khi có mỡ trong thức ăn.

### 3. Đường đi (có mũi tên)

**Dòng tuần hoàn cửa gan và khử độc:**
Máu từ ruột non / dạ dày (chứa chất dinh dưỡng, cồn, độc tố)
→ Tĩnh mạch mạc treo tràng trên và tĩnh mạch lách
→ Tĩnh mạch cửa gan
→ Các xoang mao mạch tiểu thùy gan (tế bào gan hấp thu glucose, dự trữ glycogen, chuyển hóa chất, tế bào Kupffer diệt vi khuẩn)
→ Tĩnh mạch trung tâm tiểu thùy
→ Các tĩnh mạch gan (tĩnh mạch trên gan)
→ Tĩnh mạch chủ dưới $\to$ Tâm nhĩ phải

*Tóm tắt đường máu cửa:* Xuất phát từ **mao mạch các cơ quan tiêu hóa** và kết thúc tại **tĩnh mạch chủ dưới**.

**Dòng bài xuất dịch mật tiêu hóa:**
Tế bào gan sản xuất liên tục dịch mật
→ Các vi quản mật
→ Ống gan phải + Ống gan trái $\to$ Ống gan chung
→ Ống túi mật $\to$ Lưu trữ và cô đặc trong Túi mật
→ Khi ăn thức ăn có lipid $\to$ Túi mật co bóp $\to$ Ống mật chủ
→ Cơ vòng Oddi mở ra $\to$ Đổ vào Tá tràng (nhũ tương hóa lipid)

### 4. Cơ chế hoạt động chi tiết
1. **Chức năng điều hòa đường huyết ($Glucose$):**
   - *Khi đường huyết tăng cao sau bữa ăn:* Tế bào beta đảo tụy tiết **Insulin** $\to$ kích thích tế bào gan tăng thu nhận glucose và biến đổi thành **Glycogen** dự trữ ($\text{Glucose} \xrightarrow{\text{Insulin}} \text{Glycogen}$).
   - *Khi đường huyết hạ (xa bữa ăn, đói):* Tế bào alpha đảo tụy tiết **Glucagon** (cùng adrenaline) $\to$ kích thích tế bào gan phân giải glycogen thành glucose giải phóng vào máu ($\text{Glycogen} \xrightarrow{\text{Glucagon}} \text{Glucose}$), duy trì đường huyết ổn định ở mức $4,4\text{–}6,1\text{ mmol/L}$ ($80\text{–}110\text{ mg/dL}$).
2. **Chức năng khử độc (giải độc):**
   - *Chuyển hóa amoniac ($NH_3$):* $NH_3$ là chất cực độc sinh ra từ chuyển hóa protein; tế bào gan chuyển hóa $NH_3$ qua chu trình Ornithine thành **Urê** ít độc hơn gấp hàng ngàn lần, rồi đưa vào máu để thận bài xuất ra nước tiểu:
     $$2NH_3 + CO_2 \xrightarrow{\text{Chu trình Urê}} (NH_2)_2CO + H_2O$$
   - *Khử độc bằng phản ứng oxy hóa và liên hợp:* Gan dùng hệ enzyme Cytochrome P450 biến đổi các chất độc, thuốc, rượu (ethanol) thành các dẫn xuất tan trong nước để thải qua thận hoặc bài xuất theo dịch mật.
3. **Chức năng tổng hợp protein huyết tương và đông máu:**
   - Gan tổng hợp hầu hết **Albumin** (tạo áp suất keo giữ nước trong lòng mạch, chống phù).
   - Gan tổng hợp các yếu tố đông máu chủ chốt: **Fibrinogen (yếu tố I), Prothrombin (yếu tố II)** cùng các yếu tố VII, IX, X (dưới sự hỗ trợ của Vitamin K).
4. **Tạo và bài tiết mật:**
   - Dịch mật gồm muối mật (muối của axit cholic), sắc tố mật (Bilirubin từ sự thoái hóa hemoglobin hồng cầu già), cholesterol và nước. Mật không chứa enzyme nhưng bắt buộc phải có để tiêu hóa và hấp thu lipid.

### 5. Lưu ý thi THPT
- **[Nhận biết]** Gan nhận máu từ 2 nguồn: Động mạch gan (máu giàu $O_2$) và Tĩnh mạch cửa gan (máu giàu dinh dưỡng từ ruột).
- **[Nhận biết]** Gan là cơ quan duy nhất thực hiện chu trình tổng hợp Urê từ $NH_3$ độc hại để bảo vệ não bộ.
- **[Thông hiểu]** Vì sao xơ gan hoặc suy gan nặng dẫn đến hiện tượng phù nề toàn thân và cổ trướng (bụng to chứa đầy dịch)? Do gan suy giảm tổng hợp protein Albumin $\to$ áp suất keo của huyết tương giảm mạnh $\to$ nước thoát ra ngoài gian bào và khoang màng bụng.
- **[Thông hiểu]** Người bị suy gan thường có triệu chứng máu khó đông và chảy máu dưới da (xuất huyết) vì gan không tổng hợp đủ các yếu tố đông máu (Fibrinogen, Prothrombin).
- **[Vận dụng]** Rượu bia khi vào gan được enzyme ADH biến đổi thành Acetaldehyde (chất gây độc tế bào gan). Uống rượu kéo dài làm tế bào gan bị thoái hóa mỡ, viêm gan và biến đổi mô hạt xơ không phục hồi (xơ gan).

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Ở người bình thường, nồng độ glucose trong tĩnh mạch cửa gan và trong tĩnh mạch gan có sự chênh lệch như thế nào ngay sau khi ăn một bữa ăn giàu tinh bột?  
A. Nồng độ glucose trong tĩnh mạch gan cao hơn nhiều so với trong tĩnh mạch cửa gan.  
B. Nồng độ glucose trong tĩnh mạch cửa gan cao hơn nhiều so với trong tĩnh mạch gan.  
C. Nồng độ glucose ở hai mạch máu này luôn luôn bằng nhau tuyệt đối.  
D. Nồng độ glucose trong tĩnh mạch cửa gan giảm dần về 0 do dạ dày giữ lại.  

**Câu 2 [Vận dụng]:** Một bệnh nhân bị sỏi mật làm tắc hoàn toàn ống mật chủ dẫn đến dịch mật không thể đổ vào tá tràng. Bệnh nhân này sẽ có biểu hiện lâm sàng nào sau đây?  
A. Nước tiểu nhạt màu như nước lã và phân có màu đen sẫm.  
B. Vàng da, ngứa ngáy, phân có màu trắng bạc (phân sống như phân cò) và nước tiểu sẫm màu.  
C. Đường huyết tăng vọt không kiểm soát tương tự bệnh đái tháo đường typ 1.  
D. Huyết áp động mạch tụt giảm nghiêm trọng do mất máu qua đường ruột.  

--- Đáp án ---  
**Câu 1:** **B**. Giải thích: Sau bữa ăn giàu tinh bột, glucose hấp thu ồ ạt từ ruột vào tĩnh mạch cửa gan nên nồng độ tại đây rất cao. Tế bào gan giữ lại phần lớn glucose để tổng hợp thành glycogen dự trữ, do đó nồng độ glucose trong máu rời gan (qua tĩnh mạch gan) được điều hòa chỉ duy trì ở mức sinh lý ổn định.  
**Câu 2:** **B**. Giải thích: Mật bị ứ trệ tràn ngược vào máu $\to$ sắc tố mật (Bilirubin) lắng đọng ở da gây vàng da, bài tiết qua nước tiểu làm nước tiểu vàng sẫm. Ngược lại, sắc tố mật không xuống được ruột làm phân mất màu vàng nâu đặc trưng và chuyển sang màu trắng nhợt nhạt (phân như đất sét/phân cò).

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "liver",
  "flows": [
    {
      "name": "Hệ tuần hoàn cửa gan",
      "color": "#1E63D6",
      "path": ["ruot_non", "gan", "tinh_mach_chu"],
      "labels": ["Hấp thu chất dinh dưỡng", "Tiểu thùy gan (Khử độc, Glycogen)", "Tĩnh mạch chủ dưới về tim"]
    },
    {
      "name": "Dòng bài xuất mật tiêu hóa",
      "color": "#43A047",
      "path": ["gan", "tui_mat", "ta_trang"],
      "labels": ["Tạo dịch mật", "Túi mật cô đặc mật", "Tá tràng (Nhũ tương hóa mỡ)"]
    }
  ],
  "highlight": ["gan", "tui_mat", "ta_trang"]
}
```

---

## CHUYÊN ĐỀ 8: NÃO BỘ & HỆ THẦN KINH (BRAIN & NERVOUS SYSTEM)

### 1. Tổng quan
Não bộ là cơ quan chỉ huy tối cao của hệ thần kinh, nằm trong hộp sọ vững chắc và liên kết liên tục với tủy sống bên dưới. Não bộ điều khiển, điều hòa và phối hợp nhịp nhàng hoạt động của tất cả các cơ quan trong cơ thể, xử lý thông tin giác quan, đồng thời là cơ sở vật chất của các hoạt động tâm lý cấp cao như tư duy, trí nhớ, ý thức và cảm xúc.

### 2. Cấu tạo liên quan đến chức năng
- **Đại não (hai bán cầu đại não):** Vỏ não là lớp chất xám dày 2–4 mm gồm khoảng 14 tỷ nơron với nhiều nếp gấp (khe và rãnh) làm tăng diện tích vỏ não lên tới $2200\text{ cm}^2$ → nơi tập trung các trung khu cảm giác, vận động có ý thức và phản xạ có điều kiện (tư duy, ngôn ngữ).
- **Tiểu não:** Nằm ở phía sau dưới đại não, có vỏ chất xám gấp nếp mảnh → phụ trách điều hòa trương lực cơ, giữ thăng bằng cơ thể và phối hợp các cử động phức tạp, chính xác.
- **Thân não (Não giữa, Cầu não, Hành não):** Chứa các nhân xám là trung khu của các phản xạ sinh tồn tự động không điều kiện như **hô hấp, nhịp tim mạch, nuốt, ho, hắt hơi, chớp mắt**.
- **Tủy sống:** Nằm trong ống xương sống, dài $\sim 45\text{ cm}$, là trung khu của các phản xạ không điều kiện cấp thấp (như phản xạ rút tay khi chạm vật nóng, phản xạ đầu gối) và là đường dẫn truyền xung thần kinh nối ngoại vi với não.
- **Hàng rào máu - não (Blood-Brain Barrier):** Các tế bào nội mô mao mạch não gắn với nhau bằng các liên kết vòng bịt (tight junctions) cực khít cùng chân các tế bào sao bao bọc → bảo vệ nghiêm ngặt mô thần kinh khỏi các chất độc hại và sự dao động ion trong máu.

### 3. Đường đi (có mũi tên)

**Cung phản xạ thần kinh (5 thành phần chuẩn SGK):**
Thụ thể cảm giác (tiếp nhận kích thích từ môi trường ngoài hoặc trong)
→ Sợi thần kinh hướng tâm (dây thần kinh cảm giác)
→ Tủy sống hoặc Não bộ (trung khu thần kinh: xử lý, phân tích thông tin)
→ Sợi thần kinh ly tâm (dây thần kinh vận động)
→ Cơ quan đáp ứng (cơ co hoặc tuyến tiết dịch)

*Tóm tắt cung phản xạ:* Xuất phát từ **thụ thể cảm giác** và kết thúc tại **cơ quan đáp ứng**.

**Đường dẫn truyền xung điện qua Synapse hóa học:**
Xung điện truyền đến chùy synapse (đầu mút nơron trước)
→ Khử cực màng làm mở kênh $Ca^{2+}$ phụ thuộc điện thế
→ Ion $Ca^{2+}$ từ dịch ngoại bào tràn vào trong chùy synapse
→ Kích thích các bóng chứa chất trung gian hóa học (ví dụ: Acetylcholine) hòa màng
→ Chất trung gian hóa học giải phóng vào khe synapse bằng con đường xuất bào
→ Khuếch tán qua khe synapse và gắn vào thụ thể đặc hiệu ở màng sau synapse
→ Mở kênh ion ($Na^+$) ở màng sau $\to$ gây khử cực màng và phát sinh xung điện mới lan truyền tiếp tục.
(Ngay sau đó, enzyme Acetylcholinesterase ở màng sau phân giải Acetylcholine thành Choline và Axit axetic để tái hấp thu về màng trước).

### 4. Cơ chế hoạt động chi tiết
1. **Điện thế nghỉ và Điện thế hoạt động của nơron:**
   - *Điện thế nghỉ (phân cực):* Mặt trong màng nơron tích điện âm so với mặt ngoài ($\approx -70\text{ mV}$). Nguyên nhân do nồng độ ion $K^+$ bên trong tế bào cao hơn bên ngoài gấp nhiều lần, kênh $K^+$ mở cho $K^+$ rò rỉ ra ngoài mang theo điện tích dương, cùng với hoạt động của bơm $Na^+/K^+$-ATPase (bơm $3Na^+$ ra ngoài và nhận $2K^+$ vào trong tiêu tốn 1 ATP).
   - *Điện thế hoạt động (khi bị kích thích đạt ngưỡng):* Gồm 3 pha:
     1. **Pha khử cực:** Kênh $Na^+$ mở ồ ạt, $Na^+$ tràn vào trong làm điện thế trong màng từ $-70\text{ mV}$ tăng vọt lên $0\text{ mV}$.
     2. **Pha đảo cực:** $Na^+$ tiếp tục vào làm điện thế mặt trong chuyển sang dương ($\approx +30\text{ mV}$).
     3. **Pha tái phân cực:** Kênh $Na^+$ đóng lại, kênh $K^+$ mở nhanh đưa $K^+$ ùa ra ngoài khôi phục điện thế âm bên trong màng.
2. **Sự lan truyền xung thần kinh trên sợi thần kinh:**
   - *Sợi không có bao myelin:* Xung thần kinh lan truyền liên tục từ vùng này sang vùng kế tiếp, tốc độ chậm ($\sim 1\text{–}3\text{ m/s}$) và tốn nhiều ATP.
   - *Sợi có bao myelin:* Bao myelin cách điện gián đoạn ở các **eo Ranvier** $\to$ xung thần kinh lan truyền theo lối **nhảy cóc (saltatory conduction)** từ eo Ranvier này sang eo Ranvier kế tiếp, tốc độ cực nhanh (lên tới $100\text{–}120\text{ m/s}$) và tiết kiệm năng lượng.
3. **Phân hệ thần kinh giao cảm và đối giao cảm (hệ thần kinh sinh dưỡng):**
   - *Giao cảm (khi khẩn cấp / "chiến đấu hoặc bỏ chạy" - Fight or Flight):* Tăng nhịp tim, dãn phế quản, dãn đồng tử, ức chế tiêu hóa, kích thích tủy thượng thận tiết Adrenaline.
   - *Đối giao cảm (khi nghỉ ngơi và tiêu hóa - Rest and Digest):* Giảm nhịp tim, co phế quản, co đồng tử, tăng tiết dịch và nhu động đường tiêu hóa (chủ yếu do dây thần kinh X / phế vị chi phối).

### 5. Lưu ý thi THPT
- **[Nhận biết]** Cung phản xạ gồm đầy đủ **5 khâu**: Thụ thể cảm giác $\to$ Sợi hướng tâm $\to$ Trung ương thần kinh $\to$ Sợi ly tâm $\to$ Cơ quan đáp ứng.
- **[Nhận biết]** Bơm $Na^+/K^+$-ATPase tiêu tốn ATP bơm theo tỉ lệ: **$3 Na^+$ ra ngoài và $2 K^+$ vào trong**.
- **[Thông hiểu]** Trong synapse hóa học, xung thần kinh **chỉ truyền một chiều từ màng trước sang màng sau**, vì màng sau không có chất trung gian hóa học và màng trước không có thụ thể tiếp nhận đặc hiệu.
- **[Thông hiểu]** Xung thần kinh lan truyền trên sợi có bao myelin theo lối nhảy cóc nhanh hơn và tốn ít ATP hơn sợi không có myelin vì chỉ khử cực ở các eo Ranvier.
- **[Vận dụng]** Khí độc thần kinh (như Sarin) hoặc thuốc trừ sâu gốc phospho hữu cơ ức chế enzyme Acetylcholinesterase $\to$ Acetylcholine không bị phân hủy, tích tụ liên tục ở màng sau synapse $\to$ gây co giật cơ liên tục, liệt cơ hô hấp và ngạt thở dẫn đến tử vong.
- **[Vận dụng]** Thuốc gây tê (như Novocaine, Lidocaine) hoạt động bằng cách chẹn các kênh $Na^+$ phụ thuộc điện thế trên màng nơron cảm giác $\to$ ngăn cản khử cực và không phát sinh xung thần kinh cảm giác đau về não.

### 6. Câu hỏi tự luyện
**Câu 1 [Thông hiểu]:** Khi nói về cơ chế lan truyền xung thần kinh qua synapse hóa học dùng chất trung gian Acetylcholine, phát biểu nào sau đây là SAI?  
A. Xung điện có thể truyền hai chiều qua lại giữa màng trước và màng sau của synapse.  
B. Ion $Ca^{2+}$ tràn vào chùy synapse là tác nhân trực tiếp kích thích giải phóng chất trung gian hóa học.  
C. Enzyme acetylcholinesterase ở màng sau có nhiệm vụ phân giải acetylcholine thành axetat và choline.  
D. Chất trung gian hóa học khuếch tán qua khe synapse và gắn vào thụ thể đặc hiệu ở màng sau.  

**Câu 2 [Vận dụng]:** Một người không may bị tai nạn giao thông chấn thương vùng gáy, dẫn đến tổn thương nghiêm trọng tại hành não (thuộc thân não). Nạn nhân này đối mặt với nguy cơ tử vong trực tiếp cao nhất do nguyên nhân nào sau đây?  
A. Mất hoàn toàn khả năng tư duy trừu tượng và mất trí nhớ dài hạn.  
B. Ngừng thở và ngừng đập tự động của tim do tê liệt các trung khu điều hòa tuần hoàn và hô hấp sinh tồn.  
C. Mất khả năng cảm nhận vị giác của thức ăn và giảm phản xạ thăng bằng của mắt.  
D. Gan ngừng bài tiết dịch mật dẫn đến ngộ độc tiêu hóa cấp tính.  

--- Đáp án ---  
**Câu 1:** **A**. Giải thích: Synapse hóa học chỉ truyền tin một chiều duy nhất từ màng trước sang màng sau vì chỉ chùy synapse màng trước mới có bóng chứa chất trung gian hóa học và chỉ màng sau mới có thụ thể tiếp nhận tương ứng.  
**Câu 2:** **B**. Giải thích: Hành não chứa các trung khu thần kinh sinh dưỡng tối quan trọng điều khiển hoạt động hô hấp và tim mạch tự động. Tổn thương dập hành não lập tức làm ngừng thở và ngừng tim gây tử vong nhanh chóng.

### 7. Dữ liệu mũi tên cho mô hình 3D
```json
{
  "organ": "brain",
  "flows": [
    {
      "name": "Dòng dẫn truyền xung thần kinh",
      "color": "#F2C94C",
      "path": ["dai_nao", "tieu_nao", "than_nao", "tuy_song", "day_than_kinh"],
      "labels": ["Vỏ đại não", "Tiểu não", "Thân não (Hành não)", "Tủy sống", "Dây thần kinh vận động"]
    }
  ],
  "highlight": ["dai_nao", "tieu_nao", "than_nao", "tuy_song", "day_than_kinh"]
}
```

---
*Tài liệu Giáo trình Giải phẫu THPT GDPT 2018 biên soạn bởi Trợ lý Giải phẫu – Bộ học liệu số 3D.*
