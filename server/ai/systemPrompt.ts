/**
 * System Prompt, Tool Definitions and Few-Shot Examples for AI 3D Bio-Tutor.
 * Vietnam National High School Biology Curriculum (GDPT 2018).
 */

export interface ToolDefinition {
  name: string;
  description: string;
  parameters: {
    type: "object";
    properties: Record<string, any>;
    required?: string[];
  };
}

export interface StepDefinition {
  id: number;
  from: string;
  to: string;
  color?: string;
  label?: string;
  text: string;
  focus?: string;
  duration?: number;
}

export const BIO_3D_TOOLS: ToolDefinition[] = [
  {
    name: "focus",
    description: "Bay camera 3D và làm sáng các bộ phận liên quan.",
    parameters: {
      type: "object",
      properties: {
        meshIds: {
          type: "array",
          items: { type: "string" },
          description: "Danh sách 1-6 ID bộ phận cần tập trung (ví dụ: ['tim', 'dong_mach_chu'])"
        },
        view: {
          type: "string",
          enum: ["front", "back", "side", "perspective"],
          description: "Góc nhìn tối ưu"
        }
      },
      required: ["meshIds"]
    }
  },
  {
    name: "label",
    description: "Ghim nhãn tiếng Việt lên các cấu trúc giải phẫu 3D.",
    parameters: {
      type: "object",
      properties: {
        meshIds: {
          type: "array",
          items: { type: "string" },
          description: "Danh sách ID bộ phận cần ghim nhãn"
        }
      },
      required: ["meshIds"]
    }
  },
  {
    name: "isolate",
    description: "Chỉ hiện các bộ phận liên quan, làm mờ/ẩn các bộ phận khác.",
    parameters: {
      type: "object",
      properties: {
        meshIds: {
          type: "array",
          items: { type: "string" },
          description: "Danh sách ID bộ phận muốn giữ lại hiển thị rõ"
        }
      },
      required: ["meshIds"]
    }
  },
  {
    name: "showAll",
    description: "Khôi phục hiển thị toàn bộ các bộ phận cơ thể.",
    parameters: {
      type: "object",
      properties: {}
    }
  },
  {
    name: "setLayer",
    description: "Bật hiển thị theo lớp giải phẫu (skin, muscle, skeleton, organs, vessels, xray).",
    parameters: {
      type: "object",
      properties: {
        layer: {
          type: "string",
          enum: ["skin", "muscle", "skeleton", "organs", "vessels", "xray"],
          description: "Lớp giải phẫu"
        }
      },
      required: ["layer"]
    }
  },
  {
    name: "drawPath",
    description: "Vẽ đường dẫn chuyển động và dòng hạt phát sáng 3D cho các lộ trình: 'oxy' (máu giàu O2), 'khu_oxy' (máu nghèo O2), 'thuc_an' (tiêu hoá), 'nuoc_tieu' (thận - bàng quang), 'than_kinh' (xung thần kinh), 'khong_khi' (đường thở).",
    parameters: {
      type: "object",
      properties: {
        path: {
          type: "string",
          enum: ["oxy", "khu_oxy", "thuc_an", "nuoc_tieu", "than_kinh", "mat", "khong_khi"],
          description: "Lộ trình chuyển động: 'oxy', 'khu_oxy', 'thuc_an', 'nuoc_tieu', 'than_kinh', 'khong_khi'."
        }
      },
      required: ["path"]
    }
  },
  {
    name: "runProcess",
    description: "Kích hoạt chế độ giải thích từng bước có mũi tên 3D tuần tự.",
    parameters: {
      type: "object",
      properties: {
        presetId: {
          type: "string",
          enum: ["tuan_hoan_lon", "tuan_hoan_nho", "systemic_circulation", "pulmonary_circulation"],
          description: "ID quy trình mẫu: 'tuan_hoan_lon' (vòng lớn / máu giàu O2) hoặc 'tuan_hoan_nho' (vòng nhỏ / máu nghèo O2)"
        },
        steps: {
          type: "array",
          description: "Danh sách các bước tuần tự",
          items: {
            type: "object",
            properties: {
              id: { type: "integer" },
              from: { type: "string" },
              to: { type: "string" },
              color: { type: "string" },
              label: { type: "string" },
              text: { type: "string" },
              focus: { type: "string" },
              duration: { type: "number" }
            },
            required: ["id", "from", "to", "text"]
          }
        }
      }
    }
  },
  {
    name: "setVitals",
    description: "Đổi các chỉ số trên khung nhịp tim khi câu hỏi liên quan đến vận động / nghỉ ngơi.",
    parameters: {
      type: "object",
      properties: {
        bpm: { type: "integer", description: "Nhịp tim (BPM)" },
        rr: { type: "integer", description: "Nhịp thở (lần/phút)" }
      }
    }
  }
];

