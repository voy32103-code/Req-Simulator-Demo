# Req Simulator - Technical Improvement Roadmap

Dựa trên các rủi ro và chiến lược đã phân tích trong tài liệu **CTO Q&A Strategies**, dưới đây là danh sách các hạng mục cải tiến kỹ thuật (Technical Backlog) cần thực hiện để nâng cấp Req Simulator từ phiên bản MVP hiện tại lên chuẩn Production-ready.

---

## 1. Kiến trúc & Hiệu năng (Architecture & Performance)

- [ ] **Chuyển đổi Database:** Thay thế `DemoStore` (In-memory) bằng cơ sở dữ liệu quan hệ thực sự như **PostgreSQL** để lưu trữ người dùng, kịch bản, và lịch sử chat bền vững.
- [ ] **Tích hợp Caching (Redis):** Áp dụng Redis Cache để lưu các kịch bản mặc định (Scenarios), cấu hình hệ thống, và các context không thay đổi trong quá trình chat, giúp giảm tải truy vấn DB.
- [ ] **Triển khai Message Queue:** Thiết lập cơ chế hàng đợi (ví dụ: RabbitMQ hoặc Azure Service Bus) cho tác vụ gọi API sang Gemini. Thay vì chờ đợi đồng bộ (Synchronous), backend sẽ đẩy request vào queue để tránh bị block thread khi có hàng trăm người dùng cùng chat.
- [ ] **Tối ưu hóa Resilience (Polly):** Áp dụng thư viện `Polly` trong ASP.NET Core để cấu hình tự động Retry (thử lại) hoặc Circuit Breaker (ngắt mạch) khi API Gemini trả về lỗi `429 Too Many Requests` hoặc `500 Internal Server Error`.

---

## 2. Quản lý LLM & Prompt (AI & Context Management)

- [ ] **Giới hạn số lượt tương tác (Session Limits):** Áp dụng logic bắt buộc kết thúc buổi phỏng vấn (Session) sau tối đa 15-20 lượt hỏi/đáp. Vừa giúp rèn luyện kỹ năng sinh viên (phải khai thác thông tin nhanh), vừa ngăn chặn việc xả token vô tội vạ.
- [ ] **Tóm tắt bộ nhớ ngữ cảnh (Memory Summarization):** Chỉnh sửa logic lưu trữ `ChatMessage`. Nếu phiên chat vượt quá 10 tin nhắn, backend tự động gửi một prompt ẩn yêu cầu Gemini "tóm tắt lại các thông tin đã trao đổi" thành một đoạn văn ngắn (Context State). Đoạn này sẽ được nối vào System Prompt thay vì gửi lại toàn bộ lịch sử hội thoại.
- [ ] **Phân tách LLM Roles:** Hiện tại hệ thống đang gộp chung. Cần phân tách rõ ràng thành 2 instance/model khác nhau (có thể dùng model nhẹ hơn/rẻ hơn như *Gemini 1.5 Flash* cho việc Roleplay Chat, và model mạnh hơn như *Gemini 1.5 Pro* cho việc Semantic Evaluation/Chấm điểm).

---

## 3. Bảo mật & Chống lạm dụng (Security & Rate Limiting)

- [ ] **Triển khai API Rate Limiting:** Sử dụng middleware `Microsoft.AspNetCore.RateLimiting` tích hợp sẵn trong .NET để cấu hình:
  - Giới hạn 10 request/phút trên mỗi User ID.
  - Từ chối ngay lập tức (Status Code 429) nếu vượt quá ngưỡng.
- [ ] **Bảo vệ tài nguyên API (Anti-Bot):** Áp dụng thuật toán theo dõi thời gian giữa 2 tin nhắn của cùng một User. Nếu khoảng thời gian < 1 giây liên tục trong 3 tin nhắn, tự động tạm khóa (Soft-block) tài khoản vì nghi ngờ dùng tool tự động.
- [ ] **Xác thực JWT bảo mật:** Cải tiến hệ thống Authentication hiện tại (chưa có JWT hoàn chỉnh). Đảm bảo tất cả endpoint API đều được bảo vệ bằng JWT Token sinh ra từ backend, có thời hạn (Expiration).

---

## 4. Vận hành & Triển khai (DevOps & CI/CD)

- [ ] **Xây dựng Dockerfile toàn diện:** Hiện tại đã có Dockerfile cho Backend, cần tạo thêm `Dockerfile` (sử dụng Nginx multi-stage build) cho Frontend React để dễ dàng scale.
- [ ] **Thiết lập GitHub Actions (CI):** Tạo file `.github/workflows/ci.yml`:
  - Tự động build source code Frontend và Backend khi có code mới push lên nhánh `main`.
  - Chạy bộ Unit Test.
- [ ] **Tích hợp Monitoring & Logging:** Cài đặt Application Insights hoặc Elasticsearch/Kibana (ELK) để theo dõi real-time xem thời gian trung bình (Latency) mỗi lần gọi sang Gemini mất bao lâu, từ đó có căn cứ scale hệ thống.

---

## 5. Trải nghiệm người dùng (UX/UI)

- [ ] **Hiển thị tiến trình & Quản lý kỳ vọng:** Bổ sung thanh hiển thị "Số câu hỏi còn lại" trong màn hình Simulation Workspace để tạo áp lực thực tế cho sinh viên.
- [ ] **Nâng cấp Dashboard cho Giảng viên:** Cung cấp biểu đồ trực quan (Data Visualization) thống kê lỗ hổng chung của toàn bộ sinh viên trong lớp để giảng viên dễ dàng điều chỉnh giáo án trên lớp.
