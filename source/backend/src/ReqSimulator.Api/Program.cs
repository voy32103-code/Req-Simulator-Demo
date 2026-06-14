using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

const string CurrentUserIdHeader = "X-ReqSim-UserId";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var rawDatabaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["DATABASE_URL"];
var databaseConnectionString = PostgresConnectionString.Normalize(rawDatabaseConnectionString);

builder.Services.AddSingleton<DemoStore>();
builder.Services.AddSingleton(new DatabaseOptions(databaseConnectionString));
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton(GeminiOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient(GeminiOptions.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<MockAiService>();
builder.Services.AddSingleton<GeminiAiService>();
builder.Services.AddSingleton<IAiService>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<GeminiOptions>();
    var mock = serviceProvider.GetRequiredService<MockAiService>();

    if (!options.IsConfigured)
    {
        return mock;
    }

    return new ResilientAiService(
        serviceProvider.GetRequiredService<GeminiAiService>(),
        mock,
        serviceProvider.GetRequiredService<ILogger<ResilientAiService>>());
});

var app = builder.Build();
var geminiOptions = app.Services.GetRequiredService<GeminiOptions>();

if (geminiOptions.IsConfigured)
{
    app.Logger.LogInformation(
        "AI provider configured: Gemini (Model: {Model}, Source: {Source})",
        geminiOptions.Model,
        geminiOptions.KeySource);
    app.Logger.LogInformation(
        "AI runtime not verified yet. The first live Gemini request will confirm whether quota and network access are available.");
}
else
{
    app.Logger.LogWarning("No AI API key found. Falling back to MockAiService.");
}

app.UseCors();

await app.Services.GetRequiredService<UserRepository>().InitializeAsync();

app.MapGet("/api/health", () => Results.Ok(new
{
    ok = true,
    app = "Req Simulator API",
    databaseConfigured = !string.IsNullOrWhiteSpace(databaseConnectionString),
    aiProvider = app.Services.GetRequiredService<GeminiOptions>().IsConfigured ? "Gemini" : "Mock"
}));

app.MapGet("/api/database/status", () => Results.Ok(new
{
    provider = "PostgreSQL",
    configured = !string.IsNullOrWhiteSpace(databaseConnectionString),
    source = !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection"))
        ? "ConnectionStrings:DefaultConnection"
        : !string.IsNullOrWhiteSpace(app.Configuration["DATABASE_URL"])
            ? "DATABASE_URL"
            : "Not configured"
}));

app.MapGet("/api/ai/status", (GeminiOptions options) => Results.Ok(new
{
    provider = options.IsConfigured ? "Gemini" : "Mock",
    configured = options.IsConfigured,
    model = options.IsConfigured ? options.Model : "mock",
    safeMessage = options.IsConfigured
        ? "AI provider is configured. API key is hidden."
        : "No AI API key found. Using mock AI service."
}));

app.MapPost("/api/ai/test", async (
    AiTestRequest request,
    DemoStore store,
    IAiService aiService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("ReqSimulator.AiTest");
    var scenario = store.Scenarios
        .FirstOrDefault(item => item.Id == request.ScenarioId)
        ?? store.Scenarios.FirstOrDefault(item => item.Domain.Equals("E-commerce", StringComparison.OrdinalIgnoreCase))
        ?? store.Scenarios.First();
    var language = NormalizeLanguage(request.Language ?? request.ResponseLanguage);

    var aiRequest = SanitizeStakeholderRequest(new AiStakeholderRequest
    {
        ScenarioTitle = scenario.Title,
        Domain = scenario.Domain,
        ScenarioDescription = scenario.Description,
        StakeholderRole = scenario.StakeholderRole,
        StakeholderPersona = scenario.StakeholderPersona,
        InitialContext = scenario.InitialContext,
        VisibleRequirements = scenario.VisibleRequirements,
        HiddenRequirements = scenario.HiddenRequirements.Select(item => $"{item.Title}: {item.Description}").ToList(),
        ChatHistory = [],
        LearnerMessage = string.IsNullOrWhiteSpace(request.Message)
            ? "Say hello as an e-commerce operations manager."
            : request.Message.Trim(),
        ResponseLanguage = ToResponseLanguage(language)
    }, logger);

    var reply = await aiService.GenerateStakeholderReplyAsync(aiRequest, cancellationToken);
    return Results.Ok(new
    {
        reply = reply.Reply,
        category = GetReplyCategory(scenario, aiRequest.LearnerMessage, []),
        provider = reply.Provider
    });
});

app.MapPost("/api/auth/login", async (LoginRequest request, UserRepository users) =>
{
    var email = NormalizeEmail(request.Email);
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email and password are required" });
    }

    var user = await users.FindByEmailAsync(email);
    if (user is null || !PasswordTools.Verify(request.Password, user.PasswordHash))
    {
        return Results.Json(new { message = "Invalid email or password" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new { user = ToUserDto(user) });
});

app.MapPost("/api/auth/register", async (RegisterRequest request, UserRepository users) =>
{
    var email = NormalizeEmail(request.Email);
    if (string.IsNullOrWhiteSpace(request.FullName) ||
        string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Full name, email, and password are required" });
    }

    if (request.Password.Length < 6)
    {
        return Results.BadRequest(new { message = "Password must be at least 6 characters" });
    }

    if (await users.EmailExistsAsync(email))
    {
        return Results.Conflict(new { message = "Email is already registered" });
    }

    var role = NormalizeRole(request.Role);
    var user = new User
    {
        Id = CreateId("user"),
        FullName = request.FullName.Trim(),
        Email = email,
        Role = role,
        PasswordHash = PasswordTools.Hash(request.Password),
        CreatedAt = DateTimeOffset.UtcNow
    };

    await users.CreateAsync(user);

    return Results.Created($"/api/users/{user.Id}", new { user = ToUserDto(user) });
});

app.MapPost("/api/auth/external-demo", async (ExternalLoginRequest request, UserRepository users) =>
{
    var provider = NormalizeExternalProvider(request.Provider);
    if (provider is null)
    {
        return Results.BadRequest(new { message = "Supported providers: Google, GitHub" });
    }

    var user = await users.GetOrCreateExternalDemoUserAsync(provider);
    return Results.Ok(new { user = ToUserDto(user) });
});

app.MapGet("/api/scenarios", (DemoStore store) =>
    Results.Ok(new { scenarios = store.Scenarios.Select(ToScenarioSummary) }));

app.MapGet("/api/scenarios/{id}", (string id, DemoStore store) =>
{
    var scenario = store.Scenarios.FirstOrDefault(item => item.Id == id);
    return scenario is null
        ? Results.NotFound(new { message = "Scenario not found" })
        : Results.Ok(new { scenario = ToScenarioSummary(scenario) });
});

app.MapPost("/api/sessions", async (CreateSessionRequest request, HttpContext httpContext, DemoStore store, UserRepository users, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("ReqSimulator.Session");
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    var scenario = store.Scenarios.FirstOrDefault(item => item.Id == request.ScenarioId);

    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    if (!string.IsNullOrWhiteSpace(request.UserId) && request.UserId != currentUser.Id)
    {
        return ForbiddenResult("You can only create sessions for your own account.");
    }

    if (scenario is null)
    {
        return Results.BadRequest(new { message = "Scenario not found" });
    }

    var now = DateTimeOffset.UtcNow;
    var session = new SimulationSession
    {
        Id = CreateId("session"),
        UserId = currentUser.Id,
        ScenarioId = scenario.Id,
        StartedAt = now,
        Status = "InProgress"
    };

    store.Sessions.Add(session);
    store.Messages.Add(new ChatMessage
    {
        Id = CreateId("message"),
        SessionId = session.Id,
        Sender = "AIStakeholder",
        Content = scenario.InitialContext,
        CreatedAt = now
    });
    store.Notes.Add(new LearnerNote
    {
        Id = CreateId("note"),
        SessionId = session.Id,
        Content = "",
        UpdatedAt = now
    });

    logger.LogInformation(
        "Created fresh simulation session. SessionId: {SessionId}, ScenarioId: {ScenarioId}, ScenarioTitle: {ScenarioTitle}, Domain: {Domain}",
        session.Id,
        scenario.Id,
        scenario.Title,
        scenario.Domain);

    return Results.Created($"/api/sessions/{session.Id}", new { session = ComposeSession(store, session.Id) });
});

app.MapGet("/api/sessions/{id}", async (string id, HttpContext httpContext, DemoStore store, UserRepository users) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == id);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    if (!CanViewSession(currentUser, session))
    {
        return ForbiddenResult();
    }

    return Results.Ok(new { session = ComposeSession(store, id) });
});

app.MapPost("/api/sessions/{id}/messages", async (
    string id,
    AddMessageRequest request,
    HttpContext httpContext,
    DemoStore store,
    UserRepository users,
    IAiService aiService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
        await AddSimulationMessageAsync(id, request.Content, httpContext, store, users, aiService, cancellationToken, loggerFactory, request.Language, request.ResponseLanguage));

app.MapPost("/api/simulation/{sessionId}/message", async (
    string sessionId,
    SimulationMessageRequest request,
    HttpContext httpContext,
    DemoStore store,
    UserRepository users,
    IAiService aiService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
        await AddSimulationMessageAsync(sessionId, request.Message, httpContext, store, users, aiService, cancellationToken, loggerFactory, request.Language, request.ResponseLanguage));

app.MapPut("/api/sessions/{id}/notes", async (string id, SaveNoteRequest request, HttpContext httpContext, DemoStore store, UserRepository users) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == id);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    if (!CanEditSession(currentUser, session))
    {
        return ForbiddenResult("Only the session owner can update notes.");
    }

    var note = store.Notes.FirstOrDefault(item => item.SessionId == id);
    if (note is null)
    {
        note = new LearnerNote { Id = CreateId("note"), SessionId = id };
        store.Notes.Add(note);
    }

    note.Content = request.Content ?? "";
    note.UpdatedAt = DateTimeOffset.UtcNow;

    return Results.Ok(new { note });
});

app.MapPost("/api/sessions/{id}/submissions", async (
    string id,
    RequirementSubmission request,
    HttpContext httpContext,
    DemoStore store,
    UserRepository users,
    IAiService aiService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == id);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    if (!CanEditSession(currentUser, session))
    {
        return ForbiddenResult("Only the session owner can submit requirements.");
    }

    return await EvaluateAndSaveSubmissionAsync(id, request, store, aiService, cancellationToken, loggerFactory);
});

app.MapPost("/api/evaluation/{sessionId}", async (
    string sessionId,
    EvaluationApiRequest request,
    HttpContext httpContext,
    DemoStore store,
    UserRepository users,
    IAiService aiService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    if (!CanEditSession(currentUser, session))
    {
        return ForbiddenResult("Only the session owner can evaluate this submission.");
    }

    var submission = new RequirementSubmission
    {
        UserStories = request.UserStories,
        UseCases = request.UseCases,
        AcceptanceCriteria = request.AcceptanceCriteria,
        AdditionalNotes = string.IsNullOrWhiteSpace(request.AdditionalNotes)
            ? request.LearnerSubmission
            : request.AdditionalNotes,
        Reflection = request.Reflection
    };

    return await EvaluateAndSaveSubmissionAsync(sessionId, submission, store, aiService, cancellationToken, loggerFactory, request.Language, request.ResponseLanguage);
});

app.MapGet("/api/sessions/{id}/evaluation", async (string id, HttpContext httpContext, DemoStore store, UserRepository users) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == id);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    if (!CanViewSession(currentUser, session))
    {
        return ForbiddenResult();
    }

    var evaluation = store.Evaluations.FirstOrDefault(item => item.SessionId == id);
    return evaluation is null
        ? Results.NotFound(new { message = "Evaluation not found" })
        : Results.Ok(new { evaluation });
});

app.MapGet("/api/instructor/dashboard", async (HttpContext httpContext, DemoStore store, UserRepository users) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    if (!CanUseInstructorTools(currentUser))
    {
        return ForbiddenResult("Instructor or admin access is required.");
    }

    var evaluations = store.Evaluations;
    var averageScore = evaluations.Count == 0 ? 0 : (int)Math.Round(evaluations.Average(item => item.OverallScore));
    var commonGaps = evaluations
        .SelectMany(item => item.MissingRequirementsJson)
        .GroupBy(item => item.Title)
        .OrderByDescending(group => group.Count())
        .Take(5)
        .Select(group => new { title = group.Key, count = group.Count() })
        .ToList();

    var userLookup = await users.GetByIdsAsync(store.Sessions.Select(item => item.UserId).Distinct());

    var sessions = store.Sessions
        .OrderByDescending(item => item.StartedAt)
        .Select(session =>
        {
            userLookup.TryGetValue(session.UserId, out var user);
            var scenario = store.Scenarios.FirstOrDefault(item => item.Id == session.ScenarioId);
            var evaluation = store.Evaluations.FirstOrDefault(item => item.SessionId == session.Id);
            var review = store.Reviews.FirstOrDefault(item => item.SessionId == session.Id);

            return new
            {
                session.Id,
                studentName = user?.FullName ?? "Unknown learner",
                scenarioTitle = scenario?.Title ?? "Unknown scenario",
                session.StartedAt,
                session.Status,
                discoveredCount = session.DiscoveredRequirementIds.Count,
                hiddenRequirementCount = scenario?.HiddenRequirements.Count ?? 0,
                evaluation,
                review
            };
        });

    return Results.Ok(new
    {
        summary = new
        {
            totalSessions = store.Sessions.Count,
            evaluatedSessions = store.Sessions.Count(item => item.Status == "Evaluated"),
            averageScore,
            pendingReviews = evaluations.Count - store.Reviews.Count
        },
        sessions,
        commonGaps
    });
});

