import { useEffect, useMemo, useRef, useState } from "react";
import { api, API_BASE_URL } from "./api/client";

const demoStudent = { email: "student@reqsim.local", password: "demo123" };
const demoInstructor = { email: "instructor@reqsim.local", password: "demo123" };

const defaultAiStatus = {
  loading: true,
  provider: "Mock",
  configured: false,
  model: "mock",
  safeMessage: "",
  error: ""
};

const emptySubmission = {
  userStories: "",
  useCases: "",
  acceptanceCriteria: "",
  additionalNotes: "",
  reflection: ""
};

export default function App() {
  const [currentUser, setCurrentUser] = useState(readStoredUser);
  const [language, setLanguage] = useState(readStoredLanguage);
  const [role, setRole] = useState(() => readStoredUser()?.role || "Student");
  const [view, setView] = useState("scenarios");
  const [scenarios, setScenarios] = useState([]);
  const [selectedScenarioId, setSelectedScenarioId] = useState("");
  const [session, setSession] = useState(null);
  const [dashboard, setDashboard] = useState(null);
  const [message, setMessage] = useState("");
  const [note, setNote] = useState("");
  const [submission, setSubmission] = useState(emptySubmission);
  const [authMode, setAuthMode] = useState("login");
  const [status, setStatus] = useState({ loading: Boolean(currentUser), error: "" });
  const [aiStatus, setAiStatus] = useState(defaultAiStatus);
  const [messagePending, setMessagePending] = useState(false);
  const [evaluationPending, setEvaluationPending] = useState(false);
  const [recentDiscoveries, setRecentDiscoveries] = useState([]);
  const [discoveryLedger, setDiscoveryLedger] = useState([]);
  const [messageMeta, setMessageMeta] = useState({});
  const [liveReply, setLiveReply] = useState(null);
  const [noteState, setNoteState] = useState("idle");
  const chatEndRef = useRef(null);
  const copy = UI_COPY[language];
  const displayScenarios = useMemo(
    () => scenarios.map((item) => localizeScenario(item, language)),
    [scenarios, language]
  );
  const displaySession = useMemo(
    () => localizeSession(session, language),
    [session, language]
  );
  const displayDashboard = useMemo(
    () => localizeDashboard(dashboard, language),
    [dashboard, language]
  );
  const displayRecentDiscoveries = useMemo(
    () => localizeTextList(recentDiscoveries, language),
    [recentDiscoveries, language]
  );
  const displayDiscoveryLedger = useMemo(
    () => localizeTextList(discoveryLedger, language),
    [discoveryLedger, language]
  );

  const selectedScenario = useMemo(
    () => displayScenarios.find((item) => item.id === selectedScenarioId) || displayScenarios[0] || null,
    [displayScenarios, selectedScenarioId]
  );

  const canInstruct = currentUser?.role === "Instructor" || currentUser?.role === "Admin";
  const learnerQuestionCount = displaySession?.messages?.filter((item) => item.sender === "Learner").length || 0;

  useEffect(() => {
    loadAiStatus();
    if (currentUser) {
      loadScenarios();
    } else {
      setStatus({ loading: false, error: "" });
    }
  }, [currentUser]);

  useEffect(() => {
    if (role === "Instructor" && currentUser) {
      loadDashboard();
    }
  }, [role, currentUser]);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [session?.messages?.length, messagePending]);

  useEffect(() => {
    localStorage.setItem("reqsim_language", language);
  }, [language]);

  async function loadAiStatus() {
    try {
      const data = await api("/ai/status");
      setAiStatus({ loading: false, error: "", ...data });
    } catch (error) {
      setAiStatus({
        loading: false,
        provider: "Unavailable",
        configured: false,
        model: "-",
        safeMessage: error.message,
        error: error.message
      });
    }
  }

  async function loadScenarios() {
    try {
      setStatus({ loading: true, error: "" });
      const data = await api("/scenarios");
      setScenarios(data.scenarios);
      setSelectedScenarioId((current) => current || data.scenarios[0]?.id || "");
      setStatus({ loading: false, error: "" });
    } catch (error) {
      setStatus({ loading: false, error: error.message });
    }
  }

  async function loadDashboard() {
    try {
      const data = await api("/instructor/dashboard");
      setDashboard(data);
    } catch (error) {
      setStatus((current) => ({ ...current, error: error.message }));
    }
  }

  async function login(credentials) {
    try {
      setStatus({ loading: true, error: "" });
      const data = await api("/auth/login", {
        method: "POST",
        body: credentials
      });
      completeAuth(data.user);
    } catch (error) {
      setStatus({ loading: false, error: error.message });
    }
  }

  async function register(payload) {
    try {
      setStatus({ loading: true, error: "" });
      const data = await api("/auth/register", {
        method: "POST",
        body: payload
      });
      completeAuth(data.user);
    } catch (error) {
      setStatus({ loading: false, error: error.message });
    }
  }

  async function externalLogin(provider) {
    try {
      setStatus({ loading: true, error: "" });
      const data = await api("/auth/external-demo", {
        method: "POST",
        body: { provider }
      });
      completeAuth(data.user);
    } catch (error) {
      setStatus({ loading: false, error: error.message });
    }
  }

  function completeAuth(user) {
    setCurrentUser(user);
    setRole(user.role === "Instructor" || user.role === "Admin" ? user.role : "Student");
    setView(user.role === "Instructor" || user.role === "Admin" ? "scenarios" : "scenarios");
    localStorage.setItem("reqsim.user", JSON.stringify(user));
  }

  function logout() {
    setCurrentUser(null);
    setRole("Student");
    setView("scenarios");
    setSession(null);
    setDashboard(null);
    setMessage("");
    setNote("");
    setSubmission(emptySubmission);
    setRecentDiscoveries([]);
    setDiscoveryLedger([]);
    setMessageMeta({});
    setLiveReply(null);
    setNoteState("idle");
    localStorage.removeItem("reqsim.user");
  }

  async function startSession(scenarioId = selectedScenarioId) {
    if (!currentUser || !scenarioId) return;

    try {
      setStatus((current) => ({ ...current, error: "" }));
      const data = await api("/sessions", {
        method: "POST",
        body: {
          userId: currentUser.id,
          scenarioId
        }
      });

      setSession(decorateSessionMessages(data.session, {}));
      setRecentDiscoveries([]);
      setDiscoveryLedger([]);
      setMessageMeta({});
      setLiveReply(null);
      setNote(data.session.note?.content || "");
      setNoteState("idle");
      setSubmission(emptySubmission);
      setMessage("");
      setSelectedScenarioId(scenarioId);
      setView("session");
    } catch (error) {
      setStatus((current) => ({ ...current, error: error.message }));
    }
  }

  async function sendMessage(event) {
    event.preventDefault();
    if (!session || !message.trim()) return;

    const draft = message.trim();
    const activeSessionId = session.id;
    setMessage("");
    setMessagePending(true);
    setStatus((current) => ({ ...current, error: "" }));
    setSession((current) => current
      ? {
          ...current,
          messages: [
            ...current.messages,
            {
              id: `local-learner-${Date.now()}`,
              sender: "Learner",
              content: draft,
              createdAt: new Date().toISOString(),
              revealedRequirementIds: []
            },
            {
              id: "local-thinking",
              sender: "AIStakeholder",
              content: copy.stakeholderThinking,
              createdAt: new Date().toISOString(),
              revealedRequirementIds: [],
              pending: true
            }
          ]
        }
      : current);

    try {
      const data = await api(`/simulation/${activeSessionId}/message`, {
        method: "POST",
        body: {
          message: draft,
          language,
          responseLanguage: toResponseLanguage(language)
        }
      });

      const nextMeta = mergeMessageMeta(messageMeta, data.session.messages, data.provider, data.category);
      setMessageMeta(nextMeta);
      setRecentDiscoveries(data.discoveredRequirements || []);
      setDiscoveryLedger((current) => dedupeStrings([...current, ...(data.discoveredRequirements || [])]));
      setLiveReply({
        provider: data.provider || (aiStatus.configured ? aiStatus.provider : "Mock"),
        category: data.category || "general",
        createdAt: data.createdAt
      });
      setSession(decorateSessionMessages(data.session, nextMeta));
    } catch (error) {
      setStatus((current) => ({ ...current, error: error.message }));
      setRecentDiscoveries([]);
      setLiveReply({
        provider: "Unavailable",
        category: "error",
        createdAt: new Date().toISOString()
      });
      setSession((current) => current
        ? {
            ...current,
            messages: [
              ...current.messages.filter((item) => item.id !== "local-thinking"),
              {
                id: `local-error-${Date.now()}`,
                sender: "AIStakeholder",
                content: copy.aiUnavailable,
                createdAt: new Date().toISOString(),
                revealedRequirementIds: []
              }
            ]
          }
        : current);
    } finally {
      setMessagePending(false);
    }
  }

  async function saveNote() {
    if (!session) return;

    try {
      setNoteState("saving");
      const data = await api(`/sessions/${session.id}/notes`, {
        method: "PUT",
        body: { content: note }
      });
      setSession((current) => current ? { ...current, note: data.note } : current);
      setNoteState("saved");
    } catch (error) {
      setNoteState("error");
      throw error;
    }
  }

  async function submitRequirements(event) {
    event.preventDefault();
    if (!session) return;

    try {
      setEvaluationPending(true);
      setStatus((current) => ({ ...current, error: "" }));
      await saveNote();
      const data = await api(`/evaluation/${session.id}`, {
        method: "POST",
        body: {
          ...submission,
          learnerSubmission: buildLearnerSubmission(submission, note, language),
          language,
          responseLanguage: toResponseLanguage(language)
        }
      });
      setSession(decorateSessionMessages(data.session, messageMeta));
      setView("feedback");
    } catch (error) {
      setStatus((current) => ({ ...current, error: error.message }));
    } finally {
      setEvaluationPending(false);
    }
  }

  async function submitReview(event, sessionId) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);

    await api("/instructor/reviews", {
      method: "POST",
      body: {
        sessionId,
        instructorId: currentUser?.id,
        adjustedScore: Number(form.get("adjustedScore")),
        comment: String(form.get("comment") || "")
      }
    });
    await loadDashboard();
  }

  function switchRole(nextRole) {
    if (nextRole === "Instructor" && !canInstruct) {
      return;
    }

    setRole(nextRole);
    setView(nextRole === "Instructor" ? "instructor" : "scenarios");
  }

  function handleNoteChange(value) {
    setNote(value);
    if (noteState !== "idle") {
      setNoteState("idle");
    }
  }

  if (!currentUser) {
    return (
      <AuthPage
        copy={copy}
        language={language}
        mode={authMode}
        loading={status.loading}
        error={status.error}
        aiStatus={aiStatus}
        onLanguageChange={setLanguage}
        onModeChange={setAuthMode}
        onLogin={login}
        onRegister={register}
        onExternalLogin={externalLogin}
      />
    );
  }

  return (
    <div className="app-shell">
      {status.error && <ApiBanner copy={copy} message={status.error} />}
      <main className="workspace-frame">
        <AppNavbar
          copy={copy}
          language={language}
          onLanguageChange={setLanguage}
          user={currentUser}
          role={role}
          canInstruct={canInstruct}
          onRoleChange={switchRole}
          onLogout={logout}
          aiStatus={aiStatus}
          liveReply={liveReply}
        />

        <div className="workspace-body">
          <SideNav
            copy={copy}
            language={language}
            view={view}
            user={currentUser}
            role={role}
            canInstruct={canInstruct}
            selectedScenario={selectedScenario}
            session={displaySession}
            onRoleChange={switchRole}
            onLogout={logout}
            onViewChange={setView}
            onStart={() => startSession(selectedScenarioId)}
          />

          <section className="content-shell">
            {role === "Instructor" ? (
              <InstructorDashboard
                copy={copy}
                dashboard={displayDashboard}
                aiStatus={aiStatus}
                onSubmitReview={submitReview}
              />
            ) : (
              <main className="main-workspace">
                {status.loading && <EmptyState title={copy.loadingScenarios} detail={copy.loadingShell} />}

                {!status.loading && view === "scenarios" && (
                  <ScenarioLibrary
                    copy={copy}
                    language={language}
                    scenarios={displayScenarios}
                    selectedScenario={selectedScenario}
                    onSelect={setSelectedScenarioId}
                    onStart={startSession}
                  />
                )}

                {!status.loading && view === "session" && displaySession && (
                  <SimulationWorkspace
                    copy={copy}
                    language={language}
                    session={displaySession}
                    aiStatus={aiStatus}
                    liveReply={liveReply}
                    recentDiscoveries={displayRecentDiscoveries}
                    discoveryLedger={displayDiscoveryLedger}
                    noteState={noteState}
                    note={note}
                    message={message}
                    chatEndRef={chatEndRef}
                    onMessageChange={setMessage}
                    onNoteChange={handleNoteChange}
                    onSaveNote={saveNote}
                    onSendMessage={sendMessage}
                    messagePending={messagePending}
                    submission={submission}
                    onSubmissionChange={setSubmission}
                    onSubmitRequirements={submitRequirements}
                    evaluationPending={evaluationPending}
                  />
                )}

                {!status.loading && view === "feedback" && displaySession?.evaluation && (
                  <FeedbackReport
                    copy={copy}
                    language={language}
                    session={displaySession}
                    liveReply={liveReply}
                    discoveryLedger={displayDiscoveryLedger}
                    learnerQuestionCount={learnerQuestionCount}
                    onRetry={() => startSession(displaySession.scenario.id)}
                    onRefine={() => {
                      setView("session");
                      setEvaluationPending(false);
                    }}
                  />
                )}

                {!status.loading && view !== "scenarios" && view !== "session" && view !== "feedback" && (
                  <HistoryView
                    copy={copy}
                    language={language}
                    session={displaySession}
                    liveReply={liveReply}
                    discoveryLedger={displayDiscoveryLedger}
                    learnerQuestionCount={learnerQuestionCount}
                    onRetry={() => displaySession?.scenario?.id && startSession(displaySession.scenario.id)}
                    onRefine={() => {
                      if (displaySession?.scenario?.id) {
                        setView("session");
                        setEvaluationPending(false);
                      }
                    }}
                  />
                )}

                {!status.loading && view === "session" && !session && (
                  <EmptyState title={copy.startFromLibrary} detail={copy.startFromLibraryHint} />
                )}
              </main>
            )}
          </section>
        </div>
      </main>
    </div>
  );
}

