# AI Prompt Engineering Upgrades - Req Simulator

Tài liệu này tổng hợp các cải tiến và nguyên tắc Prompt Engineering đã được áp dụng vào Req Simulator để biến AI từ một "chatbot thông thường" thành một **Stakeholder (Bên liên quan) chân thực, thông minh và có tính sư phạm cao**.

---

## 1. Vấn đề của AI Chatbot thông thường
Trước khi tối ưu hóa Prompt, hệ thống gặp phải các vấn đề sau:
- **Ngôn ngữ quá máy móc:** AI thường trả lời theo kiểu liệt kê gạch đầu dòng (bullet points) giống sách giáo khoa.
- **Ngoan ngoãn một cách "lười biếng":** Khi học viên hỏi một câu quá rộng như *"Kể cho tôi mọi thứ về hệ thống đi"*, AI lập tức tuôn ra toàn bộ yêu cầu ẩn, làm mất đi giá trị của việc "khai thác yêu cầu" (Requirement Gathering).
- **Cắt ngang câu (Cut-off):** Khi bị giới hạn số lượng từ (ví dụ: dưới 130 từ) nhưng lại được yêu cầu giải thích chi tiết, LLM (Gemini) thường tự động dừng sinh chữ giữa chừng, gây ra trải nghiệm hụt hẫng.
- **Lạc đề (Hallucination):** AI có thể lấy dữ liệu từ tình huống khác (ví dụ: đang làm về E-commerce lại nhắc đến sinh viên đăng ký môn học).

---

## 2. Các Chỉ Thị "Thông Minh" Đã Triển Khai (Smarter Directives)

Để giải quyết các vấn đề trên, chúng tôi đã đưa các chỉ thị (directives) sau vào **Stakeholder System Prompt**:

### 2.1. Nhập vai trọn vẹn (Embody Persona)
- **Chỉ thị:** *"Embody the persona fully: if the persona is busy or frustrated, show subtle signs of it in the response without being rude."*
- **Ý nghĩa:** AI không chỉ đóng vai theo chức danh (Role) mà còn mang theo cảm xúc (Persona). Nếu nhân vật là một người quản lý đang đau đầu vì lỗi thanh toán, AI sẽ đan xen sự bực dọc nhẹ nhàng hoặc áp lực công việc vào câu trả lời, tạo cảm giác áp lực thực tế cho học viên (BA).

### 2.2. Phản kháng thực tế (Realistic Pushback)
- **Chỉ thị:** *"Push back realistically if the learner asks extremely broad questions like 'tell me all your rules' or 'what else?'. Tell them you have a lot going on and they need to ask about a specific area."*
- **Ý nghĩa:** Chống lại các câu hỏi "lười biếng". Stakeholder ngoài đời thực không có thời gian để tự đọc thuộc lòng mọi quy trình cho BA. Học viên buộc phải hỏi các câu hỏi có mục tiêu cụ thể.

### 2.3. Điều hướng mượt mà (Smooth Redirect & Hinting)
- **Chỉ thị:** *"If you have no more hidden rules to reveal about a topic, confirm that's everything for that area, and smoothly redirect them by dropping a hint about a different operational pain point (e.g., 'That's all for stock, but honestly I'm also worried about how we handle payment failures...')."*
- **Ý nghĩa:** Giúp cuộc hội thoại không bị đi vào ngõ cụt. Khi một chủ đề đã được khai thác hết, AI sẽ thả một "gợi ý" tự nhiên để hướng học viên sang một mảng nghiệp vụ khác còn đang bị bỏ ngỏ.

### 2.4. Khai thác chuyên sâu (Context-Aware Elaboration)
- **Chỉ thị:** *"If the learner asks a specific follow-up, or explicitly asks for 'more information', 'details', or 'elaborate', you MUST provide a detailed explanation of the business rules, constraints, exceptions, and edge cases related to that topic."*
- **Ý nghĩa:** Khi học viên thực sự biết cách đào sâu (ví dụ: *"Bạn có thể giải thích thêm quy trình khi thanh toán lỗi không?"*), AI sẽ cung cấp các đoạn văn bản chi tiết, tiết lộ các Edge Cases (Trường hợp ngoại lệ) thay vì chỉ trả lời ngắn gọn.

### 2.5. Xóa bỏ giới hạn từ (No Strict Word Count Limits)
- **Cải tiến:** Xóa bỏ luật *"Keep the answer under 130 words"*. Thay vào đó sử dụng *"Be concise normally, but provide thorough and complete paragraphs when the learner asks for more details. Never cut off your sentences abruptly."*
- **Ý nghĩa:** Ngăn chặn hiện tượng AI tự động ngắt câu (cut-off) khi đang giải thích một logic nghiệp vụ phức tạp.

---

## 3. Cải Tiến Hệ Thống Đánh Giá (AI Evaluation / Skill-Based Guidance)

Bên cạnh phần Chat, AI Evaluator (Hệ thống chấm điểm) cũng được nâng cấp:
- **Chỉ thị:** *"IMPORTANT: The feedback MUST include Skill-Based Guidance. Explicitly suggest the next scenario type or skill focus based on the learner's weakest score area."*
- **Ý nghĩa:** Sau khi chấm điểm (dựa trên Thang điểm Rubric: Completeness, Business Rules, Question Quality...), thay vì chỉ nhận xét chung chung, AI phải đưa ra **Gợi ý học tập định hướng kỹ năng**. Ví dụ: *"Điểm về Business Rules của bạn khá thấp. Lần tới, hãy thử một tình huống có nhiều quy tắc ngoại lệ (edge cases) hơn như hệ thống Đặt chỗ Nhà hàng nhé."*

---

## 4. Tổng Kết

Bằng cách sử dụng **Information Control** (Kiểm soát luồng thông tin) kết hợp với **Behavioral Prompting** (Nhắc lệnh hành vi), Req Simulator mang đến một môi trường giả lập tiệm cận với đời thực:
1. Thông tin không có sẵn, phải tự tìm kiếm.
2. Khách hàng bận rộn và có cảm xúc.
3. BA phải biết cách đặt câu hỏi và dẫn dắt vấn đề.
4. Có 피드백 (Feedback) rõ ràng để định hướng lộ trình học tiếp theo.