app.MapPost("/api/instructor/reviews", async (InstructorReview request, HttpContext httpContext, DemoStore store, UserRepository users) =>
{
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    if (!CanUseInstructorTools(currentUser))
    {
        return ForbiddenResult("Instructor or admin access is required.");
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == request.SessionId);
    var evaluation = store.Evaluations.FirstOrDefault(item => item.SessionId == request.SessionId);

    if (session is null || evaluation is null)
    {
        return Results.BadRequest(new { message = "Evaluated session not found" });
    }

    var review = store.Reviews.FirstOrDefault(item => item.SessionId == request.SessionId);
    if (review is null)
    {
        review = new InstructorReview
        {
            Id = CreateId("review"),
            SessionId = request.SessionId,
            InstructorId = currentUser.Id
        };
        store.Reviews.Add(review);
    }

    review.InstructorId = currentUser.Id;
    review.AdjustedScore = request.AdjustedScore;
    review.Comment = request.Comment ?? "";
    review.ReviewedAt = DateTimeOffset.UtcNow;

    return Results.Created($"/api/instructor/reviews/{review.Id}", new { review });
});

app.Run();

static object ToScenarioSummary(Scenario scenario)
{
    return new
    {
        scenario.Id,
        scenario.Title,
        scenario.Domain,
        scenario.Description,
        scenario.Difficulty,
        scenario.StakeholderRole,
        scenario.StakeholderPersona,
        scenario.InitialContext,
        scenario.EstimatedMinutes,
        scenario.VisibleRequirements,
        scenario.Actors,
        scenario.SuggestedQuestions,
        hiddenRequirementCount = scenario.HiddenRequirements.Count,
        scenario.EvaluationFocus
    };
}

static object ComposeSession(DemoStore store, string sessionId)
{
    var session = store.Sessions.First(item => item.Id == sessionId);
    var scenario = store.Scenarios.First(item => item.Id == session.ScenarioId);
    var messages = store.Messages
        .Where(item => item.SessionId == sessionId)
        .OrderBy(item => item.CreatedAt)
        .ToList();

    return new
    {
        session.Id,
        session.UserId,
        session.ScenarioId,
        session.StartedAt,
        session.EndedAt,
        session.Status,
        session.DiscoveredRequirementIds,
        scenario = ToScenarioSummary(scenario),
        messages,
        note = store.Notes.FirstOrDefault(item => item.SessionId == sessionId),
        submission = store.Submissions.FirstOrDefault(item => item.SessionId == sessionId),
        evaluation = store.Evaluations.FirstOrDefault(item => item.SessionId == sessionId),
        review = store.Reviews.FirstOrDefault(item => item.SessionId == sessionId),
        discoveredCount = session.DiscoveredRequirementIds.Count,
        hiddenRequirementCount = scenario.HiddenRequirements.Count
    };
}

static object ToUserDto(User user)
{
    return new
    {
        user.Id,
        user.FullName,
        user.Email,
        user.Role,
        permissions = GetRolePermissions(user.Role),
        user.AuthProvider,
        user.CreatedAt
    };
}

static async Task<User?> ResolveCurrentUserAsync(HttpContext httpContext, UserRepository users)
{
    if (!httpContext.Request.Headers.TryGetValue(CurrentUserIdHeader, out var values))
    {
        return null;
    }

    var userId = values.FirstOrDefault()?.Trim();
    return string.IsNullOrWhiteSpace(userId)
        ? null
        : await users.FindByIdAsync(userId);
}

static bool CanViewSession(User user, SimulationSession session)
{
    return session.UserId == user.Id || CanUseInstructorTools(user);
}

static bool CanEditSession(User user, SimulationSession session)
{
    return session.UserId == user.Id;
}

static bool CanUseInstructorTools(User user)
{
    return user.Role is "Instructor" or "Admin";
}

static string[] GetRolePermissions(string role)
{
    return role switch
    {
        "Admin" => ["session:create", "session:read:any", "session:review", "dashboard:read", "review:create"],
        "Instructor" => ["session:create", "session:read:any", "session:review", "dashboard:read", "review:create"],
        _ => ["session:create", "session:read:own", "session:write:own", "evaluation:submit:own"]
    };
}

static IResult UnauthorizedResult(string message = "Sign in is required.")
{
    return Results.Json(new { message }, statusCode: StatusCodes.Status401Unauthorized);
}

static IResult ForbiddenResult(string message = "You do not have permission for this action.")
{
    return Results.Json(new { message }, statusCode: StatusCodes.Status403Forbidden);
}

static async Task<IResult> AddSimulationMessageAsync(
    string sessionId,
    string? messageContent,
    HttpContext httpContext,
    DemoStore store,
    UserRepository users,
    IAiService aiService,
    CancellationToken cancellationToken,
    ILoggerFactory loggerFactory,
    string? language = null,
    string? responseLanguage = null)
{
    var logger = loggerFactory.CreateLogger("ReqSimulator.Simulation");
    var currentUser = await ResolveCurrentUserAsync(httpContext, users);
    if (currentUser is null)
    {
        return UnauthorizedResult();
    }

    var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    if (!CanEditSession(currentUser, session))
    {
        return ForbiddenResult("Only the session owner can continue this interview.");
    }

    if (string.IsNullOrWhiteSpace(messageContent))
    {
        return Results.BadRequest(new { message = "Message content is required" });
    }

    var scenario = store.Scenarios.First(item => item.Id == session.ScenarioId);
    var history = store.Messages
        .Where(item => item.SessionId == sessionId)
        .OrderBy(item => item.CreatedAt)
        .ToList();
    var trimmedMessage = messageContent.Trim();
    var now = DateTimeOffset.UtcNow;
    var learnerMessage = new ChatMessage
    {
        Id = CreateId("message"),
        SessionId = session.Id,
        Sender = "Learner",
        Content = trimmedMessage,
        CreatedAt = now
    };

    var normalizedLanguage = NormalizeLanguage(language ?? responseLanguage);
    logger.LogInformation(
        "Preparing AI stakeholder reply. ScenarioId: {ScenarioId}, ScenarioTitle: {ScenarioTitle}, Domain: {Domain}, VisibleCount: {VisibleCount}, HiddenCount: {HiddenCount}",
        scenario.Id,
        scenario.Title,
        scenario.Domain,
        scenario.VisibleRequirements.Count,
        scenario.HiddenRequirements.Count);

    var aiRequest = BuildStakeholderRequest(scenario, session, history, trimmedMessage, normalizedLanguage, logger);
    var aiReply = await aiService.GenerateStakeholderReplyAsync(aiRequest, cancellationToken);
    var revealedRequirementIds = FindRevealedRequirementIds(scenario, session, trimmedMessage);
    var category = GetReplyCategory(scenario, trimmedMessage, revealedRequirementIds);
    var discoveredRequirements = scenario.HiddenRequirements
        .Where(item => revealedRequirementIds.Contains(item.Id))
        .Select(item => item.Description)
        .ToList();
    session.DiscoveredRequirementIds = session.DiscoveredRequirementIds
        .Concat(revealedRequirementIds)
        .Distinct()
        .ToList();

    var stakeholderMessage = new ChatMessage
    {
        Id = CreateId("message"),
        SessionId = session.Id,
        Sender = "AIStakeholder",
        Content = string.IsNullOrWhiteSpace(aiReply.Reply)
            ? "Our main goal is to reduce manual work and make the process clearer for users."
            : aiReply.Reply.Trim(),
        CreatedAt = now.AddMilliseconds(100),
        RevealedRequirementIds = revealedRequirementIds
    };

    store.Messages.Add(learnerMessage);
    store.Messages.Add(stakeholderMessage);

    logger.LogInformation(
        "AI stakeholder reply completed. ScenarioId: {ScenarioId}, ScenarioTitle: {ScenarioTitle}, Domain: {Domain}, Provider: {Provider}, RevealedCount: {RevealedCount}",
        scenario.Id,
        scenario.Title,
        scenario.Domain,
        aiReply.Provider,
        revealedRequirementIds.Count);

    return Results.Created($"/api/sessions/{session.Id}/messages/{stakeholderMessage.Id}", new
    {
        reply = stakeholderMessage.Content,
        category,
        provider = aiReply.Provider,
        createdAt = stakeholderMessage.CreatedAt,
        discoveredRequirements,
        learnerMessage,
        stakeholderMessage,
        session = ComposeSession(store, session.Id)
    });
}

static async Task<IResult> EvaluateAndSaveSubmissionAsync(
    string sessionId,
    RequirementSubmission submission,
    DemoStore store,
    IAiService aiService,
    CancellationToken cancellationToken,
    ILoggerFactory loggerFactory,
    string? language = null,
    string? responseLanguage = null)
{
    var logger = loggerFactory.CreateLogger("ReqSimulator.Evaluation");
    var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
    if (session is null)
    {
        return Results.NotFound(new { message = "Session not found" });
    }

    var scenario = store.Scenarios.First(item => item.Id == session.ScenarioId);
    var messages = store.Messages
        .Where(item => item.SessionId == sessionId)
        .OrderBy(item => item.CreatedAt)
        .ToList();
    var note = store.Notes.FirstOrDefault(item => item.SessionId == sessionId);

    submission.Id = CreateId("submission");
    submission.SessionId = sessionId;
    submission.SubmittedAt = DateTimeOffset.UtcNow;

    var normalizedLanguage = NormalizeLanguage(language ?? responseLanguage);
    var aiRequest = BuildEvaluationRequest(scenario, messages, note, submission, normalizedLanguage);
    var aiEvaluation = await aiService.EvaluateSubmissionAsync(aiRequest, cancellationToken);
    var evaluation = ToStoredEvaluation(aiEvaluation.Result);
    evaluation.Id = CreateId("evaluation");
    evaluation.SubmissionId = submission.Id;
    evaluation.SessionId = sessionId;
    evaluation.CreatedAt = DateTimeOffset.UtcNow;

    session.Status = "Evaluated";
    session.EndedAt = DateTimeOffset.UtcNow;
    store.Submissions.RemoveAll(item => item.SessionId == sessionId);
    store.Evaluations.RemoveAll(item => item.SessionId == sessionId);
    store.Submissions.Add(submission);
    store.Evaluations.Add(evaluation);

    logger.LogInformation(
        "AI evaluation completed. ScenarioId: {ScenarioId}, ScenarioTitle: {ScenarioTitle}, Domain: {Domain}, Provider: {Provider}, VisibleCount: {VisibleCount}, HiddenCount: {HiddenCount}",
        scenario.Id,
        scenario.Title,
        scenario.Domain,
        aiEvaluation.Provider,
        scenario.VisibleRequirements.Count,
        scenario.HiddenRequirements.Count);

    return Results.Created($"/api/sessions/{sessionId}/evaluation", new
    {
        provider = aiEvaluation.Provider,
        overallScore = evaluation.OverallScore,
        completenessScore = evaluation.CompletenessScore,
        businessRuleScore = evaluation.BusinessRuleScore,
        questionQualityScore = evaluation.QuestionQualityScore,
        clarityScore = evaluation.ClarityScore,
        improvementScore = evaluation.ImprovementAwarenessScore,
        missingRequirements = evaluation.MissingRequirementsJson.Select(item => item.Title).ToList(),
        feedback = evaluation.FeedbackText,
        submission,
        evaluation,
        aiEvaluation = aiEvaluation.Result,
        session = ComposeSession(store, sessionId)
    });
}

static AiStakeholderRequest BuildStakeholderRequest(
    Scenario scenario,
    SimulationSession session,
    List<ChatMessage> history,
    string learnerMessage,
    string language,
    ILogger logger)
{
    var request = new AiStakeholderRequest
    {
        ScenarioTitle = scenario.Title,
        Domain = scenario.Domain,
        ScenarioDescription = scenario.Description,
        StakeholderRole = scenario.StakeholderRole,
        StakeholderPersona = scenario.StakeholderPersona,
        InitialContext = scenario.InitialContext,
        VisibleRequirements = scenario.VisibleRequirements.ToList(),
        HiddenRequirements = scenario.HiddenRequirements
            .Select(item => $"{item.Title}: {item.Description} Reveal condition: {item.RevealCondition}")
            .ToList(),
        AlreadyRevealedRequirements = scenario.HiddenRequirements
            .Where(item => session.DiscoveredRequirementIds.Contains(item.Id))
            .Select(item => $"{item.Title}: {item.Description}")
            .ToList(),
        ChatHistory = history.Select(ToChatMessageDto).ToList(),
        LearnerMessage = learnerMessage,
        ResponseLanguage = ToResponseLanguage(language)
    };

    return SanitizeStakeholderRequest(request, logger);
}

