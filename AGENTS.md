# AGENTS.md — Req Simulator

## 1. Project Identity

**Project name:** Req Simulator  
**Tagline:** AI-powered Requirement Gathering Training Platform  
**Product type:** Web application / SaaS training platform  
**Primary goal:** Help IT students and career switchers practice Business Analyst and Requirement Engineering skills through AI-simulated stakeholder interviews.

Req Simulator is not a generic chatbot. It is a structured simulation platform where learners interview AI stakeholders, discover hidden requirements, write user stories/use cases, receive AI feedback, and optionally get reviewed by instructors or mentors.

---

## 2. Startup Proposal Context

This project is being built for a startup proposal challenge under the theme **AI First**.

The proposal must demonstrate:

1. Practicality & research
2. Creativity & uniqueness
3. Meaningful AI application
4. Technical feasibility & roadmap
5. Business model & financial plan
6. Clear presentation and convincing storytelling

When coding this project, prioritize features that help demonstrate these evaluation criteria clearly.

---

## 3. Problem Statement

Many IT students, Software Engineering students, Information Systems students, and career switchers want to understand Business Analysis or become Business Analysts, but they lack a realistic environment to practice requirement gathering.

Most learners study theory such as:

- User stories
- Use cases
- SRS documents
- BPMN diagrams
- Functional and non-functional requirements

However, when facing a real stakeholder, they often struggle with:

- Asking the right questions
- Identifying missing or unclear requirements
- Understanding business context
- Discovering business rules and edge cases
- Turning conversations into structured requirement documents
- Receiving detailed feedback on their performance

Req Simulator solves this by creating realistic AI-powered stakeholder simulations.

---

## 4. Target Users and Customers

### 4.1 Primary Users

- IT students
- Software Engineering students
- Information Systems students
- Career switchers who want to become Business Analysts
- Junior developers who want to improve requirement analysis skills

### 4.2 Paying Customers

Although students are the main users, the paying customers should be organizations:

- Universities
- IT training centers
- BA training centers
- Coding bootcamps
- Companies training interns or freshers

### 4.3 Business Positioning

Req Simulator should be positioned as a **B2B SaaS training tool** for education and workforce development.

---

## 5. Core Product Concept

Req Simulator allows learners to:

1. Select a business scenario.
2. Interview an AI-simulated stakeholder.
3. Ask questions to discover business needs, rules, constraints, and edge cases.
4. Take notes during the interview.
5. Submit requirement outputs such as user stories, use cases, or SRS drafts.
6. Receive AI-based scoring and feedback.
7. Optionally receive instructor/mentor review.

The platform should simulate the real difficulty of requirement gathering: stakeholders do not provide all information immediately. Learners must ask the right questions to uncover hidden requirements.

---

## 6. AI Usage

AI must be central to the product, not an add-on.

### 6.1 AI Stakeholder Simulation

The AI plays the role of a stakeholder in a realistic business context.

Example stakeholder roles:

- University staff for a course registration system
- Restaurant owner for a booking system
- HR manager for a recruitment workflow
- Clinic receptionist for an appointment system
- Warehouse manager for an inventory system

The AI should answer as the stakeholder, not as a teacher.

Example:

```text
Learner: Who can register for a course?
AI Stakeholder: Only students who have completed the prerequisite subjects can register.
```

### 6.2 Hidden Requirement and Information Gating

Some requirements should be hidden at the beginning. The AI only reveals them when the learner asks appropriate questions.

Example hidden requirements for a course registration system:

- Students cannot register if prerequisite subjects are not completed.
- Each course has limited capacity.
- Schedule conflicts must be checked.
- Payment must be completed before a deadline.
- Admins can override registration in special cases.

If the learner does not ask about business rules, constraints, or exceptions, the hidden information should remain undiscovered and be reported later as missing.

### 6.3 Requirement Gap Detection

After the interview, the system compares the learner's collected requirements against the scenario rubric.

The AI should detect:

- Missing actors
- Missing business rules
- Missing exception flows
- Ambiguous requirements
- Overly broad user stories
- Missing acceptance criteria
- Missing non-functional requirements

### 6.4 AI Feedback Coach

The AI provides personalized feedback.

Example feedback:

```text
You asked good questions about main features, but you missed important business rules such as course capacity, prerequisite validation, and cancellation policy.
```

### 6.5 Document Review Assistant

The learner can submit requirement documents, and AI reviews them.

Supported document types for MVP:

- User stories
- Acceptance criteria
- Use case description
- Simple SRS draft

