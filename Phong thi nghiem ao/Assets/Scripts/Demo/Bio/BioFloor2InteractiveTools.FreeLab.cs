// Phần "lab tự do": mọi sinh phẩm là vật thật trên giá, mỗi bàn có bộ dụng cụ lấy thêm được, người học tự kết hợp.
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Rendering;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    public static partial class BioFloor2InteractiveTools
    {
        struct StockDef
        {
            public string id, unit;
            public double amount;
            public float temp;
            public StockDef(string id, double amount, string unit, float temp = 25f) { this.id = id; this.amount = amount; this.unit = unit; this.temp = temp; }
        }

        // Sinh phẩm dùng được trong thao tác tự do (mã trùng data/sinh/sinh_pham_mac_dinh.json), thêm nước cất
        static readonly StockDef[] AllStock =
        {
            new StockDef(BioMatterDb.Water, 2000, "mL"), new StockDef(BioMatterDb.Methylene, 500, "mL"), new StockDef(BioMatterDb.Lugol, 500, "mL"),
            new StockDef(BioMatterDb.Nacl10, 1000, "mL"), new StockDef(BioMatterDb.Nacl09, 2000, "mL"), new StockDef(BioMatterDb.Ethanol70, 5000, "mL"),
            new StockDef(BioMatterDb.ImmersionOil, 200, "mL"), new StockDef(BioMatterDb.Glucose, 1000, "g"), new StockDef(BioMatterDb.Bicarb, 1000, "g"),
            new StockDef(BioMatterDb.Agarose, 500, "g"), new StockDef(BioMatterDb.Tae, 1000, "mL"), new StockDef(BioMatterDb.Soap, 2000, "mL"),
            new StockDef(BioMatterDb.ExpiredLysis, 100, "mL"), new StockDef("sp_bo_nhuom_gram", 200, "mL"),
            new StockDef(BioMatterDb.Onion, 20, "miếng"), new StockDef(BioMatterDb.Tradescantia, 30, "lá"), new StockDef(BioMatterDb.Elodea, 15, "cành"),
            new StockDef(BioMatterDb.Potato, 100, "g"), new StockDef(BioMatterDb.Banana, 120, "g"),
            new StockDef(BioMatterDb.Liver, 500, "g", 4f), new StockDef(BioMatterDb.Milk, 4000, "mL", 4f), new StockDef(BioMatterDb.YogurtStarter, 600, "mL", 4f),
            new StockDef(BioMatterDb.Yeast, 500, "g", 4f), new StockDef(BioMatterDb.EColi, 5, "mL", 4f), new StockDef(BioMatterDb.BSubtilis, 5, "mL", 4f),
            new StockDef(BioMatterDb.H2O2, 1000, "mL", 4f), new StockDef(BioMatterDb.Broth, 400, "mL", 4f), new StockDef("sp_dem_nap_mau_6x", 25, "mL", 4f),
            new StockDef("sp_nhuom_adn_gelgreen", 1, "mL", 4f),
            new StockDef(BioMatterDb.ColdEthanol, 1000, "mL", -20f), new StockDef("sp_kit_pcr_mastermix", 6, "mL", -20f), new StockDef("sp_cap_moi_pcr", 2, "mL", -20f),
            new StockDef(BioMatterDb.NfWater, 500, "mL", -20f), new StockDef("sp_dna_ladder_100bp", 2.5, "mL", -20f), new StockDef("sp_plasmid_puc19", 0.2, "mL", -20f),
            new StockDef("sp_enzyme_ecori", 0.2, "mL", -20f), new StockDef("sp_enzyme_bamhi", 0.2, "mL", -20f), new StockDef("sp_ligaza_t4", 0.2, "mL", -20f),
            new StockDef(BioMatterDb.Dh5a, 0.5, "mL", -20f),
        };

        // Giá dùng chung ngay trong phòng thí nghiệm sinh: thứ dùng nhiều nhất
        static readonly string[] CommonStock =
        {
            BioMatterDb.Water, BioMatterDb.Methylene, BioMatterDb.Lugol, BioMatterDb.Nacl10, BioMatterDb.Nacl09, BioMatterDb.Glucose, BioMatterDb.Onion,
            BioMatterDb.Tradescantia, BioMatterDb.Potato, BioMatterDb.Banana, BioMatterDb.Soap, BioMatterDb.Liver, BioMatterDb.H2O2, BioMatterDb.Yeast,
            BioMatterDb.Milk, BioMatterDb.YogurtStarter, BioMatterDb.ColdEthanol,
        };

        static void SetupFreeLab(Transform tang2, Transform bio, Transform micro, Transform store, Transform mol)
        {
            var blockers = new List<Bounds>();
            foreach (var r in tang2.GetComponentsInChildren<Renderer>(true))
            {
                var bb = r.bounds;
                if (bb.size.x > 5f && bb.size.z > 4f) continue;  // sàn, trần cả phòng: không phải vật chắn
                blockers.Add(bb);
            }

            var templates = new GameObject("Bản mẫu dụng cụ tự do");
            templates.transform.SetParent(bio != null ? bio : tang2, false);
            var tube = MakeVessel(templates.transform, "Ống nghiệm sạch", "ong_nghiem", "Ống nghiệm", 20f, 0.0085f, 0.1f, 8f);
            var beaker = MakeVessel(templates.transform, "Cốc sạch", "coc_co_mo", "Cốc 100 mL", 100f, 0.026f, 0.07f, 15f);
            var flask = MakeVessel(templates.transform, "Bình sạch", "binh_len_men", "Bình tam giác 250 mL", 250f, 0.04f, 0.11f, 20f);
            var dropper = MakeDropper(templates.transform);
            var slide = MakeSlideTemplate(templates.transform);
            tube.SetActive(false); beaker.SetActive(false); flask.SetActive(false); dropper.SetActive(false); slide.SetActive(false);

            // Bộ lấy thêm trên từng bàn: ống nghiệm, cốc, ống nhỏ giọt, lam kính
            if (bio != null)
            {
                for (int n = 1; n <= 12; n++)
                {
                    var b = FindNamed(bio, $"Bàn sinh học {n}");
                    if (b != null) BenchKit(bio, b, n, blockers, tube, beaker, dropper, slide, flask);
                }
                var rack = BuildRack(tang2, "Giá sinh phẩm phòng sinh", ZoneMap.BioLab, 1.5f, blockers);
                if (rack != null)
                {
                    int i = 0;
                    foreach (var id in CommonStock)
                    {
                        var d = System.Array.Find(AllStock, s => s.id == id);
                        PlaceStock(bio, rack, d, i++, 6);
                    }
                }
                var lamp = FreeBenchSpot(FindNamed(bio, "Bàn sinh học 2"), blockers);
                if (lamp.found) MakeLamp(bio, lamp.pos, "Đèn cồn (bàn 2)");
                var plate = RoomSpot(bio, blockers);
                if (plate.found) MakeHotPlate(bio, plate.pos);
                var inc = FindNamed(bio, "Khu tủ ấm và nồi hấp");
                var scale = FindNamed(inc, "Cân phân tích");
                if (scale != null) Ensure<BioBenchMeter>(scale.gameObject).kind = BioBenchMeter.Kind.Balance;
                var ph = FindNamed(inc, "Máy đo pH");
                if (ph != null) Ensure<BioBenchMeter>(ph.gameObject).kind = BioBenchMeter.Kind.PhMeter;
            }
            if (mol != null)
            {
                var epp = MakeVessel(templates.transform, "Ống eppendorf sạch", "ong_eppendorf", "Ống eppendorf 1,5 mL", 1.5f, 0.005f, 0.04f, 1f);
                epp.SetActive(false);
                var spot = RoomSpot(mol, blockers);
                if (spot.found) Dispenser(mol, spot.pos, "Hộp ống eppendorf", "ong_eppendorf", epp, "ống eppendorf sạch");
                spot = RoomSpot(mol, blockers);
                if (spot.found) Dispenser(mol, spot.pos, "Hộp ống nghiệm (sinh học phân tử)", "gia_ong_nghiem", tube, "ống nghiệm sạch");
                spot = RoomSpot(mol, blockers);
                if (spot.found) Dispenser(mol, spot.pos, "Ống nhỏ giọt (sinh học phân tử)", "ong_nho_giot", dropper, "ống nhỏ giọt sạch");
            }
            TagFixtures(tang2);
            if (store != null)
            {
                var rack = BuildRack(tang2, "Giá sinh phẩm trong kho", ZoneMap.BioStore, 2.2f, blockers);
                if (rack != null)
                {
                    int i = 0;
                    foreach (var d in AllStock) PlaceStock(store, rack, d, i++, 10);
                }
            }
            if (micro != null)
            {
                var spot = RoomSpot(micro, blockers);
                if (spot.found) MakeLamp(micro, spot.pos, "Đèn cồn (phòng vi sinh)");
                spot = RoomSpot(micro, blockers);
                if (spot.found) Stock(micro, spot.pos, new StockDef(BioMatterDb.Broth, 400, "mL", 4f), "ong_canh_thang");
                spot = RoomSpot(micro, blockers);
                if (spot.found) Stock(micro, spot.pos, new StockDef(BioMatterDb.EColi, 5, "mL", 4f), "ong_thach_nghieng");
                spot = RoomSpot(micro, blockers);
                if (spot.found) Stock(micro, spot.pos, new StockDef(BioMatterDb.Water, 500, "mL"), "binh_tia_nuoc", "Bình tia nước cất");
                spot = RoomSpot(micro, blockers);
                if (spot.found) Stock(micro, spot.pos, new StockDef(BioMatterDb.Ethanol70, 500, "mL"), "binh_tia_con", "Bình tia cồn 70 %");
                spot = RoomSpot(micro, blockers);
                if (spot.found) Dispenser(micro, spot.pos, "Hộp ống nghiệm (vi sinh)", "gia_ong_nghiem", tube, "ống nghiệm sạch");
                spot = RoomSpot(micro, blockers);
                if (spot.found) MakeLoop(micro, spot.pos, "Que cấy thẳng", "que_cay_thang", false);
                spot = RoomSpot(micro, blockers);
                if (spot.found) MakeLoop(micro, spot.pos, "Que cấy nhựa dùng 1 lần", "que_cay_nhua", true);
                spot = RoomSpot(micro, blockers);
                if (spot.found) MakeSharpsBin(micro, spot.pos);
            }
        }

        // ------------------------------------------------------------------
        // Bàn
        // ------------------------------------------------------------------

        static void BenchKit(Transform room, Transform bench, int n, List<Bounds> blockers, GameObject tube, GameObject beaker, GameObject dropper, GameObject slide, GameObject flask)
        {
            var spots = new (GameObject tpl, string name, string code, string item)[]
            {
                (tube, $"Giá ống nghiệm (bàn {n})", "gia_ong_nghiem", "ống nghiệm sạch"),
                (beaker, $"Khay cốc (bàn {n})", "coc_co_mo", "cốc sạch"),
                (dropper, $"Ống nhỏ giọt (bàn {n})", "ong_nho_giot", "ống nhỏ giọt sạch"),
                (slide, $"Hộp lam kính (bàn {n})", "hop_tieu_ban", "lam kính sạch"),
            };
            foreach (var s in spots)
            {
                var f = FreeBenchSpot(bench, blockers);
                if (f.found) Dispenser(room, f.pos, s.name, s.code, s.tpl, s.item);
            }
        }

        /// <summary>Chỗ trống trên mặt bàn (tính từ bề mặt thật của bàn). Tìm được thì đánh dấu đã dùng.</summary>
        static (bool found, Vector3 pos) FreeBenchSpot(Transform bench, List<Bounds> blockers)
        {
            if (bench == null) return (false, default);
            var top = BenchTop(bench);
            if (top.size.x < 0.2f) return (false, default);
            Vector3 size = new Vector3(0.1f, 0.12f, 0.1f);
            for (float x = top.max.x - 0.08f; x > top.min.x + 0.05f; x -= 0.11f)
                for (float z = top.min.z + 0.08f; z < top.max.z - 0.05f; z += 0.11f)
                {
                    var c = new Vector3(x, top.max.y + 0.012f + size.y / 2, z);
                    var box = new Bounds(c, size);
                    if (blockers.Exists(b => b.Intersects(box))) continue;
                    blockers.Add(box);
                    return (true, new Vector3(x, top.max.y + 0.002f, z));
                }
            return (false, default);
        }

        /// <summary>Chỗ trống trên một mặt bàn bất kỳ của phòng (mặt phẳng cao 0,7 đến 1,05 m so với sàn).</summary>
        static (bool found, Vector3 pos) RoomSpot(Transform room, List<Bounds> blockers)
        {
            Vector3 size = new Vector3(0.1f, 0.12f, 0.1f);
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                var top = r.bounds;
                float h = top.max.y - 3.8f;
                if (top.size.y > 0.12f || top.size.x * top.size.z < 0.25f || h < 0.7f || h > 1.05f) continue;
                for (float x = top.min.x + 0.08f; x < top.max.x - 0.05f; x += 0.11f)
                    for (float z = top.min.z + 0.08f; z < top.max.z - 0.05f; z += 0.11f)
                    {
                        var box = new Bounds(new Vector3(x, top.max.y + 0.012f + size.y / 2, z), size);
                        if (blockers.Exists(b => b.Intersects(box))) continue;
                        blockers.Add(box);
                        return (true, new Vector3(x, top.max.y + 0.002f, z));
                    }
            }
            return (false, default);
        }

        /// <summary>Đồ cố định đã có mô hình (vòi nước, màn hình, vòi rửa mắt...): gắn mã danh mục để trỏ vào đọc được thông tin.</summary>
        static void TagFixtures(Transform tang2)
        {
            var map = new (string model, string code)[]
            {
                ("Vòi nước", "voi_nuoc_ban"), ("Màn hình nhóm", "man_hinh_ban"), ("Vòi rửa mắt", "bon_rua_mat"),
                ("Bình chữa cháy", "binh_chua_chay"), ("Màn hình bảng tin", "man_hinh_bang_tin"),
            };
            foreach (var t in tang2.GetComponentsInChildren<Transform>(true))
                foreach (var (model, code) in map)
                    if (t.name.StartsWith(model) && t.GetComponent<BioToolCatalogTag>() == null)
                    {
                        FitBoxCollider(t.gameObject, true);
                        BioCatalogLoader.AttachToolInfo(t.gameObject, code);
                    }
        }

        static void MakeLoop(Transform room, Vector3 worldPos, string name, string code, bool disposable)
        {
            var go = new GameObject(name);
            go.transform.SetParent(room, true);
            go.transform.position = worldPos;
            BioCatalogLoader.AttachToolInfo(go, code);
            Cyl(go, "Cán", 0, 0.003f, 0.2f, disposable ? "NhuaDo" : "NhuaDen", disposable ? new Color(0.2f, 0.6f, 0.9f) : Color.black);
            MakeGrabbable(go, 0.1f, true, 0.2f, 0.004f);
            Ensure<BioTool>(go).toolId = code;
            var l = go.AddComponent<InoculatingLoop>();
            l.loopToolId = code;
            l.isDisposablePlastic = disposable;
        }

        static void MakeSharpsBin(Transform room, Vector3 worldPos)
        {
            var go = new GameObject("Thùng vật sắc nhọn");
            go.transform.SetParent(room, true);
            go.transform.position = worldPos;
            BioCatalogLoader.AttachToolInfo(go, "thung_vat_sac");
            BoxPart(go, "Thùng", new Vector3(0, 0.05f, 0), new Vector3(0.09f, 0.1f, 0.09f), "NhuaVang", new Color(0.95f, 0.8f, 0.1f));
            FitBoxCollider(go, false);
            go.AddComponent<SharpsBin>();
        }

        static void MakeHotPlate(Transform room, Vector3 worldPos)
        {
            var go = new GameObject("Máy khuấy từ gia nhiệt");
            go.transform.SetParent(room, true);
            go.transform.position = worldPos;
            BioCatalogLoader.AttachToolInfo(go, "may_khuay_tu_gia_nhiet");
            BoxPart(go, "Thân", new Vector3(0, 0.03f, 0), new Vector3(0.1f, 0.06f, 0.1f), "NhuaTrang", new Color(0.9f, 0.9f, 0.92f));
            Cyl(go, "Mặt bếp", 0.06f, 0.045f, 0.004f, "Inox", Steel);
            FitBoxCollider(go, false);
            go.AddComponent<BioHotPlate>();
        }

        /// <summary>Mặt ngang lớn nhất của bàn: khối phẳng có diện tích lớn nhất.</summary>
        static Bounds BenchTop(Transform bench)
        {
            Bounds best = default;
            float area = 0;
            foreach (var r in bench.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                float a = b.size.x * b.size.z;
                if (b.size.y < 0.12f && a > area) { area = a; best = b; }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // Giá sinh phẩm
        // ------------------------------------------------------------------

        struct RackInfo { public Vector3 origin, along; public float[] tiers; public Quaternion rot; public float length; }

        /// <summary>Giá inox 4 tầng đứng sát tường, ở chỗ sàn trống tìm được trong khu.</summary>
        static RackInfo? BuildRack(Transform tang2, string name, int zone, float length, List<Bounds> blockers)
        {
            var rect = ZoneMap.Zones[zone].area;
            const float floorY = 3.8f, depth = 0.42f, height = 1.55f;
            // chạy dọc bốn tường, cách tường 0,3 m
            var cands = new List<(Vector3 c, Vector3 along)>();
            for (float t = 0.2f; t < 0.85f; t += 0.03f)
            {
                cands.Add((new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, t), 0, rect.yMax - 0.3f), Vector3.right));
                cands.Add((new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, t), 0, rect.yMin + 0.3f), Vector3.right));
                cands.Add((new Vector3(rect.xMin + 0.3f, 0, Mathf.Lerp(rect.yMin, rect.yMax, t)), Vector3.forward));
                cands.Add((new Vector3(rect.xMax - 0.3f, 0, Mathf.Lerp(rect.yMin, rect.yMax, t)), Vector3.forward));
            }
            foreach (var (c, along) in cands)
            {
                var size = along == Vector3.right ? new Vector3(length, height, depth) : new Vector3(depth, height, length);
                var center = new Vector3(c.x, floorY + 0.05f + height / 2, c.z);
                // chừa lối đi trước giá 0,8 m để không chắn cửa
                var clear = new Bounds(center, size + (along == Vector3.right ? new Vector3(0, 0, 0.5f) : new Vector3(0.5f, 0, 0)));
                if (blockers.Exists(b => b.Intersects(clear))) continue;
                blockers.Add(new Bounds(center, size));
                return Rack(tang2, name, center, size, along);
            }
            // tường nào cũng kín: đặt giữa khoảng trống rộng nhất trong phòng
            for (float x = rect.xMin + 1f; x < rect.xMax - 1f; x += 0.25f)
                for (float z = rect.yMin + 0.7f; z < rect.yMax - 0.7f; z += 0.25f)
                {
                    var size = new Vector3(length, height, depth);
                    var center = new Vector3(x, floorY + 0.05f + height / 2, z);
                    if (blockers.Exists(b => b.Intersects(new Bounds(center, size + new Vector3(0, 0, 0.3f))))) continue;
                    blockers.Add(new Bounds(center, size));
                    return Rack(tang2, name, center, size, Vector3.right);
                }
            Debug.LogWarning($"[KHKT-SINH] Không tìm được chỗ đặt {name}");
            return null;
        }

        static RackInfo Rack(Transform tang2, string name, Vector3 center, Vector3 size, Vector3 along)
        {
            var go = new GameObject(name);
            go.transform.SetParent(tang2, true);
            go.transform.position = new Vector3(center.x, center.y - size.y / 2, center.z);
            var tiers = new[] { 0.25f, 0.7f, 1.12f, 1.5f };
            foreach (float t in tiers)
            {
                var tier = BoxPart(go, "Tầng", new Vector3(0, t, 0), new Vector3(size.x, 0.02f, size.z), "Inox", Steel);
                tier.gameObject.AddComponent<BoxCollider>();  // đặt đồ trở lại giá được
                tier.gameObject.AddComponent<PlaceSurface>();
            }
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    BoxPart(go, "Trụ", new Vector3(sx * (size.x / 2 - 0.015f), size.y / 2, sz * (size.z / 2 - 0.015f)), new Vector3(0.03f, size.y, 0.03f), "Inox", Steel);
            var info = go.AddComponent<EquipmentInfo>();
            info.title = name;
            info.description = "Sinh phẩm thật: cầm lên, rót, hút hoặc gắp sang bình của bạn. Chai gốc không đổ ngược vào.";
            float length = Mathf.Max(size.x, size.z);
            // mặt trước giá là phía vào giữa phòng
            var room = ZoneMap.At(center);
            var rc = room >= 0 ? ZoneMap.Zones[room].area.center : new Vector2(center.x, center.z);
            Vector3 toRoom = along == Vector3.right ? new Vector3(0, 0, Mathf.Sign(rc.y - center.z)) : new Vector3(Mathf.Sign(rc.x - center.x), 0, 0);
            float depth = Mathf.Min(size.x, size.z);
            return new RackInfo { origin = go.transform.position - along * (length / 2 - 0.09f) + toRoom * (depth / 2 - 0.07f), along = along, tiers = tiers, length = length };
        }

        static void PlaceStock(Transform room, RackInfo? rack, StockDef d, int index, int perTier)
        {
            if (rack == null || string.IsNullOrEmpty(d.id)) return;
            var r = rack.Value;
            perTier = Mathf.Max(perTier, Mathf.FloorToInt((r.length - 0.1f) / 0.13f));
            int tier = Mathf.Min(r.tiers.Length - 1, index / perTier), col = index % perTier;
            var pos = r.origin + r.along * (col * 0.13f) + Vector3.up * (r.tiers[tier] + 0.012f);
            Stock(room, pos, d, null);
        }

        static GameObject Stock(Transform room, Vector3 worldPos, StockDef d, string catalogId, string title = null)
        {
            var m = BioMatterDb.Get(d.id);
            string name = title ?? m?.name ?? d.id;
            var go = new GameObject(name);
            go.transform.SetParent(room, true);
            go.transform.position = worldPos;
            if (!string.IsNullOrEmpty(catalogId)) BioCatalogLoader.AttachToolInfo(go, catalogId);
            var c = m != null ? new Color(m.r, m.g, m.b) : Color.white;
            bool solid = m != null && (m.phase == BioPhase.Powder || m.phase == BioPhase.Tissue);
            if (m != null && m.phase == BioPhase.Tissue)
            {
                BoxPart(go, "Khay", new Vector3(0, 0.006f, 0), new Vector3(0.11f, 0.012f, 0.09f), "NhuaTrang", new Color(0.95f, 0.95f, 0.95f));
                Part(go, "Mẫu", PrimitiveType.Sphere, new Vector3(0, 0.03f, 0), new Vector3(0.07f, 0.04f, 0.06f), "T2_MauSinhPham", c);
            }
            else
            {
                Cyl(go, "Chai", 0, 0.03f, 0.1f, solid ? "NhuaTrang" : "ThuyTinh", solid ? new Color(0.92f, 0.92f, 0.92f) : Glass);
                Cyl(go, "Dịch", 0.002f, 0.027f, 0.075f, "T2_DungDichGoc", new Color(c.r, c.g, c.b, 0.85f));
                Cyl(go, "Nắp", 0.1f, 0.018f, 0.018f, "NhuaDen", d.temp < 0 ? new Color(0.1f, 0.3f, 0.75f) : d.temp < 10 ? new Color(0.1f, 0.6f, 0.75f) : Color.black);
            }
            MakeGrabbable(go, 0.06f);
            var bt = Ensure<BioTool>(go);
            bt.toolId = catalogId ?? d.id;
            bt.displayName = name;
            if (go.GetComponent<EquipmentInfo>() == null) go.AddComponent<EquipmentInfo>().title = name;
            var v = go.AddComponent<BioVessel>();
            v.label = name;
            v.isStock = true;
            v.stockId = d.id;
            v.stockAmount = d.amount;
            v.unit = d.unit;
            v.storedTempC = d.temp;
            v.capacityMl = (float)d.amount;
            v.pourRateMlPerS = solid ? 2f : 5f;
            return go;
        }

        // ------------------------------------------------------------------
        // Dụng cụ
        // ------------------------------------------------------------------

        static GameObject MakeVessel(Transform parent, string name, string code, string label, float capMl, float radius, float height, float pour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            BioCatalogLoader.AttachToolInfo(go, code);
            Cyl(go, "Thành", 0, radius, height, "ThuyTinh", Glass);
            var fill = Cyl(go, "Mức", 0.002f, radius * 0.9f, height, "T2_DungDich", new Color(0.8f, 0.9f, 1f, 0.6f));
            var foam = Cyl(go, "Bọt", 0.002f, radius * 0.92f, 0.01f, "NhuaTrang", Color.white);
            fill.gameObject.SetActive(false);
            foam.gameObject.SetActive(false);
            MakeGrabbable(go, height * 0.7f);
            var bt = Ensure<BioTool>(go);
            bt.toolId = code;
            bt.displayName = label;
            var v = go.AddComponent<BioVessel>();
            v.label = label;
            v.capacityMl = capMl;
            v.pourRateMlPerS = pour;
            v.fillMesh = fill;
            v.fillRenderer = fill.GetComponent<Renderer>();
            v.foamMesh = foam;
            v.fillBottomY = 0.002f;
            v.fillMaxHeight = height * 0.9f;
            return go;
        }

        static GameObject MakeDropper(Transform parent)
        {
            var go = new GameObject("Ống nhỏ giọt sạch");
            go.transform.SetParent(parent, false);
            BioCatalogLoader.AttachToolInfo(go, "ong_nho_giot");
            Cyl(go, "Thân", 0, 0.004f, 0.1f, "ThuyTinh", Glass);
            Cyl(go, "Quả bóp", 0.1f, 0.007f, 0.025f, "NhuaDen", new Color(0.8f, 0.2f, 0.15f));
            MakeGrabbable(go, 0.06f, true, 0.125f, 0.007f);
            var bt = Ensure<BioTool>(go);
            bt.toolId = "ong_nho_giot";
            bt.displayName = "Ống nhỏ giọt";
            bt.kind = BioToolKind.StainDropper;
            go.AddComponent<BioDropper>();
            return go;
        }

        static GameObject MakeSlideTemplate(Transform parent)
        {
            var go = new GameObject("Lam kính sạch");
            go.transform.SetParent(parent, false);
            BioCatalogLoader.AttachToolInfo(go, "lam_kinh");
            BoxPart(go, "Lam", new Vector3(0, 0.001f, 0), new Vector3(0.025f, 0.002f, 0.075f), "ThuyTinh", Glass);
            MakeGrabbable(go, 0.004f);
            Ensure<BioTool>(go).toolId = "lam_kinh";
            go.AddComponent<GlassSlide>();
            return go;
        }

        static void Dispenser(Transform room, Vector3 worldPos, string name, string code, GameObject template, string item)
        {
            var go = new GameObject(name);
            go.transform.SetParent(room, true);
            go.transform.position = worldPos;
            BioCatalogLoader.AttachToolInfo(go, code);
            BoxPart(go, "Hộp", new Vector3(0, 0.025f, 0), new Vector3(0.09f, 0.05f, 0.08f), "NhuaTrang", new Color(0.92f, 0.94f, 0.96f));
            var mini = Part(go, "Mẫu", PrimitiveType.Cylinder, new Vector3(0, 0.06f, 0), new Vector3(0.02f, 0.02f, 0.02f), "ThuyTinh", Glass);
            FitBoxCollider(go, false);
            var d = go.AddComponent<BioDispenser>();
            d.template = template;
            d.itemName = item;
            var info = go.GetComponent<EquipmentInfo>();
            if (info == null) info = go.AddComponent<EquipmentInfo>();
            info.title = name;
            info.usage = $"Tay không bấm vào để lấy một {item}.";
        }

        static void MakeLamp(Transform room, Vector3 worldPos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(room, true);
            go.transform.position = worldPos;
            BioCatalogLoader.AttachToolInfo(go, "den_con");
            Cyl(go, "Thân", 0, 0.03f, 0.06f, "ThuyTinh", Glass);
            Cyl(go, "Cồn", 0.002f, 0.027f, 0.035f, "T2_DungDichGoc", new Color(0.8f, 0.9f, 1f, 0.6f));
            var cap = Cyl(go, "Nắp", 0.06f, 0.012f, 0.03f, "Inox", Steel);
            var flame = Part(go, "Ngọn lửa", PrimitiveType.Sphere, new Vector3(0, 0.09f, 0), new Vector3(0.018f, 0.045f, 0.018f), "T2_BongDenVang", new Color(1f, 0.7f, 0.2f));
            flame.gameObject.SetActive(false);
            flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FitBoxCollider(go, false);
            var lamp = go.AddComponent<SpiritLamp>();
            lamp.capVisual = cap;
            lamp.flameVisual = flame;
        }
    }
}