function AuthPage({
  copy,
  language,
  mode,
  loading,
  error,
  aiStatus,
  onLanguageChange,
  onModeChange,
  onLogin,
  onRegister,
  onExternalLogin
}) {
  function submitLogin(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    onLogin({
      email: String(form.get("email") || ""),
      password: String(form.get("password") || "")
    });
  }

  function submitRegister(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    onRegister({
      fullName: String(form.get("fullName") || ""),
      email: String(form.get("email") || ""),
      password: String(form.get("password") || ""),
      role: String(form.get("role") || "Student")
    });
  }

  return (
    <main className="auth-shell">
      <section className="auth-hero">
        <div className="brand brand-space">
          <div className="brand-logo">RS</div>
          <div>
            <strong>Req Simulator</strong>
            <span>{copy.tagline}</span>
          </div>
        </div>

        <div className="badge-row">
          <span className="signal-badge accent">{copy.mvpDemo}</span>
          <span className="signal-badge">{copy.modeLabel}: B2B SaaS</span>
          <span className={`signal-badge ${getProviderTone(aiStatus, aiStatus.provider)}`}>
            {copy.aiStatusLabel}: {displayProvider(aiStatus.provider, copy)}
          </span>
        </div>

        <p className="eyebrow">{copy.authEyebrow}</p>
        <h1>{copy.authHeadline}</h1>
        <p className="hero-copy">{copy.authBody}</p>

        <div className="hero-metric-grid">
          <Metric label={copy.metrics.scenarios} value={4} />
          <Metric label={copy.metrics.rubric} value={language === "vi" ? "100 điểm" : "100 pts"} />
          <Metric label={copy.liveReplies} value={aiStatus.configured ? displayProvider(aiStatus.provider, copy) : copy.mockMode} />
        </div>

        <div className="hero-process-grid">
          <ProcessStep icon="explore" title={copy.chooseScenario} detail={copy.authFlowScenario} />
          <ProcessStep icon="forum" title={copy.aiStakeholder} detail={copy.authFlowInterview} />
          <ProcessStep icon="monitoring" title={copy.feedback} detail={copy.authFlowFeedback} />
        </div>

        <div className="hero-footer">
          <strong>{copy.multiDomainNote}</strong>
          <span>{copy.demoScenarioFocus}</span>
        </div>
      </section>

      <section className="auth-panel">
        <div className="auth-toolbar">
          <LanguageSwitcher copy={copy} language={language} onLanguageChange={onLanguageChange} />
        </div>

        <div className="inline-status-strip">
          <div>
            <span className="detail-label">{copy.systemStatus}</span>
            <strong>{aiStatus.configured ? `${displayProvider(aiStatus.provider, copy)} • ${aiStatus.model}` : copy.mockMode}</strong>
          </div>
          <span className={`signal-dot ${getProviderTone(aiStatus, aiStatus.provider)}`} />
        </div>
        <p className="panel-intro">{displayAiStatusMessage(aiStatus, copy, language) || copy.multiDomainNote}</p>

        <div className="auth-tabs">
          <button type="button" className={mode === "login" ? "active" : ""} onClick={() => onModeChange("login")}>{copy.login}</button>
          <button type="button" className={mode === "register" ? "active" : ""} onClick={() => onModeChange("register")}>{copy.register}</button>
        </div>

        {error && <div className="form-error">{error}</div>}

        {mode === "login" ? (
          <form className="auth-form" onSubmit={submitLogin}>
            <div>
              <h2>{copy.authWelcome}</h2>
              <p>{copy.authContinue}</p>
            </div>
            <div className="oauth-grid">
              <button type="button" className="oauth-button" onClick={() => onExternalLogin("Google")} disabled={loading}>
                <GoogleLogo />
                {copy.continueGoogle}
              </button>
              <button type="button" className="oauth-button" onClick={() => onExternalLogin("GitHub")} disabled={loading}>
                <GitHubLogo />
                {copy.continueGithub}
              </button>
            </div>
            <div className="auth-divider"><span>{copy.orUseEmail}</span></div>
            <label>
              {copy.email}
              <input name="email" type="email" defaultValue={demoStudent.email} />
            </label>
            <label>
              {copy.password}
              <input name="password" type="password" defaultValue={demoStudent.password} />
            </label>
            <button className="primary-button" type="submit" disabled={loading}>
              {loading ? copy.signingIn : copy.signIn}
            </button>
            <div className="demo-login-grid">
              <button type="button" className="ghost-button" onClick={() => onLogin(demoStudent)}>{copy.studentDemo}</button>
              <button type="button" className="ghost-button" onClick={() => onLogin(demoInstructor)}>{copy.instructorDemo}</button>
            </div>
          </form>
        ) : (
          <form className="auth-form" onSubmit={submitRegister}>
            <div>
              <h2>{copy.createAccount}</h2>
              <p>{copy.registerHint}</p>
            </div>
            <label>
              {copy.fullName}
              <input name="fullName" defaultValue={copy.defaultLearnerName} />
            </label>
            <label>
              {copy.email}
              <input name="email" type="email" defaultValue={`learner${Date.now()}@example.com`} />
            </label>
            <label>
              {copy.password}
              <input name="password" type="password" defaultValue="demo123" />
            </label>
            <label>
              {copy.role}
              <select name="role" defaultValue="Student">
                <option value="Student">{copy.student}</option>
                <option value="Instructor">{copy.instructor}</option>
              </select>
            </label>
            <button className="primary-button" type="submit" disabled={loading}>
              {loading ? copy.creating : copy.createAccount}
            </button>
          </form>
        )}
      </section>
    </main>
  );
}

function AppNavbar({ copy, language, onLanguageChange, user, role, canInstruct, onRoleChange, onLogout, aiStatus, liveReply }) {
  const initials = getInitials(user.fullName);
  const liveProvider = liveReply?.provider || (aiStatus.configured ? aiStatus.provider : "Mock");

  return (
    <header className="app-navbar">
      <div className="navbar-brand">
        <div className="brand-logo">RS</div>
        <div>
          <strong>Req Simulator</strong>
          <span>{copy.tagline}</span>
        </div>
      </div>

      <div className="navbar-status">
        <StatusBadge
          label={copy.aiStatusLabel}
          value={aiStatus.loading ? copy.loadingShort : displayProvider(aiStatus.provider, copy)}
          detail={aiStatus.loading ? copy.loadingShell : aiStatus.model}
          tone={getProviderTone(aiStatus, aiStatus.provider)}
        />
        <StatusBadge
          label={copy.liveReplies}
          value={displayProvider(liveProvider, copy)}
          detail={liveReply ? humanizeCategory(liveReply.category, language) : copy.runtimeAwaiting}
          tone={getProviderTone(aiStatus, liveProvider)}
        />
      </div>

      <div className="navbar-actions">
        {canInstruct && (
          <RoleToggle copy={copy} role={role} onRoleChange={onRoleChange} />
        )}
        <LanguageSwitcher copy={copy} language={language} onLanguageChange={onLanguageChange} />
        <div className="user-chip">
          <div className="candidate-avatar small">{initials}</div>
          <div>
            <strong>{user.fullName}</strong>
            <span>{role === "Instructor" ? copy.instructorAccess : copy.studentAccess}</span>
          </div>
        </div>
        <button type="button" className="ghost-button compact-button" onClick={onLogout}>
          <Icon name="logout" />
          <span>{copy.logout}</span>
        </button>
      </div>
    </header>
  );
}

function RoleToggle({ copy, role, onRoleChange }) {
  return (
    <div className="role-toggle" aria-label={copy.role}>
      <button type="button" className={role === "Student" ? "active" : ""} onClick={() => onRoleChange("Student")}>
        {copy.student}
      </button>
      <button type="button" className={role === "Instructor" ? "active" : ""} onClick={() => onRoleChange("Instructor")}>
        {copy.instructor}
      </button>
    </div>
  );
}