static AiEvaluationRequest BuildEvaluationRequest(
    Scenario scenario,
    List<ChatMessage> messages,
    LearnerNote? note,
    RequirementSubmission submission,
    string language)
{
    var masterRequirements = scenario.VisibleRequirements
        .Concat(scenario.HiddenRequirements.Select(item => $"{item.Title}: {item.Description}"))
        .ToList();

    return new AiEvaluationRequest
    {
        ScenarioTitle = scenario.Title,
        Domain = scenario.Domain,
        ScenarioDescription = scenario.Description,
        MasterRequirements = masterRequirements,
        ChatHistory = messages.Select(ToChatMessageDto).ToList(),
        LearnerSubmission = BuildLearnerSubmission(submission, note),
        ResponseLanguage = ToResponseLanguage(language)
    };
}

static ChatMessageDto ToChatMessageDto(ChatMessage message)
{
    return new ChatMessageDto
    {
        Sender = message.Sender,
        Content = message.Content,
        CreatedAt = message.CreatedAt.UtcDateTime
    };
}

static string BuildLearnerSubmission(RequirementSubmission submission, LearnerNote? note)
{
    return string.Join("\n\n", new[]
    {
        $"Learner notes:\n{note?.Content ?? ""}",
        $"User stories:\n{submission.UserStories ?? ""}",
        $"Use cases:\n{submission.UseCases ?? ""}",
        $"Acceptance criteria:\n{submission.AcceptanceCriteria ?? ""}",
        $"Additional notes:\n{submission.AdditionalNotes ?? ""}",
        $"Reflection:\n{submission.Reflection ?? ""}"
    });
}

static EvaluationResult ToStoredEvaluation(EvaluationResultDto dto)
{
    var completenessScore = Clamp(dto.CompletenessScore, 0, 30);
    var businessRuleScore = Clamp(dto.BusinessRuleScore, 0, 25);
    var questionQualityScore = Clamp(dto.QuestionQualityScore, 0, 20);
    var clarityScore = Clamp(dto.ClarityScore, 0, 15);
    var improvementScore = Clamp(dto.ImprovementScore, 0, 10);
    var summedScore = completenessScore + businessRuleScore + questionQualityScore + clarityScore + improvementScore;
    var overallScore = dto.OverallScore > 0 ? Clamp(dto.OverallScore, 0, 100) : summedScore;
    var missing = (dto.MissingRequirements ?? [])
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Select(item => new MissingRequirement(
            item.Trim(),
            "AI",
            "Medium",
            "Review this gap and decide whether it belongs in the final requirement set."))
        .ToList();

    return new EvaluationResult
    {
        CompletenessScore = completenessScore,
        BusinessRuleScore = businessRuleScore,
        QuestionQualityScore = questionQualityScore,
        ClarityScore = clarityScore,
        ImprovementAwarenessScore = improvementScore,
        OverallScore = overallScore,
        MissingRequirementsJson = missing,
        FeedbackText = string.IsNullOrWhiteSpace(dto.Feedback)
            ? "The evaluator returned limited feedback. Review the missing requirements and improve the submission with clearer business rules and acceptance criteria."
            : dto.Feedback.Trim()
    };
}

static AiStakeholderRequest SanitizeStakeholderRequest(AiStakeholderRequest request, ILogger logger)
{
    if (!request.Domain.Equals("E-commerce", StringComparison.OrdinalIgnoreCase))
    {
        return request;
    }

    request.VisibleRequirements = FilterEcommerceScenarioLines(request.VisibleRequirements, "visible requirements", logger);
    request.HiddenRequirements = FilterEcommerceScenarioLines(request.HiddenRequirements, "hidden requirements", logger);
    request.AlreadyRevealedRequirements = FilterEcommerceScenarioLines(request.AlreadyRevealedRequirements, "already revealed requirements", logger);

    var removedHistory = request.ChatHistory.Count(message =>
        !message.Sender.Equals("Learner", StringComparison.OrdinalIgnoreCase) &&
        ContainsCourseRegistrationTerms(message.Content));

    if (removedHistory > 0)
    {
        logger.LogWarning(
            "Removed {RemovedHistory} contaminated non-learner chat messages from an e-commerce AI prompt.",
            removedHistory);
        request.ChatHistory = request.ChatHistory
            .Where(message => message.Sender.Equals("Learner", StringComparison.OrdinalIgnoreCase) || !ContainsCourseRegistrationTerms(message.Content))
            .ToList();
    }

    return request;
}

static List<string> FilterEcommerceScenarioLines(IEnumerable<string> values, string sectionName, ILogger logger)
{
    var items = values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
    var removed = items.Where(ContainsCourseRegistrationTerms).ToList();

    if (removed.Count > 0)
    {
        logger.LogWarning(
            "Removed {RemovedCount} contaminated entries from e-commerce {SectionName}.",
            removed.Count,
            sectionName);
    }

    return items.Where(item => !ContainsCourseRegistrationTerms(item)).ToList();
}

