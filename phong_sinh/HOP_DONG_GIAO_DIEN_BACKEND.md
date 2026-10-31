# Hợp đồng giữa Giao diện và Backend: dụng cụ sinh học tầng 2

File này là **điểm nối duy nhất** giữa hai prompt:
- `phong_sinh/PROMPT_DUNG_CU_BACKEND.md`: dữ liệu, mô phỏng C# thuần, máy chủ API / CSDL, AI.
- `phong_sinh/PROMPT_DUNG_CU_GIAO_DIEN.md`: dụng cụ trong Unity, hành lang, trang web `/sinh`, đồng bộ Unity ↔ web.

Hai bên **phải làm đúng** những gì ghi ở đây: mã dụng cụ, tên lớp, chữ ký hàm, đường dẫn API, dạng JSON. Muốn đổi thì **không tự đổi**: ghi vào mục 7 cuối file ("Đề xuất đổi hợp đồng"), nêu lý do, chờ chủ dự án duyệt.

---

## 1. Ranh giới file: ai sở hữu file nào

Mỗi bên chỉ sửa file của mình. Cần sửa file bên kia thì ghi đề xuất ở mục 7.

| Bên | Được tạo / sửa |
|---|---|
| **Backend** | `data/sinh/**`, `data/may/gel_doc.json`, `data/may/may_do_od600.json` (và bổ sung khóa thiếu ở `data/may/kinh_hien_vi.json`, `tu_an_toan_sinh_hoc.json`, `tu_am_vi_sinh.json`, `noi_hap.json`, `may_pcr.json`, `be_dien_di.json`), `Assets/Scripts/Simulation/Bio/**`, `server/lab/sinh_db.py`, `server/lab/sinh_api.py`, `server/ai/main.py` (chỉ dòng `include_router` + phục vụ `/sinh`), `server/ai/machines.py` (`ALIASES`, ngữ cảnh dụng cụ), `tests/BioSimulationTests.cs`, `tests/test_dung_cu_sinh.py`, `tests/test_sinh_web.py`, `tests/test_ai_server.py` (thêm bài) |
| **Giao diện** | `Assets/Scripts/Interaction/Bio/**`, `Assets/Scripts/Demo/Bio/**`, `Assets/Scripts/Demo/Instruments/*Program.cs` mới, `Assets/Scripts/Environment/Tang2/**`, `Assets/Scripts/Editor/LabBuilder/Tang2/**`, `Assets/Scripts/Rendering/ZoneMap.cs` (3 khu tầng 2), sửa tối thiểu `HandController.cs`, `AutoDoor.cs`, `Assets/Lab/**` (vật liệu, shader tầng 2), `server/web/sinh.html`, `server/web/index.html` (chỉ thêm 1 nút tab), `phong_sinh/bo_cuc_tang2.json`, `tools/kiem_thu_unity/**` |
| **Chung (chỉ thêm, không sửa của nhau)** | `docs/21-dung-cu-sinh-hoc.md` (mỗi bên một phần riêng có tiêu đề "Backend" / "Giao diện"), `phong_sinh/HOP_DONG_GIAO_DIEN_BACKEND.md` (chỉ mục 7) |

Giao diện **chỉ đọc** `data/sinh/*.json` và gọi hàm `Simulation/Bio`; không sửa. Backend **không** viết thành phần Unity (MonoBehaviour).

---

## 2. Danh sách mã dụng cụ (bắt buộc dùng đúng mã)

- Backend tạo **đúng một bản ghi** trong `data/sinh/dung_cu.json` cho mỗi dòng dưới đây (không thêm, không bớt; muốn thêm thì đề xuất ở mục 7).
- Giao diện làm **đúng thành phần Unity** ghi ở cột 4 (namespace `Khkt.Demo.Bio` nếu không ghi khác). Thành phần "(có sẵn)" là lớp tầng 1, dùng lại.
- Cột 5: mã máy có màn hình vận hành (`data/may/<ma>.json`), trống nếu không có.
- Mục đặc tả (cột 6) là số mục trong `PROMPT_DUNG_CU_GIAO_DIEN.md`.

### 2.1. Phòng thí nghiệm sinh học (`phong_sinh`)

