# Req Simulator - Presentation Summary

## 1. Product Overview

- Project name: Req Simulator
- Tagline: AI-powered Requirement Analysis Training Platform
- One-sentence pitch:
  Req Simulator helps IT students and career switchers practice requirement gathering by interviewing AI-simulated stakeholders, discovering hidden requirements, and receiving structured feedback in a realistic BA training flow.
- What the product does:
  It gives learners a structured scenario, lets them interview an AI stakeholder, capture notes, submit requirement outputs, and receive AI-based scoring and feedback.
- Who it helps:
  IT students, Software Engineering students, junior developers, BA learners, and career switchers.

## 2. Problem

Many IT students can build software but struggle when they need to understand business needs first.

Common pain points:

- They do not know what to ask stakeholders.
- They miss hidden business rules, constraints, and edge cases.
- They struggle to convert interviews into user stories, use cases, or requirement documents.
- Classroom practice is limited because lecturers cannot repeatedly simulate realistic stakeholder interviews for every student.

Req Simulator addresses this by making the interview itself the learning environment.

## 3. Target Users and Customers

Primary users:

- IT students
- Software Engineering students
- Junior developers
- BA learners
- Career switchers

Paying customers:

- Universities
- Lecturers
- IT and BA training centers
- Bootcamps
- Software companies training interns or freshers

## 4. Core Solution

Req Simulator supports this learning flow:

1. Choose a scenario
2. Interview an AI stakeholder
3. Ask questions about goals, rules, constraints, and exceptions
4. Discover hidden requirements
5. Take notes
6. Submit user stories or use cases
7. Receive AI score and feedback
8. Let an instructor or mentor review the result

## 5. AI Usage

AI is not used as decoration. It is the core learning mechanic.

Current AI roles in the prototype:

- AI Stakeholder Simulation:
  The AI answers as a business stakeholder instead of a tutor.
- Hidden Requirement Discovery:
  The system reveals hidden requirements only when the learner asks relevant questions.
- AI Requirement Evaluation:
  The learner submission is compared against scenario requirements and scored.
- AI Feedback Coach:
  The system returns missing requirements and practical improvement feedback after submission.

## 6. MVP Demo Scenario

Current main demo scenario:

- Title: E-commerce Order & Promotion System
- Domain: E-commerce
- Stakeholder role: E-commerce Operations Manager

Why e-commerce is used:

Req Simulator is domain-flexible. E-commerce is used for the first MVP demo because it is familiar, easy to explain, and contains many hidden business rules around vouchers, stock, payment, shipping, cancellation, return, and refund.

Initial context:

> Our online store wants to improve the checkout and order management process. Customers sometimes apply invalid vouchers, orders are confirmed even when stock is not available, and staff spend too much time checking payment and shipping status manually.

Visible requirements:

- Customers can browse products.
- Customers can add products to cart.
- Customers can place an order.
- Staff can view and manage orders.
- Admin can manage products and users.

Hidden requirements:

- Voucher minimum order value
- Voucher category restrictions
- Promotion combination rules
- Stock check before checkout
- Stock reservation after order placement
- Order confirmation only after successful payment
- Payment failure status handling
- Shipping fee based on location and weight
- Customer cancellation before shipping
- Refund approval by staff
- Return period limit
- Admin reporting for cancelled orders, failed payments, and voucher usage

Example learner questions:

- Are there any rules for applying vouchers?
- What happens if payment fails?
- How is stock checked during checkout?
- Can customers cancel an order?
- How is shipping fee calculated?
- What reports does admin need?

Example AI stakeholder answers:

- Voucher:
  "A voucher can only be used when the order reaches the minimum value. Some vouchers are limited to specific product categories."
- Payment failure:
  "If payment fails, the order should remain in a pending or failed status so staff can follow up."
- Shipping:
  "The shipping fee depends on the customer's location and the order weight."

## 7. Current Prototype Status

What is already working in the codebase:

