const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:5088/api";

export async function api(path, options = {}) {
  const headers = {
    "Content-Type": "application/json",
    ...buildUserContextHeaders(),
    ...(options.headers || {})
  };

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method || "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined
  });

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.message || "Request failed");
  }

  return data;
}

function buildUserContextHeaders() {
  if (typeof localStorage === "undefined") {
    return {};
  }

  try {
    const rawUser = localStorage.getItem("reqsim.user");
    if (!rawUser) {
      return {};
    }

    const user = JSON.parse(rawUser);
    return user?.id ? { "X-ReqSim-UserId": String(user.id) } : {};
  } catch {
    return {};
  }
}

export { API_BASE_URL };