| Mã | Tên | Phòng | Thành phần Unity | Máy | Mục |
|---|---|---|---|---|---|
| `kinh_hien_vi` | Kính hiển vi quang học hai mắt | phong_sinh, phong_vi_sinh | `MicroscopeController` (nâng cấp) | `kinh_hien_vi` | 5.1.1 |
| `kinh_hien_vi_camera` | Kính hiển vi kỹ thuật số giáo viên | phong_sinh | `MicroscopeController` + `MicroscopeCameraProgram` | `kinh_hien_vi` | 5.1.2 |
| `lam_kinh` | Lam kính 75 × 25 mm | phong_sinh, phong_vi_sinh | `GlassSlide` | | 5.1.3 |
| `lamen` | Lamen 22 × 22 mm | phong_sinh, phong_vi_sinh | `CoverSlip` | | 5.1.3 |
| `kim_mui_mac` | Kim mũi mác | phong_sinh | `DissectingNeedle` | | 5.1.4 |
| `kep_panh` | Kẹp (panh) mũi nhọn | phong_sinh, nha_kinh | `Forceps` | | 5.1.4 |
| `dao_lam` | Dao lam | phong_sinh | `RazorBlade` | | 5.1.4 |
| `keo_nho` | Kéo nhỏ | phong_sinh, nha_kinh | `Scissors` | | 5.1.4 |
| `ong_nho_giot` | Ống nhỏ giọt | mọi phòng | `Khkt.Interaction.Dropper` (có sẵn) | | 5.1.5 |
| `lo_thuoc_thu_nho_giot` | Lọ thuốc thử có ống nhỏ giọt | phong_sinh | `ReagentDropperBottle` | | 5.1.5 |
| `giay_tham` | Giấy thấm | phong_sinh | `BlottingPaper` | | 5.1.6 |
| `giay_lau_kinh` | Giấy lau kính | phong_sinh, phong_vi_sinh | `LensTissue` | | 5.1.6 |
| `dia_dong_ho` | Đĩa đồng hồ Ø 75 mm | phong_sinh | `WatchGlass` | | 5.1.7 |
| `coc_co_mo` | Cốc có mỏ | mọi phòng | `Khkt.Interaction.LabContainer` (có sẵn) | | 5.1.7 |
| `ong_nghiem` | Ống nghiệm | mọi phòng | `Khkt.Interaction.LabContainer` (có sẵn) | | 5.1.7 |
| `gia_ong_nghiem` | Giá ống nghiệm | mọi phòng | `TubeRackSockets` | | 5.1.7 |
| `ong_dong` | Ống đong | mọi phòng | `Khkt.Interaction.LabContainer` (có sẵn) | | 5.1.7 |
| `dua_thuy_tinh` | Đũa thủy tinh | phong_sinh, phong_sinh_phan_tu | `GlassRod` | | 5.1.7 |
| `den_ban_led` | Đèn bàn LED | phong_sinh | `DeskLamp` | | 5.1.8 |
| `voi_nuoc_ban` | Vòi nước đầu bàn | phong_sinh | `BenchTap` | | 5.1.8 |
| `man_hinh_ban` | Màn hình bàn học sinh | phong_sinh | `BioBenchScreenController` | | 5.1.9 |
| `coi_chay_su` | Cối chày sứ | phong_sinh, phong_sinh_phan_tu | `MortarPestle` | | 5.1.11 |
| `pheu_loc` | Phễu lọc + giấy lọc | phong_sinh, phong_sinh_phan_tu | `FilterFunnel` | | 5.1.11 |
| `lo_dau_soi` | Lọ dầu soi kính | phong_sinh, phong_vi_sinh | `ImmersionOil` | | 5.1.12 |
| `hop_tieu_ban` | Hộp tiêu bản cố định | phong_sinh | `SlideBox` | | 5.1.13 |
| `bang_dieu_khien_lop` | Bảng điều khiển lớp học | phong_sinh | `ClassroomConsole` + `ClassroomProgram` | | 5.1.14 |
| `tu_an_toan_sinh_hoc` | Tủ an toàn sinh học cấp II A2 | phong_sinh, phong_vi_sinh | `BiosafetyCabinetController` + `BscProgram` | `tu_an_toan_sinh_hoc` | 5.2.1 |
| `tu_am` | Tủ ấm vi sinh | phong_sinh, phong_vi_sinh | `IncubatorController` + `IncubatorProgram` | `tu_am_vi_sinh` | 5.2.4 |
| `noi_hap` | Nồi hấp tiệt trùng đứng | phong_sinh, phong_vi_sinh | `AutoclaveController` + `AutoclaveProgram` (có sẵn) | `noi_hap` | 5.2.6 |
| `can_phan_tich` | Cân phân tích | phong_sinh | `Khkt.Interaction.BalanceScale` (có sẵn) | | 5.1.16 |
| `may_do_ph` | Máy đo pH để bàn | phong_sinh | `Khkt.Interaction.MeterProbe` (có sẵn) | | 5.1.16 |
| `be_on_nhiet` | Bể ổn nhiệt | phong_sinh, phong_sinh_phan_tu | `WaterBathController` | | 5.4.10 |
| `khoi_gia_nhiet_kho` | Khối gia nhiệt khô | phong_sinh_phan_tu | `DryBlockHeater` | | 5.4.10 |
| `may_vortex` | Máy lắc vortex | phong_sinh, phong_vi_sinh, phong_sinh_phan_tu | `VortexMixer` | | 5.4.5 |
| `may_ly_tam_de_ban` | Máy ly tâm để bàn | phong_sinh, phong_sinh_phan_tu | `Khkt.Interaction.Centrifuge` (có sẵn) + `CentrifugeLoad` | | 5.4.6 |
| `binh_len_men` | Bình lên men + bóng bay | phong_sinh | `FermentationFlask` | | 5.1.17 |
| `bo_lam_sua_chua` | Hũ làm sữa chua | phong_sinh | `YogurtJar` | | 5.1.18 |
| `ban_giai_phau_so` | Bàn giải phẫu số | phong_sinh | `DigitalAnatomyTable` (nâng cấp) | | 5.1.19 |
| `bo_xuong_gia` | Bộ xương trên giá bánh xe | phong_sinh | `SkeletonStand` | | 5.1.19 |
| `quay_rua_tay` | Quầy rửa tay vòi cảm ứng | phong_sinh, hanh_lang_t2 | `HandWashStation` | | 5.1.20 |
| `bon_rua_mat` | Bồn rửa mắt khẩn cấp | phong_sinh | `EyeWashStation` | | 5.1.20 |
| `bo_xu_ly_tran` | Bộ xử lý tràn đổ sinh học | phong_sinh, phong_vi_sinh | `SpillKit` | | 5.1.21 |
| `thung_rac_nguy_hai` | Thùng rác nguy hại sinh học | mọi phòng | `BiohazardBin` | | 5.1.21 |
| `thung_vat_sac` | Thùng vật sắc nhọn | mọi phòng | `SharpsBin` | | 5.1.21 |
| `binh_chua_chay` | Bình chữa cháy | mọi phòng | thẻ thông tin (`EquipmentInfo`, có sẵn) | | 5.1.21 |
| `tu_so_cuu` | Tủ sơ cứu | phong_sinh | `FirstAidCabinet` | | 5.1.21 |
| `do_bao_ho` | Đồ bảo hộ (áo, găng, kính) | phong_sinh, phong_vi_sinh, hanh_lang_t2 | `PpeStation` | | 5.1.21 |

