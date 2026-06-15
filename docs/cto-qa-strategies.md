# CTO Q&A Strategies - Req Simulator Pitch

Tài liệu này tổng hợp các câu hỏi hóc búa từ CTO/Nhà đầu tư liên quan đến khía cạnh kỹ thuật của hệ thống **Req Simulator**, cùng với các chiến lược trả lời sắc bén để thể hiện tư duy kỹ sư phần mềm thực chiến (Software Engineering) và tầm nhìn hệ thống (System Design).

---

## 1. Vấn đề Scale hệ thống & Tối ưu Chi phí (Rate Limit & Cost Optimization)

**❓ Câu hỏi của CTO:**
> "Bạn nói rằng tất cả request đều gọi qua backend ASP.NET Core tới Gemini API. Nếu một trường đại học mua gói của bạn và 500 sinh viên cùng online chat với AI một lúc, làm sao bạn đảm bảo hệ thống không bị sập vì quá tải request (rate limit) từ Google? Và làm sao bạn kiểm soát chi phí API để không bị lỗ?"

* **Mục đích hỏi:** Kiểm tra xem bạn có biết về các giới hạn của API bên thứ ba (Rate Limiting) và cách quản lý rủi ro về chi phí tài nguyên hay không.

**💡 Chiến lược trả lời:**
*   **Về Rate Limit:** 
    *   Sử dụng cơ chế **Hàng đợi (Message Queue)** (như RabbitMQ/Azure Service Bus) để điều tiết lượng request. Thay vì gọi API đồng bộ, các request sẽ được xếp hàng và xử lý tuần tự/batched.
    *   Áp dụng cơ chế **Retry & Circuit Breaker** trong ASP.NET Core (sử dụng thư viện Polly) để xử lý mượt mà các request bị lỗi tạm thời khi Google trả về mã lỗi 429 (Too Many Requests), tránh làm chết luồng xử lý chính.
*   **Về Tối ưu chi phí:** 
    *   Áp dụng **Caching (như Redis)** cho các câu hỏi trùng lặp hoặc các Scenario mặc định.
    *   Thiết lập **Token Limit & Session Limit**: Giới hạn số lượng tin nhắn tối đa mỗi phiên phỏng vấn (ví dụ: 15-20 câu hỏi) để mô phỏng áp lực thời gian thực tế, đồng thời tiết kiệm token.

---

## 2. Quản lý Bộ nhớ Ngữ cảnh (Prompt Engineering & Context Window)

**❓ Câu hỏi của CTO:**
> "Trong một buổi phỏng vấn lấy yêu cầu thực tế, cuộc hội thoại có thể rất dài. LLM (như Gemini) có giới hạn về 'Context Window' (bộ nhớ ngữ cảnh). Nếu sinh viên chat đến câu thứ 50, làm sao AI vẫn nhớ được những quy tắc ẩn bạn đã thiết lập từ System Prompt ban đầu mà không bị 'trôi' mất?"

* **Mục đích hỏi:** Kiểm tra độ hiểu biết sâu sắc của bạn về giới hạn kỹ thuật của các mô hình ngôn ngữ lớn (LLM Context Length limitations).

**💡 Chiến lược trả lời:**
*   Khẳng định rằng hệ thống **không gửi lại toàn bộ lịch sử chat từ câu đầu tiên** một cách thô sơ (vì sẽ tiêu tốn quá nhiều token và dễ dính rate limit).
*   Áp dụng kỹ thuật **Memory Summarization (Tóm tắt bộ nhớ)**: Backend sẽ tự động gọi một background prompt để tóm tắt các ý chính của cuộc hội thoại sau mỗi N câu. Bản tóm tắt này (Memory State) sẽ được gửi kèm với System Prompt ở các lượt chat tiếp theo, giúp AI duy trì nhận thức ngữ cảnh một cách nhẹ nhàng.

---

## 3. Thuật toán chấm điểm (Automated Semantic Evaluation)