The AI should suggest improvements but should not fully replace the learner's work.

---

## 7. Human-in-the-Loop Model

Req Simulator should use a Human-in-the-Loop model.

AI provides:

- Simulation
- Initial scoring
- Feedback
- Gap detection
- Document review

Human instructor or mentor provides:

- Final review
- Score adjustment
- Additional comments
- Scenario customization
- Class-level performance monitoring

This makes the product safer and more realistic for education.

---

## 8. Unique Value Proposition

Req Simulator is different from a normal chatbot.

| Generic Chatbot | Req Simulator |
|---|---|
| Learner must create prompts manually | Structured scenario library |
| AI answers freely | AI follows stakeholder persona |
| May reveal too much too early | Hidden requirements and information gating |
| No structured learning score | Scoring rubric and performance report |
| No class management | Instructor dashboard |
| No learning roadmap | Scenario difficulty levels and progress tracking |

Core differentiation:

> Req Simulator is a structured simulation environment for requirement gathering practice, not just an AI chatbot.

---

## 9. MVP Scope

Build only the features needed to demonstrate the core idea clearly.

### 9.1 MVP Features

1. User authentication
2. Scenario library
3. Scenario detail page
4. AI stakeholder chat
5. Learner note-taking area
6. Requirement submission form
7. AI feedback and scoring result
8. Basic instructor dashboard
9. Simple performance history

### 9.2 Out of Scope for MVP

Do not build these in the first version unless the core MVP is already stable:

- Mobile app
- Marketplace
- Complex team collaboration
- Custom model training
- Advanced analytics
- Payment gateway
- Real-time multiplayer
- Full LMS integration

---

## 10. Suggested Tech Stack

Use technologies that are practical and suitable for a student-built MVP.

### 10.1 Frontend

Preferred:

- React or Next.js
- TypeScript if possible
- Tailwind CSS or Bootstrap

Main pages:

- Landing page
- Login/Register
- Dashboard
- Scenario list
- Scenario detail
- Simulation chat page
- Submission page
- Feedback result page
- Instructor dashboard

### 10.2 Backend

Preferred:

- ASP.NET Core Web API

Alternative:

- Node.js / NestJS
- Python FastAPI

### 10.3 Database

Preferred:

- PostgreSQL

Possible extensions:

- pgvector for semantic similarity
- Redis for caching later

### 10.4 AI Integration

Use LLM API instead of training a custom model.

Possible providers:

- OpenAI API
- Gemini API
- Azure OpenAI

AI should be wrapped behind an internal service layer, not called directly from frontend.

### 10.5 Deployment

MVP deployment options:

- Docker
- Render
- Railway
- Azure App Service
- Vercel for frontend

---

## 11. Recommended Architecture

Use a simple modular architecture.

```text
Frontend
  -> Backend API
      -> Auth Service
      -> Scenario Service
      -> Simulation Service
      -> AI Evaluation Service
      -> Feedback Service
      -> Dashboard Service
      -> Database
      -> LLM Provider API
```

### 11.1 Important Backend Modules

- AuthModule
- UserModule
- ScenarioModule
- SimulationModule
- ChatMessageModule
- RequirementSubmissionModule
- EvaluationModule
- FeedbackModule
- InstructorDashboardModule

---

## 12. Data Model Draft

### 12.1 User

```text
User
- Id
- FullName
- Email
- PasswordHash
- Role: Student | Instructor | Admin
- CreatedAt
```

### 12.2 Scenario

```text
Scenario
- Id
- Title
- Domain
- Description
- Difficulty: Beginner | Intermediate | Advanced
- StakeholderRole
- StakeholderPersona
- InitialContext
- CreatedAt
```

### 12.3 HiddenRequirement

```text
HiddenRequirement
- Id
- ScenarioId
- Title
- Description
- RevealCondition
- Category: Actor | BusinessRule | Constraint | Exception | NonFunctional
- Importance: Low | Medium | High
```

### 12.4 SimulationSession

```text
SimulationSession
- Id
- UserId
- ScenarioId
- StartedAt
- EndedAt
- Status: InProgress | Submitted | Evaluated
```

### 12.5 ChatMessage

```text
ChatMessage
- Id
- SessionId
- Sender: Learner | AIStakeholder | System
- Content
- CreatedAt
```

### 12.6 LearnerNote

```text
LearnerNote
- Id
- SessionId
- Content
- UpdatedAt
```

### 12.7 RequirementSubmission

