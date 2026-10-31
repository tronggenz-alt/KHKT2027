// -----------------------------------------------------------------------------
// LabSceneBuilder.Floor2.Tools.cs — Gắn toàn bộ 108 dụng cụ và thiết bị tương tác
// của Tầng 2 (theo bảng Mục 2 HOP_DONG_GIAO_DIEN_BACKEND.md và data/sinh/dung_cu.json)
// vào các khu tương ứng khi dựng cảnh Tầng 2.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Demo;
using Khkt.Demo.Bio;
using Khkt.Demo.Instruments;
using Khkt.Interaction;
using Khkt.Interaction.Bio;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        static void BuildFloor2InteractiveTools(Transform tang2)
        {
            if (tang2 == null) return;
            BioCatalogLoader.EnsureLoaded();

            var bioLab = tang2.Find("Phòng thí nghiệm sinh học") ?? tang2;
            var microBio = tang2.Find("Phòng vi sinh") ?? tang2;
            var bioStore = tang2.Find("Kho sinh phẩm") ?? tang2;
            var corridor = tang2.Find("Hành lang tầng 2") ?? tang2;
            var molecular = tang2.Find("Phòng sinh học phân tử") ?? tang2;
            var greenhouse = tang2.Find("Nhà kính thực vật") ?? tang2;

            // 1. Gắn thẻ thông tin cho các kính hiển vi và bàn giải phẫu số đã dựng sẵn
            foreach (var mc in tang2.GetComponentsInChildren<MicroscopeController>(true))
            {
                EnsureBoxCollider(mc.gameObject, new Vector3(0.24f, 0.38f, 0.26f));
                if (mc.isTeacherCamera || (mc.transform.parent != null && mc.transform.parent.name.Contains("giáo viên")))
                {
                    mc.isTeacherCamera = true;
                    BioCatalogLoader.AttachToolInfo(mc.gameObject, "kinh_hien_vi_camera");
                }
                else
                {
                    BioCatalogLoader.AttachToolInfo(mc.gameObject, "kinh_hien_vi");
                }
            }

            // Đảm bảo có ít nhất 1 kính hiển vi kỹ thuật số của giáo viên
            bool hasTeacher = false;
            foreach (var mc in tang2.GetComponentsInChildren<MicroscopeController>(true))
            {
                if (mc.isTeacherCamera) { hasTeacher = true; break; }
            }
            if (!hasTeacher)
            {
                var teacherScopeGo = CreateToolNode(bioLab, "Kính hiển vi kỹ thuật số giáo viên", new Vector3(2.2f, 0.85f, 7.5f), "kinh_hien_vi_camera");
                var mc = teacherScopeGo.AddComponent<MicroscopeController>();
                mc.isTeacherCamera = true;
                BioCatalogLoader.AttachToolInfo(teacherScopeGo, "kinh_hien_vi_camera");
            }

            foreach (var dat in tang2.GetComponentsInChildren<DigitalAnatomyTable>(true))
            {
                EnsureBoxCollider(dat.gameObject, new Vector3(1.8f, 0.9f, 0.9f));
                BioCatalogLoader.AttachToolInfo(dat.gameObject, "ban_giai_phau_so");
            }

            // 2. Dụng cụ Phòng thí nghiệm sinh học (5.1) — đặt trên Bàn 1 (x ≈ 5.5, z ≈ 3.2) & các khu chức năng
            SpawnToolWith<GlassSlide>(bioLab, "Lam kính (Bàn 1)", new Vector3(5.20f, 0.82f, 3.10f), "lam_kinh");
            SpawnToolWith<CoverSlip>(bioLab, "Hộp Lamen (Bàn 1)", new Vector3(5.32f, 0.82f, 3.10f), "lamen");
            SpawnToolWith<DissectingNeedle>(bioLab, "Kim mũi mác (Bàn 1)", new Vector3(5.44f, 0.82f, 3.10f), "kim_mui_mac");
            SpawnToolWith<Forceps>(bioLab, "Kẹp panh mũi nhọn (Bàn 1)", new Vector3(5.56f, 0.82f, 3.10f), "kep_panh");
            SpawnToolWith<RazorBlade>(bioLab, "Dao lam (Bàn 1)", new Vector3(5.68f, 0.82f, 3.10f), "dao_lam");
            SpawnToolWith<Scissors>(bioLab, "Kéo mổ nhỏ (Bàn 1)", new Vector3(5.80f, 0.82f, 3.10f), "keo_nho");
            SpawnToolWith<Dropper>(bioLab, "Ống nhỏ giọt Pasteur (Bàn 1)", new Vector3(5.20f, 0.82f, 3.30f), "ong_nho_giot");

            // 5 lọ thuốc thử nhỏ giọt trên khay bàn (5.1.5)
            var bMethylen = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ Xanh methylen 1%", new Vector3(5.32f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot");
            bMethylen.reagentId = "xanh_methylen"; bMethylen.reagentName = "Xanh methylen 1 %";
            var bLugol = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ dung dịch Lugol", new Vector3(5.40f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot");
            bLugol.reagentId = "iot_lugol"; bLugol.reagentName = "Dung dịch Lugol (I₂/KI)";
            var bNaCl = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ NaCl ưu trương 0,8 M", new Vector3(5.48f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot");
            bNaCl.reagentId = "nacl_uu_truong"; bNaCl.reagentName = "NaCl ưu trương 0,8 M"; bNaCl.molarity = 0.8f;
            var bWater = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ Nước cất", new Vector3(5.56f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot");
            bWater.reagentId = "nuoc_cat"; bWater.reagentName = "Nước cất vô trùng"; bWater.molarity = 0f;
            var bH2o2 = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ H₂O₂ 3%", new Vector3(5.64f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot");
            bH2o2.reagentId = "h2o2_3_phan_tram"; bH2o2.reagentName = "Hiđrô peoxit H₂O₂ 3 %";

            SpawnToolWith<BlottingPaper>(bioLab, "Giấy thấm (Bàn 1)", new Vector3(5.74f, 0.82f, 3.30f), "giay_tham");
            SpawnToolWith<LensTissue>(bioLab, "Giấy lau kính (Bàn 1)", new Vector3(5.84f, 0.82f, 3.30f), "giay_lau_kinh");
            SpawnToolWith<WatchGlass>(bioLab, "Đĩa đồng hồ Ø 75 mm", new Vector3(5.94f, 0.82f, 3.30f), "dia_dong_ho");
            SpawnToolWith<LabContainer>(bioLab, "Cốc có mỏ 100 mL", new Vector3(6.06f, 0.82f, 3.30f), "coc_co_mo");
            SpawnToolWith<LabContainer>(bioLab, "Ống nghiệm thủy tinh", new Vector3(6.18f, 0.82f, 3.30f), "ong_nghiem");

            var rackTest = SpawnToolWith<TubeRackSockets>(bioLab, "Giá ống nghiệm 12 lỗ", new Vector3(6.30f, 0.82f, 3.30f), "gia_ong_nghiem");
            rackTest.rackToolId = "gia_ong_nghiem"; rackTest.acceptedTubeType = "ong_nghiem";

            SpawnToolWith<LabContainer>(bioLab, "Ống đong 50 mL", new Vector3(6.42f, 0.82f, 3.30f), "ong_dong");
            SpawnToolWith<GlassRod>(bioLab, "Đũa thủy tinh", new Vector3(6.54f, 0.82f, 3.30f), "dua_thuy_tinh");
            SpawnToolWith<DeskLamp>(bioLab, "Đèn bàn LED soi mẫu", new Vector3(6.66f, 0.82f, 3.30f), "den_ban_led");
            SpawnToolWith<BenchTap>(bioLab, "Vòi nước đầu bàn", new Vector3(4.85f, 0.88f, 3.20f), "voi_nuoc_ban");
            SpawnToolWith<BioBenchScreenController>(bioLab, "Màn hình bàn học sinh 1", new Vector3(5.50f, 1.05f, 2.85f), "man_hinh_ban");
            SpawnToolWith<MortarPestle>(bioLab, "Chày cối sứ Ø 9 cm", new Vector3(5.20f, 0.82f, 3.50f), "coi_chay_su");
            SpawnToolWith<FilterFunnel>(bioLab, "Phễu lọc + giấy lọc", new Vector3(5.38f, 0.82f, 3.50f), "pheu_loc");
            SpawnToolWith<ImmersionOil>(bioLab, "Lọ dầu soi kính (n=1,515)", new Vector3(5.56f, 0.82f, 3.50f), "lo_dau_soi");
            SpawnToolWith<SlideBox>(bioLab, "Hộp tiêu bản cố định (10 khe)", new Vector3(5.74f, 0.82f, 3.50f), "hop_tieu_ban");
            SpawnToolWith<ClassroomConsole>(bioLab, "Bảng điều khiển lớp học", new Vector3(2.20f, 0.85f, 8.20f), "bang_dieu_khien_lop");

            SpawnToolWith<BiosafetyCabinetController>(bioLab, "Tủ an toàn sinh học cấp II A2", new Vector3(6.60f, 1.10f, 14.20f), "tu_an_toan_sinh_hoc");
            SpawnToolWith<IncubatorController>(bioLab, "Tủ ấm 37 °C", new Vector3(18.50f, 1.10f, 4.20f), "tu_am");

            var autoGo = CreateToolNode(bioLab, "Nồi hấp tiệt trùng đứng", new Vector3(18.50f, 0.95f, 5.40f), "noi_hap");
            autoGo.AddComponent<AutoclaveController>();

            SpawnToolWith<BalanceScale>(bioLab, "Cân phân tích điện tử", new Vector3(18.50f, 0.85f, 6.20f), "can_phan_tich");
            SpawnToolWith<MeterProbe>(bioLab, "Máy đo pH để bàn", new Vector3(18.50f, 0.85f, 6.80f), "may_do_ph");
            SpawnToolWith<WaterBathController>(bioLab, "Bể ổn nhiệt (Water Bath)", new Vector3(18.50f, 0.85f, 7.30f), "be_on_nhiet");
            SpawnToolWith<VortexMixer>(bioLab, "Máy lắc ống Vortex", new Vector3(6.00f, 0.82f, 3.50f), "may_vortex");

            var centDesk = CreateToolNode(bioLab, "Máy ly tâm để bàn", new Vector3(18.50f, 0.85f, 3.40f), "may_ly_tam_de_ban");
            if (centDesk.GetComponent<Centrifuge>() == null) centDesk.AddComponent<Centrifuge>();
            var clDesk = centDesk.AddComponent<CentrifugeLoad>();
            clDesk.centrifugeId = "may_ly_tam_de_ban";

            SpawnToolWith<FermentationFlask>(bioLab, "Bình lên men rượu 250 mL", new Vector3(6.20f, 0.82f, 3.50f), "binh_len_men");
            SpawnToolWith<YogurtJar>(bioLab, "Bộ làm sữa chua (lên men lactic)", new Vector3(6.40f, 0.82f, 3.50f), "bo_lam_sua_chua");
            SpawnToolWith<SkeletonStand>(bioLab, "Bộ xương người trên giá", new Vector3(18.50f, 1.10f, 12.80f), "bo_xuong_gia");
            SpawnToolWith<HandWashStation>(bioLab, "Quầy rửa tay cảm ứng", new Vector3(15.20f, 0.90f, 14.30f), "quay_rua_tay");
            SpawnToolWith<EyeWashStation>(bioLab, "Bồn rửa mắt khẩn cấp", new Vector3(1.20f, 0.95f, 1.20f), "bon_rua_mat");
            SpawnToolWith<SpillKit>(bioLab, "Bộ xử lý tràn đổ sinh học", new Vector3(1.20f, 0.85f, 1.80f), "bo_xu_ly_tran");
            SpawnToolWith<BiohazardBin>(bioLab, "Thùng rác nguy hại sinh học", new Vector3(1.20f, 0.45f, 2.40f), "thung_rac_nguy_hai");
            SpawnToolWith<SharpsBin>(bioLab, "Hộp đựng vật sắc nhọn", new Vector3(1.20f, 0.85f, 2.90f), "thung_vat_sac");
            CreateToolNode(bioLab, "Bình chữa cháy CO₂", new Vector3(1.20f, 0.60f, 3.40f), "binh_chua_chay");
            SpawnToolWith<FirstAidCabinet>(bioLab, "Tủ sơ cứu y tế", new Vector3(1.20f, 1.35f, 3.90f), "tu_so_cuu");
            SpawnToolWith<PpeStation>(bioLab, "Trạm đồ bảo hộ cá nhân (PPE)", new Vector3(1.20f, 1.25f, 4.50f), "do_bao_ho");

            // 3. Phòng vi sinh + Buồng đệm + Pass-box (5.2)
            var lampGo = CreateToolNode(microBio, "Đèn cồn thủy tinh", new Vector3(22.20f, 0.85f, 11.20f), "den_con");
            if (lampGo.GetComponent<HeatSource>() == null) lampGo.AddComponent<HeatSource>();
            lampGo.AddComponent<SpiritLamp>();

            var loopRound = SpawnToolWith<InoculatingLoop>(microBio, "Que cấy vòng Ø 3 mm", new Vector3(22.40f, 0.85f, 11.20f), "que_cay_vong");
            loopRound.loopToolId = "que_cay_vong";
            var loopStraight = SpawnToolWith<InoculatingLoop>(microBio, "Que cấy thẳng", new Vector3(22.55f, 0.85f, 11.20f), "que_cay_thang");
            loopStraight.loopToolId = "que_cay_thang";
            SpawnToolWith<CellSpreader>(microBio, "Que trang chữ L thủy tinh", new Vector3(22.70f, 0.85f, 11.20f), "que_trang");
            var loopPlastic = SpawnToolWith<InoculatingLoop>(microBio, "Que cấy nhựa dùng 1 lần", new Vector3(22.85f, 0.85f, 11.20f), "que_cay_nhua");
            loopPlastic.loopToolId = "que_cay_nhua"; loopPlastic.isDisposablePlastic = true; loopPlastic.isSterile = true;

            SpawnToolWith<PetriDish>(microBio, "Đĩa Petri thạch LB Ø 90 mm", new Vector3(23.05f, 0.85f, 11.20f), "dia_petri");
            var brothT = SpawnToolWith<BrothTube>(microBio, "Ống canh thang LB", new Vector3(23.25f, 0.85f, 11.20f), "ong_canh_thang");
            brothT.tubeKindId = "ong_canh_thang";
            var slantT = SpawnToolWith<BrothTube>(microBio, "Ống thạch nghiêng giữ giống", new Vector3(23.40f, 0.85f, 11.20f), "ong_thach_nghieng");
            slantT.tubeKindId = "ong_thach_nghieng";

            SpawnToolWith<GramStainStation>(microBio, "Đảo nhuộm Gram 4 bước", new Vector3(23.00f, 0.90f, 11.60f), "dao_nhuom_gram");
            SpawnToolWith<WashBottle>(microBio, "Bình tia cồn 70 %", new Vector3(23.60f, 0.85f, 11.20f), "binh_tia_con");
            SpawnToolWith<WashBottle>(microBio, "Bình tia nước cất", new Vector3(23.75f, 0.85f, 11.20f), "binh_tia_nuoc");
            SpawnToolWith<ColonyCounterController>(microBio, "Máy đếm khuẩn lạc", new Vector3(24.20f, 0.88f, 13.80f), "may_dem_khuan_lac");
            SpawnToolWith<UvCeilingLamp>(microBio, "Công tắc đèn UV diệt khuẩn trần", new Vector3(25.10f, 1.35f, 9.80f), "den_uv_tran");
            SpawnToolWith<AirlockInterlock>(microBio, "Buồng đệm vô trùng (Airlock)", new Vector3(26.00f, 1.20f, 8.80f), "buong_dem");
            SpawnToolWith<PassBoxController>(microBio, "Hộp chuyển mẫu Pass-box", new Vector3(20.05f, 1.25f, 8.30f), "pass_box");
            SpawnToolWith<DilutionTubeSet>(microBio, "Dãy 6 ống pha loãng bậc 10", new Vector3(21.80f, 0.85f, 11.20f), "bo_ong_pha_loang");
            SpawnToolWith<AutoclaveBag>(microBio, "Túi hấp hủy sinh học", new Vector3(21.20f, 0.55f, 13.80f), "tui_hap_huy");

            // 4. Kho sinh phẩm (5.3)
            var fridge4 = SpawnToolWith<FridgeController>(bioStore, "Tủ mát sinh phẩm 4 °C", new Vector3(21.20f, 1.10f, 6.40f), "tu_mat_4");
            fridge4.fridgeId = "tu_mat_4"; fridge4.isDeepFreezer = false; fridge4.setTempC = 4.0f; fridge4.currentTempC = 4.0f;

            var freezer20 = SpawnToolWith<FridgeController>(bioStore, "Tủ đông sâu enzyme −20 °C", new Vector3(22.80f, 1.10f, 6.40f), "tu_dong_am20");
            freezer20.fridgeId = "tu_dong_am20"; freezer20.isDeepFreezer = true; freezer20.setTempC = -20.0f; freezer20.currentTempC = -20.0f;

            SpawnToolWith<SpecimenShelf>(bioStore, "Kệ inox mẫu ngâm bảo quản", new Vector3(24.50f, 1.20f, 3.80f), "ke_mau");
            SpawnToolWith<TeachingModel>(bioStore, "Mô hình ADN & Tế bào dạy học", new Vector3(24.50f, 1.10f, 2.20f), "mo_hinh_day_hoc");
            SpawnToolWith<StockKiosk>(bioStore, "Sổ kho điện tử cảm ứng", new Vector3(25.80f, 1.30f, 5.80f), "so_kho_dien_tu");

            // 5. Phòng sinh học phân tử (5.4)
            var p10 = SpawnToolWith<Micropipette>(molecular, "Micropipet P10 (0,5–10 µL)", new Vector3(2.20f, 0.85f, -8.30f), "micropipet_p10");
            p10.ConfigureRangeById("micropipet_p10");
            var p200 = SpawnToolWith<Micropipette>(molecular, "Micropipet P200 (20–200 µL)", new Vector3(2.35f, 0.85f, -8.30f), "micropipet_p200");
            p200.ConfigureRangeById("micropipet_p200");
            var p1000 = SpawnToolWith<Micropipette>(molecular, "Micropipet P1000 (100–1000 µL)", new Vector3(2.50f, 0.85f, -8.30f), "micropipet_p1000");
            p1000.ConfigureRangeById("micropipet_p1000");

            SpawnToolWith<TipBox>(molecular, "Hộp 96 đầu côn tiệt trùng", new Vector3(2.70f, 0.85f, -8.30f), "hop_dau_con");
            SpawnToolWith<TipWaste>(molecular, "Thùng thải bỏ đầu côn", new Vector3(2.90f, 0.85f, -8.30f), "thung_dau_con");

            var epp = SpawnToolWith<MicroTube>(molecular, "Ống Eppendorf 1,5 mL", new Vector3(5.80f, 0.85f, -6.20f), "ong_eppendorf");
            epp.tubeTypeId = "ong_eppendorf";
            var pcrT = SpawnToolWith<MicroTube>(molecular, "Ống PCR 0,2 mL thành mỏng", new Vector3(3.10f, 0.85f, -8.30f), "ong_pcr");
            pcrT.tubeTypeId = "ong_pcr"; pcrT.PrepareCompletePcrReaction(true, true, true, false);

            var rackEpp = SpawnToolWith<TubeRackSockets>(molecular, "Giá ống Eppendorf 24 lỗ", new Vector3(6.00f, 0.85f, -6.20f), "gia_ong_eppendorf");
            rackEpp.rackToolId = "gia_ong_eppendorf"; rackEpp.acceptedTubeType = "ong_eppendorf";
            var rackPcr = SpawnToolWith<TubeRackSockets>(molecular, "Giá ống PCR 96 lỗ", new Vector3(3.30f, 0.85f, -8.30f), "gia_ong_pcr");
            rackPcr.rackToolId = "gia_ong_pcr"; rackPcr.acceptedTubeType = "ong_pcr";

            SpawnToolWith<IceBox>(molecular, "Hộp đá bảo quản enzyme 0–4 °C", new Vector3(3.50f, 0.85f, -8.30f), "hop_da");
            SpawnToolWith<DryBlockHeater>(molecular, "Khối gia nhiệt khô (Dry Block)", new Vector3(6.30f, 0.85f, -6.20f), "khoi_gia_nhiet_kho");

            var centMini = CreateToolNode(molecular, "Máy ly tâm mini (Microcentrifuge)", new Vector3(6.60f, 0.85f, -6.20f), "may_ly_tam_mini");
            if (centMini.GetComponent<Centrifuge>() == null) centMini.AddComponent<Centrifuge>();
            var clMini = centMini.AddComponent<CentrifugeLoad>();
            clMini.centrifugeId = "may_ly_tam_mini";

            SpawnToolWith<PcrHoodController>(molecular, "Tủ thao tác tiền PCR (PCR Hood)", new Vector3(0.60f, 1.15f, -6.20f), "tu_thao_tac_pcr");
            SpawnToolWith<ThermalCyclerController>(molecular, "Máy luân nhiệt PCR 96 giếng", new Vector3(10.20f, 0.90f, -8.30f), "may_pcr");

            var gelTank = SpawnToolWith<GelTankController>(molecular, "Bể điện di gel agarose", new Vector3(11.10f, 0.85f, -8.30f), "be_dien_di");
            var pwr = SpawnToolWith<PowerSupplyController>(molecular, "Bộ nguồn điện di 100 V", new Vector3(11.45f, 0.85f, -8.30f), "nguon_dien_di");
            pwr.connectedTank = gelTank;

            SpawnToolWith<GelCastingTray>(molecular, "Khay đúc gel agarose + lược", new Vector3(11.80f, 0.85f, -8.30f), "khay_duc_gel");
            SpawnToolWith<MicrowaveOven>(molecular, "Lò vi sóng đun thạch agarose", new Vector3(12.20f, 0.90f, -8.30f), "lo_vi_song");
            var gelDoc = SpawnToolWith<GelDocController>(molecular, "Buồng chụp Gel UV/Blue LED", new Vector3(12.70f, 0.95f, -7.20f), "gel_doc");
            gelDoc.sourceTank = gelTank;

            var specGo = CreateToolNode(molecular, "Máy quang phổ đo OD600 / A260", new Vector3(7.20f, 0.90f, -6.20f), "may_quang_pho_od600");
            if (specGo.GetComponent<Spectrophotometer>() == null) specGo.AddComponent<Spectrophotometer>();
            specGo.AddComponent<Od600Controller>();

            SpawnToolWith<Cuvette>(molecular, "Cuvet đo quang 1 cm", new Vector3(7.50f, 0.85f, -6.20f), "cuvet");

            // 6. Nhà kính thực vật (5.5)
            SpawnToolWith<PhotosynthesisRig>(greenhouse, "Trạm quang hợp Rong đuôi chó", new Vector3(16.20f, 0.90f, -8.40f), "tram_quang_hop");
            SpawnToolWith<GlowingSplint>(greenhouse, "Que đóm thử khí Oxy", new Vector3(16.55f, 0.85f, -8.40f), "que_dom");
            SpawnToolWith<PotometerRig>(greenhouse, "Áp kế bọt khí Potometer", new Vector3(19.50f, 0.90f, -8.40f), "tram_potometer");
            SpawnToolWith<PhototropismBox>(greenhouse, "Hộp thí nghiệm hướng sáng", new Vector3(22.80f, 0.90f, -8.40f), "hop_huong_sang");
            SpawnToolWith<NftRackController>(greenhouse, "Giàn thủy canh NFT 3 tầng", new Vector3(25.20f, 1.20f, -6.20f), "gian_thuy_canh_nft");
            SpawnToolWith<MeterProbe>(greenhouse, "Bút đo EC / pH thủy canh", new Vector3(24.60f, 0.85f, -6.20f), "may_do_ec_ph");
            SpawnToolWith<WateringCan>(greenhouse, "Bình tưới cây vòi hoa sen", new Vector3(15.20f, 0.65f, -4.50f), "binh_tuoi");
            SpawnToolWith<ClimateSensor>(greenhouse, "Cảm biến vi khí hậu nhà kính", new Vector3(20.20f, 1.50f, -6.20f), "cam_bien_vi_khi_hau");
            SpawnToolWith<MistSystem>(greenhouse, "Hệ thống phun sương nhà kính", new Vector3(20.20f, 2.40f, -6.20f), "he_phun_suong");
            SpawnToolWith<PottingKit>(greenhouse, "Bộ dụng cụ sang chậu thực vật", new Vector3(15.80f, 0.85f, -4.35f), "bo_sang_chau");

            // 7. Hành lang tầng 2 mới 3,6 m & Dụng cụ dùng chung (5.6 & 5.7)
            AttachOrSpawnInGroup<CorridorBoard>(corridor, "Màn hình bảng tin", new Vector3(17.6f, 1.6f, -0.12f), "man_hinh_bang_tin");
            AttachOrSpawnInGroup<ToolKiosk>(corridor, "Trạm tra cứu dụng cụ", new Vector3(16.4f, 1.0f, F2_CorridorZ + 0.34f), "kiosk_tra_cuu");
            AttachOrSpawnInGroup<DisplayCabinet>(corridor, "Tủ trưng bày tiêu bản", new Vector3(9.5f, 1.0f, F2_CorridorZ + 0.32f), "tu_trung_bay");
            AttachOrSpawnInGroup<Locker>(corridor, "Kệ tủ đồ cá nhân", new Vector3(11.6f, 1.0f, F2_CorridorZ + 0.32f), "tu_do_ca_nhan");

            SpawnToolWith<BenchTimer>(bioLab, "Đồng hồ bấm giờ để bàn", new Vector3(6.60f, 0.82f, 3.50f), "dong_ho_bam_gio");
            SpawnToolWith<LabelPen>(bioLab, "Bút lông dầu ghi nhãn", new Vector3(6.72f, 0.82f, 3.50f), "but_ghi_nhan");
            SpawnToolWith<Thermometer>(bioLab, "Nhiệt kế phòng thí nghiệm", new Vector3(6.84f, 0.82f, 3.50f), "nhiet_ke");
            SpawnToolWith<TestPaper>(bioLab, "Tập giấy chỉ thị pH", new Vector3(6.96f, 0.82f, 3.50f), "giay_chi_thi_ph");
            SpawnToolWith<MagneticStirrer>(bioLab, "Máy khuấy từ gia nhiệt", new Vector3(7.12f, 0.85f, 3.50f), "may_khuay_tu_gia_nhiet");

            // Khởi tạo BioLabSync ở gốc Tầng 2
            if (tang2.GetComponent<BioLabSync>() == null)
                tang2.gameObject.AddComponent<BioLabSync>();
        }

        static T SpawnToolWith<T>(Transform parent, string name, Vector3 localPos, string catalogId) where T : Component
        {
            var go = CreateToolNode(parent, name, localPos, catalogId);
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        static T AttachOrSpawnInGroup<T>(Transform parent, string childName, Vector3 fallbackPos, string catalogId) where T : Component
        {
            Transform found = parent != null ? parent.Find(childName) : null;
            if (found != null)
            {
                EnsureBoxCollider(found.gameObject, new Vector3(0.8f, 1.2f, 0.4f));
                BioCatalogLoader.AttachToolInfo(found.gameObject, catalogId);
                var c = found.GetComponent<T>();
                if (c == null) c = found.gameObject.AddComponent<T>();
                return c;
            }
            return SpawnToolWith<T>(parent, childName, fallbackPos, catalogId);
        }

        static GameObject CreateToolNode(Transform parent, string name, Vector3 localPos, string catalogId)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            EnsureBoxCollider(go, new Vector3(0.12f, 0.12f, 0.12f));
            BioCatalogLoader.AttachToolInfo(go, catalogId);
            return go;
        }

        static void EnsureBoxCollider(GameObject go, Vector3 size)
        {
            if (go == null) return;
            if (go.GetComponent<Collider>() == null)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = size;
            }
        }
    }
}