function SideNav({ copy, language, view, role, canInstruct, selectedScenario, session, onRoleChange, onLogout, onViewChange, onStart }) {
  const isStudent = role !== "Instructor";
  const activeScenario = session?.scenario || selectedScenario;

  return (
    <aside className="workspace-sidebar">
      <section className="sidebar-card sidebar-intro">
        <p className="eyebrow">{role === "Instructor" ? copy.mentorWorkspace : copy.studentWorkspace}</p>
        <h2>{activeViewLabel(view, copy)}</h2>
        <p>{role === "Instructor" ? copy.instructorIntro : copy.multiDomainNote}</p>
      </section>

      <section className="sidebar-card nav-card">
        <button
          type="button"
          className={isStudent && view === "scenarios" ? "nav-item active" : "nav-item"}
          onClick={() => {
            onRoleChange("Student");
            onViewChange("scenarios");
          }}
        >
          <Icon name="explore" />
          <div>
            <strong>{copy.chooseScenario}</strong>
            <span>{copy.navScenarioDetail}</span>
          </div>
        </button>
        <button
          type="button"
          className={isStudent && view === "session" ? "nav-item active" : "nav-item"}
          onClick={() => {
            onRoleChange("Student");
            onViewChange("session");
          }}
        >
          <Icon name="forum" />
          <div>
            <strong>{copy.activeSession}</strong>
            <span>{copy.navSessionDetail}</span>
          </div>
        </button>
        <button
          type="button"
          className={isStudent && view === "feedback" ? "nav-item active" : "nav-item"}
          onClick={() => {
            onRoleChange("Student");
            onViewChange("feedback");
          }}
        >
          <Icon name="monitoring" />
          <div>
            <strong>{copy.performance}</strong>
            <span>{copy.navFeedbackDetail}</span>
          </div>
        </button>
        {canInstruct && (
          <button
            type="button"
            className={role === "Instructor" ? "nav-item active" : "nav-item"}
            onClick={() => onRoleChange("Instructor")}
          >
            <Icon name="school" />
            <div>
              <strong>{copy.instructor}</strong>
              <span>{copy.navInstructorDetail}</span>
            </div>
          </button>
        )}
      </section>

      {isStudent && activeScenario && (
        <section className="glass-card sidebar-card scenario-quick-card">
          <div className="badge-row">
            {activeScenario.id === "ecommerce-order-promotion" && <span className="signal-badge accent">{copy.mvpDemo}</span>}
            <span className="signal-badge neutral">{activeScenario.domain}</span>
          </div>
          <h2>{activeScenario.title}</h2>
          <p>{activeScenario.stakeholderRole}</p>
          <div className="mini-stat-list">
            <div>
              <span className="detail-label">{copy.duration}</span>
              <strong>{formatDuration(activeScenario.estimatedMinutes, language)}</strong>
            </div>
            <div>
              <span className="detail-label">{copy.hiddenRules}</span>
              <strong>{activeScenario.hiddenRequirementCount}</strong>
            </div>
          </div>
          <button type="button" className="primary-button full-width" onClick={onStart}>
            <Icon name="play_circle" />
            <span>{copy.startInterview}</span>
          </button>
        </section>
      )}

      <section className="glass-card sidebar-card sidebar-footer">
        <button type="button" className="ghost-button full-width" onClick={onLogout}>
          <Icon name="logout" />
          <span>{copy.logout}</span>
        </button>
      </section>
    </aside>
  );
}

function ScenarioLibrary({ copy, language, scenarios, selectedScenario, onSelect, onStart }) {
  if (!selectedScenario) {
    return <EmptyState title={copy.loadingScenarios} detail={copy.loadingShell} />;
  }

  return (
    <section className="page-stack">
      <article className="glass-card spotlight-card">
        <div className="spotlight-copy">
          <div className="badge-row">
            {selectedScenario.id === "ecommerce-order-promotion" && <span className="signal-badge accent">{copy.mvpDemo}</span>}
            <span className="signal-badge neutral">{selectedScenario.domain}</span>
            <span className="signal-badge neutral">{selectedScenario.difficulty}</span>
          </div>
          <p className="eyebrow">{copy.libraryEyebrow}</p>
          <h1>{copy.chooseScenario}</h1>
          <strong className="spotlight-title">{selectedScenario.title}</strong>
          <p>{selectedScenario.description}</p>
          <p className="muted">{copy.multiDomainNote}</p>

          <div className="spotlight-metrics">
            <Metric label={copy.stakeholder} value={selectedScenario.stakeholderRole} />
            <Metric label={copy.duration} value={formatDuration(selectedScenario.estimatedMinutes, language)} />
            <Metric label={copy.hiddenRules} value={selectedScenario.hiddenRequirementCount} />
          </div>
        </div>

        <div className="spotlight-panel">
          <div className="panel-heading">
            <h2>{copy.scenarioContext}</h2>
            <span className="signal-badge neutral">{copy.demoScenario}</span>
          </div>
          <p>{selectedScenario.initialContext}</p>

          <div className="detail-block">
            <span className="detail-label">{copy.visibleScope}</span>
            <div className="tag-list">
              {(selectedScenario.visibleRequirements || []).slice(0, 4).map((item) => (
                <span className="tag-chip" key={item}>{item}</span>
              ))}
            </div>
          </div>

          <div className="detail-block">
            <span className="detail-label">{copy.quickQuestions}</span>
            <div className="tag-list">
              {(selectedScenario.suggestedQuestions || []).slice(0, 4).map((question) => (
                <button type="button" className="question-chip-button" key={question} onClick={() => onStart(selectedScenario.id)}>
                  {question}
                </button>
              ))}
            </div>
          </div>

          <button type="button" className="primary-button full-width" onClick={() => onStart(selectedScenario.id)}>
            <Icon name="play_circle" />
            <span>{copy.startInterview}</span>
          </button>
        </div>
      </article>

      <div className="section-header">
        <div>
          <p className="eyebrow">{copy.availableScenarios}</p>
          <h2>{scenarios.length} {copy.ready}</h2>
        </div>
      </div>

      <div className="scenario-grid">
        {scenarios.map((scenario) => (
          <button
            type="button"
            key={scenario.id}
            className={scenario.id === selectedScenario.id ? "scenario-card selected" : "scenario-card"}
            onClick={() => onSelect(scenario.id)}
          >
            <div className="badge-row">
              <span className="signal-badge neutral">{scenario.domain}</span>
              <span className="signal-badge neutral">{scenario.difficulty}</span>
              {scenario.id === "ecommerce-order-promotion" && <span className="signal-badge accent">{copy.mvpDemo}</span>}
            </div>
            <strong>{scenario.title}</strong>
            <p>{scenario.description}</p>
            <div className="scenario-meta-list">
              <span>{scenario.stakeholderRole}</span>
              <span>{formatDuration(scenario.estimatedMinutes, language)}</span>
            </div>
            <div className="scenario-footer">
              <span>{scenario.hiddenRequirementCount} {copy.hiddenRules.toLowerCase()}</span>
              <span>{(scenario.actors || []).length} {copy.actorsLabel.toLowerCase()}</span>
            </div>
          </button>
        ))}
      </div>
    </section>
  );
}

function SimulationWorkspace({
  copy,
  language,
  session,
  aiStatus,
  liveReply,
  recentDiscoveries,
  discoveryLedger,
  noteState,
  note,
  message,
  chatEndRef,
  onMessageChange,
  onNoteChange,
  onSaveNote,
  onSendMessage,
  messagePending,
  submission,
  onSubmissionChange,
  onSubmitRequirements,
  evaluationPending
}) {
  const runtimeProvider = liveReply?.provider || (aiStatus.configured ? aiStatus.provider : "Mock");
  const runtimeTone = getProviderTone(aiStatus, runtimeProvider);
  const discoveredItems = discoveryLedger.length > 0 ? discoveryLedger : recentDiscoveries;

  return (
    <section className="page-stack session-page">
      <article className="glass-card session-hero">
        <div className="session-hero-copy">
          <div className="badge-row">
            {session.scenario.id === "ecommerce-order-promotion" && <span className="signal-badge accent">{copy.mvpDemo}</span>}
            <span className="signal-badge neutral">{session.scenario.domain}</span>
            <span className={`signal-badge ${runtimeTone}`}>{copy.liveReplies}: {displayProvider(runtimeProvider, copy)}</span>
          </div>
          <p className="eyebrow">{session.scenario.stakeholderRole}</p>
          <h1>{session.scenario.title}</h1>
          <p>{session.scenario.initialContext}</p>
        </div>
        <div className="session-hero-stats">
          <Metric label={copy.hiddenCoverage} value={`${session.discoveredCount}/${session.hiddenRequirementCount}`} />
          <Metric label={copy.actorsLabel} value={(session.scenario.actors || []).length} />
          <Metric label={copy.duration} value={formatDuration(session.scenario.estimatedMinutes, language)} />
        </div>
      </article>

      <div className="session-layout">
        <article className="glass-card chat-card">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">{copy.aiStakeholder}</p>
              <h2>{session.scenario.stakeholderRole}</h2>
              <p className="panel-copy">{session.scenario.description}</p>
            </div>
            <div className="inline-status-list">
              <StatusBadge
                label={copy.liveReplies}
                value={displayProvider(runtimeProvider, copy)}
                detail={liveReply ? formatMessageTime(liveReply.createdAt, language) : copy.runtimeAwaiting}
                tone={runtimeTone}
              />
              <StatusBadge
                label={copy.topicLabel}
                value={humanizeCategory(liveReply?.category, language)}
                detail={copy.realtimeChatReady}
                tone="neutral"
              />
            </div>
          </div>

          <div className="prompt-strip">
            <span className="detail-label">{copy.promptShortcuts}</span>
            <div className="question-chip-list">
              {(session.scenario.suggestedQuestions || []).map((question) => (
                <button
                  key={question}
                  type="button"
                  className="question-chip-button"
                  onClick={() => onMessageChange(question)}
                  disabled={messagePending}
                >
                  {question}
                </button>
              ))}
            </div>
          </div>

          <div className="chat-log">
            {session.messages.map((item) => (
              <MessageBubble
                key={item.id}
                copy={copy}
                language={language}
                item={item}
                meta={item.runtime}
              />
            ))}
            <div ref={chatEndRef} />
          </div>

          <form className="composer" onSubmit={onSendMessage}>
            <label className="message-field">
              <span>{copy.yourQuestion}</span>
              <textarea
                value={message}
                onChange={(event) => onMessageChange(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    event.currentTarget.form?.requestSubmit();
                  }
                }}
                placeholder={copy.messagePlaceholder}
                disabled={messagePending}
                rows={4}
              />
              <span className="field-hint">{copy.enterHint}</span>
            </label>

            <div className="composer-footer">
              <span className="muted">
                {messagePending ? copy.stakeholderThinking : copy.chatHint}
              </span>
              <button className="primary-button" type="submit" disabled={messagePending}>
                <Icon name={messagePending ? "hourglass_top" : "arrow_upward"} />
                <span>{messagePending ? copy.stakeholderThinking : copy.send}</span>
              </button>
            </div>
          </form>
        </article>

        <aside className="session-sidebar">
          {recentDiscoveries?.length > 0 && (
            <section className="glass-card sidebar-card discovery-highlight">
              <div className="panel-heading">
                <h2>{copy.discoveredNow}</h2>
                <span className="signal-badge accent">{recentDiscoveries.length}</span>
              </div>
              <div className="discovery-stack">
                {recentDiscoveries.map((item) => (
                  <div className="discovery-entry" key={item}>{item}</div>
                ))}
              </div>
            </section>
          )}

          <section className="glass-card sidebar-card">
            <div className="panel-heading">
              <h2>{copy.systemStatus}</h2>
              <span className={`signal-badge ${runtimeTone}`}>{displayProvider(runtimeProvider, copy)}</span>
            </div>
            <p>{displayAiStatusMessage(aiStatus, copy, language)}</p>
            <div className="detail-grid">
              <DetailPair label={copy.aiStatusLabel} value={displayProvider(aiStatus.provider, copy)} />
              <DetailPair label={copy.modelLabel} value={aiStatus.model} />
              <DetailPair label={copy.lastReplyLabel} value={liveReply ? formatMessageTime(liveReply.createdAt, language) : copy.runtimeAwaiting} />
              <DetailPair label={copy.topicLabel} value={humanizeCategory(liveReply?.category, language)} />
            </div>
          </section>

          <section className="glass-card sidebar-card">
            <div className="panel-heading">
              <h2>{copy.scenarioContext}</h2>
              <span className="signal-badge neutral">{session.scenario.domain}</span>
            </div>
            <p>{session.scenario.initialContext}</p>

            <div className="detail-block">
              <span className="detail-label">{copy.actorsLabel}</span>
              <div className="tag-list">
                {(session.scenario.actors || []).map((actor) => (
                  <span className="tag-chip" key={actor}>{actor}</span>
                ))}
              </div>
            </div>

            <div className="detail-block">
              <span className="detail-label">{copy.visibleScope}</span>
              <div className="tag-list">
                {(session.scenario.visibleRequirements || []).slice(0, 5).map((item) => (
                  <span className="tag-chip" key={item}>{item}</span>
                ))}
              </div>
            </div>
          </section>

          <section className="glass-card sidebar-card">
            <div className="panel-heading">
              <h2>{copy.discoveryLedger}</h2>
              <span className="signal-badge neutral">{session.discoveredCount}/{session.hiddenRequirementCount}</span>
            </div>
            {discoveredItems.length === 0 ? (
              <p className="muted">{copy.discoveryEmpty}</p>
            ) : (
              <div className="discovery-stack">
                {discoveredItems.map((item) => (
                  <div className="discovery-entry" key={item}>{item}</div>
                ))}
              </div>
            )}
          </section>

          <section className="glass-card sidebar-card">
            <div className="panel-heading">
              <div>
                <h2>{copy.requirementNotes}</h2>
                <p className="panel-copy">{copy.notesHint}</p>
              </div>
              <button className="ghost-button compact-button" type="button" onClick={onSaveNote}>
                <Icon name="save" />
                <span>{noteState === "saving" ? copy.noteSaving : noteState === "saved" ? copy.noteSaved : copy.save}</span>
              </button>
            </div>
            <textarea
              value={note}
              onChange={(event) => onNoteChange(event.target.value)}
              placeholder={copy.notesPlaceholder}
            />
          </section>

          <section className="glass-card sidebar-card">
            <div className="panel-heading">
              <div>
                <h2>{copy.submissionDraft}</h2>
                <p className="panel-copy">{copy.submissionHint}</p>
              </div>
            </div>
            <form className="submission-form" onSubmit={onSubmitRequirements}>
              <Field label={copy.userStories} name="userStories" value={submission.userStories} onChange={onSubmissionChange} />
              <Field label={copy.useCases} name="useCases" value={submission.useCases} onChange={onSubmissionChange} />
              <Field label={copy.acceptanceCriteria} name="acceptanceCriteria" value={submission.acceptanceCriteria} onChange={onSubmissionChange} />
              <Field label={copy.additionalNotes} name="additionalNotes" value={submission.additionalNotes} onChange={onSubmissionChange} />
              <Field label={copy.reflection} name="reflection" value={submission.reflection} onChange={onSubmissionChange} />
              <button className="primary-button full-width" type="submit" disabled={evaluationPending}>
                <Icon name={evaluationPending ? "hourglass_top" : "task_alt"} />
                <span>{evaluationPending ? copy.evaluatingRequirements : copy.evaluateRequirements}</span>
              </button>
            </form>
          </section>
        </aside>
      </div>
    </section>
  );
}

