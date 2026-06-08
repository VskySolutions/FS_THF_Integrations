import { boot } from "quasar/wrappers";
import axios from "axios";
import { LocalStorage } from "quasar";

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

      // Tenant Header
      if (user?.siteId) {
        config.headers["X-Site-Id"] = user.siteId;
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
