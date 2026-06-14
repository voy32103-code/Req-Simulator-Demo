# Database Design

## Core Tables

- Users
- Scenarios
- HiddenRequirements
- SimulationSessions
- ChatMessages
- LearnerNotes
- RequirementSubmissions
- EvaluationResults
- InstructorReviews

## Important Relationships

- A user has many simulation sessions.
- A scenario has many hidden requirements.
- A simulation session belongs to one user and one scenario.
- A simulation session has many chat messages.
- A simulation session has one learner note.
- A simulation session has one requirement submission.
- A requirement submission has one evaluation result.
- An evaluation result can have one instructor review.

## Future Notes

The current MVP stores `users` in PostgreSQL. The remaining simulation data is still in-memory demo state and should be moved into PostgreSQL next.

`pgvector` can be added later if semantic matching is needed for requirement gap detection.