```text
RequirementSubmission
- Id
- SessionId
- UserStories
- UseCases
- AcceptanceCriteria
- AdditionalNotes
- SubmittedAt
```

### 12.8 EvaluationResult

```text
EvaluationResult
- Id
- SubmissionId
- CompletenessScore
- ClarityScore
- BusinessRuleScore
- QuestionQualityScore
- OverallScore
- MissingRequirementsJson
- FeedbackText
- CreatedAt
```

### 12.9 InstructorReview

```text
InstructorReview
- Id
- EvaluationResultId
- InstructorId
- AdjustedScore
- Comment
- ReviewedAt
```

---

## 13. First Demo Scenario

Use this scenario for MVP because it is easy to understand and relevant to students.

### Scenario: University Course Registration System

**Stakeholder role:** Training Department Staff  
**Context:** The university wants a system where students can register for courses online.

### Initial stakeholder message

```text
Our university wants to improve the course registration process. Currently, many students register manually or through outdated tools, which causes confusion and delays. We want a better online system.
```

### Main actors

- Student
- Training Department Staff
- Lecturer
- Admin

### Visible requirements

- Students can view available courses.
- Students can register for courses.
- Staff can manage course information.
- Admin can manage users.

### Hidden requirements

- Students must complete prerequisite subjects before registration.
- Each course has limited capacity.
- Students cannot register for courses with schedule conflicts.
- Students can cancel registration before a deadline.
- Some courses require staff approval.
- Admin can override registration in special cases.
- The system must send confirmation notifications.
- Registration history must be stored.

### Evaluation focus

The learner should ask about:

- Actors
- Main workflow
- Business rules
- Edge cases
- Constraints
- Permissions
- Notifications
- Reporting
- Non-functional requirements

---

## 14. AI Prompting Guidelines

### 14.1 Stakeholder Prompt Requirements

When generating AI stakeholder responses, the AI must:

- Stay in character as the stakeholder.
- Answer only based on the scenario and known/hidden requirements.
- Not reveal all hidden requirements at once.
- Reveal hidden information only when the learner asks relevant questions.
- Avoid giving direct BA advice during the interview.
- Use natural business language, not technical documentation style.

### 14.2 Example System Prompt for Stakeholder

```text
You are an AI stakeholder in a requirement gathering simulation.
Your role is: {StakeholderRole}.
Scenario: {ScenarioDescription}.
Persona: {StakeholderPersona}.

Rules:
1. Stay in character as the stakeholder.
2. Do not act as a teacher or Business Analyst.
3. Do not reveal all requirements immediately.
4. Reveal hidden requirements only if the learner asks relevant questions.
5. If the learner asks vague questions, answer naturally but incompletely.
6. If the learner asks about business rules, constraints, exceptions, or edge cases, reveal the relevant hidden requirements.
7. Keep answers concise and realistic.
```

### 14.3 Evaluation Prompt Requirements

When evaluating a submission, the AI must:

- Compare learner output against scenario requirements.
- Identify missing requirements.
- Score the learner using a rubric.
- Provide constructive feedback.
- Avoid harsh or discouraging language.
- Suggest practical next steps.

---

## 15. Evaluation Rubric

Score from 0 to 100.

### 15.1 Completeness — 30 points

Checks whether the learner identified main actors, workflows, and important requirements.

### 15.2 Business Rules — 20 points

Checks whether the learner discovered constraints, policies, rules, deadlines, permissions, and exceptions.

### 15.3 Question Quality — 20 points

Checks whether the learner asked clear, relevant, and structured questions during the interview.

### 15.4 Requirement Clarity — 20 points

Checks whether the submitted user stories/use cases are clear, specific, and testable.

### 15.5 Improvement Awareness — 10 points

Checks whether the learner can reflect on missing areas and improvement opportunities.

---

## 16. Business Model

### 16.1 Main Model

Req Simulator uses a **B2B SaaS subscription model**.

The product is sold to:

- Universities
- IT training centers
- BA training centers
- Bootcamps
- Companies training interns/freshers

### 16.2 Pricing Plans

```text
Starter Plan
- Target: Small classes or training centers
- Price: 5,000,000 VND/month
- Includes up to 100 students

Education Plan
- Target: Universities
- Price: 15,000,000 VND/month
- Includes up to 500 students
- Includes instructor dashboard and class reports

Enterprise Training Plan
- Target: Companies training interns/freshers
- Price: 30,000,000 VND/month
- Includes custom scenarios, team analytics, and private deployment option
```

---

## 17. Investment Ask

### 17.1 Funding Request

