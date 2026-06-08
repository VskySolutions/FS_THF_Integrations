import { boot } from "quasar/wrappers";
import { notifyError } from "assets/utils";
import { getApiErrorMessage } from "services/api";
import { http2, http } from "boot/axios";
import { useAuthStore } from "stores/auth";

export default boot(({ store, router }) => {
  const notify = (error) => {
    notifyError({ timeout: 10000, message: getApiErrorMessage(error) });
  };

  // Anonymous instance (login / refresh) — surface errors, no auth handling.
  http2.interceptors.response.use(
    (response) => response,
    (error) => {
      notify(error);
      return Promise.reject(error);
    }
  );

  // Authenticated instance — 401 triggers a single silent refresh + retry
  // (AC-UI-004.1); if refresh fails, clear the session and go to login (AC-UI-004.2).
  http.interceptors.response.use(
    (response) => response,
    async (error) => {
      const original = error.config;
      const status = error.response?.status;

      if (status === 401 && original && !original._retry) {
        const authStore = useAuthStore(store);
        original._retry = true;

        if (authStore.refreshToken) {
          try {
            const newToken = await authStore.refresh();
            original.headers = original.headers || {};
            original.headers.Authorization = `Bearer ${newToken}`;
            return http(original);
          } catch {
            authStore.clearSession();
            router.push({ name: "login" });
            return Promise.reject(error);
          }
        }

        authStore.clearSession();
        router.push({ name: "login" });
        return Promise.reject(error);
      }

      if (status !== 401) {
        notify(error);
      }
      return Promise.reject(error);
    }
  );
});