### 2.2. Phòng vi sinh (`phong_vi_sinh`)

| Mã | Tên | Phòng | Thành phần Unity | Máy | Mục |
|---|---|---|---|---|---|
| `den_con` | Đèn cồn | phong_sinh, phong_vi_sinh | `Khkt.Interaction.HeatSource` (có sẵn) + `SpiritLamp` | | 5.2.2 |
| `que_cay_vong` | Que cấy vòng | phong_vi_sinh | `InoculatingLoop` | | 5.2.3 |
| `que_cay_thang` | Que cấy thẳng | phong_vi_sinh | `InoculatingLoop` (chế độ thẳng) | | 5.2.3 |
| `que_trang` | Que trang thủy tinh | phong_vi_sinh | `CellSpreader` | | 5.2.3 |
| `que_cay_nhua` | Que cấy nhựa dùng một lần | phong_vi_sinh | `InoculatingLoop` (chế độ nhựa) | | 5.2.3 |
| `dia_petri` | Đĩa petri Ø 90 mm | phong_vi_sinh, phong_sinh | `PetriDish` | | 5.2.5 |
| `ong_canh_thang` | Ống canh thang | phong_vi_sinh | `BrothTube` | | 5.2.5 |
| `ong_thach_nghieng` | Ống thạch nghiêng | phong_vi_sinh | `BrothTube` (chế độ thạch nghiêng) | | 5.2.5 |
| `dao_nhuom_gram` | Đảo nhuộm Gram | phong_vi_sinh | `GramStainStation` | | 5.2.8 |
| `binh_tia_con` | Bình tia cồn 70 % | mọi phòng | `Khkt.Interaction.WashBottle` (có sẵn) | | 5.2.9 |
| `binh_tia_nuoc` | Bình tia nước cất | mọi phòng | `Khkt.Interaction.WashBottle` (có sẵn) | | 5.2.9 |
| `may_dem_khuan_lac` | Máy đếm khuẩn lạc | phong_vi_sinh | `ColonyCounterController` | | 5.2.10 |
| `den_uv_tran` | Đèn UV diệt khuẩn trần | phong_vi_sinh | `UvCeilingLamp` | | 5.2.11 |
| `buong_dem` | Buồng đệm cửa liên động | phong_vi_sinh | `AirlockInterlock` | | 5.2.12 |
| `pass_box` | Pass-box hai cửa | phong_sinh, phong_vi_sinh | `PassBoxController` | | 5.2.12 |
| `bo_ong_pha_loang` | Bộ 6 ống pha loãng | phong_vi_sinh | `DilutionTubeSet` | | 5.2.13 |
| `tui_hap_huy` | Túi hấp hủy | phong_vi_sinh | `AutoclaveBag` | | 5.2.14 |

### 2.3. Kho sinh phẩm (`kho_sinh_pham`)

| Mã | Tên | Phòng | Thành phần Unity | Máy | Mục |
|---|---|---|---|---|---|
| `tu_mat_4` | Tủ mát 4 °C hai cánh kính | kho_sinh_pham | `FridgeController` | | 5.3.1 |
| `tu_dong_am20` | Tủ đông −20 °C | kho_sinh_pham, phong_sinh_phan_tu | `FridgeController` (chế độ đông) | | 5.3.2 |
| `ke_mau` | Kệ inox mẫu ngâm | kho_sinh_pham | `SpecimenShelf` | | 5.3.3 |
| `mo_hinh_day_hoc` | Mô hình ADN / tế bào / bán thân | kho_sinh_pham | `TeachingModel` | | 5.3.3 |
| `so_kho_dien_tu` | Sổ kho điện tử | kho_sinh_pham | `StockKiosk` + `StockProgram` | | 5.3.4 |

