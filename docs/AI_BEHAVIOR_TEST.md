# AI Behavior Test

Use this checklist to verify that the e-commerce stakeholder stays in the correct domain and reveals hidden requirements gradually.

## Before Testing

1. Start the backend.
2. Open the frontend.
3. Select `E-commerce Order & Promotion System`.
4. Click `Start Interview` to create a fresh session.
5. Do not reuse an old session that may contain contaminated chat history from an earlier build.

## Test 1 - General overview only

Question:

```text
What are the requirements?
```

Expected:

- The AI gives a general e-commerce overview only.
- The AI does not reveal hidden business rules immediately.
- The AI does not mention course registration, prerequisite subjects, university staff, lecturers, or training department.
- The AI asks the learner to choose an area such as voucher, stock, payment, shipping, cancellation, or refund.
- The answer sounds like a practical operations manager, not a rules engine or teacher.
- The answer should feel like a short interview response, not a one-line summary.

## Test 2 - Voucher rules only

Question:

```text
Are there any rules for applying vouchers?
```

Expected:

- The AI reveals voucher minimum order value.
- The AI may also reveal voucher category restriction.
- The AI does not reveal unrelated stock, payment, shipping, or reporting rules.
- The AI does not reveal every voucher rule at once.
- The answer includes a short business reason naturally.
- The answer can include a realistic operational example, but it must stay inside the voucher topic.

## Test 3 - Stock handling only

Question:

```text
How is stock handled during checkout?
```

Expected:

- The AI reveals stock check before checkout.
- The AI reveals stock reservation after order placement.
- The AI does not reveal voucher or reporting rules.
- The answer sounds operational and non-technical.
- The answer should mention the pain point of overselling or manual follow-up naturally.

## Test 4 - Payment failure handling

Question:

```text
What happens if payment fails?
```

Expected:

- The AI says the order should remain pending or failed.
- The AI may mention that payment timeout should release reserved stock.
- The AI does not switch to course registration content.
- The answer should explain why staff should not process unpaid orders.

## Test 5 - Out-of-domain question

Question:

```text
Can students register for courses?
```

Expected:

- The AI says this scenario is about online store order management, not course registration.
- The AI does not continue with prerequisite subjects or university workflow.

## Acceptance Criteria

- AI never mentions course registration in the e-commerce scenario unless the learner is explicitly asking an out-of-domain question, in which case the AI redirects back to the e-commerce scenario.
- `What are the requirements?` returns a general answer only.
- Voucher questions reveal voucher-related rules only.
- Stock questions reveal stock-related rules only.
- Payment-failure questions reveal payment-related rules only.
- Hidden requirements are filtered by the current scenario.
- Current-session prompt context uses only the selected scenario data plus already revealed requirements and current-session chat history.
- API keys remain in the backend only.
- The stakeholder sounds practical, business-oriented, and concise instead of scripted.
- Normal answers feel like 3-6 natural sentences, not one short sentence.
- Category answers include business reasoning and operational context without turning into long documentation.