**Investment ask:** 1.5 billion VND  
**Runway:** 12 months

This funding is intended to build the MVP, run pilots, improve the product, and acquire the first paying customers.

### 17.2 Budget Breakdown

| Category | Amount | Percentage |
|---|---:|---:|
| Team salary | 900M VND | 60% |
| Cloud, AI API, tools | 225M VND | 15% |
| Marketing & pilot programs | 225M VND | 15% |
| Operations, legal, contingency | 150M VND | 10% |
| Total | 1.5B VND | 100% |

---

## 18. Hiring Plan

### 18.1 Core Team for 12 Months

| Role | Quantity | Estimated Monthly Salary |
|---|---:|---:|
| Founder / Product Owner | 1 | 0–15M VND |
| Backend Engineer | 1 | 20M VND |
| Frontend Engineer | 1 | 18M VND |
| AI Engineer / Prompt Engineer | 1 | 25M VND |
| UI/UX Designer part-time | 1 | 8M VND |
| Education / BA Consultant part-time | 1 | 10M VND |
| Sales / Marketing part-time | 1 | 10M VND |

### 18.2 Hiring Logic

- Keep the early team small.
- Prioritize product development and pilot validation.
- Use part-time domain experts to reduce cost.
- Founder accepts low salary to extend runway.

---

## 19. Product Roadmap

### Phase 1: MVP — Month 0 to 3

Goals:

- Build core simulation flow.
- Create first scenario library.
- Implement AI stakeholder chat.
- Implement AI feedback and scoring.
- Test with 30–50 students.

Deliverables:

- Working MVP
- 3–5 scenarios
- Basic scoring rubric
- Basic student dashboard

### Phase 2: Pilot — Month 4 to 6

Goals:

- Pilot with 2–3 classes or training groups.
- Add instructor dashboard.
- Improve AI feedback quality.
- Collect user feedback.

Deliverables:

- Instructor dashboard
- Improved scenario management
- Pilot report
- 100–200 active users

### Phase 3: Product v1 — Month 7 to 12

Goals:

- Convert pilot users into paying customers.
- Add custom scenario builder.
- Add class analytics.
- Improve onboarding and reporting.

Deliverables:

- Product v1
- 3–5 paying customers
- Monthly revenue target: 45M–100M VND

---

## 20. Risks and Mitigation

### 20.1 AI Hallucination

Risk: AI may provide inconsistent or incorrect stakeholder answers.

Mitigation:

- Use scenario constraints.
- Use structured hidden requirement data.
- Keep AI responses grounded in scenario context.
- Add instructor review.

### 20.2 Learners Over-Rely on AI

Risk: Learners may expect AI to do the BA work for them.

Mitigation:

- AI stakeholder does not teach during interview.
- Learners must submit their own requirements.
- AI feedback happens after submission.

### 20.3 Adoption by Universities

Risk: Universities may be slow to adopt new tools.

Mitigation:

- Offer pilot programs.
- Provide instructor dashboard.
- Align with existing Software Engineering and Requirement Engineering courses.

### 20.4 AI API Cost

Risk: AI API usage may become expensive.

Mitigation:

- Limit session length.
- Use token optimization.
- Cache scenario context.
- Use smaller models for simple feedback.

---

## 21. Coding Priorities

Build in this order:

1. Database schema
2. Authentication
3. Scenario CRUD
4. Simulation session creation
5. Chat UI
6. AI stakeholder response service
7. Requirement submission
8. AI evaluation service
9. Feedback result page
10. Instructor dashboard

Do not start with advanced features. The first goal is to prove the end-to-end learning flow.

---

## 22. Definition of Done for MVP Demo

The MVP demo is considered successful if a user can:

1. Log in as a student.
2. Choose the Course Registration scenario.
3. Start an AI stakeholder interview.
4. Ask at least 5–10 questions.
5. Take notes.
6. Submit user stories or requirement notes.
7. Receive AI score and feedback.
8. View missing requirements.

An instructor should be able to:

1. View student sessions.
2. View scores.
3. Read AI feedback.
4. Add a simple review comment.

---

## 23. Presentation Reminder

When explaining this project, use this one-sentence pitch:

> Req Simulator helps IT students and career switchers practice requirement gathering by interviewing AI-simulated stakeholders, discovering hidden requirements, and receiving structured feedback like a real Business Analyst training environment.

Keep the story focused on:

1. Real student pain point
2. AI as stakeholder and feedback coach
3. Structured simulation, not generic chatbot
4. Feasible MVP
5. Clear B2B SaaS business model