function exportToMarkdown(session, copy, language) {
  const { scenario, evaluation, submission, messages, note, review } = session;
  
  const chatLog = messages?.map(m => `**${m.sender}**: ${m.content}`).join('\n\n') || "N/A";
  const missing = evaluation?.missingRequirementsJson?.map(g => `- **${g.title}** (${g.category} - ${g.importance}): ${g.guidance}`).join('\n');
  
  const content = `# Req Simulator Portfolio
## ${scenario.title}
**Domain**: ${scenario.domain}
**Stakeholder**: ${scenario.stakeholderRole}

### AI Evaluation
- **Overall Score**: ${evaluation.overallScore}/100
- **Completeness**: ${evaluation.completenessScore}/30
- **Business Rules**: ${evaluation.businessRuleScore}/25
- **Question Quality**: ${evaluation.questionQualityScore}/20
- **Requirement Clarity**: ${evaluation.clarityScore}/15
- **Improvement Awareness**: ${evaluation.improvementAwarenessScore}/10

**Feedback**:
${evaluation.feedbackText}

**Missing Requirements**:
${missing || "None"}

${review ? `### Mentor Review
- **Adjusted Score**: ${review.adjustedScore}/100
- **Comment**: ${review.comment}
` : ""}

---

### Learner Submission
**User Stories**:
${submission?.userStories || "N/A"}

**Use Cases**:
${submission?.useCases || "N/A"}

**Acceptance Criteria**:
${submission?.acceptanceCriteria || "N/A"}

**Reflection**:
${submission?.reflection || "N/A"}

---

### Interview Notes
${note?.content || "No notes taken."}

---

### Chat Log
${chatLog}
`;

  const blob = new Blob([content], { type: "text/markdown;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `portfolio-${session.id}.md`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function Field({ label, name, value, onChange }) {
  return (
    <label>
      {label}
      <textarea
        name={name}
        value={value}
        onChange={(event) => onChange((current) => ({ ...current, [name]: event.target.value }))}
      />
    </label>
  );
}

function FeedbackReport({ copy, language, session, liveReply, discoveryLedger, learnerQuestionCount, onRetry, onRefine }) {
  const evaluation = session.evaluation;
  const circumference = 565;
  const offset = circumference - (evaluation.overallScore / 100) * circumference;
  const discoveredItems = discoveryLedger.length > 0 ? discoveryLedger : [];

  return (
    <section className="page-stack feedback-page">
      <article className="glass-card feedback-hero">
        <div>
          <p className="eyebrow">{copy.historyReport}</p>
          <h1>{copy.feedback}</h1>
          <p>{session.scenario.title}</p>
        </div>
        <div className="feedback-actions">
          <StatusBadge
            label={copy.hiddenCoverage}
            value={`${session.discoveredCount}/${session.hiddenRequirementCount}`}
            detail={copy.discoveryLedger}
            tone="neutral"
          />
          <StatusBadge
            label={copy.liveReplies}
            value={liveReply?.provider ? displayProvider(liveReply.provider, copy) : copy.runtimeAwaiting}
            detail={liveReply ? formatMessageTime(liveReply.createdAt, language) : copy.runtimeAwaiting}
            tone={getProviderTone({ configured: true }, liveReply?.provider || "Mock")}
          />
          <button className="ghost-button" type="button" onClick={() => exportToMarkdown(session, copy, language)}>
            <Icon name="download" />
            <span>{copy.exportPortfolio}</span>
          </button>
          <button className="ghost-button" type="button" onClick={onRefine}>
            <Icon name="edit" />
            <span>{copy.refineRequirements}</span>
          </button>
          <button className="primary-button" type="button" onClick={onRetry}>
            <Icon name="refresh" />
            <span>{copy.retryScenario}</span>
          </button>
        </div>
      </article>

      <div className="feedback-grid">
        <article className="glass-card score-gauge">
          <div className="gauge">
            <svg viewBox="0 0 210 210">
              <circle cx="105" cy="105" r="90" />
              <circle cx="105" cy="105" r="90" style={{ strokeDasharray: circumference, strokeDashoffset: offset }} />
            </svg>
            <div>
              <strong>{evaluation.overallScore}</strong>
              <span>{copy.overallScore}</span>
            </div>
          </div>
          <h2>{evaluation.overallScore >= 80 ? copy.scoreStrong : evaluation.overallScore >= 60 ? copy.scoreGood : copy.scoreNeedsMore}</h2>
          <p>{evaluation.feedbackText}</p>
        </article>

        <article className="glass-card breakdown">
          <div className="panel-heading">
            <h2>{copy.sectionBreakdown}</h2>
            <span className="signal-badge neutral">{copy.metrics.rubric}</span>
          </div>
          <ScoreBar label={copy.completenessLabel} value={evaluation.completenessScore} max={30} />
          <ScoreBar label={copy.businessRulesLabel} value={evaluation.businessRuleScore} max={25} />
          <ScoreBar label={copy.questionQualityLabel} value={evaluation.questionQualityScore} max={20} />
          <ScoreBar label={copy.requirementClarityLabel} value={evaluation.clarityScore} max={15} />
          <ScoreBar label={copy.improvementAwarenessLabel} value={evaluation.improvementAwarenessScore} max={10} />
        </article>

        <article className="glass-card insight-card">
          <div className="panel-heading">
            <h2>{copy.sessionInsights}</h2>
            <Icon name="insights" />
          </div>
          <div className="detail-grid">
            <DetailPair label={copy.yourQuestion} value={learnerQuestionCount} />
            <DetailPair label={copy.hiddenCoverage} value={`${session.discoveredCount}/${session.hiddenRequirementCount}`} />
            <DetailPair label={copy.stakeholder} value={session.scenario.stakeholderRole} />
            <DetailPair label={copy.duration} value={formatDuration(session.scenario.estimatedMinutes, language)} />
          </div>
          <p className="muted">
            {discoveredItems.length > 0 ? discoveredItems.slice(0, 2).join(" ") : copy.nextStepHint}
          </p>
        </article>

        <article className="glass-card missing-card">
          <div className="panel-heading">
            <h2>{copy.missingRequirements}</h2>
            <span className="warning-pill">{evaluation.missingRequirementsJson.length} {copy.gapsLabel}</span>
          </div>
          <div className="gap-list">
            {evaluation.missingRequirementsJson.length === 0 && <p className="muted">{copy.noMajorGaps}</p>}
            {evaluation.missingRequirementsJson.map((gap) => (
              <div className="gap-item" key={gap.title}>
                <strong>{gap.title}</strong>
                <span>{gap.category} - {gap.importance}</span>
                <p>{gap.guidance}</p>
              </div>
            ))}
          </div>
        </article>

        <article className="glass-card coach-card">
          <div className="panel-heading">
            <h2>{copy.feedback}</h2>
            <Icon name="smart_toy" />
          </div>
          <p>{evaluation.feedbackText}</p>
          <div className="coach-action">
            <strong>{copy.nextStep}</strong>
            <span>{copy.nextStepHint}</span>
          </div>
        </article>

        {session.review && (
          <article className="glass-card coach-card mentor-review">
            <div className="panel-heading">
              <h2>{copy.mentorReview}</h2>
              <Icon name="school" />
            </div>
            <div className="detail-pair" style={{ marginBottom: "1rem" }}>
              <span>{copy.adjustedScore}:</span>
              <strong>{session.review.adjustedScore}/100</strong>
            </div>
            <p>{session.review.comment}</p>
          </article>
        )}
      </div>
    </section>
  );
}

function InstructorDashboard({ copy, dashboard, aiStatus, onSubmitReview }) {
  const summary = dashboard?.summary || { totalSessions: 0, evaluatedSessions: 0, averageScore: 0, pendingReviews: 0 };
  const sessions = dashboard?.sessions || [];
  const gaps = dashboard?.commonGaps || [];

  return (
    <main className="instructor-page page-stack">
      <article className="glass-card feedback-hero">
        <div>
          <p className="eyebrow">{copy.mentorWorkspace}</p>
          <h1>{copy.instructorDashboard}</h1>
          <p>{copy.instructorIntro}</p>
        </div>
        <div className="feedback-actions">
          <StatusBadge
            label={copy.aiStatusLabel}
            value={displayProvider(aiStatus.provider, copy)}
            detail={aiStatus.model}
            tone={getProviderTone(aiStatus, aiStatus.provider)}
          />
          <StatusBadge
            label={copy.pendingReviews}
            value={summary.pendingReviews}
            detail={copy.reviewQueue}
            tone="neutral"
          />
        </div>
      </article>

      <div className="summary-grid">
        <Metric label={copy.sessions} value={summary.totalSessions} />
        <Metric label={copy.evaluated} value={summary.evaluatedSessions} />
        <Metric label={copy.avgScore} value={summary.averageScore} />
        <Metric label={copy.pendingReviews} value={summary.pendingReviews} />
      </div>

      <div className="instructor-grid">
        <section className="glass-card dashboard-table">
          <div className="panel-heading">
            <div>
              <h2>{copy.studentSessions}</h2>
              <p className="panel-copy">{copy.reviewQueue}</p>
            </div>
            <span className="signal-badge neutral">{sessions.length} {copy.records}</span>
          </div>
          {sessions.length === 0 && <EmptyState title={copy.noSubmittedSessions} detail={copy.noEvaluatedGaps} />}
          {sessions.map((item) => (
            <article className="session-row" key={item.id}>
              <div className="session-summary">
                <div className="badge-row">
                  <span className="signal-badge neutral">{item.status}</span>
                  {item.evaluation && <span className="signal-badge accent">{item.evaluation.overallScore}/100</span>}
                </div>
                <strong>{item.studentName}</strong>
                <span>{item.scenarioTitle}</span>
                <small>{item.evaluation ? item.evaluation.feedbackText : copy.openStatus}</small>
              </div>
              {item.evaluation && (
                <form className="review-form" onSubmit={(event) => onSubmitReview(event, item.id)}>
                  <label>
                    {copy.adjustedScore}
                    <input name="adjustedScore" type="number" min="0" max="100" defaultValue={item.review?.adjustedScore || item.evaluation.overallScore} />
                  </label>
                  <label>
                    {copy.mentorComment}
                    <textarea name="comment" defaultValue={item.review?.comment || ""} />
                  </label>
                  <button className="primary-button" type="submit">
                    <Icon name="save" />
                    <span>{copy.saveReview}</span>
                  </button>
                </form>
              )}
            </article>
          ))}
        </section>

        <div className="sidebar-stack">
          <aside className="glass-card common-gaps">
            <div className="panel-heading">
              <h2>{copy.commonGaps}</h2>
              <Icon name="rule" />
            </div>
            {gaps.length === 0 && <p className="muted">{copy.noEvaluatedGaps}</p>}
            {gaps.map((gap) => (
              <div className="gap-item" key={gap.title}>
                <strong>{gap.title}</strong>
                <span>{gap.count} {copy.sessionCountSuffix}</span>
              </div>
            ))}
          </aside>

          <aside className="glass-card common-gaps">
            <div className="panel-heading">
              <h2>{copy.reviewQueue}</h2>
              <Icon name="fact_check" />
            </div>
            <p>{copy.instructorQueueHint}</p>
            <div className="detail-grid">
              <DetailPair label={copy.sessions} value={summary.totalSessions} />
              <DetailPair label={copy.pendingReviews} value={summary.pendingReviews} />
              <DetailPair label={copy.evaluated} value={summary.evaluatedSessions} />
              <DetailPair label={copy.avgScore} value={summary.averageScore} />
            </div>
          </aside>
        </div>
      </div>
    </main>
  );
}

function HistoryView({ copy, language, session, liveReply, discoveryLedger, learnerQuestionCount, onRetry, onRefine }) {
  if (!session?.evaluation) {
    return <EmptyState title={copy.completeSimulation} detail={copy.historyHint} />;
  }

  return (
    <FeedbackReport
      copy={copy}
      language={language}
      session={session}
      liveReply={liveReply}
      discoveryLedger={discoveryLedger}
      learnerQuestionCount={learnerQuestionCount}
      onRetry={onRetry}
      onRefine={onRefine}
    />
  );
}

function MessageBubble({ copy, language, item, meta }) {
  const isLearner = item.sender === "Learner";
  const toneClass = isLearner ? "learner" : "stakeholder";
  const providerTone = getProviderTone({ configured: true }, meta?.provider || "Mock");

  return (
    <article className={`message ${toneClass} ${item.pending ? "thinking" : ""}`}>
      <div className="message-topline">
        <strong>{isLearner ? copy.you : copy.aiStakeholder}</strong>
        <div className="message-meta">
          {!isLearner && meta?.provider && <span className={`signal-badge compact ${providerTone}`}>{displayProvider(meta.provider, copy)}</span>}
          {!isLearner && meta?.category && <span className="signal-badge compact neutral">{humanizeCategory(meta.category, language)}</span>}
          <span>{formatMessageTime(item.createdAt, language)}</span>
        </div>
      </div>
      <p>{item.content}</p>
      {item.revealedRequirementIds?.length > 0 && <span className="discovery-pill">{copy.hiddenRequirementDiscovered}</span>}
    </article>
  );
}

function ProcessStep({ icon, title, detail }) {
  return (
    <div className="process-step">
      <Icon name={icon} />
      <div>
        <strong>{title}</strong>
        <span>{detail}</span>
      </div>
    </div>
  );
}

function ScoreBar({ label, value, max }) {
  return (
    <div className="score-bar">
      <div>
        <span>{label}</span>
        <strong>{value}/{max}</strong>
      </div>
      <div className="track">
        <span style={{ width: `${Math.round((value / max) * 100)}%` }} />
      </div>
    </div>
  );
}

function Metric({ label, value }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function DetailPair({ label, value }) {
  return (
    <div className="detail-pair">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function StatusBadge({ label, value, detail, tone = "neutral" }) {
  return (
    <div className={`status-badge ${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </div>
  );
}

function ApiBanner({ copy, message }) {
  return (
    <div className="api-banner">
      {copy.apiBannerPrefix} {API_BASE_URL}: {message}
    </div>
  );
}

function EmptyState({ title, detail }) {
  return (
    <div className="empty-state">
      <strong>{title}</strong>
      {detail && <span>{detail}</span>}
    </div>
  );
}

function Icon({ name }) {
  return <span className="material-symbols-outlined" aria-hidden="true">{name}</span>;
}

function LanguageSwitcher({ copy, language, onLanguageChange }) {
  return (
    <div className="language-switcher" aria-label={copy.language}>
      <button type="button" className={language === "en" ? "active" : ""} onClick={() => onLanguageChange("en")}>EN</button>
      <button type="button" className={language === "vi" ? "active" : ""} onClick={() => onLanguageChange("vi")}>VI</button>
    </div>
  );
}

function buildLearnerSubmission(submission, note, language) {
  const labels = SUBMISSION_SECTION_LABELS[language] || SUBMISSION_SECTION_LABELS.en;

  return [
    `${labels.learnerNotes}:\n${note || ""}`,
    `${labels.userStories}:\n${submission.userStories || ""}`,
    `${labels.useCases}:\n${submission.useCases || ""}`,
    `${labels.acceptanceCriteria}:\n${submission.acceptanceCriteria || ""}`,
    `${labels.additionalNotes}:\n${submission.additionalNotes || ""}`,
    `${labels.reflection}:\n${submission.reflection || ""}`
  ].join("\n\n");
}

function GoogleLogo() {
  return (
    <svg className="oauth-icon google-icon" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#FFC107" d="M43.61 20.08H42V20H24v8h11.3c-1.65 4.66-6.08 8-11.3 8-6.63 0-12-5.37-12-12s5.37-12 12-12c3.06 0 5.84 1.15 7.96 3.04l5.66-5.66C34.05 6.05 29.27 4 24 4 12.95 4 4 12.95 4 24s8.95 20 20 20 20-8.95 20-20c0-1.34-.14-2.65-.39-3.92Z" />
      <path fill="#FF3D00" d="m6.31 14.69 6.57 4.82C14.65 15.11 18.96 12 24 12c3.06 0 5.84 1.15 7.96 3.04l5.66-5.66C34.05 6.05 29.27 4 24 4 16.32 4 9.66 8.34 6.31 14.69Z" />
      <path fill="#4CAF50" d="M24 44c5.17 0 9.86-1.98 13.41-5.19l-6.19-5.24C29.14 35.15 26.62 36 24 36c-5.2 0-9.62-3.32-11.28-7.95l-6.52 5.02C9.51 39.56 16.23 44 24 44Z" />
      <path fill="#1976D2" d="M43.61 20.08H42V20H24v8h11.3a12.04 12.04 0 0 1-4.08 5.57l6.19 5.24C36.97 39.21 44 34 44 24c0-1.34-.14-2.65-.39-3.92Z" />
    </svg>
  );
}

function GitHubLogo() {
  return (
    <svg className="oauth-icon github-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path fill="currentColor" d="M12 .5C5.65.5.5 5.65.5 12c0 5.08 3.29 9.39 7.86 10.91.58.11.79-.25.79-.56v-2.17c-3.2.7-3.88-1.36-3.88-1.36-.52-1.33-1.28-1.68-1.28-1.68-1.04-.71.08-.7.08-.7 1.16.08 1.77 1.19 1.77 1.19 1.03 1.76 2.69 1.25 3.35.96.1-.74.4-1.25.73-1.54-2.55-.29-5.23-1.28-5.23-5.68 0-1.25.45-2.28 1.18-3.08-.12-.29-.51-1.46.11-3.04 0 0 .97-.31 3.16 1.18A10.99 10.99 0 0 1 12 6.04c.98 0 1.96.13 2.88.39 2.19-1.49 3.15-1.18 3.15-1.18.63 1.58.24 2.75.12 3.04.74.8 1.18 1.83 1.18 3.08 0 4.42-2.69 5.39-5.25 5.67.41.36.78 1.06.78 2.14v3.17c0 .31.21.68.8.56A11.51 11.51 0 0 0 23.5 12C23.5 5.65 18.35.5 12 .5Z" />
    </svg>
  );
}

function activeViewLabel(view, copy) {
  const labels = {
    scenarios: copy.chooseScenario,
    session: copy.activeSession,
    feedback: copy.feedback,
    instructor: copy.instructorDashboard
  };
  return labels[view] || copy.studentWorkspaceTitle;
}

function mergeMessageMeta(currentMeta, messages, provider, category) {
  const nextMeta = { ...currentMeta };
  const latestAiMessage = [...messages].reverse().find((item) => item.sender === "AIStakeholder");

  if (latestAiMessage) {
    nextMeta[latestAiMessage.id] = { provider, category };
  }

  return nextMeta;
}

function decorateSessionMessages(session, metaMap) {
  if (!session) return session;

  return {
    ...session,
    messages: (session.messages || []).map((item) => ({
      ...item,
      runtime: metaMap[item.id] || item.runtime || null
    }))
  };
}

function dedupeStrings(values) {
  return values.filter((value, index) => value && values.indexOf(value) === index);
}

function getInitials(name) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase() || "RS";
}

function formatMessageTime(value, language) {
  if (!value) return "--";

  try {
    return new Date(value).toLocaleTimeString(language === "vi" ? "vi-VN" : "en-US", {
      hour: "2-digit",
      minute: "2-digit"
    });
  } catch {
    return "--";
  }
}

function humanizeCategory(category, language) {
  const normalized = (category || "general").toLowerCase();
  const labels = CATEGORY_LABELS[language];
  return labels[normalized] || labels.general;
}

function displayProvider(provider, copy) {
  if (provider === "Unavailable") {
    return copy.unavailable;
  }

  return provider || copy.runtimeAwaiting;
}

function getProviderTone(aiStatus, provider) {
  if (provider === "Gemini") {
    return "is-live";
  }

  if (provider === "Mock" && aiStatus?.configured) {
    return "is-fallback";
  }

  if (provider === "Unavailable") {
    return "is-offline";
  }

  return "neutral";
}

function formatDuration(minutes, language) {
  if (!Number.isFinite(minutes)) {
    return language === "vi" ? "-- phút" : "-- min";
  }

  return language === "vi" ? `${minutes} phút` : `${minutes} min`;
}

function displayAiStatusMessage(aiStatus, copy, language) {
  if (aiStatus?.configured) {
    return copy.aiConfiguredMessage;
  }

  if (aiStatus?.provider === "Mock" && !aiStatus?.loading) {
    return copy.aiMockMessage;
  }

  return localizeText(aiStatus?.safeMessage, language) || copy.loadingShell;
}

function localizeText(value, language) {
  if (language !== "vi" || typeof value !== "string") {
    return value;
  }

  return CONTENT_TRANSLATIONS.vi[value] || value;
}

function localizeTextList(values, language) {
  return (values || []).map((item) => localizeText(item, language));
}

function localizeScenario(scenario, language) {
  if (!scenario) {
    return scenario;
  }

  return {
    ...scenario,
    title: localizeText(scenario.title, language),
    domain: localizeText(scenario.domain, language),
    difficulty: localizeText(scenario.difficulty, language),
    stakeholderRole: localizeText(scenario.stakeholderRole, language),
    stakeholderPersona: localizeText(scenario.stakeholderPersona, language),
    description: localizeText(scenario.description, language),
    initialContext: localizeText(scenario.initialContext, language),
    visibleRequirements: localizeTextList(scenario.visibleRequirements, language),
    actors: localizeTextList(scenario.actors, language),
    suggestedQuestions: localizeTextList(scenario.suggestedQuestions, language),
    evaluationFocus: localizeTextList(scenario.evaluationFocus, language)
  };
}

function localizeEvaluation(evaluation, language) {
  if (!evaluation) {
    return evaluation;
  }

  return {
    ...evaluation,
    missingRequirementsJson: (evaluation.missingRequirementsJson || []).map((gap) => ({
      ...gap,
      title: localizeText(gap.title, language),
      category: localizeGapCategory(gap.category, language),
      importance: localizeImportance(gap.importance, language),
      guidance: localizeText(gap.guidance, language)
    }))
  };
}

function localizeSession(session, language) {
  if (!session) {
    return session;
  }

  return {
    ...session,
    scenario: localizeScenario(session.scenario, language),
    evaluation: localizeEvaluation(session.evaluation, language)
  };
}

function localizeDashboard(dashboard, language) {
  if (!dashboard) {
    return dashboard;
  }

  return {
    ...dashboard,
    sessions: (dashboard.sessions || []).map((item) => ({
      ...item,
      status: localizeSessionStatus(item.status, language),
      scenarioTitle: localizeText(item.scenarioTitle, language),
      evaluation: localizeEvaluation(item.evaluation, language)
    })),
    commonGaps: (dashboard.commonGaps || []).map((gap) => ({
      ...gap,
      title: localizeText(gap.title, language)
    }))
  };
}

function localizeGapCategory(category, language) {
  if (language !== "vi") {
    return category || "";
  }

  return GAP_CATEGORY_TRANSLATIONS.vi[category] || category || "";
}

function localizeImportance(importance, language) {
  if (language !== "vi") {
    return importance || "";
  }

  return IMPORTANCE_TRANSLATIONS.vi[importance] || importance || "";
}

function localizeSessionStatus(status, language) {
  if (language !== "vi") {
    return status || "";
  }

  return SESSION_STATUS_TRANSLATIONS.vi[status] || status || "";
}

const SUBMISSION_SECTION_LABELS = {
  en: {
    learnerNotes: "Learner notes",
    userStories: "User stories",
    useCases: "Use cases",
    acceptanceCriteria: "Acceptance criteria",
    additionalNotes: "Additional notes",
    reflection: "Reflection"
  },
  vi: {
    learnerNotes: "Ghi chú học viên",
    userStories: "User stories",
    useCases: "Ca sử dụng",
    acceptanceCriteria: "Acceptance criteria",
    additionalNotes: "Ghi chú bổ sung",
    reflection: "Tự đánh giá"
  }
};

const SESSION_STATUS_TRANSLATIONS = {
  vi: {
    InProgress: "Đang thực hiện",
    Submitted: "Đã nộp",
    Evaluated: "Đã đánh giá"
  }
};

const GAP_CATEGORY_TRANSLATIONS = {
  vi: {
    AI: "AI",
    BusinessRule: "Quy tắc nghiệp vụ",
    Constraint: "Ràng buộc",
    Exception: "Ngoại lệ",
    NonFunctional: "Phi chức năng"
  }
};

const IMPORTANCE_TRANSLATIONS = {
  vi: {
    High: "Cao",
    Medium: "Trung bình",
    Low: "Thấp"
  }
};

const CONTENT_TRANSLATIONS = {
  vi: {
    "AI provider is configured. API key is hidden.": "AI đã được cấu hình. API key được ẩn an toàn.",
    "No AI API key found. Using mock AI service.": "Không tìm thấy AI API key. Hệ thống đang dùng dịch vụ AI giả lập.",
    "Review this gap and decide whether it belongs in the final requirement set.": "Hãy xem lại khoảng trống này và quyết định xem nó có nên nằm trong bộ yêu cầu cuối cùng hay không.",
    "Unknown scenario": "Tình huống không xác định",
    "E-commerce": "Thương mại điện tử",
    Education: "Giáo dục",
    Healthcare: "Y tế",
    Hospitality: "Nhà hàng và dịch vụ",
    Beginner: "Cơ bản",
    Intermediate: "Trung cấp",
    "E-commerce Order & Promotion System": "Hệ thống đơn hàng và khuyến mãi e-commerce",
    "University Course Registration System": "Hệ thống đăng ký môn học đại học",
    "Clinic Appointment Booking": "Hệ thống đặt lịch khám tại phòng khám",
    "Restaurant Table Booking": "Hệ thống đặt bàn nhà hàng",
    "E-commerce Operations Manager": "Quản lý vận hành e-commerce",
    "Training Department Staff": "Nhân viên phòng đào tạo",
    "Clinic Receptionist": "Lễ tân phòng khám",
    "Restaurant Owner": "Chủ nhà hàng",
    Customer: "Khách hàng",
    "Operations Staff": "Nhân viên vận hành",
    "Payment Gateway": "Cổng thanh toán",
    "Shipping Partner": "Đối tác vận chuyển",
    Student: "Sinh viên",
    Lecturer: "Giảng viên",
    Patient: "Bệnh nhân",
    Receptionist: "Lễ tân",
    Doctor: "Bác sĩ",
    Staff: "Nhân viên",
    Manager: "Quản lý",
    Admin: "Quản trị viên",
    "The online store wants a better checkout and order management flow with stronger promotion rules, payment control, and shipping visibility.": "Cửa hàng trực tuyến muốn cải thiện luồng checkout và quản lý đơn hàng, với quy tắc khuyến mãi chặt chẽ hơn, kiểm soát thanh toán tốt hơn và theo dõi vận chuyển rõ ràng hơn.",
    "Our online store wants to improve the checkout and order management process. Customers sometimes apply invalid vouchers, orders are confirmed even when stock is not available, and staff spend too much time checking payment and shipping status manually.": "Cửa hàng trực tuyến của chúng tôi muốn cải thiện quy trình checkout và quản lý đơn hàng. Khách hàng đôi khi áp dụng voucher không hợp lệ, đơn vẫn được xác nhận dù không còn hàng, và nhân viên tốn quá nhiều thời gian để kiểm tra thủ công trạng thái thanh toán và vận chuyển.",
    "Customers can browse products.": "Khách hàng có thể xem sản phẩm.",
    "Customers can add products to cart.": "Khách hàng có thể thêm sản phẩm vào giỏ hàng.",
    "Customers can place an order.": "Khách hàng có thể đặt hàng.",
    "Staff can view and manage orders.": "Nhân viên có thể xem và quản lý đơn hàng.",
    "Admin can manage products and users.": "Quản trị viên có thể quản lý sản phẩm và người dùng.",
    "Are there any rules for applying vouchers?": "Có quy tắc nào khi áp dụng voucher không?",
    "What happens if payment fails?": "Điều gì xảy ra nếu thanh toán thất bại?",
    "How is stock checked during checkout?": "Tồn kho được kiểm tra như thế nào trong lúc checkout?",
    "Can customers cancel an order?": "Khách hàng có thể hủy đơn hàng không?",
    "How is shipping fee calculated?": "Phí vận chuyển được tính như thế nào?",
    "What reports does admin need?": "Quản trị viên cần những báo cáo nào?",
    "Voucher minimum order value": "Giá trị đơn hàng tối thiểu cho voucher",
    "Voucher can only be used if the minimum order value is reached.": "Voucher chỉ được sử dụng khi đơn hàng đạt giá trị tối thiểu.",
    "Voucher category restrictions": "Giới hạn danh mục áp dụng voucher",
    "Some vouchers are limited to specific product categories.": "Một số voucher chỉ áp dụng cho danh mục sản phẩm nhất định.",
    "Promotion combination rule": "Quy tắc kết hợp khuyến mãi",
    "Voucher cannot be combined with some other promotions.": "Voucher không thể kết hợp với một số chương trình khuyến mãi khác.",
    "Stock check before checkout": "Kiểm tra tồn kho trước khi checkout",
    "Stock must be checked before checkout.": "Tồn kho phải được kiểm tra trước khi checkout.",
    "Stock reservation": "Giữ tồn kho",
    "Stock should be reserved when the order is placed.": "Tồn kho cần được giữ lại khi đơn hàng được tạo.",
    "Order confirmation after payment": "Xác nhận đơn hàng sau thanh toán",
    "Order is confirmed only after successful payment.": "Đơn hàng chỉ được xác nhận sau khi thanh toán thành công.",
    "Payment failure handling": "Xử lý thanh toán thất bại",
    "Payment failure should keep the order in pending or failed status.": "Khi thanh toán thất bại, đơn hàng nên ở trạng thái chờ hoặc thất bại.",
    "Payment timeout releases stock": "Hết thời gian thanh toán thì nhả tồn kho",
    "Payment timeout should release reserved stock.": "Khi hết thời gian thanh toán, tồn kho đã giữ cần được nhả ra.",
    "Shipping fee calculation": "Tính phí vận chuyển",
    "Shipping fee depends on customer location and order weight.": "Phí vận chuyển phụ thuộc vào địa điểm của khách hàng và trọng lượng đơn hàng.",
    "Cancellation before shipping": "Hủy đơn trước khi giao",
    "Customers can cancel an order before it is shipped.": "Khách hàng có thể hủy đơn trước khi đơn được giao đi.",
    "Refund approval": "Phê duyệt hoàn tiền",
    "Refund requests require staff approval.": "Yêu cầu hoàn tiền cần có nhân viên phê duyệt.",
    "Return period": "Thời hạn đổi trả",
    "Return period is limited, for example 7 days after delivery.": "Thời gian đổi trả có giới hạn, ví dụ 7 ngày sau khi giao hàng.",
    "Admin reporting": "Báo cáo quản trị",
    "Admin needs reports about cancelled orders, failed payments, and voucher usage.": "Quản trị viên cần báo cáo về đơn bị hủy, thanh toán thất bại và mức độ sử dụng voucher.",
    "The university wants a system where students can register for courses online and reduce manual registration delays.": "Trường đại học muốn có một hệ thống để sinh viên đăng ký môn học trực tuyến và giảm độ trễ do đăng ký thủ công.",
    "Our university wants to improve the course registration process. Currently, many students register manually or through outdated tools, which causes confusion and delays. We want a better online system.": "Trường đại học của chúng tôi muốn cải thiện quy trình đăng ký môn học. Hiện tại nhiều sinh viên vẫn đăng ký thủ công hoặc qua công cụ cũ, gây nhầm lẫn và chậm trễ. Chúng tôi muốn một hệ thống trực tuyến tốt hơn.",
    "Students can view available courses.": "Sinh viên có thể xem các môn học đang mở.",
    "Students can register for courses.": "Sinh viên có thể đăng ký môn học.",
    "Training department staff can manage course information.": "Nhân viên phòng đào tạo có thể quản lý thông tin môn học.",
    "Admins can manage users.": "Quản trị viên có thể quản lý người dùng.",
    "Who can register for a course?": "Ai có thể đăng ký một môn học?",
    "Are there any prerequisite rules?": "Có quy tắc tiên quyết nào không?",
    "What happens if a class is full?": "Điều gì xảy ra nếu lớp đã đủ chỗ?",
    "How are schedule conflicts handled?": "Xử lý trùng lịch như thế nào?",
    "Can students cancel registration?": "Sinh viên có thể hủy đăng ký không?",
    "What notifications should the system send?": "Hệ thống nên gửi những thông báo nào?",
    "Prerequisite validation": "Kiểm tra điều kiện tiên quyết",
    "Students must complete prerequisite subjects before registration.": "Sinh viên phải hoàn thành các môn tiên quyết trước khi đăng ký.",
    "Course capacity limit": "Giới hạn sức chứa của lớp học",
    "Each course has limited capacity.": "Mỗi môn học có số lượng chỗ giới hạn.",
    "Schedule conflict check": "Kiểm tra trùng lịch học",
    "Students cannot register for courses with schedule conflicts.": "Sinh viên không thể đăng ký các môn bị trùng lịch.",
    "Cancellation deadline": "Hạn chót hủy đăng ký",
    "Students can cancel registration before a deadline.": "Sinh viên có thể hủy đăng ký trước thời hạn quy định.",
    "Staff approval for selected courses": "Phê duyệt của nhân viên cho một số môn học",
    "Some courses require staff approval.": "Một số môn học cần nhân viên phê duyệt.",
    "Admin override": "Quyền ghi đè của quản trị viên",
    "Admins can override registration in special cases.": "Quản trị viên có thể ghi đè đăng ký trong các trường hợp đặc biệt.",
    "Confirmation notifications": "Thông báo xác nhận",
    "The system must send confirmation notifications.": "Hệ thống phải gửi thông báo xác nhận.",
    "Registration history": "Lịch sử đăng ký",
    "Registration history must be stored.": "Lịch sử đăng ký phải được lưu lại.",
    "A local clinic wants patients to book appointments online and help receptionists manage daily schedules.": "Một phòng khám địa phương muốn bệnh nhân đặt lịch trực tuyến và giúp lễ tân quản lý lịch hằng ngày.",
    "We spend a lot of time answering calls and writing appointment details by hand. We need a cleaner way for patients to book visits and for reception staff to manage the schedule.": "Chúng tôi tốn rất nhiều thời gian để nghe điện thoại và ghi tay thông tin lịch hẹn. Chúng tôi cần một cách gọn gàng hơn để bệnh nhân đặt lịch và để lễ tân quản lý lịch làm việc.",
    "Patients can request appointment slots.": "Bệnh nhân có thể yêu cầu khung giờ khám.",
    "Receptionists can confirm or reschedule appointments.": "Lễ tân có thể xác nhận hoặc đổi lịch hẹn.",
    "Doctors can view their daily appointment list.": "Bác sĩ có thể xem danh sách lịch hẹn trong ngày.",
    "Admins can manage clinic users.": "Quản trị viên có thể quản lý người dùng của phòng khám.",
    "Doctor availability": "Lịch trống của bác sĩ",
    "Patients can only book slots when the selected doctor is available.": "Bệnh nhân chỉ có thể đặt khung giờ khi bác sĩ được chọn đang có lịch trống.",
    "Urgent cases": "Ca khẩn cấp",
    "Urgent cases must be routed to the receptionist instead of normal booking.": "Các ca khẩn cấp phải được chuyển cho lễ tân xử lý thay vì đi theo luồng đặt lịch thông thường.",
    "Appointment reminders": "Nhắc lịch hẹn",
    "Patients should receive reminders before their appointment.": "Bệnh nhân nên nhận được nhắc nhở trước lịch hẹn.",
    "A restaurant owner wants customers to reserve tables online and help staff manage capacity during peak hours.": "Chủ nhà hàng muốn khách đặt bàn trực tuyến và hỗ trợ nhân viên quản lý sức chứa trong giờ cao điểm.",
    "Customers call or message us to reserve tables, and sometimes we lose track during busy evenings. We want a simple online booking system.": "Khách hàng gọi điện hoặc nhắn tin để đặt bàn, và đôi khi chúng tôi bị rối trong những buổi tối đông khách. Chúng tôi muốn một hệ thống đặt bàn trực tuyến đơn giản.",
    "Customers can request table reservations.": "Khách hàng có thể gửi yêu cầu đặt bàn.",
    "Staff can approve or reject reservations.": "Nhân viên có thể chấp nhận hoặc từ chối yêu cầu đặt bàn.",
    "Staff can view bookings by date.": "Nhân viên có thể xem các lượt đặt bàn theo ngày.",
    "Admins can manage restaurant settings.": "Quản trị viên có thể quản lý cấu hình nhà hàng.",
    "Party size limit": "Giới hạn số lượng khách trong một nhóm",
    "Large parties require staff confirmation before the reservation is accepted.": "Nhóm khách đông cần nhân viên xác nhận trước khi đơn đặt bàn được chấp nhận.",
    "Deposit for peak hours": "Đặt cọc trong giờ cao điểm",
    "Peak-hour reservations may require a deposit.": "Các lượt đặt bàn trong giờ cao điểm có thể yêu cầu đặt cọc.",
    "Cancellation window": "Khoảng thời gian được phép hủy",
    "Customers can cancel bookings until a configured cutoff time.": "Khách hàng có thể hủy đặt bàn cho đến trước mốc thời gian cắt đã được cấu hình."
  }
};

const CATEGORY_LABELS = {
  en: {
    overview: "Overview",
    voucher: "Voucher",
    stock: "Stock",
    payment: "Payment",
    shipping: "Shipping",
    cancellation: "Cancellation",
    refund: "Refund",
    reporting: "Reporting",
    "out-of-domain": "Domain redirect",
    error: "Unavailable",
    general: "General"
  },
  vi: {
    overview: "Tổng quan",
    voucher: "Voucher",
    stock: "Tồn kho",
    payment: "Thanh toán",
    shipping: "Vận chuyển",
    cancellation: "Hủy đơn",
    refund: "Hoàn tiền",
    reporting: "Báo cáo",
    "out-of-domain": "Chuyển hướng domain",
    error: "Không khả dụng",
    general: "Chung"
  }
};

const UI_COPY = {
  en: {
    tagline: "AI-powered Requirement Analysis Training Platform",
    authEyebrow: "Next-gen requirement analysis practice",
    authHeadline: "Practice stakeholder interviews before the real project starts.",
    authBody: "Run realistic BA-style interviews with AI stakeholders, uncover hidden business rules, and turn each conversation into a scored requirement draft.",
    authWelcome: "Welcome back",
    authContinue: "Continue your requirement gathering practice.",
    authFlowScenario: "Pick a domain scenario and focus the interview like a real BA training exercise.",
    authFlowInterview: "Interview an AI stakeholder that reveals business rules gradually instead of all at once.",
    authFlowFeedback: "Submit your requirement draft and review missing business rules, clarity, and depth.",
    login: "Login",
    register: "Register",
    continueGoogle: "Continue with Google",
    continueGithub: "Continue with GitHub",
    orUseEmail: "or use email",
    email: "Email",
    password: "Password",
    signIn: "Sign in",
    signingIn: "Signing in...",
    studentDemo: "Student Demo",
    instructorDemo: "Instructor Demo",
    createAccount: "Create account",
    registerHint: "Register a learner or instructor account for the MVP demo.",
    defaultLearnerName: "New BA Learner",
    fullName: "Full name",
    role: "Role",
    student: "Student",
    instructor: "Instructor",
    creating: "Creating...",
    chooseScenario: "Choose Scenario",
    startInterview: "Start Interview",
    activeSession: "Active Session",
    performance: "Performance",
    logout: "Logout",
    libraryEyebrow: "Structured simulation library",
    multiDomainNote: "Req Simulator stays domain-flexible. This MVP demo uses an e-commerce scenario to show how hidden requirements surface during a stakeholder interview.",
    demoScenarioNote: "This MVP demo uses an e-commerce scenario.",
    demoScenario: "MVP Demo Scenario",
    demoScenarioFocus: "The current demo spotlights E-commerce Order & Promotion System, while the platform remains ready for education, healthcare, HR, logistics, finance, and public services.",
    stakeholder: "Stakeholder",
    duration: "Duration",
    hiddenRules: "Hidden rules",
    hiddenCoverage: "Hidden coverage",
    quickQuestions: "Quick question prompts",
    availableScenarios: "Available Scenarios",
    ready: "ready",
    hiddenDiscovered: "hidden discovered",
    discoveredNow: "Hidden requirement discovered",
    discoveryLedger: "Discovered requirements",
    discoveryEmpty: "No hidden requirements have been uncovered yet. Ask about rules, exceptions, or edge cases to surface them.",
    aiStakeholder: "AI Stakeholder",
    yourQuestion: "Your Question",
    messagePlaceholder: "Ask about voucher rules, payment failures, stock reservation, shipping, cancellation, returns, or reports",
    send: "Send",
    stakeholderThinking: "Stakeholder is thinking...",
    hiddenRequirementDiscovered: "Hidden requirement discovered",
    requirementNotes: "Requirement Notes",
    notesPlaceholder: "Capture actors, business rules, edge cases, assumptions, and follow-ups.",
    notesHint: "Notes are saved to the active simulation session and can be used in the evaluation prompt.",
    save: "Save",
    noteSaving: "Saving...",
    noteSaved: "Saved",
    submitRequirements: "Submit Requirements",
    submissionDraft: "Requirement Draft",
    submissionHint: "Turn the interview into user stories, use cases, acceptance criteria, and a reflection before evaluation.",
    evaluateRequirements: "Evaluate Requirements",
    evaluatingRequirements: "Evaluating your requirements...",
    userStories: "User stories",
    useCases: "Use cases",
    acceptanceCriteria: "Acceptance criteria",
    additionalNotes: "Additional notes",
    reflection: "Reflection",
    overallScore: "Overall Score",
    missingRequirements: "Missing Requirements",
    feedback: "Feedback",
    historyReport: "History / Simulation Report",
    retryScenario: "Retry Scenario",
    nextStep: "Next Step",
    nextStepHint: "Re-run the interview with more focused questions about rules, exception flows, reporting needs, and actor permissions.",
    loadingScenarios: "Loading scenarios",
    loadingShell: "Refreshing scenario library and AI status...",
    loadingShort: "Loading",
    startFromLibrary: "Start an interview from the scenario library",
    startFromLibraryHint: "Pick a scenario, then launch a fresh session to open the chat workspace, notes, and evaluation flow.",
    completeSimulation: "Complete a simulation to view a feedback report",
    historyHint: "Once a session is evaluated, this area becomes a polished score and gap report for the demo.",
    aiUnavailable: "AI service is temporarily unavailable. Please try again.",
    aiConfiguredMessage: "AI provider is configured. API key is hidden.",
    aiMockMessage: "No AI API key found. Using mock AI service.",
    apiBannerPrefix: "Backend API unavailable at",
    mentorWorkspace: "Mentor review workspace",
    studentWorkspace: "Student simulation workspace",
    studentWorkspaceTitle: "Student Workspace",
    instructorDashboard: "Instructor Dashboard",
    instructorAccess: "Instructor access",
    instructorIntro: "Review submitted sessions, adjust scores, and highlight recurring requirement gaps for your learners.",
    instructorQueueHint: "Use this queue to review AI scoring, add human comments, and keep the classroom demo grounded.",
    studentAccess: "Student access",
    sessions: "Sessions",
    evaluated: "Evaluated",
    avgScore: "Avg score",
    pendingReviews: "Pending reviews",
    studentSessions: "Student Sessions",
    records: "records",
    modeLabel: "Mode",
    language: "Language",
    you: "You",
    sectionBreakdown: "Section Breakdown",
    instructorRoleLabel: "Instructor",
    candidateRoleLabel: "BA Candidate",
    scoreStrong: "Strong Performance",
    scoreGood: "Good Foundation",
    scoreNeedsMore: "Needs More Depth",
    completenessLabel: "Completeness",
    businessRulesLabel: "Business Rules",
    questionQualityLabel: "Question Quality",
    requirementClarityLabel: "Requirement Clarity",
    improvementAwarenessLabel: "Improvement Awareness",
    gapsLabel: "gaps",
    noMajorGaps: "No major missing requirements detected.",
    noSubmittedSessions: "No submitted sessions yet",
    openStatus: "Open",
    adjustedScore: "Adjusted score",
    mentorComment: "Mentor comment",
    saveReview: "Save Review",
    commonGaps: "Common Gaps",
    noEvaluatedGaps: "No evaluated gaps yet.",
    sessionCountSuffix: "session(s)",
    mvpDemo: "MVP Demo",
    aiStatusLabel: "AI Provider",
    liveReplies: "Live replies",
    runtimeAwaiting: "Awaiting first reply",
    realtimeChatReady: "Realtime chat ready",
    unavailable: "Unavailable",
    mockMode: "Mock fallback",
    systemStatus: "System Status",
    modelLabel: "Model",
    lastReplyLabel: "Last reply",
    topicLabel: "Topic",
    scenarioContext: "Scenario Context",
    visibleScope: "Visible scope",
    actorsLabel: "Actors",
    promptShortcuts: "Suggested opening prompts",
    enterHint: "Press Enter to send. Use Shift + Enter for a new line.",
    chatHint: "Keep the interview moving: ask about actors, rules, exception flows, reporting, and non-functional constraints.",
    sessionInsights: "Interview Snapshot",
    navScenarioDetail: "Explore the multi-domain scenario library.",
    navSessionDetail: "Continue the active AI stakeholder interview.",
    navFeedbackDetail: "Review scores, gaps, and coaching feedback.",
    navInstructorDetail: "Open instructor review and monitoring tools.",
    reviewQueue: "Review queue",
    mentorReview: "Mentor's Review",
    refineRequirements: "Refine Requirements",
    exportPortfolio: "Export Portfolio",
    metrics: {
      scenarios: "Scenarios",
      rubric: "Rubric"
    }
  },
  vi: {
    tagline: "Nền tảng luyện tập phân tích yêu cầu bằng AI",
    authEyebrow: "Luyện tập phân tích yêu cầu thế hệ mới",
    authHeadline: "Luyện phỏng vấn stakeholder trước khi bắt đầu dự án thật.",
    authBody: "Mô phỏng buổi khai thác yêu cầu với AI stakeholder, khám phá quy tắc nghiệp vụ ẩn và chuyển hội thoại thành bản nháp yêu cầu được chấm điểm.",
    authWelcome: "Chào mừng trở lại",
    authContinue: "Tiếp tục buổi luyện tập khai thác yêu cầu của bạn.",
    authFlowScenario: "Chọn một tình huống theo lĩnh vực để tập trung buổi phỏng vấn như một bài tập BA thực tế.",
    authFlowInterview: "Phỏng vấn AI stakeholder và khám phá quy tắc nghiệp vụ theo từng lớp thông tin.",
    authFlowFeedback: "Nộp bản nháp yêu cầu để xem thiếu sót về quy tắc nghiệp vụ, độ rõ ràng và chiều sâu nội dung.",
    login: "Đăng nhập",
    register: "Đăng ký",
    continueGoogle: "Tiếp tục với Google",
    continueGithub: "Tiếp tục với GitHub",
    orUseEmail: "hoặc dùng email",
    email: "Email",
    password: "Mật khẩu",
    signIn: "Đăng nhập",
    signingIn: "Đang đăng nhập...",
    studentDemo: "Demo học viên",
    instructorDemo: "Demo giảng viên",
    createAccount: "Tạo tài khoản",
    registerHint: "Đăng ký tài khoản học viên hoặc giảng viên cho bản demo MVP.",
    defaultLearnerName: "Học viên BA mới",
    fullName: "Họ và tên",
    role: "Vai trò",
    student: "Học viên",
    instructor: "Giảng viên",
    creating: "Đang tạo...",
    chooseScenario: "Chọn tình huống",
    startInterview: "Bắt đầu phỏng vấn",
    activeSession: "Phiên hiện tại",
    performance: "Kết quả",
    logout: "Đăng xuất",
    libraryEyebrow: "Thư viện mô phỏng có cấu trúc",
    multiDomainNote: "Req Simulator vẫn giữ tính đa domain. Bản demo MVP này dùng tình huống e-commerce để thể hiện cách yêu cầu ẩn được mở ra trong buổi phỏng vấn stakeholder.",
    demoScenarioNote: "Bản demo MVP này sử dụng tình huống e-commerce.",
    demoScenario: "Tình huống demo MVP",
    demoScenarioFocus: "Bản demo hiện tại tập trung vào Hệ thống đơn hàng và khuyến mãi e-commerce, nhưng nền tảng vẫn sẵn sàng mở rộng sang giáo dục, y tế, nhân sự, logistics, tài chính và dịch vụ công.",
    stakeholder: "Stakeholder",
    duration: "Thời lượng",
    hiddenRules: "Quy tắc ẩn",
    hiddenCoverage: "Độ bao phủ yêu cầu ẩn",
    quickQuestions: "Câu hỏi gợi ý",
    availableScenarios: "Tình huống sẵn sàng",
    ready: "sẵn sàng",
    hiddenDiscovered: "yêu cầu ẩn đã phát hiện",
    discoveredNow: "Đã phát hiện yêu cầu ẩn",
    discoveryLedger: "Yêu cầu đã khám phá",
    discoveryEmpty: "Chưa có yêu cầu ẩn nào được khám phá. Hãy hỏi về quy tắc, ngoại lệ hoặc edge case để mở thêm thông tin.",
    aiStakeholder: "Stakeholder AI",
    yourQuestion: "Câu hỏi của bạn",
    messagePlaceholder: "Hỏi về quy tắc voucher, lỗi thanh toán, giữ tồn kho, phí vận chuyển, hủy đơn, đổi trả hoặc báo cáo",
    send: "Gửi",
    stakeholderThinking: "Stakeholder đang suy nghĩ...",
    hiddenRequirementDiscovered: "Đã phát hiện yêu cầu ẩn",
    requirementNotes: "Ghi chú yêu cầu",
    notesPlaceholder: "Ghi lại tác nhân, quy tắc nghiệp vụ, edge case, assumption và các câu hỏi cần hỏi thêm.",
    notesHint: "Ghi chú được lưu vào phiên mô phỏng hiện tại và sẽ được đưa vào phần đánh giá.",
    save: "Lưu",
    noteSaving: "Đang lưu...",
    noteSaved: "Đã lưu",
    submitRequirements: "Nộp yêu cầu",
    submissionDraft: "Bản nháp yêu cầu",
    submissionHint: "Chuyển buổi phỏng vấn thành câu chuyện người dùng, ca sử dụng, tiêu chí chấp nhận và phần tự đánh giá trước khi chấm điểm.",
    evaluateRequirements: "Đánh giá yêu cầu",
    evaluatingRequirements: "Đang đánh giá yêu cầu...",
    userStories: "Câu chuyện người dùng",
    useCases: "Ca sử dụng",
    acceptanceCriteria: "Tiêu chí chấp nhận",
    additionalNotes: "Ghi chú bổ sung",
    reflection: "Tự đánh giá",
    overallScore: "Điểm tổng quan",
    missingRequirements: "Yêu cầu còn thiếu",
    feedback: "Nhận xét",
    historyReport: "Lịch sử / Báo cáo mô phỏng",
    retryScenario: "Thử lại tình huống",
    nextStep: "Bước tiếp theo",
    nextStepHint: "Hãy chạy lại buổi phỏng vấn với các câu hỏi tập trung hơn vào quy tắc, luồng ngoại lệ, báo cáo và quyền của từng tác nhân.",
    loadingScenarios: "Đang tải tình huống",
    loadingShell: "Đang làm mới thư viện tình huống và trạng thái AI...",
    loadingShort: "Đang tải",
    startFromLibrary: "Hãy bắt đầu một buổi phỏng vấn từ thư viện tình huống",
    startFromLibraryHint: "Chọn một tình huống rồi mở phiên mới để vào không gian chat, ghi chú và luồng đánh giá.",
    completeSimulation: "Hoàn thành một buổi mô phỏng để xem báo cáo đánh giá",
    historyHint: "Sau khi phiên được evaluate, khu vực này sẽ hiển thị báo cáo điểm và khoảng trống yêu cầu theo dạng gọn đẹp cho demo.",
    aiUnavailable: "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại.",
    aiConfiguredMessage: "AI đã được cấu hình. API key được ẩn an toàn.",
    aiMockMessage: "Không tìm thấy AI API key. Hệ thống đang dùng dịch vụ AI giả lập.",
    apiBannerPrefix: "Backend API hiện không khả dụng tại",
    mentorWorkspace: "Không gian review của mentor",
    studentWorkspace: "Không gian mô phỏng của học viên",
    studentWorkspaceTitle: "Không gian học viên",
    instructorDashboard: "Bảng điều khiển giảng viên",
    instructorAccess: "Quyền giảng viên",
    instructorIntro: "Xem các phiên đã nộp, điều chỉnh điểm và làm rõ các khoảng trống yêu cầu lặp lại trong nhóm học viên.",
    instructorQueueHint: "Dùng hàng đợi này để đánh giá điểm AI, thêm nhận xét của người thật và giữ cho buổi demo có cảm giác thực tế.",
    studentAccess: "Quyền học viên",
    sessions: "Phiên",
    evaluated: "Đã đánh giá",
    avgScore: "Điểm TB",
    pendingReviews: "Chờ đánh giá",
    studentSessions: "Phiên của học viên",
    records: "bản ghi",
    modeLabel: "Chế độ",
    language: "Ngôn ngữ",
    you: "Bạn",
    sectionBreakdown: "Chi tiết từng phần",
    instructorRoleLabel: "Giảng viên",
    candidateRoleLabel: "Người học BA",
    scoreStrong: "Thể hiện rất tốt",
    scoreGood: "Nền tảng khá tốt",
    scoreNeedsMore: "Cần đào sâu hơn",
    completenessLabel: "Độ đầy đủ",
    businessRulesLabel: "Quy tắc nghiệp vụ",
    questionQualityLabel: "Chất lượng câu hỏi",
    requirementClarityLabel: "Độ rõ ràng của yêu cầu",
    improvementAwarenessLabel: "Nhận thức cải thiện",
    gapsLabel: "khoảng trống",
    noMajorGaps: "Chưa phát hiện thiếu sót lớn.",
    noSubmittedSessions: "Chưa có phiên nào được nộp.",
    openStatus: "Đang mở",
    adjustedScore: "Điểm điều chỉnh",
    mentorComment: "Nhận xét mentor",
    saveReview: "Lưu đánh giá",
    commonGaps: "Khoảng trống phổ biến",
    noEvaluatedGaps: "Chưa có khoảng trống nào được tổng hợp.",
    sessionCountSuffix: "phiên",
    mvpDemo: "Demo MVP",
    aiStatusLabel: "Nhà cung cấp AI",
    liveReplies: "Phản hồi trực tiếp",
    runtimeAwaiting: "Đang chờ câu trả lời đầu tiên",
    realtimeChatReady: "Chat realtime sẵn sàng",
    unavailable: "Không khả dụng",
    mockMode: "Mock dự phòng",
    systemStatus: "Trạng thái hệ thống",
    modelLabel: "Model",
    lastReplyLabel: "Phản hồi cuối",
    topicLabel: "Chủ đề",
    scenarioContext: "Bối cảnh tình huống",
    visibleScope: "Phạm vi hiển thị",
    actorsLabel: "Tác nhân",
    promptShortcuts: "Câu hỏi mở đầu gợi ý",
    enterHint: "Nhấn Enter để gửi. Dùng Shift + Enter để xuống dòng.",
    chatHint: "Giữ nhịp buổi phỏng vấn: hỏi về tác nhân, quy tắc nghiệp vụ, luồng ngoại lệ, báo cáo và các ràng buộc phi chức năng.",
    sessionInsights: "Tóm tắt buổi phỏng vấn",
    navScenarioDetail: "Khám phá thư viện tình huống đa domain.",
    navSessionDetail: "Tiếp tục buổi phỏng vấn AI stakeholder hiện tại.",
    navFeedbackDetail: "Xem điểm, khoảng trống và nhận xét hướng dẫn.",
    navInstructorDetail: "Mở công cụ đánh giá và theo dõi của giảng viên.",
    reviewQueue: "Hàng đợi đánh giá",
    mentorReview: "Nhận xét của Mentor",
    refineRequirements: "Chỉnh sửa yêu cầu",
    exportPortfolio: "Xuất Portfolio",
    metrics: {
      scenarios: "Tình huống",
      rubric: "Thang điểm"
    }
  }
};

function readStoredUser() {
  try {
    const raw = localStorage.getItem("reqsim.user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function readStoredLanguage() {
  try {
    const value = localStorage.getItem("reqsim_language");
    return value === "vi" ? "vi" : "en";
  } catch {
    return "en";
  }
}

function toResponseLanguage(language) {
  return language === "vi" ? "Vietnamese" : "English";
}