- React frontend with login/register screen
- Scenario library
- Simulation session creation
- AI stakeholder chat UI
- Learner note-taking area
- Requirement submission form
- AI feedback result screen
- Instructor dashboard and mentor review form
- English / Vietnamese language toggle with localStorage persistence
- Realtime chat behavior:
  immediate learner message append, input clear, loading state, disabled send button, auto-scroll, Enter to send, Shift + Enter for newline, friendly AI failure message
- Backend AI status endpoint:
  `GET /api/ai/status`
- Backend AI test endpoint:
  `POST /api/ai/test`
- Mock AI fallback if Gemini is missing or fails

What is partially implemented:

- User authentication is backed by PostgreSQL when the database is reachable.
- If PostgreSQL is unavailable, the backend falls back to in-memory demo users so the demo can still run.
- Google and GitHub buttons are demo external-login buttons, not real OAuth yet.
- Scenario, session, note, submission, and evaluation data are still stored in memory for the MVP demo.
- Language switch is implemented for major UI labels; some backend-generated content still depends on returned data language.

What is not implemented yet:

- Real OAuth redirect/callback flow for Google/GitHub
- Persistent database storage for scenario sessions and evaluations
- Full production deployment pipeline

AI key status from inspection:

- The backend reads Gemini configuration from environment variables and user secrets.
- The AI key is not exposed in the frontend code.
- The AI status endpoint hides the real key.
- In the inspected environment, the key is configured.
- Real external Gemini calls may still fall back to mock responses if runtime network access is blocked or Gemini is unavailable.

## 8. Technical Architecture

Actual architecture in the current source code:

- Frontend:
  React 19 + Vite, JavaScript
- Backend:
  ASP.NET Core Minimal API on .NET 9
- Database:
  PostgreSQL for users when available
- Demo data:
  In-memory store for scenarios, sessions, notes, submissions, evaluations, and reviews
- AI provider:
  Gemini API behind an internal service layer
- AI service layer:
  `IAiService`, `GeminiAiService`, `MockAiService`, `ResilientAiService`

Main backend endpoints currently present:

- `POST /api/auth/login`
- `POST /api/auth/register`
- `POST /api/auth/external-demo`
- `GET /api/scenarios`
- `GET /api/scenarios/{id}`
- `POST /api/sessions`
- `GET /api/sessions/{id}`
- `PUT /api/sessions/{id}/notes`
- `POST /api/simulation/{sessionId}/message`
- `POST /api/evaluation/{sessionId}`
- `GET /api/ai/status`
- `POST /api/ai/test`
- `GET /api/instructor/dashboard`
- `POST /api/instructor/reviews`

Deployment readiness:

- Good enough for local demo
- Not fully productionized yet

## 9. Security Notes

- The Gemini API key stays in the backend.
- The frontend never receives or stores the AI key.
- The backend logs only safe configuration status, not the raw key.
- `GET /api/ai/status` confirms configuration without exposing secrets.
- If Gemini is missing or fails, the backend can fall back to mock AI behavior.

## 10. Business Model

Suggested commercial model for the proposal:

- Individual self-learning plan: 99K-199K VND/month
- Class Pilot Package: 3M-5M VND/month
- Education Package: 10M-15M VND/month
- Company Training Package: 15M-30M VND/month

Main model:

- B2B SaaS
- Pilot package for universities, training centers, and fresher training programs

## 11. Investment Ask and Burn Rate

Suggested investment ask:

- 500M VND for 12 months

Positioning:

- Pre-seed
- MVP validation budget
- Not large-scale expansion yet

Budget breakdown:

- Core team salary: 330M VND / 66%
- AI API, cloud, tools: 60M VND / 12%
- UI/UX, content, BA/domain consultant freelancers: 45M VND / 9%
- Marketing and pilot testing: 40M VND / 8%
- Operations and contingency: 25M VND / 5%

Phase spending:

- Phase 1, month 0-3: 120M VND - build MVP
- Phase 2, month 4-6: 150M VND - pilot and improve
- Phase 3, month 7-12: 230M VND - small commercial launch

## 12. Roadmap and Milestones