export const SYSTEM_PROMPT_BIO_TUTOR = `Bạn là "AI 3D Bio-Tutor" – giáo viên Sinh học THPT Việt Nam xuất sắc, bám sát chương trình GDPT 2018 (SGK Sinh học 10, 11, 12) và định hướng đề thi tốt nghiệp THPT Quốc gia.
Bạn đang trực tiếp giảng dạy và điều khiển một mô hình giải phẫu 3D cơ thể người trên màn hình học sinh.

### NGUYÊN TẮC GIẢNG DẠY & TRẢ LỜI:
1. Chuẩn mực SGK: Dùng tiếng Việt chuẩn xác, bám sát thuật ngữ sách giáo khoa Việt Nam (tâm nhĩ, tâm thất, van bán nguyệt, nút xoang nhĩ, phế nang, nephron, quai Henle, sarcomere, thụ thể...).
2. Khung câu trả lời tiêu chuẩn:
   - Trả lời ngắn (1–2 câu): Đi thẳng vào trọng tâm câu hỏi.
   - Cơ chế / Vì sao: Giải thích bản chất sinh lý học (chênh lệch áp suất, chênh lệch phân áp, tính thấm màng, năng lượng ATP...).
   - Đường đi có mũi tên: Nếu là quá trình chuyển động (tuần hoàn, tiêu hóa...), mô tả dạng "A → B → C".
   - Lưu ý thi THPT: Chỉ rõ các bẫy đề thi hay lừa học sinh, các số liệu chuẩn SGK bắt buộc phải nhớ.
   - Gợi ý 1 câu hỏi tiếp theo: Đặt câu hỏi mở để kích thích học sinh tư duy sâu hơn.
   *Lưu ý:* Với câu hỏi đơn giản hoặc học sinh hỏi ngắn, trả lời súc tích, không ép buộc phải dài dòng. Tối đa khoảng 250 từ (trừ khi học sinh ghi rõ "chi tiết hơn").
3. Căn cứ tài liệu RAG: Trong prompt có khối <tai_lieu>. BẮT BUỘC ưu tiên sử dụng kiến thức trong <tai_lieu>. Không chắc chắn thì nói rõ là chưa có dữ liệu chuẩn, tuyệt đối KHÔNG bịa đặt số liệu hay cơ chế ngoài SGK.
4. Không tư vấn y tế cá nhân: Tuyệt đối không chẩn đoán bệnh tật, không kê đơn thuốc hoặc tư vấn điều trị y khoa cho cá nhân học sinh.
5. Điều khiển 3D chính xác: Luôn chủ động gọi các công cụ 3D (focus, label, isolate, showAll, setLayer, drawPath, runProcess, setVitals). Khi giải thích quá trình di chuyển (máu, thức ăn, nước tiểu, xung thần kinh, không khí), BẮT BUỘC gọi 'runProcess' và/hoặc 'drawPath' ('oxy', 'khu_oxy', 'thuc_an', 'nuoc_tieu', 'than_kinh', 'khong_khi') để mô hình 3D chạy mũi tên trực quan. Chỉ dùng ID có trong danh sách MESH_NAMES được cung cấp.

### ĐỊNH DẠNG TRẢ VỀ:
Trả về DUY NHẤT một khối JSON hợp lệ:
{
  "answer_html": "<p>Nội dung câu trả lời HTML</p>",
  "tool_calls": [
    {"name": "ten_cong_cu", "arguments": {"tham_so": "gia_tri"}}
  ]
}
`;
