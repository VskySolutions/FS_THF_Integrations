import { onMounted, onBeforeUnmount } from "vue";
import { Dialog } from "quasar";
import { useAuthStore } from "stores/auth";

// SessionExpiryTimer (WO-50, AC-UI-004.3): warns the user shortly before the
// refresh-token window closes and offers to extend; auto-logs-out at expiry.
const WARNING_MS = 5 * 60 * 1000; // 5 minutes
const CHECK_INTERVAL_MS = 30 * 1000; // poll every 30s

export function useSessionExpiry () {
  const authStore = useAuthStore();
  let timer = null;
  let warned = false;
  let dialog = null;

  const check = () => {
    if (!authStore.isAuthenticated || !authStore.sessionExpiresAt) {
      return;
    }
    const msLeft = new Date(authStore.sessionExpiresAt).getTime() - Date.now();

    if (msLeft <= 0) {
      stop();
      authStore.logout();
      return;
    }

    if (msLeft <= WARNING_MS && !warned) {
      warned = true;
      const minutes = Math.max(1, Math.ceil(msLeft / 60000));
      dialog = Dialog.create({
        title: "Session expiring",
        message: `Your session will expire in about ${minutes} minute(s). Extend your session?`,
        ok: { label: "Extend", color: "primary", unelevated: true, noCaps: true },
        cancel: { label: "Logout", flat: true, noCaps: true },
        persistent: true
      })
        .onOk(async () => {
          try {
            await authStore.refresh();
            warned = false;
          } catch {
            authStore.logout();
          }
        })
        .onCancel(() => {
          authStore.logout();
        });
    }
  };

  const start = () => {
    stop();
    timer = setInterval(check, CHECK_INTERVAL_MS);
  };

  const stop = () => {
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
    if (dialog) {
      dialog.hide();
      dialog = null;
    }
  };

  onMounted(start);
  onBeforeUnmount(stop);

  return { start, stop };
}