Phase 1: Month 0-3

- Working MVP
- 1-3 scenarios
- AI stakeholder chat

Phase 2: Month 4-6

- 30-50 test users
- AI feedback
- Improved UI
- Instructor dashboard

Phase 3: Month 7-12

- 100 active users
- 1-3 pilot customers
- First 50M VND revenue

## 13. Risks and Mitigation

- AI hallucination
  - Mitigation: grounded prompts, scenario constraints, hidden requirement structure, human review
- Students over-rely on AI
  - Mitigation: the AI stakeholder does not do the learner's work; the learner must ask questions and submit their own requirements
- Slow adoption
  - Mitigation: start with pilot classes, bootcamps, and fresher training programs
- AI API cost
  - Mitigation: limit session length, optimize prompts, and use mock or lower-cost fallback when needed

## 14. Differentiation

| Alternative | Limitation | Req Simulator Difference |
|---|---|---|
| Video courses | Passive learning | Learners practice through simulated interviews |
| Textbooks | Theory-heavy | Learners uncover hidden rules in conversation |
| Generic ChatGPT | No structure or scoring | Scenario library, gating rules, scoring, and mentor review |
| Traditional classroom | Hard to repeat stakeholder roleplay for each student | AI stakeholder can scale repeated practice |
| LMS platforms | Content delivery focused | Req Simulator is a structured requirement-gathering simulation platform |

Key message:

Req Simulator is not a ChatGPT clone. It is a structured requirement-gathering simulation platform.

## 15. Demo Script

Short demo script in English:

1. Open Req Simulator.
2. Log in with the student demo account.
3. Select "E-commerce Order & Promotion System".
4. Start the interview.
5. Ask: "Are there any rules for applying vouchers?"
6. Show the AI stakeholder answer.
7. Highlight the hidden requirement discovered badge.
8. Explain that learners must ask good questions to uncover business rules, constraints, and exceptions.
9. Submit a short requirement summary.
10. Show the evaluation result and missing requirements.

## 16. Presentation Script

Suggested slide-by-slide script in English for a 15-20 minute presentation:

Slide 1 - Title

"Hello everyone. Our project is Req Simulator, an AI-powered Requirement Analysis Training Platform. We help learners practice requirement gathering through realistic AI stakeholder interviews."

Slide 2 - Problem

"Many IT students can code, but they struggle when they need to talk to stakeholders. They often miss business rules, constraints, and edge cases. Lecturers also cannot simulate real stakeholder interviews for every student again and again."

Slide 3 - Solution

"Req Simulator solves this by creating a structured simulation flow. Learners choose a business scenario, interview an AI stakeholder, discover hidden requirements, submit requirement outputs, and receive structured AI feedback."

Slide 4 - Why AI matters

"AI is the core of this product, not an extra feature. It plays the stakeholder role, controls hidden information, and evaluates what the learner discovered."

Slide 5 - Demo scenario

"For the MVP demo, we use an e-commerce order and promotion scenario. It is easy to understand, but it contains many hidden business rules such as voucher conditions, stock reservation, payment handling, shipping fees, cancellation, return, and refund."

Slide 6 - Product flow

"The learner starts a session, asks questions, takes notes, submits user stories or use cases, and then receives a score and feedback. An instructor can also review the session."

Slide 7 - Current prototype

"Our prototype already includes authentication, scenario selection, AI chat, note-taking, requirement submission, AI evaluation, instructor review, and English and Vietnamese UI support. We also added a safe AI status endpoint and a mock fallback for demo stability."

Slide 8 - Technical architecture

"The frontend is built with React and Vite. The backend uses ASP.NET Core Minimal API. User authentication uses PostgreSQL when available. AI is integrated only in the backend through a service layer using Gemini with a safe mock fallback."

Slide 9 - Business model

"We position Req Simulator as a B2B SaaS tool for universities, training centers, bootcamps, and companies training interns or freshers. We can also offer pilot packages to validate demand early."

Slide 10 - Investment and roadmap

