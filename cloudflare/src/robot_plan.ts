// Kế hoạch robot: kỹ năng có sẵn và kiểm tra từng bước (bản Cloudflare của server/ai/robot_plan.py, giữ nguyên quy tắc).

export const SKILLS: Record<string, string> = {
  di_toi: "đi tới cạnh vật (doi_tuong)",
  chi_vao: "chỉ tay vào vật để minh họa (doi_tuong)",
  cam: "cầm vật lên và giữ (doi_tuong)",
  dat: "đặt vật đang cầm lên chỗ (dich): mã chỗ đặt, mã vật chứa để đặt cạnh, hoặc 'ban_nguoi_dung'",
  tra_ve: "trả vật (doi_tuong) về chỗ cất ban đầu (kệ, giá)",
  lay_hoa_chat: "vào phòng hóa chất lấy lọ (hoa_chat: tên hoặc công thức) mang ra bàn người dùng",
  rot: "rót so_luong ml từ vật chứa (doi_tuong) vào vật chứa (dich)",
  nho_giot: "nhỏ so_luong giọt từ lọ (doi_tuong) vào vật chứa (dich)",
  xuc: "xúc so_luong thìa chất rắn từ lọ (doi_tuong) vào vật chứa (dich)",
  hut_pipet: "dùng pipet bầu 25 ml lấy chính xác so_luong ml (mặc định 25) từ (doi_tuong) cho vào (dich)",
  tia_nuoc: "tia so_luong ml nước cất vào vật chứa (dich): pha loãng, tráng",
  suc_khi: "sục khí từ bình khí (doi_tuong, mã hoa_chat:...) vào dung dịch (dich) trong so_luong giây; khí độc robot tự đưa vào tủ hút",
  do_bo: "đổ bỏ dung dịch trong (doi_tuong) vào cốc thải / thùng chất thải (so_luong ml, 0 là đổ hết)",
  lac: "lắc đều vật chứa (doi_tuong) trong so_luong giây",
  khuay: "đặt (doi_tuong) lên máy khuấy từ, khuấy so_luong giây",
  dun_nong: "đun (doi_tuong) tới so_luong °C (mặc định 70): ống nghiệm hơ trên đèn cồn, cốc đặt lên bếp; dich là mã bếp nếu muốn chọn",
  ly_tam: "cho ống nghiệm (doi_tuong) vào máy ly tâm để kết tủa lắng xuống đáy",
  dua_vao_tu_hut: "mang (doi_tuong) vào tủ hút và bật quạt hút",
  doi: "chờ so_luong giây để quan sát hiện tượng",
  quan_sat: "nhìn và nói rõ trong (doi_tuong) có gì: chất, pH, nhiệt độ, kết tủa, khí, hiện tượng vừa xảy ra",
  do_ph: "đo pH dung dịch trong (doi_tuong) bằng máy đo pH",
  do_nhiet_do: "đo nhiệt độ (doi_tuong) bằng nhiệt kế điện tử",
  do_do_dan: "đo độ dẫn điện (doi_tuong)",
  thu_quy: "nhúng giấy quỳ tím vào (doi_tuong)",
  thu_giay_ph: "nhúng giấy chỉ thị pH vạn năng vào (doi_tuong)",
  can: "cân vật (doi_tuong) trên cân điện tử",
  can_chat_ran: "đặt (dich) lên cân, trừ bì rồi xúc chất rắn từ lọ (doi_tuong) tới đủ so_luong gam",
  phan_tich: "đặt mẫu (doi_tuong) vào máy phân tích (dich: mã may_...; bỏ trống thì máy gần nhất) và đọc kết quả",
  bat_tat: "bật hoặc tắt thiết bị (doi_tuong): đèn cồn, bếp, máy khuấy, quạt tủ hút",
  bat_tu_hut: "bật quạt tủ hút và quạt hút của phòng",
  tat_lua: "tắt mọi nguồn lửa và bếp",
  don_ban: "dọn bàn: đổ dung dịch thừa, trả dụng cụ, lau chỗ tràn",
  rua_mat: "dẫn tới vòi rửa mắt khẩn cấp",
  chuan_do: "làm mẫu trọn vẹn bài chuẩn độ đang chọn ở bàn 1: lắp buret, nạp, đọc V đầu, pipet mẫu, chỉ thị, chuẩn độ tới điểm cuối, tính nồng độ",
  thi_nghiem_mau: "làm một thí nghiệm mẫu có sẵn (ghi khóa vào hoa_chat)",
  noi: "nói một câu (loi) trong lúc làm, ví dụ giải thích bước đang minh họa",
};