static List<string> FindRevealedRequirementIds(Scenario scenario, SimulationSession session, string learnerMessage)
{
    var normalized = Normalize(learnerMessage);

    if (scenario.Domain.Equals("E-commerce", StringComparison.OrdinalIgnoreCase))
    {
        if (IsBroadOverviewQuestion(normalized) || IsEcommerceOutOfDomainQuestion(normalized))
        {
            return [];
        }

        var topic = GetEcommerceTopic(normalized);
        if (topic is null)
        {
            return [];
        }

        var candidates = scenario.HiddenRequirements
            .Where(requirement => !session.DiscoveredRequirementIds.Contains(requirement.Id))
            .Where(requirement => GetEcommerceRequirementTopic(requirement) == topic)
            .Select((requirement, index) => new
            {
                Requirement = requirement,
                Index = index,
                Score = ScoreEcommerceRequirementMatch(requirement, normalized, topic)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        var revealCount = GetEcommerceRevealCount(normalized, topic, candidates.Count);
        return candidates.Take(revealCount).Select(item => item.Requirement.Id).ToList();
    }

    return scenario.HiddenRequirements
        .Where(requirement => !session.DiscoveredRequirementIds.Contains(requirement.Id))
        .Where(requirement => requirement.Keywords.Any(keyword => normalized.Contains(keyword.ToLowerInvariant())))
        .Take(2)
        .Select(item => item.Id)
        .ToList();
}

static string GetReplyCategory(Scenario scenario, string learnerMessage, IReadOnlyCollection<string> revealedRequirementIds)
{
    var normalized = Normalize(learnerMessage);

    if (scenario.Domain.Equals("E-commerce", StringComparison.OrdinalIgnoreCase))
    {
        if (IsBroadOverviewQuestion(normalized))
        {
            return "overview";
        }

        if (IsEcommerceOutOfDomainQuestion(normalized))
        {
            return "out-of-domain";
        }

        return GetEcommerceTopic(normalized) ?? (
            revealedRequirementIds.Count > 0
                ? GetEcommerceRequirementTopic(scenario.HiddenRequirements.First(item => revealedRequirementIds.Contains(item.Id)))
                : "general");
    }

    if (revealedRequirementIds.Count > 0)
    {
        return scenario.HiddenRequirements
            .Where(item => revealedRequirementIds.Contains(item.Id))
            .Select(item => item.Category)
            .FirstOrDefault() ?? "general";
    }

    return "general";
}

static bool ContainsCourseRegistrationTerms(string value)
{
    var normalized = Normalize(value);
    string[] phrases =
    [
        "prerequisite subject",
        "prerequisite subjects",
        "course registration",
        "training department",
        "training department staff",
        "register for a course",
        "register for courses",
        "students register courses",
        "student register course",
        "lecturer",
        "university"
    ];

    return phrases.Any(normalized.Contains);
}

static bool IsBroadOverviewQuestion(string normalized)
{
    return HasAny(
        normalized,
        "what are the requirements",
        "what is the requirement",
        "what do you need",
        "tell me about the system",
        "tell me about this system",
        "tell me about the project",
        "what do we need",
        "give me an overview",
        "high level",
        "summary",
        "all business rules",
        "list all business rules",
        "can you list all business rules",
        "all requirements",
        "all the requirements");
}

static bool IsEcommerceOutOfDomainQuestion(string normalized)
{
    return HasAny(
        normalized,
        "course registration",
        "register for courses",
        "register for a course",
        "prerequisite",
        "lecturer",
        "training department",
        "university");
}

static string? GetEcommerceTopic(string normalized)
{
    if (HasAny(normalized, "voucher", "promotion", "discount", "coupon"))
    {
        return "voucher";
    }

    if (HasAny(normalized, "stock", "inventory", "availability", "available", "reserve", "reserved", "oversell"))
    {
        return "stock";
    }

    if (HasAny(normalized, "payment", "pay", "paid", "failed", "failure", "timeout"))
    {
        return "payment";
    }

    if (HasAny(normalized, "shipping", "ship", "delivery", "weight", "location", "zone", "fee"))
    {
        return "shipping";
    }

    if (HasAny(normalized, "cancel", "cancellation"))
    {
        return "cancellation";
    }

    if (HasAny(normalized, "refund", "return"))
    {
        return "refund";
    }

    if (HasAny(normalized, "report", "dashboard", "analytics", "admin"))
    {
        return "reporting";
    }

    return null;
}

static string GetEcommerceRequirementTopic(HiddenRequirement requirement)
{
    var combined = Normalize($"{requirement.Id} {requirement.Title} {requirement.Description}");

    if (HasAny(combined, "voucher", "promotion", "discount", "coupon"))
    {
        return "voucher";
    }

    if (HasAny(combined, "payment", "paid", "failed", "timeout"))
    {
        return "payment";
    }

    if (HasAny(combined, "stock", "inventory", "reserve", "oversell"))
    {
        return "stock";
    }

    if (HasAny(combined, "shipping", "delivery", "weight", "location", "ship"))
    {
        return "shipping";
    }

    if (HasAny(combined, "cancel"))
    {
        return "cancellation";
    }

    if (HasAny(combined, "refund", "return"))
    {
        return "refund";
    }

    if (HasAny(combined, "report", "analytics", "dashboard"))
    {
        return "reporting";
    }

    return "general";
}

static bool IsSpecificEcommerceFollowUp(string normalized, string topic)
{
    return topic switch
    {
        "voucher" => HasAny(normalized, "combine", "combined", "stack", "minimum", "category", "categories"),
        "stock" => HasAny(normalized, "reserve", "reserved", "reservation", "oversell"),
        "payment" => HasAny(normalized, "timeout", "confirm", "confirmed", "successful", "success"),
        "shipping" => HasAny(normalized, "location", "weight", "zone"),
        "cancellation" => HasAny(normalized, "before shipping", "before shipped", "after shipping"),
        "refund" => HasAny(normalized, "approval", "approve", "7 day", "7 days", "after delivery"),
        "reporting" => HasAny(normalized, "cancelled", "failed payments", "voucher usage"),
        _ => false
    };
}

static int GetEcommerceRevealCount(string normalized, string topic, int candidateCount)
{
    if (candidateCount <= 0)
    {
        return 0;
    }

    if (IsSpecificEcommerceFollowUp(normalized, topic))
    {
        return 1;
    }

    if (topic == "payment" && HasAny(normalized, "fail", "failed", "failure") && !HasAny(normalized, "timeout"))
    {
        return 1;
    }

    return Math.Min(2, candidateCount);
}

static int ScoreEcommerceRequirementMatch(HiddenRequirement requirement, string normalized, string topic)
{
    var score = requirement.Keywords.Count(keyword => normalized.Contains(keyword.ToLowerInvariant()));

    if (topic == "payment" && HasAny(normalized, "fail", "failed", "failure", "timeout"))
    {
        if (requirement.Id.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
            requirement.Id.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }
    }

    if (topic == "voucher" && HasAny(normalized, "combine", "combined", "stack"))
    {
        if (requirement.Id.Contains("combination", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }
    }

    if (topic == "voucher" && HasAny(normalized, "minimum"))
    {
        if (requirement.Id.Contains("minimum", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }
    }

    if (topic == "voucher" && HasAny(normalized, "category", "categories"))
    {
        if (requirement.Id.Contains("category", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }
    }

    if (topic == "stock" && HasAny(normalized, "reserve", "reserved", "reservation"))
    {
        if (requirement.Id.Contains("reservation", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }
    }

    return score;
}

static bool HasAny(string value, params string[] terms) => terms.Any(value.Contains);

static string Normalize(string value)
{
    var lower = value.ToLowerInvariant();
    var alphanumeric = Regex.Replace(lower, "[^a-z0-9\\s-]", " ");
    return Regex.Replace(alphanumeric, "\\s+", " ").Trim();
}

static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

static string CreateId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 1 + 12, prefix.Length + 33)];

static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

static string NormalizeLanguage(string? language)
{
    return language?.Trim().ToLowerInvariant() switch
    {
        "vi" or "vietnamese" => "vi",
        _ => "en"
    };
}

static string ToResponseLanguage(string language) => language == "vi" ? "Vietnamese" : "English";

static string NormalizeRole(string? role)
{
    return role is "Instructor" or "Admin" ? role : "Student";
}

static string? NormalizeExternalProvider(string? provider)
{
    return provider?.Trim().ToLowerInvariant() switch
    {
        "google" => "Google",
        "github" => "GitHub",
        _ => null
    };
}

public sealed record DatabaseOptions(string ConnectionString);

public static class AiLogSafety
{
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown error.";
        }

        return Regex.Replace(value, "key=[^&\\s]+", "key=hidden", RegexOptions.IgnoreCase);
    }

    public static string Compact(string? value, int maxLength = 220)
    {
        var sanitized = Sanitize(value)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return sanitized[..maxLength] + "...";
    }
}

public sealed class UserRepository
{
    private readonly DatabaseOptions options;
    private readonly ILogger<UserRepository> logger;
    private readonly List<User> memoryUsers = [];
    private bool useMemoryStore;

    public UserRepository(DatabaseOptions options, ILogger<UserRepository> logger)
    {
        this.options = options;
        this.logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            EnsureConfigured();

            await using var connection = await OpenConnectionAsync();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS users (
                        id TEXT PRIMARY KEY,
                        full_name TEXT NOT NULL,
                        email TEXT NOT NULL UNIQUE,
                        password_hash TEXT NOT NULL,
                        role TEXT NOT NULL CHECK (role IN ('Student', 'Instructor', 'Admin')),
                        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );

                    CREATE INDEX IF NOT EXISTS idx_users_email ON users (LOWER(email));

                    ALTER TABLE users
                        ADD COLUMN IF NOT EXISTS auth_provider TEXT NOT NULL DEFAULT 'Password';

                    ALTER TABLE users
                        ADD COLUMN IF NOT EXISTS provider_subject TEXT;

                    CREATE UNIQUE INDEX IF NOT EXISTS idx_users_provider_subject
                        ON users (auth_provider, provider_subject)
                        WHERE provider_subject IS NOT NULL;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await UpsertDemoUserAsync("user-student-1", "Demo Student", "student@reqsim.local", "Student", "demo123");
            await UpsertDemoUserAsync("user-instructor-1", "Demo Instructor", "instructor@reqsim.local", "Instructor", "demo123");
        }
        catch (Exception error)
        {
            ActivateMemoryStore(error);
        }
    }

    public async Task<User?> FindByEmailAsync(string email)
    {
        if (useMemoryStore)
        {
            return memoryUsers.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
        }

        EnsureConfigured();

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, full_name, email, password_hash, role, created_at, auth_provider, provider_subject
            FROM users
            WHERE LOWER(email) = LOWER(@email)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("email", email);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    public async Task<User?> FindByIdAsync(string id)
    {
        if (useMemoryStore)
        {
            return memoryUsers.FirstOrDefault(user => user.Id == id);
        }

        EnsureConfigured();

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, full_name, email, password_hash, role, created_at, auth_provider, provider_subject
            FROM users
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        if (useMemoryStore)
        {
            return memoryUsers.Any(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
        }

        EnsureConfigured();

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM users WHERE LOWER(email) = LOWER(@email));";
        command.Parameters.AddWithValue("email", email);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public async Task CreateAsync(User user)
    {
        if (useMemoryStore)
        {
            memoryUsers.Add(user);
            return;
        }

        EnsureConfigured();

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (id, full_name, email, password_hash, role, created_at)
            VALUES (@id, @fullName, @email, @passwordHash, @role, @createdAt);
            """;
        AddUserParameters(command, user);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<User> GetOrCreateExternalDemoUserAsync(string provider)
    {
        if (useMemoryStore)
        {
            var providerSubject = $"demo-{provider.ToLowerInvariant()}-learner";
            var existing = memoryUsers.FirstOrDefault(user =>
                user.AuthProvider == provider &&
                user.ProviderSubject == providerSubject);
            if (existing is not null)
            {
                return existing;
            }

            var memoryUser = new User
            {
                Id = CreateRepositoryId($"user-{provider.ToLowerInvariant()}"),
                FullName = $"{provider} Demo Learner",
                Email = $"{provider.ToLowerInvariant()}.demo@reqsim.local",
                PasswordHash = PasswordTools.Hash(CreateRepositoryId("external")),
                Role = "Student",
                AuthProvider = provider,
                ProviderSubject = providerSubject,
                CreatedAt = DateTimeOffset.UtcNow
            };
            memoryUsers.Add(memoryUser);
            return memoryUser;
        }

        EnsureConfigured();

        var subject = $"demo-{provider.ToLowerInvariant()}-learner";
        await using var connection = await OpenConnectionAsync();

        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText = """
                SELECT id, full_name, email, password_hash, role, created_at, auth_provider, provider_subject
                FROM users
                WHERE auth_provider = @provider AND provider_subject = @subject
                LIMIT 1;
                """;
            selectCommand.Parameters.AddWithValue("provider", provider);
            selectCommand.Parameters.AddWithValue("subject", subject);

            await using var reader = await selectCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return ReadUser(reader);
            }
        }

        var user = new User
        {
            Id = CreateRepositoryId($"user-{provider.ToLowerInvariant()}"),
            FullName = $"{provider} Demo Learner",
            Email = $"{provider.ToLowerInvariant()}.demo@reqsim.local",
            PasswordHash = PasswordTools.Hash(CreateRepositoryId("external")),
            Role = "Student",
            AuthProvider = provider,
            ProviderSubject = subject,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = """
                INSERT INTO users (id, full_name, email, password_hash, role, created_at, auth_provider, provider_subject)
                VALUES (@id, @fullName, @email, @passwordHash, @role, @createdAt, @authProvider, @providerSubject)
                ON CONFLICT (email)
                DO UPDATE SET
                    full_name = EXCLUDED.full_name,
                    auth_provider = EXCLUDED.auth_provider,
                    provider_subject = EXCLUDED.provider_subject
                RETURNING id, full_name, email, password_hash, role, created_at, auth_provider, provider_subject;
                """;
            AddUserParameters(insertCommand, user);

            await using var reader = await insertCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return ReadUser(reader);
            }
        }

        throw new InvalidOperationException("Could not create external demo user.");
    }

    public async Task<Dictionary<string, User>> GetByIdsAsync(IEnumerable<string> ids)
    {
        if (useMemoryStore)
        {
            return memoryUsers
                .Where(user => ids.Contains(user.Id))
                .ToDictionary(user => user.Id, user => user);
        }

        EnsureConfigured();

        var idArray = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        if (idArray.Length == 0)
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, full_name, email, password_hash, role, created_at, auth_provider, provider_subject
            FROM users
            WHERE id = ANY(@ids);
            """;
        command.Parameters.AddWithValue("ids", idArray);

        var users = new Dictionary<string, User>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var user = ReadUser(reader);
            users[user.Id] = user;
        }

        return users;
    }

    private async Task UpsertDemoUserAsync(string id, string fullName, string email, string role, string password)
    {
        if (useMemoryStore)
        {
            UpsertMemoryUser(new User
            {
                Id = id,
                FullName = fullName,
                Email = email,
                PasswordHash = PasswordTools.Hash(password),
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (id, full_name, email, password_hash, role, created_at)
            VALUES (@id, @fullName, @email, @passwordHash, @role, @createdAt)
            ON CONFLICT (email)
            DO UPDATE SET
                id = EXCLUDED.id,
                full_name = EXCLUDED.full_name,
                password_hash = EXCLUDED.password_hash,
                role = EXCLUDED.role;
            """;

        AddUserParameters(command, new User
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PasswordHash = PasswordTools.Hash(password),
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await command.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string is not configured.");
        }
    }

    private static void AddUserParameters(NpgsqlCommand command, User user)
    {
        command.Parameters.AddWithValue("id", user.Id);
        command.Parameters.AddWithValue("fullName", user.FullName);
        command.Parameters.AddWithValue("email", user.Email);
        command.Parameters.AddWithValue("passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("role", user.Role);
        command.Parameters.AddWithValue("createdAt", user.CreatedAt.UtcDateTime);
        command.Parameters.AddWithValue("authProvider", user.AuthProvider);
        command.Parameters.AddWithValue("providerSubject", (object?)user.ProviderSubject ?? DBNull.Value);
    }

    private static User ReadUser(NpgsqlDataReader reader)
    {
        var createdAt = reader.GetFieldValue<DateTime>(5);

        return new User
        {
            Id = reader.GetString(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            Role = reader.GetString(4),
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)),
            AuthProvider = reader.FieldCount > 6 && !reader.IsDBNull(6) ? reader.GetString(6) : "Password",
            ProviderSubject = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : null
        };
    }

    private static string CreateRepositoryId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 1 + 12, prefix.Length + 33)];
    }

    private void ActivateMemoryStore(Exception error)
    {
        useMemoryStore = true;
        logger.LogWarning(
            "PostgreSQL is unavailable for user storage. Falling back to in-memory demo users. Reason: {Reason}",
            AiLogSafety.Sanitize(error.Message));

        if (memoryUsers.Count == 0)
        {
            UpsertMemoryUser(new User
            {
                Id = "user-student-1",
                FullName = "Demo Student",
                Email = "student@reqsim.local",
                PasswordHash = PasswordTools.Hash("demo123"),
                Role = "Student",
                CreatedAt = DateTimeOffset.UtcNow
            });
            UpsertMemoryUser(new User
            {
                Id = "user-instructor-1",
                FullName = "Demo Instructor",
                Email = "instructor@reqsim.local",
                PasswordHash = PasswordTools.Hash("demo123"),
                Role = "Instructor",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private void UpsertMemoryUser(User user)
    {
        var existingIndex = memoryUsers.FindIndex(item => string.Equals(item.Email, user.Email, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            memoryUsers[existingIndex] = user;
        }
        else
        {
            memoryUsers.Add(user);
        }
    }
}

public static class PostgresConnectionString
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (!value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var query = ParseQuery(uri.Query);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "",
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Pooling = true,
            SslMode = SslMode.Require
        };

        if (query.TryGetValue("sslmode", out var sslMode))
        {
            builder.SslMode = sslMode.ToLowerInvariant() switch
            {
                "disable" => SslMode.Disable,
                "prefer" => SslMode.Prefer,
                "require" => SslMode.Require,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                _ => SslMode.Require
            };
        }

        if (query.TryGetValue("channel_binding", out var channelBinding))
        {
            builder["Channel Binding"] = ToPascalCase(channelBinding);
        }

        return builder.ConnectionString;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]).ToLowerInvariant(),
                parts => Uri.UnescapeDataString(parts[1]),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ToPascalCase(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "disable" => "Disable",
            "prefer" => "Prefer",
            "require" => "Require",
            _ => value
        };
    }
}

public sealed class DemoStore
{
    public List<Scenario> Scenarios { get; } =
    [
        new()
        {
            Id = "ecommerce-order-promotion",
            Title = "E-commerce Order & Promotion System",
            Domain = "E-commerce",
            Difficulty = "Beginner",
            StakeholderRole = "E-commerce Operations Manager",
            StakeholderPersona = "A practical operations manager responsible for checkout, order confirmation, voucher rules, stock availability, payment status, shipping coordination, cancellation, return, and refund handling. They understand business rules but will not reveal all details unless asked relevant questions.",
            EstimatedMinutes = 30,
            Description = "The online store wants a better checkout and order management flow with stronger promotion rules, payment control, and shipping visibility.",
            InitialContext = "Our online store wants to improve the checkout and order management process. Customers sometimes apply invalid vouchers, orders are confirmed even when stock is not available, and staff spend too much time checking payment and shipping status manually.",
            VisibleRequirements =
            [
                "Customers can browse products.",
                "Customers can add products to cart.",
                "Customers can place an order.",
                "Staff can view and manage orders.",
                "Admin can manage products and users."
            ],
            Actors = ["Customer", "Operations Staff", "Admin", "Payment Gateway", "Shipping Partner"],
            SuggestedQuestions =
            [
                "Are there any rules for applying vouchers?",
                "What happens if payment fails?",
                "How is stock checked during checkout?",
                "Can customers cancel an order?",
                "How is shipping fee calculated?",
                "What reports does admin need?"
            ],
            EvaluationFocus = ["Actors and roles", "Checkout flow", "Voucher and promotion rules", "Inventory and stock reservation", "Payment status", "Shipping fee calculation", "Cancellation, return, and refund rules", "Admin permissions", "Reporting requirements", "Non-functional requirements"],
            HiddenRequirements =
            [
                new() { Id = "voucher-minimum-order", Title = "Voucher minimum order value", Description = "Voucher can only be used if the minimum order value is reached.", Category = "BusinessRule", Importance = "High", RevealCondition = "Ask about voucher conditions, order value thresholds, discount rules, or promotion policies.", Keywords = ["voucher", "promotion", "discount", "minimum order", "minimum value", "coupon", "rule"] },
                new() { Id = "voucher-category-limit", Title = "Voucher category restrictions", Description = "Some vouchers are limited to specific product categories.", Category = "BusinessRule", Importance = "Medium", RevealCondition = "Ask about product categories, voucher scope, or promotion eligibility.", Keywords = ["voucher", "category", "promotion", "eligible product", "scope", "applies to"] },
                new() { Id = "voucher-combination", Title = "Promotion combination rule", Description = "Voucher cannot be combined with some other promotions.", Category = "BusinessRule", Importance = "Medium", RevealCondition = "Ask about stacking discounts, combining vouchers, or promotion conflicts.", Keywords = ["combine", "stack", "promotion", "voucher", "discount", "conflict"] },
                new() { Id = "stock-check-before-checkout", Title = "Stock check before checkout", Description = "Stock must be checked before checkout.", Category = "Constraint", Importance = "High", RevealCondition = "Ask about inventory validation, item availability, or checkout checks.", Keywords = ["stock", "inventory", "availability", "checkout", "validate stock", "in stock"] },
                new() { Id = "stock-reservation", Title = "Stock reservation", Description = "Stock should be reserved when the order is placed.", Category = "BusinessRule", Importance = "High", RevealCondition = "Ask what happens after order placement, whether stock is reserved, or how overselling is prevented.", Keywords = ["reserve", "reservation", "stock", "oversell", "order placed", "hold stock"] },
                new() { Id = "confirm-after-payment", Title = "Order confirmation after payment", Description = "Order is confirmed only after successful payment.", Category = "BusinessRule", Importance = "High", RevealCondition = "Ask about payment confirmation, order status, or when an order becomes confirmed.", Keywords = ["payment", "confirm", "confirmed", "success", "order status", "paid"] },
                new() { Id = "payment-failure-status", Title = "Payment failure handling", Description = "Payment failure should keep the order in pending or failed status.", Category = "Exception", Importance = "High", RevealCondition = "Ask what happens when payment fails, retries, or unsuccessful checkout cases.", Keywords = ["payment fails", "payment failure", "failed", "pending", "retry", "payment error"] },
                new() { Id = "payment-timeout-release-stock", Title = "Payment timeout releases stock", Description = "Payment timeout should release reserved stock.", Category = "Exception", Importance = "Medium", RevealCondition = "Ask what happens after payment timeout, whether reserved stock is released, or how inventory is restored after abandoned checkout.", Keywords = ["payment timeout", "timeout", "release stock", "reserved stock", "abandoned checkout", "inventory restored"] },
                new() { Id = "shipping-fee-rule", Title = "Shipping fee calculation", Description = "Shipping fee depends on customer location and order weight.", Category = "BusinessRule", Importance = "Medium", RevealCondition = "Ask about delivery charges, shipping fee calculation, zones, or weight rules.", Keywords = ["shipping", "delivery fee", "location", "weight", "shipping fee", "zone"] },
                new() { Id = "customer-cancel-before-ship", Title = "Cancellation before shipping", Description = "Customers can cancel an order before it is shipped.", Category = "Exception", Importance = "Medium", RevealCondition = "Ask about cancellation, order changes, or what happens before dispatch.", Keywords = ["cancel", "cancellation", "before shipped", "before shipping", "change order"] },
                new() { Id = "refund-approval", Title = "Refund approval", Description = "Refund requests require staff approval.", Category = "BusinessRule", Importance = "Medium", RevealCondition = "Ask about refunds, money return approval, or staff review.", Keywords = ["refund", "approve refund", "approval", "staff review", "money back"] },
                new() { Id = "return-period", Title = "Return period", Description = "Return period is limited, for example 7 days after delivery.", Category = "Constraint", Importance = "Medium", RevealCondition = "Ask about returns, after-delivery rules, or time limits for return requests.", Keywords = ["return", "7 days", "return period", "after delivery", "deadline"] },
                new() { Id = "admin-reporting", Title = "Admin reporting", Description = "Admin needs reports about cancelled orders, failed payments, and voucher usage.", Category = "NonFunctional", Importance = "Medium", RevealCondition = "Ask about reports, dashboards, monitoring, or admin analytics.", Keywords = ["report", "dashboard", "analytics", "voucher usage", "failed payment", "cancelled orders"] }
            ]
        },
        new()
        {
            Id = "course-registration",
            Title = "University Course Registration System",
            Domain = "Education",
            Difficulty = "Beginner",
            StakeholderRole = "Training Department Staff",
            StakeholderPersona = "A practical university staff member who understands registration pain points but answers only what learners ask.",
            EstimatedMinutes = 25,
            Description = "The university wants a system where students can register for courses online and reduce manual registration delays.",
            InitialContext = "Our university wants to improve the course registration process. Currently, many students register manually or through outdated tools, which causes confusion and delays. We want a better online system.",
            VisibleRequirements =
            [
                "Students can view available courses.",
                "Students can register for courses.",
                "Training department staff can manage course information.",
                "Admins can manage users."
            ],
            Actors = ["Student", "Training Department Staff", "Lecturer", "Admin"],
            SuggestedQuestions =
            [
                "Who can register for a course?",
                "Are there any prerequisite rules?",
                "What happens if a class is full?",
                "How are schedule conflicts handled?",
                "Can students cancel registration?",
                "What notifications should the system send?"
            ],
            EvaluationFocus = ["Actors", "Main workflow", "Business rules", "Edge cases", "Constraints", "Permissions", "Notifications", "Reporting"],
            HiddenRequirements =
            [
                new() { Id = "prerequisites", Title = "Prerequisite validation", Description = "Students must complete prerequisite subjects before registration.", Category = "BusinessRule", Importance = "High", RevealCondition = "Ask about eligibility, prerequisites, validation, or who can register.", Keywords = ["prerequisite", "eligible", "eligibility", "condition", "validation", "who can register"] },
                new() { Id = "capacity", Title = "Course capacity limit", Description = "Each course has limited capacity.", Category = "Constraint", Importance = "High", RevealCondition = "Ask about limits, seats, full classes, quota, or capacity.", Keywords = ["capacity", "limit", "seat", "full", "maximum", "slot", "quota"] },
                new() { Id = "schedule-conflict", Title = "Schedule conflict check", Description = "Students cannot register for courses with schedule conflicts.", Category = "BusinessRule", Importance = "High", RevealCondition = "Ask about timetable, schedule clashes, conflicts, or overlapping classes.", Keywords = ["schedule", "conflict", "clash", "timetable", "overlap", "same time"] },
                new() { Id = "cancellation-deadline", Title = "Cancellation deadline", Description = "Students can cancel registration before a deadline.", Category = "Exception", Importance = "Medium", RevealCondition = "Ask about cancellation, dropping courses, changes, or deadlines.", Keywords = ["cancel", "drop", "withdraw", "deadline", "change", "modify"] },
                new() { Id = "staff-approval", Title = "Staff approval for selected courses", Description = "Some courses require staff approval.", Category = "BusinessRule", Importance = "Medium", RevealCondition = "Ask about approval, special courses, manual review, or permission.", Keywords = ["approval", "approve", "manual review", "special course", "permission"] },
                new() { Id = "admin-override", Title = "Admin override", Description = "Admins can override registration in special cases.", Category = "Exception", Importance = "Medium", RevealCondition = "Ask about admin permissions, exceptions, overrides, or special cases.", Keywords = ["override", "admin", "special case", "exception", "force"] },
                new() { Id = "confirmation-notification", Title = "Confirmation notifications", Description = "The system must send confirmation notifications.", Category = "NonFunctional", Importance = "Medium", RevealCondition = "Ask about notifications, email, confirmation, SMS, or communication.", Keywords = ["notification", "email", "confirm", "message", "alert", "sms"] },
                new() { Id = "registration-history", Title = "Registration history", Description = "Registration history must be stored.", Category = "NonFunctional", Importance = "Medium", RevealCondition = "Ask about records, reporting, audit trail, history, or past registrations.", Keywords = ["history", "record", "audit", "report", "past", "log", "tracking"] }
            ]
        },
        new()
        {
            Id = "clinic-appointment",
            Title = "Clinic Appointment Booking",
            Domain = "Healthcare",
            Difficulty = "Intermediate",
            StakeholderRole = "Clinic Receptionist",
            StakeholderPersona = "A busy receptionist focused on reducing phone calls and avoiding appointment mistakes.",
            EstimatedMinutes = 30,
            Description = "A local clinic wants patients to book appointments online and help receptionists manage daily schedules.",
            InitialContext = "We spend a lot of time answering calls and writing appointment details by hand. We need a cleaner way for patients to book visits and for reception staff to manage the schedule.",
            VisibleRequirements = ["Patients can request appointment slots.", "Receptionists can confirm or reschedule appointments.", "Doctors can view their daily appointment list.", "Admins can manage clinic users."],
            Actors = ["Patient", "Receptionist", "Doctor", "Admin"],
            EvaluationFocus = ["Actors", "Schedule rules", "Exceptions", "Notifications", "Permissions"],
            HiddenRequirements =
            [
                new() { Id = "doctor-availability", Title = "Doctor availability", Description = "Patients can only book slots when the selected doctor is available.", Category = "Constraint", Importance = "High", RevealCondition = "Ask about doctor schedules, availability, or slot validation.", Keywords = ["doctor", "availability", "available", "slot", "schedule", "time"] },
                new() { Id = "urgent-cases", Title = "Urgent cases", Description = "Urgent cases must be routed to the receptionist instead of normal booking.", Category = "Exception", Importance = "High", RevealCondition = "Ask about emergency cases, urgent patients, or exceptions.", Keywords = ["urgent", "emergency", "critical", "exception"] },
                new() { Id = "reminders", Title = "Appointment reminders", Description = "Patients should receive reminders before their appointment.", Category = "NonFunctional", Importance = "Medium", RevealCondition = "Ask about reminders, no-shows, notifications, SMS, or email.", Keywords = ["reminder", "notification", "sms", "email", "no-show"] }
            ]
        },
        new()
        {
            Id = "restaurant-booking",
            Title = "Restaurant Table Booking",
            Domain = "Hospitality",
            Difficulty = "Beginner",
            StakeholderRole = "Restaurant Owner",
            StakeholderPersona = "A hands-on owner who cares about full tables, fewer missed bookings, and simple staff workflows.",
            EstimatedMinutes = 20,
            Description = "A restaurant owner wants customers to reserve tables online and help staff manage capacity during peak hours.",
            InitialContext = "Customers call or message us to reserve tables, and sometimes we lose track during busy evenings. We want a simple online booking system.",
            VisibleRequirements = ["Customers can request table reservations.", "Staff can approve or reject reservations.", "Staff can view bookings by date.", "Admins can manage restaurant settings."],
            Actors = ["Customer", "Staff", "Manager", "Admin"],
            EvaluationFocus = ["Actors", "Booking workflow", "Capacity", "Payment rules", "Exceptions"],
            HiddenRequirements =
            [
                new() { Id = "party-size", Title = "Party size limit", Description = "Large parties require staff confirmation before the reservation is accepted.", Category = "BusinessRule", Importance = "High", RevealCondition = "Ask about group size, table capacity, or special bookings.", Keywords = ["party", "group", "capacity", "large", "people", "table size"] },
                new() { Id = "deposit", Title = "Deposit for peak hours", Description = "Peak-hour reservations may require a deposit.", Category = "BusinessRule", Importance = "Medium", RevealCondition = "Ask about payment, deposit, no-shows, or peak hours.", Keywords = ["deposit", "payment", "peak", "no-show", "busy"] },
                new() { Id = "cancellation-window", Title = "Cancellation window", Description = "Customers can cancel bookings until a configured cutoff time.", Category = "Exception", Importance = "Medium", RevealCondition = "Ask about cancellation, changes, cutoff, or exceptions.", Keywords = ["cancel", "change", "cutoff", "deadline", "modify"] }
            ]
        }
    ];

    public List<SimulationSession> Sessions { get; } = [];
    public List<ChatMessage> Messages { get; } = [];
    public List<LearnerNote> Notes { get; } = [];
    public List<RequirementSubmission> Submissions { get; } = [];
    public List<EvaluationResult> Evaluations { get; } = [];
    public List<InstructorReview> Reviews { get; } = [];
}

public sealed class User
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Student";
    public string AuthProvider { get; set; } = "Password";
    public string? ProviderSubject { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Scenario
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Description { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string StakeholderRole { get; set; } = "";
    public string StakeholderPersona { get; set; } = "";
    public string InitialContext { get; set; } = "";
    public int EstimatedMinutes { get; set; }
    public List<string> VisibleRequirements { get; set; } = [];
    public List<string> Actors { get; set; } = [];
    public List<string> SuggestedQuestions { get; set; } = [];
    public List<string> EvaluationFocus { get; set; } = [];
    public List<HiddenRequirement> HiddenRequirements { get; set; } = [];
}

public sealed class HiddenRequirement
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string RevealCondition { get; set; } = "";
    public string Category { get; set; } = "";
    public string Importance { get; set; } = "";
    public List<string> Keywords { get; set; } = [];
}

public sealed class SimulationSession
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public List<string> DiscoveredRequirementIds { get; set; } = [];
}

public sealed class ChatMessage
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public List<string> RevealedRequirementIds { get; set; } = [];
}

public sealed class LearnerNote
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RequirementSubmission
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string? UserStories { get; set; }
    public string? UseCases { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public string? AdditionalNotes { get; set; }
    public string? Reflection { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}

public sealed class EvaluationResult
{
    public string Id { get; set; } = "";
    public string SubmissionId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int CompletenessScore { get; set; }
    public int BusinessRuleScore { get; set; }
    public int QuestionQualityScore { get; set; }
    public int ClarityScore { get; set; }
    public int ImprovementAwarenessScore { get; set; }
    public int OverallScore { get; set; }
    public List<MissingRequirement> MissingRequirementsJson { get; set; } = [];
    public string FeedbackText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class InstructorReview
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string InstructorId { get; set; } = "";
    public int AdjustedScore { get; set; }
    public string Comment { get; set; } = "";
    public DateTimeOffset ReviewedAt { get; set; }
}

public sealed record CreateSessionRequest(string? UserId, string ScenarioId);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RegisterRequest(string? FullName, string? Email, string? Password, string? Role);
public sealed record ExternalLoginRequest(string? Provider);
public sealed record AddMessageRequest(string Content, string? Language = null, string? ResponseLanguage = null);
public sealed record SaveNoteRequest(string? Content);
public sealed record StakeholderReply(string Content, List<string> RevealedRequirementIds);
public sealed record MissingRequirement(string Title, string Category, string Importance, string Guidance);
public sealed record HiddenCoverage(HiddenRequirement Requirement, bool Covered);
public sealed record QuestionStats(int QuestionCount, int CategoryHits);
public sealed record ClarityStats(double StoryPoints, double CriteriaPoints, double SpecificityPoints);

public interface IAiService
{
    Task<AiReplyResult> GenerateStakeholderReplyAsync(AiStakeholderRequest request, CancellationToken cancellationToken = default);
    Task<AiEvaluationResultEnvelope> EvaluateSubmissionAsync(AiEvaluationRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiReplyResult(string Reply, string Provider);
public sealed record AiEvaluationResultEnvelope(EvaluationResultDto Result, string Provider);

public enum AiRuntimeState
{
    NotVerified = 0,
    Available = 1,
    QuotaFallback = 2,
    NetworkFallback = 3,
    TimeoutFallback = 4,
    ProviderFallback = 5,
    UnexpectedFallback = 6
}

public sealed class GeminiApiException : InvalidOperationException
{
    public GeminiApiException(int statusCode, string? reasonPhrase, string responseSummary)
        : base($"Gemini API returned {statusCode} {reasonPhrase}: {responseSummary}")
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase ?? "Unknown";
        ResponseSummary = responseSummary;
    }

    public int StatusCode { get; }
    public string ReasonPhrase { get; }
    public string ResponseSummary { get; }
}

public sealed record AiRuntimeFallbackInfo(AiRuntimeState State, string Cause, string Detail)
{
    public static AiRuntimeFallbackInfo From(Exception error)
    {
        if (error is GeminiApiException geminiError)
        {
            if (geminiError.StatusCode == StatusCodes.Status429TooManyRequests ||
                ContainsAny(geminiError.ResponseSummary, "quota exceeded", "resource_exhausted", "rate limit"))
            {
                return new(
                    AiRuntimeState.QuotaFallback,
                    "quota exhausted (HTTP 429)",
                    AiLogSafety.Compact(geminiError.ResponseSummary));
            }

            return new(
                AiRuntimeState.ProviderFallback,
                $"provider error (HTTP {geminiError.StatusCode})",
                AiLogSafety.Compact(geminiError.ResponseSummary));
        }

        if (error is HttpRequestException)
        {
            return new(
                AiRuntimeState.NetworkFallback,
                "network error",
                AiLogSafety.Compact(error.Message));
        }

        if (error is TaskCanceledException or OperationCanceledException)
        {
            return new(
                AiRuntimeState.TimeoutFallback,
                "request timeout or cancellation",
                AiLogSafety.Compact(error.Message));
        }

        return new(
            AiRuntimeState.UnexpectedFallback,
            "unexpected runtime error",
            AiLogSafety.Compact(error.Message));
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AiStakeholderRequest
{
    public string ScenarioTitle { get; set; } = "";
    public string Domain { get; set; } = "";
    public string ScenarioDescription { get; set; } = "";
    public string StakeholderRole { get; set; } = "";
    public string StakeholderPersona { get; set; } = "";
    public string InitialContext { get; set; } = "";
    public List<string> VisibleRequirements { get; set; } = [];
    public List<string> HiddenRequirements { get; set; } = [];
    public List<string> AlreadyRevealedRequirements { get; set; } = [];
    public List<ChatMessageDto> ChatHistory { get; set; } = [];
    public string LearnerMessage { get; set; } = "";
    public string ResponseLanguage { get; set; } = "English";
}

public sealed class ChatMessageDto
{
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class AiEvaluationRequest
{
    public string ScenarioTitle { get; set; } = "";
    public string Domain { get; set; } = "";
    public string ScenarioDescription { get; set; } = "";
    public List<string> MasterRequirements { get; set; } = [];
    public List<ChatMessageDto> ChatHistory { get; set; } = [];
    public string LearnerSubmission { get; set; } = "";
    public string ResponseLanguage { get; set; } = "English";
}

public sealed class EvaluationResultDto
{
    public int OverallScore { get; set; }
    public int CompletenessScore { get; set; }
    public int BusinessRuleScore { get; set; }
    public int QuestionQualityScore { get; set; }
    public int ClarityScore { get; set; }
    public int ImprovementScore { get; set; }
    public List<string> MissingRequirements { get; set; } = [];
    public string Feedback { get; set; } = "";
}

public sealed class SimulationMessageRequest
{
    public string? Message { get; set; }
    public string? Language { get; set; }
    public string? ResponseLanguage { get; set; }
}

public sealed class EvaluationApiRequest
{
    public string? LearnerSubmission { get; set; }
    public string? UserStories { get; set; }
    public string? UseCases { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public string? AdditionalNotes { get; set; }
    public string? Reflection { get; set; }
    public string? Language { get; set; }
    public string? ResponseLanguage { get; set; }
}

public sealed class AiTestRequest
{
    public string? Message { get; set; }
    public string? ScenarioId { get; set; }
    public string? Language { get; set; }
    public string? ResponseLanguage { get; set; }
}

public sealed class GeminiOptions
{
    public const string HttpClientName = "Gemini";

    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gemini-2.5-flash";
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";
    public string KeySource { get; init; } = "Not configured";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public static GeminiOptions FromConfiguration(IConfiguration configuration)
    {
        var envGemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var envGoogle = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var configGeminiScoped = configuration["Gemini:ApiKey"];
        var configGemini = configuration["GEMINI_API_KEY"];
        var configGoogle = configuration["GOOGLE_API_KEY"];

        var apiKey = envGemini
            ?? envGoogle
            ?? configGeminiScoped
            ?? configGemini
            ?? configGoogle
            ?? "";
        var keySource = !string.IsNullOrWhiteSpace(envGemini)
            ? "Environment:GEMINI_API_KEY"
            : !string.IsNullOrWhiteSpace(envGoogle)
                ? "Environment:GOOGLE_API_KEY"
                : !string.IsNullOrWhiteSpace(configGeminiScoped)
                    ? "Configuration:Gemini:ApiKey"
                    : !string.IsNullOrWhiteSpace(configGemini)
                        ? "Configuration:GEMINI_API_KEY"
                        : !string.IsNullOrWhiteSpace(configGoogle)
                            ? "Configuration:GOOGLE_API_KEY"
                            : "Not configured";

        return new GeminiOptions
        {
            ApiKey = apiKey,
            Model = NormalizeModel(configuration["Gemini:Model"] ?? "gemini-2.5-flash"),
            BaseUrl = configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta",
            KeySource = keySource
        };
    }

    private static string NormalizeModel(string? model)
    {
        var value = string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash" : model.Trim();
        return value.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? value["models/".Length..]
            : value;
    }
}

public sealed class ResilientAiService(
    GeminiAiService gemini,
    MockAiService fallback,
    ILogger<ResilientAiService> logger) : IAiService
{
    private int runtimeState = (int)AiRuntimeState.NotVerified;

    public async Task<AiReplyResult> GenerateStakeholderReplyAsync(AiStakeholderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await gemini.GenerateStakeholderReplyAsync(request, cancellationToken);
            LogGeminiRuntimeAvailable("stakeholder");
            return reply;
        }
        catch (Exception error)
        {
            LogGeminiRuntimeFallback("stakeholder", error);
            return await fallback.GenerateStakeholderReplyAsync(request, cancellationToken);
        }
    }

    public async Task<AiEvaluationResultEnvelope> EvaluateSubmissionAsync(AiEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await gemini.EvaluateSubmissionAsync(request, cancellationToken);
            LogGeminiRuntimeAvailable("evaluation");
            return result;
        }
        catch (Exception error)
        {
            LogGeminiRuntimeFallback("evaluation", error);
            return await fallback.EvaluateSubmissionAsync(request, cancellationToken);
        }
    }

    private void LogGeminiRuntimeAvailable(string operation)
    {
        var previousState = (AiRuntimeState)Interlocked.Exchange(ref runtimeState, (int)AiRuntimeState.Available);
        if (previousState != AiRuntimeState.Available)
        {
            logger.LogInformation(
                "Gemini runtime available: live {Operation} request succeeded. Responses are coming from Gemini.",
                operation);
        }
    }

    private void LogGeminiRuntimeFallback(string operation, Exception error)
    {
        var fallbackInfo = AiRuntimeFallbackInfo.From(error);
        Interlocked.Exchange(ref runtimeState, (int)fallbackInfo.State);
        logger.LogWarning(
            "Gemini runtime unavailable for live {Operation} request. Falling back to MockAiService. Cause: {Cause}. Detail: {Detail}",
            operation,
            fallbackInfo.Cause,
            fallbackInfo.Detail);
    }
}

public sealed class GeminiAiService(
    IHttpClientFactory httpClientFactory,
    GeminiOptions options,
    ILogger<GeminiAiService> logger) : IAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiReplyResult> GenerateStakeholderReplyAsync(AiStakeholderRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = BuildStakeholderPrompt(request);
        var response = await GenerateTextAsync(prompt, maxOutputTokens: 560, temperature: 0.65, cancellationToken);
        return new AiReplyResult(response.Trim(), "Gemini");
    }

    public async Task<AiEvaluationResultEnvelope> EvaluateSubmissionAsync(AiEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = BuildEvaluationPrompt(request);
        var response = await GenerateTextAsync(prompt, maxOutputTokens: 900, temperature: 0.2, cancellationToken);

        try
        {
            var json = ExtractFirstJsonObject(response);
            var result = JsonSerializer.Deserialize<EvaluationResultDto>(json, JsonOptions);
            if (result is not null)
            {
                result.MissingRequirements ??= [];
                return new AiEvaluationResultEnvelope(result, "Gemini");
            }
        }
        catch (Exception error)
        {
            logger.LogWarning(
                "Gemini evaluation response could not be parsed as JSON. Reason: {Reason}",
                AiLogSafety.Sanitize(error.Message));
        }

        return new AiEvaluationResultEnvelope(
            new EvaluationResultDto
            {
                OverallScore = 70,
                CompletenessScore = 21,
                BusinessRuleScore = 13,
                QuestionQualityScore = 14,
                ClarityScore = 14,
                ImprovementScore = 8,
                MissingRequirements =
                [
                    "The AI evaluator returned a response that could not be parsed as JSON.",
                    "Review business rules, exceptions, and acceptance criteria manually."
                ],
                Feedback = "The evaluator response was not valid JSON, so a safe fallback score was used. Review the submission for missing business rules, edge cases, and clearer acceptance criteria."
            },
            "Gemini");
    }

    private async Task<string> GenerateTextAsync(
        string prompt,
        int maxOutputTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var client = httpClientFactory.CreateClient(GeminiOptions.HttpClientName);
        var endpoint = $"{options.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(options.Model)}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}";
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature,
                topP = 0.9,
                maxOutputTokens
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GeminiApiException((int)response.StatusCode, response.ReasonPhrase, TrimForLog(responseBody));
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates))
        {
            throw new InvalidOperationException("Gemini API response did not include candidates.");
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts))
            {
                continue;
            }

            var text = string.Join("\n", parts.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new InvalidOperationException("Gemini API returned an empty text response.");
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }
    }

    private static string BuildStakeholderPrompt(AiStakeholderRequest request)
    {
        return $"""
            You are an AI stakeholder in a requirement gathering simulation.

            Your role: {request.StakeholderRole}
            Scenario: {request.ScenarioTitle}
            Domain: {request.Domain}
            Persona: {request.StakeholderPersona}
            Initial context: {request.InitialContext}

            You are simulating a real business stakeholder so the learner can practice requirement gathering.

            You are NOT:
            - a teacher
            - a Business Analyst mentor
            - a software architect
            - a documentation writer
            - an AI assistant

            Communication style:
            - Speak naturally like a real business stakeholder.
            - Be practical, realistic, business-oriented, and slightly busy but still helpful.
            - Answer direct questions directly.
            - Use 3-6 sentences for normal answers.
            - For category questions, aim for around 60-130 words unless the learner asks for something brief.
            - Add a short business reason when appropriate.
            - Add a realistic operational example when it helps.
            - Mention real pain points from your role when relevant.
            - Prefer natural phrasing such as "we need", "from our side", "in practice", "normally", "our staff", or "our customers" when it fits.
            - Avoid sounding like a checklist or documentation.
            - Avoid bullet points unless the learner explicitly asks for a list.
            - Avoid very short one-sentence answers.
            - Do not say "Good question", "That is useful to clarify", "You should consider", "As a BA", or "As an AI".
            - Do not mention "hidden requirement".
            - Ground every answer in the visible requirements, hidden requirements, initial context, and already revealed conversation details. Do not invent unrelated rules.

            Information control:
            1. Do not reveal all requirements at once.
            2. If the learner asks a vague question, give general business context only.
            3. If the learner asks about a category, reveal only 1-2 relevant rules from that category.
            4. If the learner asks a specific follow-up, reveal only one additional related detail.
            5. If the learner asks for everything, ask them to choose an area first.
            6. Do not reveal unrelated categories.
            7. Do not repeat already revealed requirements as new.
            8. Keep the answer under 130 words unless the learner asks for more detail.
            9. If the learner asks a broad overview question, explain the main pain points and ask which area they want to explore first.
            10. Answer only the learner's latest question.
            11. If the learner asks who can do something, mention relevant actors and permissions.
            12. If the domain is E-commerce, focus on cart, checkout, voucher, stock, payment, shipping, cancellation, return, refund, and admin reports.
            13. If the domain is E-commerce, never mention course registration, prerequisite subjects, university, lecturers, training department staff, or students registering courses unless those exact concepts are present in the current scenario data.
            14. Ignore contaminated details from previous sessions that do not belong to the current scenario.

            Visible requirements:
            {FormatList(request.VisibleRequirements)}

            Hidden requirements:
            {FormatList(request.HiddenRequirements)}

            Already revealed hidden requirements:
            {FormatList(request.AlreadyRevealedRequirements)}

            Conversation so far:
            {FormatChat(request.ChatHistory)}

            Learner question:
            {request.LearnerMessage}

            Response language:
            {request.ResponseLanguage}

            Reply as the stakeholder in {request.ResponseLanguage}.
            """;
    }

    private static string BuildEvaluationPrompt(AiEvaluationRequest request)
    {
        const string jsonExample = """
            {
              "overallScore": 78,
              "completenessScore": 24,
              "businessRuleScore": 15,
              "questionQualityScore": 16,
              "clarityScore": 15,
              "improvementScore": 8,
              "missingRequirements": [
                "Course capacity was not mentioned.",
                "Schedule conflict checking was missing."
              ],
              "feedback": "You asked good questions about main features, but missed several important business rules and exception cases."
            }
            """;

        return $"""
            You are an AI evaluator for a requirement gathering training platform.

            Evaluate the learner's requirement submission based on the scenario master requirements.

            Scenario:
            {request.ScenarioTitle}

            Domain:
            {request.Domain}

            Scenario description:
            {request.ScenarioDescription}

            Master requirements:
            {FormatList(request.MasterRequirements)}

            Learner interview history:
            {FormatChat(request.ChatHistory)}

            Learner submission:
            {request.LearnerSubmission}

            Score the learner from 0 to 100 using:
            - Completeness: 30 points
            - Business Rules: 25 points
            - Question Quality: 20 points
            - Requirement Clarity: 15 points
            - Improvement Awareness: 10 points

            Focus on whether the learner discovered important business rules, actors, constraints, exception flows, and non-functional requirements for the selected domain.

            If the domain is E-commerce, focus especially on:
            - Voucher and promotion rules
            - Stock checking and stock reservation
            - Payment success/failure handling
            - Shipping fee rules
            - Cancellation rules
            - Return and refund rules
            - Admin reporting requirements
            - Actor permissions
            - Exception flows

            Write the feedback text and missing requirement messages in {request.ResponseLanguage}.

            Return JSON only. Do not use markdown. Do not explain outside JSON.

            Required JSON format:
            {jsonExample}
            """;
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var items = values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        return items.Count == 0 ? "- None" : string.Join("\n", items.Select(item => $"- {item}"));
    }

    private static string FormatChat(IEnumerable<ChatMessageDto> messages)
    {
        var lines = messages
            .OrderBy(item => item.CreatedAt)
            .Select(item => $"{item.Sender}: {item.Content}")
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return lines.Count == 0 ? "No previous messages." : string.Join("\n", lines);
    }

    private static string ExtractFirstJsonObject(string value)
    {
        var start = value.IndexOf('{');
        if (start < 0)
        {
            throw new FormatException("No JSON object found.");
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = start; index < value.Length; index++)
        {
            var current = value[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return value[start..(index + 1)];
                }
            }
        }

        throw new FormatException("JSON object was not complete.");
    }

    private static string TrimForLog(string value)
    {
        return value.Length <= 800 ? value : value[..800];
    }
}

public sealed class MockAiService : IAiService
{
    public Task<AiReplyResult> GenerateStakeholderReplyAsync(AiStakeholderRequest request, CancellationToken cancellationToken = default)
    {
        var message = Normalize(request.LearnerMessage);
        var language = request.ResponseLanguage == "Vietnamese" ? "vi" : "en";
        var revealedRequirements = request.AlreadyRevealedRequirements.Select(Normalize).ToList();

        if (request.Domain.Equals("E-commerce", StringComparison.OrdinalIgnoreCase))
        {
            if (IsEcommerceOutOfDomainQuestion(message))
            {
                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Tinh huong nay tap trung vao checkout va quan ly don hang cua cua hang online, khong phai course registration. Tu goc do van hanh, ben toi quan tam den voucher, ton kho, thanh toan, van chuyen va xu ly sau don hang. Neu ban muon tiep tuc buoi phong van nay, hay chon mot trong cac phan do va toi se giai thich cu the hon.", "Mock")
                    : new AiReplyResult("This scenario is about online store checkout and order management, not course registration. From the operations side, my concerns are vouchers, stock, payment, shipping, and post-order handling. If you want to continue this interview, choose one of those areas and I can explain it in more detail.", "Mock"));
            }

            if (IsBroadOverviewQuestion(message))
            {
                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Tu phia van hanh, van de lon nhat la checkout va xu ly don hang van con qua thu cong. Khach hang ky vong quy trinh nhanh va ro rang, trong khi nhan vien van mat nhieu thoi gian de doi chieu voucher, ton kho, thanh toan va trang thai giao hang. Moi nhom nghiep vu lai co quy tac rieng, nen toi khong muon gop tat ca vao mot cau tra loi. Ban muon di vao phan nao truoc: voucher, ton kho, thanh toan, van chuyen, huy don hay hoan tien?", "Mock")
                    : new AiReplyResult("From our side, the biggest issue is that checkout and order handling are still too manual. Customers expect the process to be fast and clear, while our staff spend too much time checking vouchers, stock, payment, and shipping status by hand. Each area has its own rules, so I do not want to mix everything together in one answer. Which part would you like to clarify first: voucher rules, stock handling, payment, shipping, cancellation, or refund?", "Mock"));
            }

            if (HasAny(message, "hello", "hi", "greet", "introduce"))
            {
                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Chao ban. Toi phu trach van hanh e-commerce, nen phan lon cong viec cua toi xoay quanh checkout, thanh toan, ton kho, van chuyen va viec theo doi don hang thu cong. Ap luc lon nhat ben toi la giu trai nghiem khach hang muot ma ma khong lam tang tai cho nhan vien. Neu ban muon, chung ta co the di tung phan mot.", "Mock")
                    : new AiReplyResult("Hello. I handle e-commerce operations, so most of my time goes into checkout, payment, stock, shipping, and the manual work behind order follow-up. The main pressure on our side is keeping the customer journey smooth without creating extra work for staff. If you want, we can go area by area.", "Mock"));
            }

            if (HasAny(message, "voucher", "promotion", "discount", "coupon"))
            {
                if (HasAny(message, "combine", "combined", "stack"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Khong phai luc nao cung duoc. Mot so voucher khong the dung chung voi cac khuyen mai khac, vi khi uu dai bi cong don thi gia cuoi cung rat kho kiem soat. Trong thuc te, ben toi thuong dat quy tac nay theo tung campaign de mot khuyen mai khong vo tinh an vao bien loi nhuan cua khuyen mai khac. Dieu do dac biet quan trong vao cac dot sale lon.", "Mock")
                        : new AiReplyResult("Not always. Some vouchers cannot be combined with other promotions, because once discounts start stacking, the final price becomes hard for our staff to control. In practice, we usually set that rule at campaign level so one promotion does not accidentally eat into the margin of another. That matters even more during large sale events.", "Mock"));
                }

                if (HasAny(message, "minimum"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Co. Thuong thi voucher chi nen hoat dong khi don hang dat gia tri toi thieu. Tu goc do van hanh, quy tac nay giup ben toi giu campaign trong muc ngan sach va tranh viec don qua nho van ap uu dai khong dung muc tieu. Neu bo qua dieu kien nay, nhan vien thuong phai xu ly them nhieu phan hoi ve gia.", "Mock")
                        : new AiReplyResult("Yes. A voucher normally should not work unless the order reaches the minimum value. From our side, that keeps the campaign within budget and stops very small orders from using discounts that were meant to drive larger baskets. If we skip that rule, staff often end up handling price complaints manually.", "Mock"));
                }

                if (HasAny(message, "category", "categories"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Co. Mot so voucher chi ap dung cho nhom san pham cu the, vi khong phai campaign nao cung danh cho tat ca mat hang. Vi du, neu uu dai dang chay cho thoi trang thi ben toi khong muon no tu dong ap sang dien tu. Neu de logic qua rong, nhan vien se phai sua loi giam gia sau checkout.", "Mock")
                        : new AiReplyResult("Yes, some vouchers are limited to specific product categories. We do that because not every campaign is meant for every item, especially when margins are different across categories. For example, a promotion for fashion items should not automatically apply to electronics. Otherwise, our team has to clean up discount mistakes after checkout.", "Mock"));
                }

                if (HasRevealedRequirement(revealedRequirements, "voucher minimum order value") &&
                    HasRevealedRequirement(revealedRequirements, "voucher category restrictions") &&
                    !HasRevealedRequirement(revealedRequirements, "promotion combination rule"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Ngoai hai dieu kien do, mot so voucher con khong duoc dung chung voi khuyen mai khac. Ben toi can quy tac nay vi giam gia bi cong don qua muc se tao ra van de gia rat nhanh trong luc campaign dang chay. Nhu vay nhan vien khong phai ngoi ra soat tung truong hop giam gia bat thuong vao ngay cao diem.", "Mock")
                        : new AiReplyResult("Beyond those two checks, some vouchers also cannot be combined with other promotions. We need that because stacked discounts can create pricing issues very quickly during campaigns. In practice, that rule protects both margin and operations, since our staff should not have to manually review every suspicious discount case on high-volume sale days.", "Mock"));
                }

                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Co, ben toi co mot vai quy tac cho voucher. Tu goc do van hanh, dieu quan trong nhat la voucher chi nen hoat dong khi don hang dat gia tri toi thieu. Ngoai ra, co nhung campaign chi cho phep voucher ap dung voi mot so nhom san pham nhat dinh, vi ben toi khong muon uu dai nao cung ap vao moi mat hang. Vi du, mot campaign cho thoi trang khong nen tu dong giam gia cho dien tu. Neu logic voucher qua rong, nhan vien se phai xu ly rat nhieu truong hop sai gia sau do.", "Mock")
                    : new AiReplyResult("Yes, we do have a few rules for vouchers. From the operations side, the most important one is that a voucher should only work when the order reaches a minimum value. We also have campaigns where the voucher is limited to certain product categories, because we do not want every discount to apply to every item. For example, a campaign for fashion items should not automatically discount electronics. If that logic is too loose, our staff end up fixing pricing complaints manually.", "Mock"));
            }

            if (HasAny(message, "stock", "inventory", "available", "availability"))
            {
                if (HasAny(message, "reserve", "reserved", "reservation"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Co. Khi don hang duoc dat, ton kho nen duoc tam giu cho don do. Neu khong lam vay, cung mot san pham co the bi ban cho nhieu khach truoc khi ben kho kip xu ly. Tu phia van hanh, day la cach ben toi tranh oversell va bot viec xin loi khach sau do.", "Mock")
                        : new AiReplyResult("Yes, once an order is placed, we need to reserve the stock for that order. If we do not, another customer may buy the same item before payment or fulfillment is settled. From the operations side, that creates overselling cases and extra manual follow-up for staff.", "Mock"));
                }

                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Trong thuc te, ton kho phai duoc kiem tra truoc khi khach hoan tat checkout. Khi don hang duoc dat, ben toi cung can tam giu ton kho cho don do, neu khong cung mot san pham co the bi ban cho nguoi khac. Van de nay ro nhat trong cac dot khuyen mai, khi rat nhieu khach cung mua mot mat hang trong thoi gian ngan. Moi quan tam lon nhat cua ben toi la tranh overselling va tranh phan hoi xau tu khach hang.", "Mock")
                    : new AiReplyResult("In practice, stock must be checked before the customer can complete checkout. When an order is placed, we also need to reserve the stock for that order, otherwise the same item may be sold to another customer. This becomes more serious during promotion campaigns, when many people try to buy the same item at once. Our main concern is avoiding overselling and the customer complaints that come after it.", "Mock"));
            }

            if (HasAny(message, "payment", "paid", "pay", "failed", "failure", "timeout"))
            {
                if (HasAny(message, "timeout"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Neu thanh toan bi timeout, ton kho da tam giu nen duoc giai phong lai. Neu khong, he thong se hien nhu het hang du du khach chua hoan tat mua. Tu goc do van hanh, dieu nay de tao ra ton kho ao va lam tang so cau hoi ho tro khong can thiet.", "Mock")
                        : new AiReplyResult("If payment times out, any reserved stock should be released again. Otherwise, the item may look unavailable even though no one actually completed the purchase. From our side, that creates a false stock shortage and unnecessary support questions from both customers and staff.", "Mock"));
                }

                if (HasAny(message, "failed", "failure", "fail"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Neu thanh toan that bai, don hang khong nen duoc xac nhan. Tu goc do van hanh, nhan vien khong nen bat dau dong goi hay giao mot don hang chua duoc thanh toan. Don hang co the o trang thai pending hoac failed de khach thu lai thanh toan hoac lien he ho tro. Nhu vay ben toi se tranh nham lan giua don da tra tien va don chua tra tien.", "Mock")
                        : new AiReplyResult("If payment fails, the order should not be confirmed. From our operations perspective, staff should not start packing or shipping an order that has not been paid for. The order can stay in a pending or failed status, so the customer may retry payment or contact support. This also helps us avoid confusion between paid orders and unpaid orders.", "Mock"));
                }

                if (HasAny(message, "confirm", "confirmed", "successful", "success"))
                {
                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Thong thuong, ben toi chi xac nhan don hang sau khi thanh toan thanh cong. Nhan vien kho va van hanh khong nen xu ly mot don ma tien chua ve. Quy tac nay giup trang thai don hang ro rang hon va tranh nham lan giua don da tra tien va don chua tra tien.", "Mock")
                        : new AiReplyResult("Normally, we only confirm the order after successful payment. Our staff should not start packing or shipping anything that has not been paid for, because that creates avoidable operational risk. That rule keeps the order statuses clean and prevents confusion between paid orders and unpaid ones.", "Mock"));
                }

                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Voi ben toi, don hang chi nen chuyen sang xu ly xac nhan sau khi thanh toan thanh cong. Neu thanh toan that bai, no nen dung o pending hoac failed thay vi day sang fulfillment. Neu khong, nhan vien co the bat dau dong goi mot don hang chua thu tien. Ben toi can luong trang thai ro rang de kho, ho tro va van hanh cung nhin cung mot tinh huong.", "Mock")
                    : new AiReplyResult("For our team, the key point is that an order should only move into confirmed processing after successful payment. If payment fails, it should stay in a pending or failed status instead of going to fulfillment. Otherwise, staff may start packing an order that has not been paid for. We need the status flow to be clear so support and warehouse teams are looking at the same situation.", "Mock"));
            }

            if (HasAny(message, "shipping", "delivery", "fee", "location", "weight"))
            {
                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Phi van chuyen thuong phu thuoc vao dia diem giao hang va trong luong don hang. Mot don nho giao gan se khong co chi phi giong mot kien hang nang giao di xa. Tu goc do van hanh, ben toi can quy tac nay vi chi phi cua doi tac giao hang thay doi theo khoang cach va kich thuoc goi hang. Neu phi tinh sai, nhan vien thuong phai xu ly khieu nai truoc ca khi don duoc giao.", "Mock")
                    : new AiReplyResult("Shipping fee usually depends on the customer's location and the order weight. A small order delivered nearby will not cost the same as a heavy package sent farther away. From the operations side, we need that rule because carrier cost changes with distance and package size. If the fee looks wrong, our staff end up handling customer complaints before the order is even shipped.", "Mock"));
            }

            if (HasAny(message, "cancel", "return", "refund"))
            {
                if (HasAny(message, "refund", "return"))
                {
                    if (HasAny(message, "approval", "approve"))
                    {
                        return Task.FromResult(language == "vi"
                            ? new AiReplyResult("Co. Yeu cau hoan tien thuong can nhan vien phe duyet truoc khi tra tien lai. Ben toi can buoc do de kiem tra ly do, trang thai don hang va tranh hoan nham hoac hoan trung lap. Neu bo qua khau nay, quy trinh hoan tien se rat kho kiem soat.", "Mock")
                            : new AiReplyResult("Yes. Refund requests usually need staff approval before money is returned. From our side, someone has to check the reason, the order status, and whether the case fits policy. If that step is skipped, refund handling becomes very hard to control.", "Mock"));
                    }

                    if (HasAny(message, "7 day", "7 days", "after delivery", "return period"))
                    {
                        return Task.FromResult(language == "vi"
                            ? new AiReplyResult("Co gioi han thoi gian. Ben toi thuong chi nhan yeu cau doi tra trong mot khoang sau khi giao hang, vi du 7 ngay sau khi khach nhan duoc don. Neu de qua lau, nhan vien se rat kho xac minh tinh trang san pham va lich su xu ly.", "Mock")
                            : new AiReplyResult("Yes, there is usually a time limit for returns. From our side, we would normally accept that request only within a defined period after delivery, for example 7 days. If the window is too open-ended, staff have a much harder time verifying the product condition and the case history.", "Mock"));
                    }

                    return Task.FromResult(language == "vi"
                        ? new AiReplyResult("Voi hoan tien va doi tra, ben toi thuong co hai diem can kiem soat. Mot la yeu cau hoan tien can nhan vien xem xet truoc khi tra tien. Hai la thoi gian doi tra khong the mo vo thoi han, thuong se co gioi han sau khi giao hang. Nhu vay ben toi moi kiem soat duoc rui ro va tranh xu ly nhung truong hop qua muon.", "Mock")
                        : new AiReplyResult("For refunds and returns, there are usually two controls on our side. First, a refund request needs staff approval before money is returned. Second, the return window cannot stay open forever, so there is normally a limited period after delivery. That is how we keep refund risk under control and avoid very late cases that are hard to verify.", "Mock"));
                }

                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Co, nhung chi truoc khi don hang duoc giao di. Khi kho da ban giao goi hang cho doi van chuyen, viec huy don se kho hon rat nhieu cho nhan vien xu ly. Luc do, khach thuong phai di theo quy trinh doi tra hoac hoan tien thay vi huy don truc tiep. Quy tac nay giup ben toi tranh xung dot giua xu ly don, giao hang va hoan tien.", "Mock")
                    : new AiReplyResult("Yes, but only before the order is shipped. Once the warehouse has already handed the package to the delivery partner, cancellation becomes much harder for our staff to handle. In that case, the customer usually needs to follow the return or refund process instead. This rule helps us avoid conflicts between order processing, shipping, and refund handling.", "Mock"));
            }

            if (HasAny(message, "report", "dashboard", "admin", "analytics"))
            {
                return Task.FromResult(language == "vi"
                    ? new AiReplyResult("Co. Ben toi can bao cao ve don bi huy, thanh toan that bai va viec su dung voucher. Nhin vao cac so nay, ben toi moi thay duoc cho nao quy trinh checkout dang bi nghen va campaign nao dang tao ra qua nhieu truong hop ho tro. Neu khong co bao cao, nhan vien chi nhan ra van de khi khoi luong cong viec da tang len roi.", "Mock")
                    : new AiReplyResult("Yes, admin needs reports on cancelled orders, failed payments, and voucher usage. Those numbers tell us where the checkout flow is breaking and whether a promotion is creating too many support cases. From the operations side, we use that information to spot patterns early instead of waiting for complaints to pile up. Otherwise, staff only notice problems after the workload has already increased.", "Mock"));
            }

            return Task.FromResult(language == "vi"
                ? new AiReplyResult("Tu phia van hanh, van de lon nhat la checkout va xu ly don hang van con qua thu cong. Khach hang ky vong quy trinh nhanh va ro rang, trong khi nhan vien van mat nhieu thoi gian de doi chieu voucher, ton kho, thanh toan va trang thai giao hang. Moi nhom nghiep vu lai co quy tac rieng, nen toi khong muon gop tat ca vao mot cau tra loi. Ban muon di vao phan nao truoc: voucher, ton kho, thanh toan, van chuyen, huy don hay hoan tien?", "Mock")
                : new AiReplyResult("From our side, the biggest issue is that checkout and order handling are still too manual. Customers expect the process to be fast and clear, while our staff spend too much time checking vouchers, stock, payment, and shipping status by hand. Each area has its own rules, so I do not want to mix everything together in one answer. Which part would you like to clarify first: voucher rules, stock handling, payment, shipping, cancellation, or refund?", "Mock"));
        }

        if (HasAny(message, "prerequisite", "requirement", "rule", "condition", "eligible", "eligibility"))
        {
            return Task.FromResult(language == "vi"
                ? new AiReplyResult("Co. Mot so mon hoc yeu cau sinh vien hoan thanh mon tien quyet truoc khi dang ky.", "Mock")
                : new AiReplyResult("Yes. For some courses, students must complete prerequisite subjects before they can register.", "Mock"));
        }

        if (HasAny(message, "capacity", "limit", "seat", "quota", "full"))
        {
            return Task.FromResult(language == "vi"
                ? new AiReplyResult("Moi lop hoc deu co gioi han so luong. Neu lop da day, sinh vien khong the dang ky them tru khi co truong hop dac biet.", "Mock")
                : new AiReplyResult("Each course has a limited capacity. Once a class is full, students should not be able to register unless staff handle a special case.", "Mock"));
        }

        if (HasAny(message, "schedule", "conflict", "time", "clash", "overlap", "timetable"))
        {
            return Task.FromResult(language == "vi"
                ? new AiReplyResult("Sinh vien khong nen duoc dang ky cac mon hoc bi trung lich. Dieu do gay van de cho diem danh va quan ly hoc tap.", "Mock")
                : new AiReplyResult("Students should not be allowed to register for courses that overlap in the timetable.", "Mock"));
        }

        if (HasAny(message, "cancel", "deadline", "drop", "withdraw", "change"))
        {
            return Task.FromResult(language == "vi"
                ? new AiReplyResult("Sinh vien co the huy hoac doi dang ky truoc han cu the. Sau thoi diem do, nhan vien can xem xet thu cong.", "Mock")
                : new AiReplyResult("Students can cancel or change a registration, but only before a deadline. After that, staff need to review the case manually.", "Mock"));
        }

        return Task.FromResult(language == "vi"
            ? new AiReplyResult("Muc tieu chinh cua ben toi la giam thao tac thu cong va giup quy trinh ro rang hon cho nguoi dung.", "Mock")
            : new AiReplyResult("Our main goal is to reduce manual work and make the process clearer for users.", "Mock"));
    }

    public Task<AiEvaluationResultEnvelope> EvaluateSubmissionAsync(AiEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var isVietnamese = request.ResponseLanguage == "Vietnamese";

        if (request.Domain.Equals("E-commerce", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AiEvaluationResultEnvelope(
                new EvaluationResultDto
                {
                    OverallScore = 74,
                    CompletenessScore = 22,
                    BusinessRuleScore = 17,
                    QuestionQualityScore = 16,
                    ClarityScore = 11,
                    ImprovementScore = 8,
                    MissingRequirements =
                    isVietnamese
                        ? ["Chua neu dieu kien gia tri toi thieu cua voucher.", "Thieu quy tac tam giu ton kho khi dat hang.", "Chua lam ro xu ly thanh toan that bai."]
                        : ["Voucher minimum order value was not mentioned.", "Stock reservation after order placement was missing.", "Payment failure handling was not clarified."],
                    Feedback = isVietnamese
                        ? "Ban da mo ta duoc luong checkout co ban, nhung van bo sot mot so quy tac e-commerce quan trong nhu dieu kien voucher, tam giu ton kho va xu ly thanh toan that bai."
                        : "You covered the main checkout flow, but missed several important e-commerce business rules such as voucher conditions, stock reservation, and payment failure handling."
                },
                "Mock"));
        }

        return Task.FromResult(new AiEvaluationResultEnvelope(
            new EvaluationResultDto
            {
                OverallScore = 76,
                CompletenessScore = 23,
                BusinessRuleScore = 18,
                QuestionQualityScore = 16,
                ClarityScore = 11,
                ImprovementScore = 8,
                MissingRequirements =
                isVietnamese
                    ? ["Chua lam ro gioi han suc chua cua lop.", "Thieu kiem tra trung lich.", "Chua lam ro han huy dang ky."]
                    : ["Course capacity was not clearly defined.", "Schedule conflict checking was missing.", "Cancellation deadline was not clarified."],
                Feedback = isVietnamese
                    ? "Ban da bao quat luong chinh, nhung van bo sot mot so quy tac nghiep vu va truong hop ngoai le quan trong."
                    : "You covered the main workflow, but missed several important business rules and exception cases."
            },
            "Mock"));
    }

    private static bool HasAny(string value, params string[] terms) => terms.Any(value.Contains);

    private static bool IsBroadOverviewQuestion(string normalized)
    {
        return HasAny(
            normalized,
            "what are the requirements",
            "what is the requirement",
            "what do you need",
            "tell me about the system",
            "tell me about this system",
            "tell me about the project",
            "what do we need",
            "give me an overview",
            "high level",
            "summary",
            "all requirements",
            "all the requirements");
    }

    private static bool IsEcommerceOutOfDomainQuestion(string normalized)
    {
        return HasAny(
            normalized,
            "course registration",
            "register for courses",
            "register for a course",
            "prerequisite",
            "lecturer",
            "training department",
            "university");
    }

    private static bool HasRevealedRequirement(IEnumerable<string> revealedRequirements, string phrase)
    {
        var normalizedPhrase = Normalize(phrase);
        return revealedRequirements.Any(item => item.Contains(normalizedPhrase));
    }

    private static string Normalize(string value)
    {
        var lower = value.ToLowerInvariant();
        var alphanumeric = Regex.Replace(lower, "[^a-z0-9\\s-]", " ");
        return Regex.Replace(alphanumeric, "\\s+", " ").Trim();
    }
}

public static class PasswordTools
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 3 || parts[0] != "pbkdf2")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public partial class Program;