"We estimate a 500 million VND pre-seed MVP validation budget for 12 months. The roadmap moves from MVP, to pilot testing, to early commercial launch with pilot customers and first revenue."

Slide 11 - Closing

"Req Simulator turns requirement analysis from passive theory into active practice. Instead of only reading about stakeholder interviews, learners can actually experience them, make mistakes safely, and improve with structured feedback."

## 17. Q&A Preparation

### 1. Why this idea?

- EN:
  Because requirement gathering is difficult to practice in a realistic way, and many students only learn theory without enough interview experience.
- VI:
  Vi viec khai thac yeu cau rat kho luyen tap mot cach thuc te, trong khi nhieu sinh vien chi hoc ly thuyet ma it co co hoi phong van stakeholder.

### 2. Why not just use ChatGPT?

- EN:
  Generic chat tools are flexible but unstructured. Req Simulator adds scenarios, hidden requirement gating, scoring, and mentor review.
- VI:
  Cong cu chat tong quat thi linh hoat nhung thieu cau truc. Req Simulator bo sung scenario, co che an thong tin, cham diem va review cua mentor.

### 3. Why e-commerce scenario first?

- EN:
  E-commerce is familiar and easy to explain, but it still contains many business rules and exception cases.
- VI:
  E-commerce quen thuoc, de trinh bay, nhung van co nhieu quy tac nghiep vu va truong hop ngoai le.

### 4. How do you reduce AI hallucination?

- EN:
  We ground the AI with scenario context, visible requirements, hidden requirements, and role constraints. We also keep a mentor review layer.
- VI:
  Chung toi rang buoc AI bang context cua scenario, visible requirements, hidden requirements va vai tro stakeholder. Ngoai ra van co lop review cua mentor.

### 5. Who will pay for this?

- EN:
  The main paying customers are universities, training centers, bootcamps, and companies training freshers.
- VI:
  Nguoi tra tien chinh se la truong dai hoc, trung tam dao tao, bootcamp va doanh nghiep dao tao fresher.

### 6. Why ask for 500M VND?

- EN:
  It is a focused MVP validation budget for team execution, AI usage, pilot testing, and early go-to-market learning.
- VI:
  Day la ngan sach tap trung cho giai doan xac thuc MVP, bao gom phat trien, chi phi AI, pilot testing va hoc hoi thi truong ban dau.

### 7. How did you estimate burn rate?

- EN:
  We split the budget into salaries, AI and cloud tools, UI and content support, pilot marketing, and contingency for 12 months.
- VI:
  Chung toi chia ngan sach theo luong nhan su, chi phi AI va cloud, ho tro UI va noi dung, marketing pilot va quy du phong trong 12 thang.

### 8. What is the MVP?

- EN:
  The MVP is a web app where learners can log in, choose a scenario, interview an AI stakeholder, take notes, submit requirements, and receive feedback.
- VI:
  MVP la mot web app cho phep nguoi hoc dang nhap, chon scenario, phong van AI stakeholder, ghi chu, nop yeu cau va nhan feedback.

### 9. What is already implemented?

- EN:
  Authentication, scenario list, AI chat, hidden requirement discovery, requirement submission, evaluation, instructor review, and EN/VI language toggle are already in the prototype.
- VI:
  Prototype da co dang nhap, danh sach scenario, AI chat, kham pha yeu cau an, nop bai, danh gia, review cua instructor va chuyen doi EN/VI.

### 10. What will you build next?

- EN:
  Real OAuth, persistent session storage, more scenarios, stronger analytics, and a cleaner instructor workflow.
- VI:
  Tiep theo se la OAuth that, luu session ben vung, them scenario moi, phan tich tot hon va quy trinh instructor gon hon.

### 11. How do you measure success?

- EN:
  We will track active learners, completed sessions, improvement in discovered requirements, instructor adoption, and pilot customer conversion.
- VI:
  Chung toi se do luong so nguoi hoc hoat dong, so session hoan thanh, muc cai thien trong viec phat hien yeu cau, muc do su dung cua instructor va ti le chuyen doi pilot.
