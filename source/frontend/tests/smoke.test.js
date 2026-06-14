const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");

const root = path.resolve(__dirname, "..");

test("static website files exist", () => {
  assert.ok(fs.existsSync(path.join(root, "index.html")));
  assert.ok(fs.existsSync(path.join(root, "src", "main.jsx")));
  assert.ok(fs.existsSync(path.join(root, "src", "App.jsx")));
  assert.ok(fs.existsSync(path.join(root, "src", "styles.css")));
  assert.ok(fs.existsSync(path.join(root, "src", "api", "client.js")));
});

test("prototype includes core MVP concepts", () => {
  const app = fs.readFileSync(path.join(root, "src", "App.jsx"), "utf8");

  assert.match(app, /AuthPage/);
  assert.match(app, /external-demo/);
  assert.match(app, /ScenarioLibrary/);
  assert.match(app, /SimulationWorkspace/);
  assert.match(app, /FeedbackReport/);
  assert.match(app, /Instructor/);
});

test("polished demo shell surfaces exist in the frontend", () => {
  const app = fs.readFileSync(path.join(root, "src", "App.jsx"), "utf8");

  assert.match(app, /AppNavbar/);
  assert.match(app, /AI Provider/);
  assert.match(app, /Live replies/);
  assert.match(app, /Discovered requirements/);
  assert.match(app, /Scenario Context/);
  assert.match(app, /Instructor Dashboard/);
});

test("english and vietnamese UI copy include core demo labels", () => {
  const app = fs.readFileSync(path.join(root, "src", "App.jsx"), "utf8");

  assert.match(app, /AI-powered Requirement Analysis Training Platform/);
  assert.match(app, /Req Simulator stays domain-flexible\. This MVP demo uses an e-commerce scenario to show how hidden requirements surface during a stakeholder interview\./);
  assert.match(app, /Nền tảng luyện tập phân tích yêu cầu bằng AI/);
  assert.match(app, /Req Simulator vẫn giữ tính đa domain\./);
  assert.match(app, /Hệ thống đơn hàng và khuyến mãi e-commerce/);
});

test("dark ui stylesheet includes glassmorphism shell tokens", () => {
  const css = fs.readFileSync(path.join(root, "src", "styles.css"), "utf8");

  assert.match(css, /--page:\s*#050608/i);
  assert.match(css, /backdrop-filter:\s*blur\(18px\)/i);
  assert.match(css, /\.app-navbar/);
  assert.match(css, /\.workspace-body/);
  assert.match(css, /\.session-layout/);
  assert.match(css, /\.status-badge/);
});

test("public entry references app assets", () => {
  const html = fs.readFileSync(path.join(root, "index.html"), "utf8");

  assert.match(html, /\/src\/main.jsx/);
  assert.match(html, /Req Simulator/);
});

test("api client sends demo user context header", () => {
  const client = fs.readFileSync(path.join(root, "src", "api", "client.js"), "utf8");

  assert.match(client, /X-ReqSim-UserId/);
  assert.match(client, /localStorage\.getItem\("reqsim\.user"\)/);
});

test("asp.net backend project exists", () => {
  const backendRoot = path.resolve(root, "..", "backend");
  assert.ok(fs.existsSync(path.join(backendRoot, "src", "ReqSimulator.Api", "ReqSimulator.Api.csproj")));
  assert.ok(fs.existsSync(path.join(backendRoot, "src", "ReqSimulator.Api", "Program.cs")));
});