### 2.4. Phòng sinh học phân tử (`phong_sinh_phan_tu`)

| Mã | Tên | Phòng | Thành phần Unity | Máy | Mục |
|---|---|---|---|---|---|
| `micropipet_p10` | Micropipet P10 (0,5 – 10 µL) | phong_sinh_phan_tu, phong_vi_sinh | `Micropipette` | | 5.4.1 |
| `micropipet_p200` | Micropipet P200 (20 – 200 µL) | phong_sinh_phan_tu, phong_vi_sinh | `Micropipette` | | 5.4.1 |
| `micropipet_p1000` | Micropipet P1000 (100 – 1000 µL) | phong_sinh_phan_tu, phong_vi_sinh | `Micropipette` | | 5.4.1 |
| `hop_dau_con` | Hộp đầu côn (trắng / vàng / xanh) | phong_sinh_phan_tu, phong_vi_sinh | `TipBox` | | 5.4.2 |
| `thung_dau_con` | Thùng rác đầu côn | phong_sinh_phan_tu, phong_vi_sinh | `TipWaste` | | 5.4.2 |
| `ong_eppendorf` | Ống eppendorf 1,5 mL | phong_sinh_phan_tu | `MicroTube` | | 5.4.3 |
| `ong_pcr` | Ống PCR 0,2 mL (đơn, dải 8) | phong_sinh_phan_tu | `MicroTube` (chế độ PCR) | | 5.4.3 |
| `gia_ong_eppendorf` | Giá ống eppendorf | phong_sinh_phan_tu | `TubeRackSockets` | | 5.4.4 |
| `gia_ong_pcr` | Giá ống PCR 96 lỗ | phong_sinh_phan_tu | `TubeRackSockets` | | 5.4.4 |
| `hop_da` | Hộp đá | phong_sinh_phan_tu | `IceBox` | | 5.4.13 |
| `may_ly_tam_mini` | Máy ly tâm mini | phong_sinh_phan_tu | `Khkt.Interaction.Centrifuge` (có sẵn) + `CentrifugeLoad` | | 5.4.6 |
| `tu_thao_tac_pcr` | Tủ thao tác PCR | phong_sinh_phan_tu | `PcrHoodController` | | 5.4.7 |
| `may_pcr` | Máy PCR (luân nhiệt) | phong_sinh_phan_tu | `ThermalCyclerController` + `PcrProgram` | `may_pcr` | 5.4.9 |
| `be_dien_di` | Bể điện di agarose | phong_sinh_phan_tu | `GelTankController` | `be_dien_di` | 5.4.11 |
| `nguon_dien_di` | Bộ nguồn điện di | phong_sinh_phan_tu | `PowerSupplyController` | `be_dien_di` | 5.4.11 |
| `khay_duc_gel` | Khay đúc gel + lược | phong_sinh_phan_tu | `GelCastingTray` | | 5.4.11 |
| `lo_vi_song` | Lò vi sóng nhỏ (đun agarose) | phong_sinh_phan_tu | `MicrowaveOven` | | 5.4.11 |
| `gel_doc` | Buồng chụp gel | phong_sinh_phan_tu | `GelDocController` + `GelDocProgram` | `gel_doc` | 5.4.12 |
| `may_quang_pho_od600` | Máy quang phổ OD600 / ADN | phong_sinh_phan_tu | `Khkt.Interaction.Spectrophotometer` (có sẵn) + `Od600Program` | `may_do_od600` | 5.4.14 |
| `cuvet` | Cuvet 1 cm | phong_sinh_phan_tu | `Cuvette` | | 5.4.14 |

### 2.5. Nhà kính (`nha_kinh`)

| Mã | Tên | Phòng | Thành phần Unity | Máy | Mục |
|---|---|---|---|---|---|
| `tram_quang_hop` | Trạm quang hợp rong đuôi chó | nha_kinh | `PhotosynthesisRig` | | 5.5.1 |
| `que_dom` | Que đóm | nha_kinh, phong_sinh | `GlowingSplint` | | 5.5.1 |
| `tram_potometer` | Trạm thoát hơi nước (potometer) | nha_kinh | `PotometerRig` | | 5.5.2 |
| `hop_huong_sang` | Hộp thí nghiệm hướng sáng | nha_kinh | `PhototropismBox` | | 5.5.3 |
| `gian_thuy_canh_nft` | Giàn thủy canh NFT | nha_kinh | `NftRackController` | | 5.5.4 |
| `may_do_ec_ph` | Máy đo EC / pH cầm tay | nha_kinh | `Khkt.Interaction.MeterProbe` (có sẵn) | | 5.5.4 |
| `binh_tuoi` | Bình tưới | nha_kinh | `WateringCan` | | 5.5.5 |
| `cam_bien_vi_khi_hau` | Cảm biến vi khí hậu | nha_kinh | `ClimateSensor` | | 5.5.5 |
| `he_phun_suong` | Hệ phun sương | nha_kinh | `MistSystem` | | 5.5.5 |
| `bo_sang_chau` | Bộ sang chậu | nha_kinh | `PottingKit` | | 5.5.5 |

