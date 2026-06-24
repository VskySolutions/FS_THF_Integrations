import { ref } from "vue";
import { ufColourApi } from "services/api";

// Batch colour-code fetch for a list page: given the visible rows' ids, returns a reactive
// map { entityId: colour } used to render a left-border stripe via AppDataTable row styling.
export function useColourCodes (entityType) {
  const colours = ref({});

  const loadFor = async (entityIds) => {
    const ids = (entityIds || []).filter(Boolean);
    if (!ids.length) {
      colours.value = {};
      return;
    }
    try {
      colours.value = (await ufColourApi.batch(entityType, ids)) || {};
    } catch {
      colours.value = {};
    }
  };

  const colourOf = (entityId) => colours.value[entityId] || null;

  return { colours, loadFor, colourOf };
}
