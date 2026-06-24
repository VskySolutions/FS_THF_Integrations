import { ref } from "vue";
import { ufPinApi } from "services/api";
import { useNotify } from "composables/useNotify";

// Reactive pin state for a single entity record (detail header Pin toggle).
// The list endpoint is the source of truth for "is this record pinned"; we resolve the pin id so
// it can be removed. Best-effort: failures notify but never throw.
export function usePins (entityType, entityId) {
  const notify = useNotify();
  const pinned = ref(false);
  const pinId = ref(null);
  const busy = ref(false);

  const refresh = async () => {
    try {
      // Scan the user's pins for this record (small set — max 50 per user).
      const res = await ufPinApi.list({ page: 1, limit: 50 });
      const match = (res?.data || []).find(
        (p) => Number(p.entityType) === Number(entityType) && p.entityId === entityId
      );
      pinned.value = !!match;
      pinId.value = match?.id || null;
    } catch {
      // ignore — toggle still works
    }
  };

  const toggle = async () => {
    if (busy.value) return;
    busy.value = true;
    try {
      if (pinned.value && pinId.value) {
        await ufPinApi.remove(pinId.value);
        pinned.value = false;
        pinId.value = null;
        notify.info("Removed from pinned.");
      } else {
        const pin = await ufPinApi.create({ entityType, entityId });
        pinned.value = true;
        pinId.value = pin?.id || null;
        notify.success("Pinned.");
      }
    } catch (err) {
      notify.error(err?.response?.data?.error?.details || "Could not update pin.");
    } finally {
      busy.value = false;
    }
  };

  return { pinned, busy, refresh, toggle };
}