// Khóa phải trùng RobotDemos.cs trong Unity
export const TEMPLATES: Record<string, string> = {
  nhan_biet_cu2: "Nhận biết ion Cu²⁺ bằng NaOH (kết tủa xanh lam)",
  nhan_biet_cl: "Nhận biết ion Cl⁻ bằng AgNO₃ (kết tủa trắng)",
  nhan_biet_so4: "Nhận biết ion SO₄²⁻ bằng BaCl₂ (kết tủa trắng)",
  nhan_biet_fe3: "Nhận biết ion Fe³⁺ bằng KSCN (đỏ máu)",
  chi_thi_axit_bazo: "Phenolphtalein trong bazơ và phản ứng trung hòa",
  kim_loai_axit: "Kẽm tác dụng với axit clohiđric (sủi bọt H₂)",
  do_ph_dung_dich: "So sánh pH của axit, bazơ và nước cất bằng máy đo pH",
  ly_tam_ket_tua: "Tạo kết tủa Cu(OH)₂ rồi ly tâm tách rắn - lỏng",
  glucozo_cu_oh2: "Glucozơ với Cu(OH)₂: phức xanh lam, đun ra Cu₂O đỏ gạch",
  phan_tich_nuoc: "Phân tích kim loại trong nước giếng và nước thải bằng AAS",
  chuan_do: "Chuẩn độ axit - bazơ trọn vẹn",
};

const NEEDS_OBJECT = new Set(["di_toi", "chi_vao", "cam", "tra_ve", "rot", "nho_giot", "xuc", "hut_pipet", "suc_khi", "do_bo", "lac", "khuay",
  "dun_nong", "ly_tam", "dua_vao_tu_hut", "quan_sat", "do_ph", "do_nhiet_do", "do_do_dan", "thu_quy", "thu_giay_ph",
  "can", "can_chat_ran", "phan_tich", "bat_tat"]);
const NEEDS_DEST = new Set(["rot", "nho_giot", "xuc", "hut_pipet", "tia_nuoc", "suc_khi", "can_chat_ran"]);
const OPTIONAL_DEST = new Set(["phan_tich", "dun_nong"]);
const LIMITS: Record<string, [number, number]> = { rot: [2, 100], nho_giot: [1, 20], xuc: [1, 5], hut_pipet: [25, 25], tia_nuoc: [5, 100], suc_khi: [5, 30],
  lac: [4, 30], khuay: [15, 120], doi: [5, 60], dun_nong: [70, 100], can_chat_ran: [1, 20], do_bo: [0, 1000] };

export const PLAN_SCHEMA = {
  type: "object",
  properties: {
    tra_loi: { type: "string" },
    ke_hoach: {
      type: "array",
      items: {
        type: "object",
        properties: {
          ky_nang: { type: "string", enum: Object.keys(SKILLS) },
          doi_tuong: { type: "string" }, dich: { type: "string" }, so_luong: { type: "number" }, hoa_chat: { type: "string" }, loi: { type: "string" },
        },
        required: ["ky_nang", "doi_tuong", "dich", "so_luong", "hoa_chat", "loi"],
        additionalProperties: false,
      },
    },
  },
  required: ["tra_loi", "ke_hoach"],
  additionalProperties: false,
};

export const MAX_STEPS = 20;