### 2.6. Hành lang và dụng cụ chung

| Mã | Tên | Phòng | Thành phần Unity | Máy | Mục |
|---|---|---|---|---|---|
| `man_hinh_bang_tin` | Màn hình bảng tin hành lang | hanh_lang_t2 | `CorridorBoard` | | 5.6 |
| `kiosk_tra_cuu` | Ki-ốt tra cứu dụng cụ | hanh_lang_t2 | `ToolKiosk` | | 5.6 |
| `tu_trung_bay` | Tủ trưng bày mẫu vật | hanh_lang_t2 | `DisplayCabinet` | | 5.6 |
| `tu_do_ca_nhan` | Tủ đồ cá nhân | hanh_lang_t2 | `Locker` | | 5.6 |
| `dong_ho_bam_gio` | Đồng hồ bấm giờ để bàn | mọi phòng | `BenchTimer` | | 5.7 |
| `but_ghi_nhan` | Bút ghi nhãn | mọi phòng | `LabelPen` | | 5.7 |
| `nhiet_ke` | Nhiệt kế | phong_sinh, nha_kinh | `Thermometer` | | 5.7 |
| `giay_chi_thi_ph` | Giấy chỉ thị pH | phong_sinh | `Khkt.Interaction.TestPaper` (có sẵn) | | 5.7 |
| `may_khuay_tu_gia_nhiet` | Máy khuấy từ gia nhiệt | phong_sinh, phong_sinh_phan_tu | `Khkt.Interaction.MagneticStirrer` (có sẵn) | | 5.7 |

**Vật tư tiêu hao** (mẫu vật, thuốc thử, môi trường, kit PCR, men, giống sữa chua, agarose…) **không** nằm trong `dung_cu.json` mà nằm trong `data/sinh/sinh_pham_mac_dinh.json` (mục 3.6), có `id` dạng `sp_<ten>` (ví dụ `sp_xanh_methylen_1`, `sp_men_kho`, `sp_taq_polymerase`, `sp_thach_pca`, `sp_hanh_tim`).

---

## 3. Dạng dữ liệu JSON (`data/sinh/`)

Mọi thông số có `nguon` (URL https thật) hoặc `"mo_phong": true`. Mọi file có `"phien_ban": 1`.

### 3.1. `dung_cu.json`

```json
{ "phien_ban": 1, "dung_cu": [ {
  "ma": "micropipet_p200",
  "ten": "Micropipet đơn kênh P200 (20 – 200 µL)",
  "ten_tieng_anh": "Single-channel micropipette P200",
  "phan_loai": "Dụng cụ sinh học phân tử",
  "phong": ["phong_sinh_phan_tu", "phong_vi_sinh"],
  "so_luong": 6,
  "vi_tri": "Giá pipet quầy tiền PCR; đảo tách chiết ADN",
  "cong_dung": "Hút và nhả chính xác thể tích nhỏ",
  "thong_so": [ { "ten": "Dải thể tích", "gia_tri": "20 – 200 µL", "nguon": "https://..." } ],
  "cach_dung": ["..."],
  "loi_thuong_gap": ["..."],
  "an_toan": ["..."],
  "hieu_chuan_ngay": 365,
  "thanh_phan_unity": "Khkt.Demo.Bio.Micropipette",
  "may_man_hinh": null,
  "muc_dac_ta": "5.4.1"
} ] }
```
`hieu_chuan_ngay` = 0 nếu không cần hiệu chuẩn. `phong` dùng mã khu của `ZoneMap` (`phong_sinh`, `phong_vi_sinh`, `kho_sinh_pham`, `hanh_lang_t2`, `phong_sinh_phan_tu`, `nha_kinh`, `hanh_lang_dong`). "mọi phòng" ở bảng mục 2 viết thành danh sách đủ các phòng có thí nghiệm.

### 3.2. `tieu_ban.json`

`{ "ma", "ten", "loai": "tu_lam" | "co_dinh", "nhuom": "khong" | "xanh_methylen" | "lugol" | "gram" | "he", "kich_thuoc_te_bao_um": [min, max], "bo_phan_chu_thich": [{ "ten", "x", "y" }], "nguon" | "mo_phong" }` — `x`, `y` là tọa độ 0..1 trên ảnh tiêu bản để Giao diện đặt mũi tên chú thích.

### 3.3. `chung_vi_sinh.json`

`{ "ma", "ten_khoa_hoc", "ten", "cap_an_toan": "BSL-1", "gram": "duong" | "am" | "khong_ap_dung" (nấm men), "hinh_dang": "que" | "cau" | "nam_men", "thoi_gian_the_he_phut", "nhiet_do_toi_uu_c", "khuan_lac": { "mau": [r, g, b], "be_mat": "bong" | "kho_nhan" | "loi", "bo": "tron" | "khong_deu", "duong_kinh_24h_mm" }, "nguon" | "mo_phong" }`
Chỉ chủng dạy học an toàn (E. coli K-12, *Saccharomyces cerevisiae*, *Lactobacillus bulgaricus*, *Streptococcus thermophilus*, *Bacillus subtilis* chủng dạy học) và 2 loại tạp nhiễm môi trường dùng cho đĩa nhiễm (`tap_nam_moc`, `tap_bacillus_khong_khi`).

