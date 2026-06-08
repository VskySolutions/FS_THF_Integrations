import { boot } from "quasar/wrappers";
import { http } from "boot/axios";
import { useAuthStore } from "stores/auth";

// Error display is owned by the views (inline banners / useNotify on catch);
// these interceptors only handle authentication concerns.
export default boot(({ store, router }) => {
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

      return Promise.reject(error);
    }
  );
});
