import { ref } from "vue";
import { ufModifiedLogApi } from "services/api";

// Fetches the Modified Log icon counts for an entity record on detail mount, returning a reactive
// map { fieldName: count }. Each FieldLogIcon consumes getCount(fieldName) to show/hide + badge.
export function useFieldLogCounts (entityType, entityId) {
  const counts = ref({});

  const load = async () => {
    try {
      counts.value = (await ufModifiedLogApi.iconCounts(entityType, entityId)) || {};
    } catch {
      counts.value = {};
    }
  };

  const getCount = (fieldName) => counts.value[fieldName] || 0;

  return { counts, load, getCount };
}