### 3.4. `moi_truong.json`

`{ "ma", "ten", "loai": "thach" | "long", "thanh_phan": [{ "chat", "g_l" }], "mau": [r, g, b], "do_trong": 0..1, "ph", "nguon" | "mo_phong" }` — PCA, thạch dinh dưỡng, LB, Sabouraud, canh LB, nước muối sinh lý.

### 3.5. `bai_thi_nghiem.json`

`{ "ma", "ten", "lop", "sgk", "muc_tieu", "dung_cu": [mã], "sinh_pham": [id], "buoc": [{ "so", "mo_ta", "dieu_kien": { "loai", ... } }], "ket_qua_mong_doi", "cau_hoi": [{ "hoi", "dap_an" }], "nguon" }`
15 bài, mã: `te_bao_hanh`, `khi_khong`, `co_nguyen_sinh`, `catalase`, `len_men_ruou`, `len_men_lactic`, `quang_hop`, `thoat_hoi_nuoc`, `huong_sang`, `tach_chiet_adn`, `dem_vi_sinh`, `cay_ria`, `nhuom_gram`, `pcr_dien_di`, `duong_cong_od600`.
`dieu_kien.loai` (Giao diện dùng để tự đánh dấu bước xong): `dat_vat` (`ma`, `vao`), `the_tich` (`ma`, `ml_min`, `ml_max`), `trang_thai` (`ma`, `ten`, `gia_tri`), `thoi_gian_u` (`gio_min`), `vat_kinh` (`gia_tri`), `nhiet_do` (`ma`, `c_min`, `c_max`).

### 3.6. `sinh_pham_mac_dinh.json`

`{ "id": "sp_...", "ten", "loai": "chung_vi_sinh" | "moi_truong" | "hoa_chat_sinh" | "kit" | "mau" | "giong", "tu": "tu_mat_4" | "tu_dong_am20" | "ke", "ngan", "so_luong", "don_vi", "so_lo", "han_dung_ngay": số ngày kể từ lúc tạo CSDL, "cap_an_toan": "BSL-1", "ma_chung"?: mã trong chung_vi_sinh, "ma_chat"?: mã trong ChemEngine }`

---

## 4. Mô phỏng C# (`Assets/Scripts/Simulation/Bio`, namespace `Khkt.Simulation.Bio`, C# thuần)

Backend viết, Giao diện gọi. **Không** `using UnityEngine`. Không đổi chữ ký hàm đã có (`MicroscopeController`, `ElodeaBubbles`, `BacterialColonyAnimation`, `GelMigrationAnimation` đang gọi). Kết quả xác định: cùng đầu vào + cùng `seed` → cùng kết quả. Không cấp phát trong hàm gọi mỗi khung (hàm trả danh sách thì nhận danh sách đầu ra do người gọi cấp sẵn).