export const SYSTEM_PLAN =
  "Bạn là bộ não của một robot trợ lý hình người trong phòng thí nghiệm hóa học ảo của trường THPT Việt Nam. " +
  "Bạn KHÔNG nhìn thấy cảnh 3D; bạn chỉ biết ẢNH CHỤP CẢNH dạng chữ đi kèm (danh sách vật với mã vật, tên, loại, " +
  "khoảng cách tới người dùng, đang chứa gì). Hãy trả lời học sinh bằng tiếng Việt ngắn gọn (tối đa 3 câu) và, nếu cần " +
  "hành động, lập KẾ HOẠCH là danh sách kỹ năng theo đúng thứ tự thực hiện. Kỹ năng có sẵn:\n" +
  Object.entries(SKILLS).map(([k, v]) => `- ${k}: ${v}`).join("\n") + "\n" +
  "Thí nghiệm mẫu có sẵn (kỹ năng thi_nghiem_mau, ghi khóa vào hoa_chat):\n" +
  Object.entries(TEMPLATES).map(([k, v]) => `- ${k}: ${v}`).join("\n") + "\n" +
  "Quy tắc:\n" +
  "- Học sinh xin làm mẫu cả một thí nghiệm có trong danh sách mẫu thì dùng MỘT bước thi_nghiem_mau (hoặc chuan_do), " +
  "không tự chia nhỏ. Thí nghiệm khác thì tự ghép các kỹ năng.\n" +
  "- Robot tự đi lấy dụng cụ cần cho kỹ năng (máy đo pH, nhiệt kế, máy đo độ dẫn, giấy quỳ, giấy pH, pipet, bình tia, " +
  "cân, máy ly tâm, máy khuấy từ, bếp, đèn cồn, tủ hút) và dùng xong tự cất: không cần mã vật cho các dụng cụ đó.\n" +
  "- doi_tuong và dich PHẢI là mã vật có trong ảnh chụp cảnh (hoặc 'ban_nguoi_dung' cho dich của dat); trường nào " +
  "không dùng thì để chuỗi rỗng, so_luong không dùng thì để 0.\n" +
  "- Hóa chất chưa có trên bàn thì dùng lay_hoa_chat trước (ghi tên TIẾNG VIỆT theo danh pháp trong SGK như " +
  "'axit oxalic', 'kali pemanganat', 'axit sunfuric', hoặc công thức như H2C2O4), sau đó gọi lọ đó bằng mã " +
  "'hoa_chat:<đúng chuỗi đã ghi ở hoa_chat>'.\n" +
  "- Nếu phản ứng cần môi trường hay chất xúc tác (ví dụ KMnO4 oxi hóa axit oxalic cần H2SO4 loãng) thì lấy thêm chất đó " +
  "và cho vào đúng thứ tự.\n" +
  "- Lời giải thích (noi, tra_loi) phải đúng hóa học: nêu hiện tượng thật sẽ thấy (ví dụ dung dịch thuốc tím mất màu, " +
  "sủi bọt khí CO2), không đoán bừa; nếu không chắc thì để robot quan_sat rồi nói theo hiện tượng quan sát được.\n" +
  "- Dùng ống nghiệm hoặc cốc đang trống trong ảnh chụp cảnh cho phản ứng mới. Rót lượng nhỏ (ống nghiệm 1-5 ml, cốc 10-50 ml).\n" +
  "- Sau khi trộn nên lac rồi quan_sat để học sinh thấy hiện tượng; xen kỹ năng noi để giải thích từng bước.\n" +
  "- An toàn trường phổ thông: chất sinh khí độc làm trong tủ hút; không pha nước vào axit đặc; không đun chất dễ cháy " +
  "trên lửa trần; dùng dung dịch loãng, lượng nhỏ.\n" +
  "- CÁC THÍ NGHIỆM TRONG SGK HÓA 10-12 ĐỀU ĐƯỢC LÀM, không được từ chối, ví dụ: KMnO4 với axit oxalic / H2O2 / FeSO4 " +
  "(môi trường H2SO4 loãng), kim loại với axit loãng, nhận biết ion, trung hòa, chuẩn độ, điện phân dung dịch, " +
  "glucozơ với Cu(OH)2, tốc độ phản ứng, cân bằng hóa học. Chỉ từ chối (ke_hoach rỗng, giải thích nhẹ nhàng) khi yêu cầu " +
  "chế tạo chất nổ, pháo, chất độc / ma túy, hoặc tạo lượng lớn khí độc ngoài tủ hút.\n" +
  `- Câu hỏi chỉ cần trả lời thì ke_hoach rỗng. Tối đa ${MAX_STEPS} bước.`;

const foldK = (s: string) => (s || "").replace(/đ/g, "d").replace(/Đ/g, "D").normalize("NFKD").replace(/\p{Mn}/gu, "").toLowerCase().trim();

export function findTemplate(name: string): string | null {
  const q = foldK(name).replace(/ /g, "_");
  if (q in TEMPLATES) return q;
  const words = foldK(name).replace(/_/g, " ").split(/\s+/).filter((w) => w.length > 1);
  let best: string | null = null, best_hits = 0;
  for (const [k, title] of Object.entries(TEMPLATES)) {
    const hay = foldK(title) + " " + k.replace(/_/g, " ");
    const hits = words.filter((w) => hay.includes(w)).length;
    if (hits > best_hits) { best = k; best_hits = hits; }
  }
  return words.length && best_hits >= Math.max(1, Math.floor((words.length + 1) / 2)) ? best : null;
}

export interface Buoc { ky_nang: string; doi_tuong: string; dich: string; so_luong: number; stt: number; loi: string }

const laSoChuoi = (s: string) => /^\d+$/.test(s);

