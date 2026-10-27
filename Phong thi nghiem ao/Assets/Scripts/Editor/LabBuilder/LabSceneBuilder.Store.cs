using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Demo;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEditor;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Phòng hóa chất: 27 kệ / tủ theo nhóm bảo quản (tủ axit, tủ bazơ, tủ chất oxi hóa, kệ muối theo gốc axit,
    /// tủ chất dễ cháy, giá bình khí...). Mỗi chất trong danh mục (720) là một lọ thật đặt đúng tầng, đúng ô
    /// ghi trong data/chem/hoa_chat.json, cầm lên dùng được. Có sơ đồ kho và máy tra cứu ở cửa.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        const string ChemJsonPath = Root + "/Data/hoa_chat.json";
        const string EquipJsonPath = Root + "/Data/thiet_bi.json";

        static ChemFile chemData;
        static EquipFile equipData;
        static Dictionary<int, EquipEntry> equipByStt;

        static void LoadCatalogs()
        {
            AssetDatabase.ImportAsset(ChemJsonPath);
            AssetDatabase.ImportAsset(EquipJsonPath);
            chemData = File.Exists(ChemJsonPath) ? JsonUtility.FromJson<ChemFile>(File.ReadAllText(ChemJsonPath)) : new ChemFile();
            equipData = File.Exists(EquipJsonPath) ? JsonUtility.FromJson<EquipFile>(File.ReadAllText(EquipJsonPath)) : new EquipFile();
            equipByStt = (equipData.thiet_bi ?? new EquipEntry[0]).ToDictionary(e => e.stt);
            if (chemData.hoa_chat == null) Debug.LogWarning("[KHKT] Chưa có " + ChemJsonPath + ": chạy tools/chuyen_hoa_chat.py");
        }

        const float UnitW = 1.2f, UnitD = 0.45f, UnitH = 2.15f;

        static Material shelfSteel, shelfBoard, translucentPlastic, labelPaper;

        static void BuildChemStore(Transform root)
        {
            var g = Group("Phòng hóa chất", root);
            shelfSteel = Mat("KeTrangSon", new Color(0.93f, 0.94f, 0.95f), 0.45f, 0.3f);
            shelfBoard = Mat("TangKeTrang", new Color(0.88f, 0.9f, 0.91f), 0.4f, 0.2f);
            translucentPlastic = Mat("NhuaMo", new Color(0.95f, 0.96f, 0.97f, 0.42f), 0.6f, 0, transparent: true);
            labelPaper = Mat("NhanGiay", new Color(0.98f, 0.98f, 0.96f), 0.15f);

            // Vị trí kệ: dãy sát tường đông (11 kệ), dãy giữa hai mặt (9 + 7 kệ)
            var spots = new List<(Vector3 pos, float yaw)>();
            for (int i = 0; i < 11; i++) spots.Add((new Vector3(StoreX - 0.1f - UnitD / 2, 0, 0.85f + i * 1.25f), 0f));
            for (int i = 0; i < 9; i++) spots.Add((new Vector3(23.95f, 0, 2.2f + i * 1.25f), 180f));
            for (int i = 0; i < 9; i++) spots.Add((new Vector3(23.45f, 0, 2.2f + i * 1.25f), 0f));

            var shelves = chemData.ke ?? new ChemShelf[0];
            var items = (chemData.hoa_chat ?? new ChemEntry[0]).GroupBy(c => c.vi_tri.ke).ToDictionary(x => x.Key, x => x.ToList());
            var groups = (chemData.nhom ?? new ChemGroup[0]).ToDictionary(x => x.ma);
            var units = Group("Các kệ hóa chất", g);
            for (int i = 0; i < shelves.Length && i < spots.Count; i++)
            {
                var s = shelves[i];
                groups.TryGetValue(s.nhom, out var grp);
                items.TryGetValue(s.ma, out var list);
                ChemUnit(units, s, grp, spots[i].pos, spots[i].yaw, list ?? new List<ChemEntry>());
            }
            // Mặt sau dãy giữa: tấm ốp liền
            Box(g, "Vách giữa dãy kệ", new Vector3(23.7f, UnitH / 2, 7.2f), new Vector3(0.04f, UnitH, 9 * 1.25f), panelGrey);

            StoreMap(g, shelves, groups);
            PrepTable(g, new Vector3(21.45f, 0, 3.4f));
            var kiosk = Group("Máy tra cứu hóa chất", g, new Vector3(20.55f, 0, 9.9f), 90);
            Box(kiosk, "Chân máy", new Vector3(0, 0.5f, 0), new Vector3(0.3f, 1.0f, 0.3f), whitePlastic);
            Box(kiosk, "Đầu máy", new Vector3(0, 1.25f, 0.02f), new Vector3(0.62f, 0.45f, 0.06f), blackPlastic, true, new Vector3(-12, 0, 0));
            NoShadow(Box(kiosk, "Màn hình tra cứu", new Vector3(0, 1.255f, 0.054f), new Vector3(0.56f, 0.38f, 0.004f), screen, false, new Vector3(-12, 0, 0)));
            Label(kiosk, "TRA CỨU HÓA CHẤT", new Vector3(0, 1.36f, 0.08f), Vector3.forward, 0.04f, new Color(0.8f, 0.95f, 1f));
            Label(kiosk, "Nhấn để tìm vị trí", new Vector3(0, 1.22f, 0.075f), Vector3.forward, 0.025f, new Color(0.6f, 0.8f, 0.9f));
            kiosk.gameObject.AddComponent<SearchKiosk>();
            kiosk.gameObject.AddComponent<EquipmentInfo>().title = "Máy tra cứu hóa chất";
            kiosk.GetComponent<EquipmentInfo>().description = "Gõ tên hoặc công thức để biết hóa chất nằm ở kệ, tầng, ô nào.";

            ExhaustFan(g, new Vector3(StoreX - 0.05f, 2.9f, 13.1f));
            Extinguisher(g, new Vector3(20.35f, 0, 6.3f), Vector3.right);
            var lights = Group("Đèn trần", g);
            int k = 1;
            foreach (float x in new[] { 21.8f, 25.3f })
                foreach (float z in new[] { 2.5f, 7.5f, 12.5f })
                    LedPanel(lights, $"Đèn LED {k++}", x, z, 0, 0, false);
            RoomLights(lights, new[] { 23.5f }, new[] { 2.5f, 7.5f, 12.5f }, 8f, 3.0f);
        }

        static Color GroupColor(ChemGroup g) => g != null && ColorUtility.TryParseHtmlString(g.mau, out var c) ? c : new Color(0.3f, 0.6f, 0.6f);

        static void ChemUnit(Transform p, ChemShelf shelf, ChemGroup grp, Vector3 pos, float yaw, List<ChemEntry> items)
        {
            var g = Group($"Kệ {shelf.ma}: {shelf.ten}", p, pos, yaw);
            var info = g.gameObject.AddComponent<EquipmentInfo>();
            info.title = $"Kệ {shelf.ma} · {shelf.ten}";
            info.description = $"{shelf.so_luong} chất, {shelf.so_tang} tầng × {shelf.so_o} ô. Tầng 1 ở dưới cùng; mã ô ghi trên gờ tầng.";
            info.usage = "Lấy xong để lại đúng ô; không dùng hóa chất mất nhãn.";
            var color = GroupColor(grp);
            var trim = Mat("Vien" + shelf.nhom, color, 0.4f);
            bool cabinetStyle = shelf.kieu_tu != "ke" && shelf.kieu_tu != "khi";
            bool gas = shelf.kieu_tu == "khi";
            const float w = UnitW, d = UnitD, h = UnitH;
            // khung: trụ ở 4 góc (kệ hở) hoặc vách hai bên (tủ an toàn)
            if (cabinetStyle)
            {
                Box(g, "Vách trái", new Vector3(0, h / 2, -w / 2 + 0.01f), new Vector3(d, h, 0.02f), shelfSteel);
                Box(g, "Vách phải", new Vector3(0, h / 2, w / 2 - 0.01f), new Vector3(d, h, 0.02f), shelfSteel);
                Box(g, "Nóc tủ", new Vector3(0, h - 0.01f, 0), new Vector3(d, 0.02f, w), shelfSteel);
                Box(g, "Viền màu trái", new Vector3(-d / 2 - 0.002f, h / 2, -w / 2 + 0.01f), new Vector3(0.006f, h, 0.024f), trim, false);
                Box(g, "Viền màu phải", new Vector3(-d / 2 - 0.002f, h / 2, w / 2 - 0.01f), new Vector3(0.006f, h, 0.024f), trim, false);
                foreach (float z in new[] { -w / 4, w / 4 })
                    NoShadow(Box(g, "Cửa kính", new Vector3(-d / 2 - 0.012f, h / 2 + 0.06f, z), new Vector3(0.006f, h - 0.2f, w / 2 - 0.02f), cabinetGlass, false));
                string warn = shelf.kieu_tu == "tu_axit" ? "ĂN MÒN" : shelf.kieu_tu == "tu_bazo" ? "ĂN DA" : shelf.kieu_tu == "tu_chay" ? "DỄ CHÁY · CẤM LỬA" : shelf.kieu_tu == "tu_oxh" ? "CHẤT OXI HÓA" : "";
                if (warn.Length > 0)
                {
                    Box(g, "Biển cảnh báo", new Vector3(-d / 2 - 0.018f, 0.06f, 0), new Vector3(0.004f, 0.08f, 0.6f), trim, false);
                    Label(g, warn, new Vector3(-d / 2 - 0.022f, 0.06f, 0), Vector3.left, 0.04f, Color.white);
                }
                if (shelf.kieu_tu == "tu_chay" || shelf.kieu_tu == "tu_axit")
                    Cyl(g, "Ống thông gió tủ", new Vector3(0.1f, h + (H - h) / 2, w / 2 - 0.15f), 0.045f, H - h, Mat("OngNhua", new Color(0.85f, 0.85f, 0.82f), 0.3f));
            }
            else
            {
                foreach (float x in new[] { -d / 2 + 0.015f, d / 2 - 0.015f })
                    foreach (float z in new[] { -w / 2 + 0.015f, w / 2 - 0.015f })
                        Box(g, "Trụ kệ", new Vector3(x, h / 2, z), new Vector3(0.03f, h, 0.03f), shelfSteel);
            }
            Box(g, "Tấm sau", new Vector3(d / 2 - 0.005f, h / 2, 0), new Vector3(0.01f, h, w), shelfSteel);

            int levels = shelf.so_tang, slots = shelf.so_o;
            float pitch = gas ? 0.52f : 0.35f;
            for (int i = 0; i < levels; i++)
            {
                float y = 0.08f + i * pitch;
                Box(g, $"Tầng {i + 1}", new Vector3(0, y, 0), new Vector3(d, 0.02f, w), shelfBoard);
                Box(g, "Gờ chắn", new Vector3(-d / 2 + 0.01f, y + 0.025f, 0), new Vector3(0.008f, 0.03f, w), trim, false);
                Label(g, $"{shelf.ma}-{i + 1}", new Vector3(-d / 2 + 0.004f, y + 0.026f, -w / 2 + 0.07f), Vector3.left, 0.014f, Color.white);
            }
            // Biển tên kệ
            Box(g, "Biển kệ", new Vector3(-d / 2 - 0.008f, h + 0.1f, 0), new Vector3(0.012f, 0.18f, w - 0.04f), trim, false);
            Label(g, shelf.ma, new Vector3(-d / 2 - 0.016f, h + 0.13f, -w / 2 + 0.14f), Vector3.left, 0.075f, Color.white);
            Label(g, Wrap(shelf.ten, 30), new Vector3(-d / 2 - 0.016f, h + 0.1f, 0.08f), Vector3.left, 0.03f, Color.white);

            foreach (var e in items)
            {
                int lvl = e.vi_tri.tang - 1, o = e.vi_tri.o - 1;
                float y = 0.08f + lvl * pitch + 0.011f;
                float z = -w / 2 + (o + 0.5f) * w / slots;
                ChemBottleObject(g, e, new Vector3(-0.04f, y, z), color);
            }
        }

        static string Wrap(string s, int max)
        {
            if (s.Length <= max) return s;
            int cut = s.LastIndexOf(' ', max);
            if (cut < 10) return s;
            return s.Substring(0, cut) + "\n" + s.Substring(cut + 1);
        }

        static Material GasColor(string formula)
        {
            switch (formula)
            {
                case "O2": return Mat("BinhKhiXanh", new Color(0.15f, 0.35f, 0.75f), 0.5f, 0.4f);
                case "H2": return Mat("BinhKhiDo", new Color(0.75f, 0.12f, 0.1f), 0.5f, 0.4f);
                case "N2": return Mat("BinhKhiDen", new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.4f);
                case "Cl2": return Mat("BinhKhiVangLuc", new Color(0.55f, 0.6f, 0.15f), 0.5f, 0.4f);
                case "NH3": return Mat("BinhKhiVang", new Color(0.9f, 0.75f, 0.1f), 0.5f, 0.4f);
                case "CO2": return Mat("BinhKhiXam", new Color(0.45f, 0.47f, 0.5f), 0.5f, 0.4f);
                case "C2H2": return Mat("BinhKhiTrang", new Color(0.92f, 0.92f, 0.9f), 0.5f, 0.4f);
                default: return Mat("BinhKhiNau", new Color(0.5f, 0.35f, 0.25f), 0.5f, 0.4f);
            }
        }

        /// <summary>Một lọ / chai / bình khí của hóa chất e, gốc ở đáy, nhãn nhìn về -x (mặt trước kệ).</summary>
        static Transform ChemBottleObject(Transform p, ChemEntry e, Vector3 pos, Color groupColor)
        {
            var root = Group($"Hóa chất {e.stt}: {e.ten}", p, pos);
            string labelText = string.IsNullOrEmpty(e.cong_thuc) || e.cong_thuc.Length > 14 ? ShortName(e.ten) : e.cong_thuc;
            var cb = root.gameObject.AddComponent<ChemicalBottle>();
            cb.stt = e.stt;
            var gr = root.gameObject.AddComponent<Grabbable>();
            float labelY, labelR;
            switch (e.dang_chua)
            {
                case "binh_khi":
                {
                    var m = Lathe("BinhKhiNho", 18, V(0, 0), V(0.034f, 0), V(0.034f, 0), V(0.035f, 0.01f), V(0.035f, 0.27f), V(0.024f, 0.3f), V(0.01f, 0.31f), V(0, 0.31f));
                    MeshObj(root, "Vỏ bình khí", m, Vector3.zero, GasColor(e.cong_thuc), true);
                    Cyl(root, "Van bình khí", new Vector3(0, 0.33f, 0), 0.01f, 0.04f, steel);
                    Box(root, "Tay van", new Vector3(0, 0.355f, 0), new Vector3(0.05f, 0.008f, 0.012f), darkMetal, false);
                    Cyl(root, "Vòi xả khí", new Vector3(-0.02f, 0.335f, 0), 0.004f, 0.03f, steel, false, new Vector3(0, 0, 90));
                    cb.stockAmount = 0.5f;
                    gr.kind = "binh_khi";
                    gr.gripHeightM = 0.2f;
                    gr.massG = 900f;
                    labelY = 0.16f; labelR = 0.036f;
                    break;
                }
                case "lo_rong":
                {
                    NoShadow(MeshObj(root, "Lọ", WideJar(), Vector3.zero, translucentPlastic, true));
                    var powder = Cyl(root, "Bột", new Vector3(0, 0.04f, 0), 0.036f, 0.07f, Mat("BotHoaChat", new Color(0.95f, 0.95f, 0.93f), 0.1f));
                    Cyl(root, "Nắp", new Vector3(0, 0.135f, 0), 0.037f, 0.025f, Mat("Nap" + e.nhom, groupColor * 0.9f, 0.4f));
                    var c = root.gameObject.AddComponent<LabContainer>();
                    c.label = e.ten; c.capacityMl = 150f; c.reactive = false;
                    c.mouth = Child(root, "Miệng", new Vector3(0, 0.13f, 0));
                    cb.container = c;
                    cb.powder = powder.GetComponent<Renderer>();
                    cb.stockAmount = 60f;
                    gr.kind = "lo_rong";
                    gr.gripHeightM = 0.09f;
                    gr.massG = 60f;
                    labelY = 0.065f; labelR = 0.041f;
                    break;
                }
                case "lo_nho_giot":
                {
                    MeshObj(root, "Lọ", DropperBottleMesh(), Vector3.zero, amber, true);
                    Cyl(root, "Núm cao su", new Vector3(0, 0.075f, 0), 0.007f, 0.028f, rubber);
                    var c = root.gameObject.AddComponent<LabContainer>();
                    c.label = e.ten; c.capacityMl = 40f; c.reactive = false;
                    c.mouth = Child(root, "Miệng", new Vector3(0, 0.062f, 0));
                    var dr = root.gameObject.AddComponent<Dropper>();
                    dr.source = c;
                    dr.reagent = e.ten;
                    dr.tip = Child(root, "Đầu nhỏ giọt", new Vector3(0, 0.09f, 0));
                    cb.container = c;
                    cb.stockAmount = 30f;
                    gr.kind = "lo_nho_giot";
                    gr.gripHeightM = 0.05f;
                    gr.massG = 25f;
                    labelY = 0.03f; labelR = 0.0185f;
                    break;
                }
                case "hop_giay":
                {
                    Box(root, "Hộp giấy", new Vector3(0, 0.035f, 0), new Vector3(0.06f, 0.07f, 0.09f), Mat("HopGiay", new Color(0.95f, 0.92f, 0.8f), 0.2f));
                    var tip = Box(root, "Đầu giấy", new Vector3(0, 0.085f, 0), new Vector3(0.006f, 0.04f, 0.012f), Mat("GiayThu", new Color(0.95f, 0.88f, 0.55f), 0.1f), false);
                    var tp = root.gameObject.AddComponent<TestPaper>();
                    tp.tipRenderer = tip.GetComponent<Renderer>();
                    string n = ChemicalCatalog.Fold(e.ten);
                    tp.kind = n.Contains("quy") ? TestPaper.Kind.Litmus : n.Contains("coban") || n.Contains("am") ? TestPaper.Kind.CobaltChloride : TestPaper.Kind.Universal;
                    if (tp.kind == TestPaper.Kind.Litmus) tp.dryColor = new Color(0.55f, 0.35f, 0.6f);
                    if (tp.kind == TestPaper.Kind.CobaltChloride) tp.dryColor = new Color(0.3f, 0.45f, 0.85f);
                    gr.kind = "giay";
                    gr.gripHeightM = 0.06f;
                    gr.massG = 20f;
                    labelY = 0.035f; labelR = 0.031f;
                    break;
                }
                default:  // chai, chai nâu
                {
                    bool brown = e.dang_chua == "chai_nau";
                    var glass = MeshObj(root, "Chai", ReagentBottle(), Vector3.zero, brown ? amber : labGlass, true);
                    if (!brown) NoShadow(glass);
                    var fill = LiquidObject(root, "Dung dịch", new[] { V(0.0305f, 0.003f), V(0.031f, 0.006f), V(0.031f, 0.1f), V(0.028f, 0.113f), V(0.012f, 0.128f), V(0.011f, 0.148f) },
                                            Mat("DungDichChai", new Color(0.85f, 0.92f, 1f, 0.35f), 0.9f, 0, transparent: true));
                    Cyl(root, "Nắp", new Vector3(0, 0.16f, 0), 0.015f, 0.022f, Mat("Nap" + e.nhom, groupColor * 0.9f, 0.4f));
                    var c = root.gameObject.AddComponent<LabContainer>();
                    c.label = e.ten; c.capacityMl = 320f; c.reactive = false; c.isStock = true;
                    c.fill = fill;
                    c.liquidRenderer = fill.GetComponent<Renderer>();
                    c.mouth = Child(root, "Miệng", new Vector3(0, 0.15f, 0));
                    c.bottomRadiusM = 0.03f;
                    cb.container = c;
                    cb.stockAmount = 250f;
                    gr.kind = "chai";
                    gr.gripHeightM = 0.12f;
                    gr.massG = 180f;
                    labelY = 0.06f; labelR = 0.0335f;
                    break;
                }
            }
            // Nhãn: dải màu nhóm + chữ công thức, quay ra mặt trước kệ (-x)
            Box(root, "Nhãn", new Vector3(-labelR, labelY, 0), new Vector3(0.002f, 0.036f, 0.04f), labelPaper, false);
            Box(root, "Dải màu nhãn", new Vector3(-labelR - 0.0005f, labelY + 0.015f, 0), new Vector3(0.002f, 0.006f, 0.04f), Mat("Vien" + e.nhom, groupColor, 0.4f), false);
            Label(root, labelText, new Vector3(-labelR - 0.0015f, labelY - 0.002f, 0), Vector3.left, labelText.Length > 8 ? 0.0068f : 0.0095f, new Color(0.08f, 0.1f, 0.14f));
            Label(root, e.stt.ToString(), new Vector3(-labelR - 0.0015f, labelY - 0.013f, 0), Vector3.left, 0.0045f, new Color(0.35f, 0.38f, 0.42f));
            return root;
        }

        static string ShortName(string name)
        {
            if (name.Length <= 12) return name;
            var words = name.Split(' ');
            string s = "";
            foreach (var w in words)
            {
                if ((s + " " + w).Trim().Length > 12) break;
                s = (s + " " + w).Trim();
            }
            return s.Length > 0 ? s : name.Substring(0, 12);
        }

        static void StoreMap(Transform p, ChemShelf[] shelves, Dictionary<string, ChemGroup> groups)
        {
            // Sơ đồ kho trên tường bắc phòng hóa chất (nhìn vào trong phòng)
            var g = Group("Sơ đồ kho hóa chất", p, new Vector3(23.7f, 1.55f, LabZ - 0.11f));
            Box(g, "Bảng sơ đồ", Vector3.zero, new Vector3(3.0f, 1.25f, 0.02f), signWhite, false);
            Box(g, "Dải tiêu đề", new Vector3(0, 0.56f, -0.012f), new Vector3(3.0f, 0.13f, 0.004f), accentDark, false);
            Label(g, "SƠ ĐỒ KHO HÓA CHẤT", new Vector3(0, 0.56f, -0.016f), Vector3.back, 0.06f, Color.white);
            int i = 0;
            foreach (var s in shelves)
            {
                int col = i / 10, row = i % 10;
                groups.TryGetValue(s.nhom, out var grp);
                var pos = new Vector3(-1.38f + col * 1.0f, 0.4f - row * 0.1f, -0.012f);
                Box(g, "Ô màu", pos + new Vector3(0.03f, 0, 0), new Vector3(0.05f, 0.06f, 0.004f), Mat("Vien" + s.nhom, GroupColor(grp), 0.4f), false);
                var lab = Label(g, $"{s.ma}  {Shorten(s.ten, 30)}", pos + new Vector3(0.09f, 0, -0.004f), Vector3.back, 0.026f, new Color(0.1f, 0.12f, 0.16f));
                lab.GetComponent<TextMesh>().anchor = TextAnchor.MiddleLeft;
                i++;
            }
            Label(g, "Tường đông: K01–K11 (vô cơ) · Dãy giữa mặt đông: K12–K20 · Dãy giữa mặt tây: K21–K27", new Vector3(0, -0.56f, -0.016f), Vector3.back, 0.024f, new Color(0.3f, 0.35f, 0.4f));
        }

        static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static void PrepTable(Transform p, Vector3 pos)
        {
            // Bàn pha chế ở lối vào phòng hóa chất, bồn rửa ở đầu +z
            var g = Group("Bàn pha chế", p, pos);
            const float top = 0.9f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, -0.2f), new Vector3(0.72f, 0.08f, 1.3f), darkMetal);
            Box(g, "Thân tủ", new Vector3(0, 0.47f, -0.2f), new Vector3(0.78f, 0.78f, 1.36f), cabinet);
            HollowCabinet(g, "Tủ dưới bồn", new Vector3(0, 0.08f, 0.7f), new Vector3(0.78f, 0.78f, 0.38f));
            TopWithSink(g, "Mặt bàn", 0.82f, 1.84f, top, new Vector2(0f, 0.68f), new Vector2(0.4f, 0.36f), 0.22f);
            Faucet(g, "Vòi nước", new Vector3(0, top, 0.89f), Vector3.back, 0.3f, 0.15f);
            Box(g, "Sổ theo dõi hóa chất", new Vector3(-0.15f, top + 0.006f, -0.1f), new Vector3(0.21f, 0.012f, 0.3f), Mat("BiaSo", new Color(0.15f, 0.3f, 0.55f), 0.2f), false, new Vector3(0, 12, 0));
            NoShadow(MeshObj(g, "Bình định mức", VolumetricFlask(), new Vector3(0.05f, top, 0.2f), labGlass));
            Cyl(g, "Vạch định mức", new Vector3(0.05f, top + 0.175f, 0.2f), 0.0094f, 0.0008f,
                Mat("VachTrang", new Color(0.97f, 0.97f, 0.97f), 0.4f, 0, emission: new Color(0.35f, 0.35f, 0.35f)));
            Stool(g, "Ghế đẩu", new Vector3(0.65f, 0, -0.3f));
            Balance(g, "Cân điện tử", new Vector3(0.05f, top, -0.55f), 34, 2);
        }
    }
}