**❓ Câu hỏi của CTO:**
> "Bạn nói hệ thống dùng 'Golden Requirement List' để chấm điểm. Nhưng sinh viên có thể dùng từ ngữ rất khác so với danh sách mẫu. Làm sao thuật toán của bạn phân biệt được một sinh viên viết đúng ý nhưng sai từ, so với một sinh viên thực sự viết sai yêu cầu?"

* **Mục đích hỏi:** CTO muốn biết hệ thống của bạn chấm điểm có đang dựa trên "so sánh chuỗi" (String matching - rất dễ sai lệch) hay thực sự hiểu được ý nghĩa của tài liệu (Semantic understanding).

**💡 Chiến lược trả lời:**
*   Khẳng định rõ ràng: Hệ thống **không dùng** thuật toán so sánh chuỗi (Exact/Regex match) lạc hậu.
*   Giải pháp: Giao phó việc này cho một **LLM Scoring Agent** (Gemini) được cung cấp bộ Rubric rõ ràng. AI sẽ so sánh dựa trên **ngữ nghĩa (Semantic understanding)**. 
*   Ví dụ: Miễn là sinh viên đề cập đủ logic kinh doanh *(như "đơn hàng trên 500k thì miễn phí vận chuyển")*, cho dù họ dùng từ *"giỏ hàng > 500.000 VNĐ được freeship"*, AI vẫn có khả năng nhận diện, đối chiếu với Golden Requirement và cho điểm chính xác.

---

## 4. Bảo mật & Chống lạm dụng (Security & Abuse Prevention)

**❓ Câu hỏi của CTO:**
> "Bạn đã giấu API key ở backend, điều đó rất tốt. Nhưng điều gì ngăn cản một người dùng đăng ký tài khoản 199k, sau đó viết một đoạn script (bot) liên tục gửi hàng ngàn request lên server của bạn để 'phá bĩnh', làm cạn kiệt ngân sách Gemini API của bạn?"

* **Mục đích hỏi:** Đánh giá tư duy lập trình phòng thủ (Defensive Programming) và cách xử lý rủi ro kinh doanh (Financial DoS).

**💡 Chiến lược trả lời:**
*   Áp dụng **Rate Limiting** ở tầng API Gateway / ASP.NET Core Middleware: Chỉ cho phép một tài khoản gửi một số lượng request nhất định trong 1 phút (ví dụ: 5-10 requests/phút).
*   **Giám sát hành vi bất thường (Anomaly Detection):** Nếu một User ID bắn request với tốc độ bất thường (nhanh hơn tốc độ gõ phím của con người một cách vô lý), hệ thống sẽ ngay lập tức **tạm khóa tài khoản (Soft Block)** và yêu cầu Captcha hoặc sự can thiệp từ Admin.

---

## 5. Lộ trình Triển khai (DevOps & CI/CD)

**❓ Câu hỏi của CTO:**
> "Ở Slide 11, bạn nói MVP cần xây dựng Deployment Pipeline. Với vai trò là người phát triển chính, chiến lược CI/CD (Tích hợp liên tục / Triển khai liên tục) của bạn cho dự án này là gì để đảm bảo code lên production không làm sập hệ thống đang chạy?"

* **Mục đích hỏi:** Kiểm tra tư duy vận hành hệ thống thực tế thay vì chỉ biết code và chạy thủ công ở môi trường Localhost.

**💡 Chiến lược trả lời:**
*   **Automation:** Sử dụng **GitHub Actions** để thiết lập luồng CI tự động chạy các luồng **Unit Test** (đặc biệt chú trọng test các class xử lý Prompt và Logic chấm điểm) mỗi khi có Pull Request mới.
*   **Containerization:** Đóng gói ứng dụng backend và frontend bằng **Docker**. Điều này đảm bảo rằng "code chạy được trên máy Dev thì cũng sẽ chạy chính xác 100% trên môi trường Server (Production)", loại bỏ triệt để lỗi môi trường.