export function validate(plan: any[], object_ids: Set<string>, find_chemical: (q: string) => any): [Buoc[], string[]] {
  const out: Buoc[] = [], dropped: string[] = [];
  const fetched = new Map<string, number>();
  const known = (v: string) => object_ids.has(v) || v.startsWith("hoa_chat:");
  plan.slice(0, MAX_STEPS).forEach((s: any, i: number) => {
    s = s && typeof s === "object" ? s : {};
    const k = String(s.ky_nang ?? "");
    if (!(k in SKILLS)) { dropped.push(`bước ${i + 1}: kỹ năng lạ '${k}'`); return; }
    const step: Buoc = { ky_nang: k, doi_tuong: String(s.doi_tuong || ""), dich: String(s.dich || ""), so_luong: Number(s.so_luong || 0) || 0, stt: 0, loi: String(s.loi || "").slice(0, 300) };
    if (k === "thi_nghiem_mau") {
      const key = findTemplate(String(s.hoa_chat || "") || step.doi_tuong);
      if (key === null) { dropped.push(`bước ${i + 1}: không có thí nghiệm mẫu '${s.hoa_chat || step.doi_tuong}'`); return; }
      out.push({ ...step, doi_tuong: key, dich: "", so_luong: 0 });
      return;
    }
    if (k === "lay_hoa_chat") {
      const c = find_chemical(String(s.hoa_chat ?? ""));
      if (!c) { dropped.push(`bước ${i + 1}: không tìm thấy hóa chất '${s.hoa_chat ?? ""}'`); return; }
      step.stt = c.stt;
      step.doi_tuong = `hoa_chat:${c.stt}`;
      fetched.set(String(s.hoa_chat ?? "").trim().toLowerCase(), c.stt);
      fetched.set(String(c.stt), c.stt);
    }
    let missing = "";
    for (const field of ["doi_tuong", "dich"] as const) {
      const v = step[field];
      if (v.startsWith("hoa_chat:")) {
        const key = v.split(":").slice(1).join(":").trim().toLowerCase();
        if (fetched.has(key)) step[field] = `hoa_chat:${fetched.get(key)}`;
        else if (laSoChuoi(key)) step[field] = `hoa_chat:${key}`;
        else {
          const c = find_chemical(key);
          step[field] = c ? `hoa_chat:${c.stt}` : "";
          missing = missing || (c ? "" : key);
        }
      }
    }
    if (missing) { dropped.push(`bước ${i + 1}: hóa chất '${missing}' không có trong danh mục`); return; }
    if (k === "tia_nuoc" && !step.dich && known(step.doi_tuong)) step.dich = step.doi_tuong;
    if (NEEDS_OBJECT.has(k) && !known(step.doi_tuong)) { dropped.push(`bước ${i + 1}: mã vật '${step.doi_tuong}' không có trong cảnh`); return; }
    if (NEEDS_DEST.has(k) && !known(step.dich)) { dropped.push(`bước ${i + 1}: nơi nhận '${step.dich}' không có trong cảnh`); return; }
    if (OPTIONAL_DEST.has(k) && step.dich && !object_ids.has(step.dich)) step.dich = "";
    if (k in LIMITS) {
      const [md, lim] = LIMITS[k];
      step.so_luong = Math.max(0, Math.min(step.so_luong || md, lim));
    }
    if (k === "dat" && !(object_ids.has(step.dich) || step.dich === "ban_nguoi_dung" || step.dich === "")) step.dich = "ban_nguoi_dung";
    out.push(step);
  });
  if (plan.length > MAX_STEPS) dropped.push(`bỏ ${plan.length - MAX_STEPS} bước sau bước ${MAX_STEPS}`);
  return [out, dropped];
}

export function formatScene(scene: any): string {
  const lines: string[] = [];
  if (scene.khu_vuc) lines.push(`Người dùng đang ở: ${scene.khu_vuc}.`);
  if (scene.nguoi_dang_cam) lines.push(`Người dùng đang cầm: ${scene.nguoi_dang_cam}.`);
  if (scene.robot_dang_cam) lines.push(`Robot đang cầm: ${scene.robot_dang_cam}.`);
  if (scene.che_do) lines.push(`Chế độ: ${scene.che_do}.`);
  lines.push("Các vật gần người dùng (mã | tên | loại | cách người dùng | đang chứa):");
  for (const v of (scene.vat || []).slice(0, 60)) lines.push(`- ${v.id} | ${v.ten} | ${v.loai} | ${Number(v.khoang_cach || 0).toFixed(1)} m | ${v.chua || ""}`);
  if (scene.su_kien && scene.su_kien.length) lines.push("Hiện tượng gần đây: " + scene.su_kien.slice(-5).join("; "));
  return lines.join("\n");
}
