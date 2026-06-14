using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ReqSimulator.Api.Tests;

public sealed class ApiPermissionTests(ApiServerFixture fixture) : IClassFixture<ApiServerFixture>
{
    [Fact]
    public async Task SessionsRequireAuthenticatedUserHeader()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/sessions", new
        {
            userId = "user-student-1",
            scenarioId = "ecommerce-order-promotion"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StudentCanCreateOwnSessionAndSaveNotes()
    {
        var student = await fixture.LoginAsync("student@reqsim.local", "demo123");
        var session = await fixture.CreateSessionAsync(student.Id);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/sessions/{session.Id}/notes")
        {
            Content = JsonContent.Create(new
            {
                content = "Need to clarify voucher rules and payment failure flow."
            })
        };
        request.Headers.Add("X-ReqSim-UserId", student.Id);

        var response = await fixture.Client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<NoteEnvelope>(fixture.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Need to clarify voucher rules and payment failure flow.", payload!.Note.Content);
    }

    [Fact]
    public async Task StudentCannotOpenInstructorDashboard()
    {
        var student = await fixture.LoginAsync("student@reqsim.local", "demo123");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/instructor/dashboard");
        request.Headers.Add("X-ReqSim-UserId", student.Id);

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StudentCannotSubmitInstructorReview()
    {
        var student = await fixture.LoginAsync("student@reqsim.local", "demo123");
        var evaluatedSession = await fixture.CreateEvaluatedSessionAsync(student.Id);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/instructor/reviews")
        {
            Content = JsonContent.Create(new
            {
                sessionId = evaluatedSession.Id,
                instructorId = student.Id,
                adjustedScore = 88,
                comment = "Spoofed review attempt"
            })
        };
        request.Headers.Add("X-ReqSim-UserId", student.Id);

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InstructorCanAccessDashboardAndReviewAsSelf()
    {
        var student = await fixture.LoginAsync("student@reqsim.local", "demo123");
        var instructor = await fixture.LoginAsync("instructor@reqsim.local", "demo123");
        var evaluatedSession = await fixture.CreateEvaluatedSessionAsync(student.Id);

        var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/api/instructor/dashboard");
        dashboardRequest.Headers.Add("X-ReqSim-UserId", instructor.Id);
        var dashboardResponse = await fixture.Client.SendAsync(dashboardRequest);

        var reviewRequest = new HttpRequestMessage(HttpMethod.Post, "/api/instructor/reviews")
        {
            Content = JsonContent.Create(new
            {
                sessionId = evaluatedSession.Id,
                instructorId = student.Id,
                adjustedScore = 91,
                comment = "Strong interview follow-up on voucher rules."
            })
        };
        reviewRequest.Headers.Add("X-ReqSim-UserId", instructor.Id);
        var reviewResponse = await fixture.Client.SendAsync(reviewRequest);
        var reviewPayload = await reviewResponse.Content.ReadFromJsonAsync<ReviewEnvelope>(fixture.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        Assert.NotNull(reviewPayload);
        Assert.Equal(instructor.Id, reviewPayload!.Review.InstructorId);
        Assert.Equal(91, reviewPayload.Review.AdjustedScore);
    }
}

public sealed class ApiServerFixture : IAsyncLifetime
{
    private readonly StringBuilder logs = new();
    private Process? process;
    private string baseAddress = "";

    public HttpClient Client { get; private set; } = default!;
    public JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot();
        var apiDll = Path.Combine(repoRoot, "source", "backend", "src", "ReqSimulator.Api", "bin", "Debug", "net9.0", "ReqSimulator.Api.dll");
        var port = GetFreePort();
        baseAddress = $"http://127.0.0.1:{port}";

        if (!File.Exists(apiDll))
        {
            throw new FileNotFoundException("ReqSimulator.Api.dll was not found. Ensure the API project builds before the permission tests run.", apiDll);
        }

        var startInfo = new ProcessStartInfo("dotnet", $"\"{apiDll}\" --urls {baseAddress}")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

        process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Req Simulator API test process.");
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                lock (logs)
                {
                    logs.AppendLine(args.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                lock (logs)
                {
                    logs.AppendLine(args.Data);
                }
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        Client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress)
        };

        await WaitForServerAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    public async Task<UserDto> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UserEnvelope>(JsonOptions);
        return payload?.User ?? throw new InvalidOperationException("Login response did not include a user payload.");
    }

    public async Task<SessionDto> CreateSessionAsync(string userId, string scenarioId = "ecommerce-order-promotion")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions")
        {
            Content = JsonContent.Create(new
            {
                userId,
                scenarioId
            })
        };
        request.Headers.Add("X-ReqSim-UserId", userId);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SessionEnvelope>(JsonOptions);
        return payload?.Session ?? throw new InvalidOperationException("Session creation did not return a session payload.");
    }

    public async Task<SessionDto> CreateEvaluatedSessionAsync(string studentUserId)
    {
        var session = await CreateSessionAsync(studentUserId);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/evaluation/{session.Id}")
        {
            Content = JsonContent.Create(new
            {
                learnerSubmission = "Customers can browse products, add to cart, and place orders.",
                userStories = "As a customer, I want to place an order.",
                useCases = "Checkout flow",
                acceptanceCriteria = "Order should only confirm after payment.",
                additionalNotes = "Need voucher and payment checks.",
                reflection = "Need more questions on stock and refund.",
                language = "en",
                responseLanguage = "English"
            })
        };
        request.Headers.Add("X-ReqSim-UserId", studentUserId);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return session;
    }

    private async Task WaitForServerAsync()
    {
        var healthPath = new Uri(new Uri(baseAddress), "/api/health");

        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (process is not null && process.HasExited)
            {
                throw new InvalidOperationException($"Req Simulator API test server exited unexpectedly.{Environment.NewLine}{logs}");
            }

            try
            {
                using var response = await Client.GetAsync(healthPath);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Server is still starting.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Timed out waiting for Req Simulator API test server.{Environment.NewLine}{logs}");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "source", "backend", "src", "ReqSimulator.Api", "ReqSimulator.Api.csproj");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Req Simulator repository root for API tests.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

public sealed record UserEnvelope(UserDto User);
public sealed record UserDto(string Id, string FullName, string Email, string Role);
public sealed record SessionEnvelope(SessionDto Session);
public sealed record SessionDto(string Id);
public sealed record NoteEnvelope(NoteDto Note);
public sealed record NoteDto(string Content);
public sealed record ReviewEnvelope(ReviewDto Review);
public sealed record ReviewDto(string Id, string SessionId, string InstructorId, int AdjustedScore, string Comment);
