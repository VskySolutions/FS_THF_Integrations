import { boot } from "quasar/wrappers";
import axios from "axios";
import { LocalStorage } from "quasar";

// UUID v4 for per-request correlation tracing (falls back when crypto.randomUUID is unavailable).
function generateCorrelationId () {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// Token based axios instance
const http = axios.create({
  baseURL: process.env.API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
    Accept: "application/json"
  }
});

// Anonymous axios instance
const http2 = axios.create({
  baseURL: process.env.API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
    Accept: "application/json"
  }
});

export default boot(({ app }) => {
  // Request interceptor
  http.interceptors.request.use(
    (config) => {
      const token = LocalStorage.getItem("token");
      const user = LocalStorage.getItem("user");

      // Authorization
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      } else {
        delete config.headers.Authorization;
      }

      // Correlation id for request tracing
      config.headers["X-Correlation-Id"] = generateCorrelationId();

      // Tenant Header. "Site" and "tenant" are the same thing under two names, so a Super Admin's
      // tenant-scope selection simply takes precedence over the user's own site id here — one header, one
      // meaning (TenantResolutionMiddleware honours it for a Super Admin and ignores it for anyone else).
      const siteId = LocalStorage.getItem("adminTenantOverride") || user?.siteId;
      if (siteId) {
        config.headers["X-Site-Id"] = siteId;
      }

      // Tenant Name
      if (user?.siteName) {
        config.headers["X-Site-Name"] = user.siteName;
      }

      // Landing Page
      if (user?.siteLandingPageLink) {
        config.headers["X-Site-LandingPage"] = user.siteLandingPageLink;
      }

      // Timezone Header
      if (user?.siteTimeZone) {
        config.headers["X-Site-Timezone"] = user.siteTimeZone;
      }

      return config;
    },
    (error) => {
      return Promise.reject(error);
    }
  );

  app.config.globalProperties.$axios = axios;
  app.config.globalProperties.$http2 = http2;
  app.config.globalProperties.$http = http;
});

export { axios, http, http2 };