```csharp
namespace Khkt.Simulation.Bio
{
    // ----- Mẫu sinh học (Giao diện bọc trong MonoBehaviour BioSample) -----
    public struct DnaBand { public int bp; public double ng; public bool smear; }
    public struct Contaminant { public string organismId; public int colonies; }

    public sealed class BioSampleData
    {
        public string organismId = "";     // mã chung_vi_sinh, "" nếu không có vi sinh
        public double cfuPerMl;
        public string specimenId = "";     // mã tieu_ban, "" nếu không phải tiêu bản
        public readonly List<DnaBand> dna = new List<DnaBand>();
        public bool sterile = true;
        public readonly List<Contaminant> contaminants = new List<Contaminant>();
        public void CopyFrom(BioSampleData other);
        // Trộn src (srcMl) vào dst (dstMl, thể tích TRƯỚC khi trộn); mật độ, ADN, tạp nhiễm chia theo thể tích.
        public static void MixInto(BioSampleData dst, double dstMl, BioSampleData src, double srcMl);
    }

    public static class Dilution
    {
        public static double CfuPerMl(int colonies, double platedMl, double dilution); // dilution = 1e-4 ...
        public static bool IsCountable(int colonies);                                   // 30..300
        public static double CarryOverMl(double tipNominalUl);                          // dư bám đầu côn khi không thay
        public static int SampleColonies(double expected, int seed);                    // Poisson
    }

    public struct StreakStroke { public int zone; public float x0, y0, x1, y1; public bool loopSterilizedBefore; } // mm, tâm đĩa = (0,0)
    public struct ColonySpot { public float x, y, diameterMm; public string organismId; }
    public static class StreakPlate
    {
        public static void Colonies(IReadOnlyList<StreakStroke> strokes, double inoculumCfu, string organismId,
                                    double hours, double tempC, int seed, List<ColonySpot> output);
    }

    public struct AsepticContext
    {
        public bool insideBsc, bscRunning; public double bscPurgeMinutes, sashHeightCm;
        public bool grilleBlocked, surfaceClean, handsWashed, ppeOn, nearFlame, toolSterile;
        public double openSeconds;
    }
    public static class ContaminationRules
    {
        public static double Probability(in AsepticContext c);                        // 0..1
        public static bool Roll(in AsepticContext c, int seed, out Contaminant result);
    }

    public enum GramResult { Tim, Hong, LoangLo, KhongCoTeBao }
    public static class GramStain
    {
        public static GramResult Result(bool gramPositive, double crystalVioletS, double iodineS, double decolorizeS,
                                        double safraninS, bool heatFixed, double smearThickness01);
    }

    public static class Transpiration
    {
        public static double BubbleShiftMmPerMin(double wind01, double light01, double humidity01, bool stomataBlocked);
    }
    public static class Phototropism
    {
        public static double BendAngleDeg(double hours, double lightAzimuthDeg, bool fullyDark);
        public static double EtiolationFactor(double hours, bool fullyDark);           // 1 = bình thường, > 1 vươn dài
    }
    public static class DnaExtraction
    {
        public static double YieldUg(double sampleGrams, string sampleKind, bool salt, bool detergent,
                                     double incubateC, double ethanolC, double ethanolToLysateRatio);
    }
    public static class Catalase { public static double ActivityFactor(double tempC, bool boiled); } // phản ứng vẫn qua ChemEngine
    public static class PipetteAccuracy
    {
        public static double ActualUl(double setUl, double nominalMaxUl, int stageWhenAspirating, double tiltDeg, bool tipSubmerged);
        public static double ResidualFraction(int stageWhenDispensing);                // nấc 1 còn sót, nấc 2 ≈ 0
    }
    public static class Centrifugation
    {
        public static double Rcf(double radiusCm, double rpm);
        public static double Rpm(double radiusCm, double rcf);
        public static bool IsBalanced(IReadOnlyList<(int slot, double grams)> load, int slotCount, out string reasonVi);
    }

    public struct PcrSetup
    {
        public bool hasTemplate, hasPrimers, hasTaq, hasDntp, hasBuffer, lidHeated, productContamination;
        public double annealC, primerTm, enzymeWarmMinutes, templateCopies; public int cycles, targetBp;
    }
    // Mở rộng lớp có sẵn (thêm hàm, giữ hàm cũ):
    //   Pcr.Run(in PcrSetup s, int seed, List<DnaBand> output)
    //   Electrophoresis.BandPositionCm(int bp, double agarosePct, double volts, double minutes, bool reversedPolarity)
    //   Electrophoresis.LeftGel(int bp, double agarosePct, double volts, double minutes, double gelLengthCm)
    //   Optics.Brightness(double lamp01, double iris01, double condenser01, bool oilNeededButMissing)
    //   Fermentation.LacticAcidGramsAt(double hours, double tempC, double milkMl)
    //   Fermentation.Co2MolesAt(double hours, double tempC, double sugarG, bool yeastAlive)
}
```

Dung dịch, pH, phản ứng hóa học (H₂O₂ + catalase, CO₂ + nước vôi, axit lactic hạ pH) **đi qua `ChemEngine`**; `Bio` chỉ cấp hệ số (hoạt tính enzyme, lượng chất sinh ra theo thời gian).

---

## 5. API máy chủ (`/lab/sinh`, FastAPI, JSON UTF-8)

- Lỗi: mã HTTP + `{"detail": "<lời nhắn tiếng Việt>"}` (kiểu mặc định của FastAPI). 409 = trùng lịch, 422 = dữ liệu sai hoặc sinh phẩm không phải `BSL-1`, 404 = không có.
- Thời gian: số giây Unix (`float`) trong JSON; web tự đổi sang `dd/MM/yyyy HH:mm`.
- Danh sách trả `{ "muc": [...], "tong": n }`.

| Phương thức, đường dẫn | Gửi | Nhận |
|---|---|---|
| `GET /lab/sinh/tong-quan` | | `{ "dung_cu": {"tong","hong","dang_muon","can_bao_tri","qua_han_hieu_chuan"}, "sinh_pham": {"sap_het": [...], "sap_het_han": [...], "qua_han": [...]}, "tu": [{"tu","nhiet_do","thoi_gian","canh_bao"}], "su_co_moi": [...], "lich_hom_nay": [...] }` |
| `GET /lab/sinh/dung-cu?phong=&phan_loai=&tim=` | | `muc`: bản ghi `dung_cu.json` + `so_luong`, `hong`, `dang_muon`, `can_bao_tri`, `vi_tri`, `lan_hieu_chuan`, `han_hieu_chuan`, `ghi_chu` |
| `PATCH /lab/sinh/dung-cu/{ma}` | các trường trên (tùy chọn) | bản ghi sau khi sửa |
| `GET /lab/sinh/sinh-pham?tu=&loai=&tim=` | | `muc`: bản ghi sinh phẩm |
| `POST /lab/sinh/sinh-pham` | `ten, loai, tu, ngan, so_luong, don_vi, so_lo, han_dung, cap_an_toan` | bản ghi mới |
| `PATCH` / `DELETE /lab/sinh/sinh-pham/{id}` | | |
| `POST /lab/sinh/sinh-pham/{id}/xuat` · `/nhap` | `{ "so_luong", "nguoi", "ghi_chu" }` | bản ghi sau khi đổi |
| `GET /lab/sinh/nhiet-do?tu=&tu_luc=` | | `muc`: `{tu, thoi_gian, nhiet_do, cua_mo}` |
| `POST /lab/sinh/nhiet-do` | `{tu, nhiet_do, cua_mo}` | |
| `GET` / `POST /lab/sinh/dat-lich`, `PATCH` / `DELETE /lab/sinh/dat-lich/{id}` | `{doi_tuong, nguoi, lop, bat_dau, ket_thuc, muc_dich}` | 409 kèm `{"detail", "trung": [lịch bị trùng]}` |
| `GET` / `POST /lab/sinh/muon-tra`, `PATCH /lab/sinh/muon-tra/{id}` | mượn: `{ma_dung_cu, so_luong, nguoi, lop, han_tra}`; trả: `{luc_tra, tinh_trang_tra: "tot" \| "hong"}` | |
| `GET /lab/sinh/ket-qua?lop=&ma_bai=&hoc_sinh=`, `GET /lab/sinh/ket-qua/{id}`, `POST /lab/sinh/ket-qua`, `PATCH /lab/sinh/ket-qua/{id}` (nhận xét, điểm) | `{hoc_sinh, lop, ban, ma_bai, du_lieu}` | |
| `GET /lab/sinh/ket-qua.csv` | | CSV UTF-8 có BOM |
| `GET` / `POST /lab/sinh/su-co`, `PATCH /lab/sinh/su-co/{id}` | `{phong, loai, muc_do, mo_ta, xu_ly, trang_thai}` | |
| `GET` / `POST /lab/sinh/bao-tri` | `{ma_dung_cu, loai, ngay, nguoi, ket_qua, ghi_chu}` | |
| `GET /lab/sinh/huong-dan/{ma}` | | bản ghi `dung_cu.json` |
| `GET /lab/sinh/bai-thi-nghiem` | | `muc`: `bai_thi_nghiem.json` |
| `GET /lab/sinh/trang-thai-may` | | `muc`: `{ma, trang_thai: "san_sang" \| "dang_chay" \| "canh_bao" \| "hong", mo_ta, tien_do, cap_nhat}` |
| `POST /lab/sinh/su-kien` | `{ "su_kien": [ {"loai", "thoi_gian", "du_lieu"} ] }` | `{ "nhan": n, "loi": [{"chi_so", "ly_do"}] }` |
| `GET /sinh` | | trang `server/web/sinh.html` |

**Loại sự kiện** của `/su-kien` (Unity gửi):

| `loai` | `du_lieu` | Ghi vào |
|---|---|---|
| `dung_cu_hong` | `{ma, so_luong, ly_do, phong}` | `sinh_dung_cu.hong` += so_luong, `su_co_sinh` (loại `vo_dung_cu`) |
| `can_bao_tri` | `{ma, ly_do}` | `sinh_dung_cu.can_bao_tri` = 1 |
| `sinh_pham_xuat` / `sinh_pham_nhap` | `{id, so_luong, nguoi}` | `sinh_pham`, `nhat_ky` |
| `nhiet_do_tu` | `{tu, nhiet_do, cua_mo}` | `nhiet_do_tu` |
| `trang_thai_may` | `{ma, trang_thai, mo_ta, tien_do}` | bộ nhớ máy chủ (không cần lưu CSDL) |
| `ket_qua_tn` | `{hoc_sinh, lop, ban, ma_bai, du_lieu}` | `ket_qua_tn` |
| `su_co` | `{phong, loai, muc_do, mo_ta}` | `su_co_sinh`, `nhat_ky` |

`hoc_sinh` mặc định `"Học sinh"` khi chưa có tài khoản (chưa làm đăng nhập trong lần này).

---

## 6. Thứ tự làm, nhánh git

1. **Backend chạy trước**, nhánh `tang2/dung-cu-backend` tách từ `main`.
2. **Giao diện** tạo nhánh `tang2/dung-cu-giao-dien` **tách từ `tang2/dung-cu-backend`** sau khi Backend viết xong báo cáo bàn giao.
   - Nếu muốn chạy song song: Giao diện được làm trước các giai đoạn không cần Backend (nới hành lang, khung tương tác chung). Tới giai đoạn cần dữ liệu / mô phỏng / API thì dừng, chờ Backend xong rồi `git merge tang2/dung-cu-backend` vào nhánh mình (trên máy, không đẩy).
   - Hai Antigravity **không được** làm cùng một thư mục: Giao diện dùng thư mục riêng `git worktree add ../KHKT_giao_dien tang2/dung-cu-giao-dien`.
3. **Cả hai bên chỉ commit trên máy. Không `git push`, không mở PR, không gộp vào `main`.** Chủ dự án và Claude test, sửa xong mới đẩy.

---

## 7. Đề xuất đổi hợp đồng

Ghi theo mẫu, không tự sửa mục 1 – 6:

```
### [ngày] [Backend | Giao diện] – tiêu đề ngắn
- Muốn đổi: ...
- Lý do: ...
- Ảnh hưởng tới bên kia: ...
- Trạng thái: chờ duyệt
```

(chưa có đề xuất)
