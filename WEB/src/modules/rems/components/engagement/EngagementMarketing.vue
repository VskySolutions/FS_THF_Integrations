<template>
  <div>
    <div class="text-body2 text-grey-8 q-mb-sm">
      Tag how this engagement was won. At least one marketing method is required; saving unlocks the Approval step
      (AC-REMS-017).
    </div>

    <q-banner v-if="marketingUnavailable" dense class="bg-orange-1 text-orange-9 rounded-borders">
      <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
      The marketing method list could not be loaded for your account.
    </q-banner>

    <template v-else>
      <!-- Selected methods as removable chips (AC-REMS-017.3). -->
      <div class="rems-selected q-mb-sm">
        <template v-if="selected.length">
          <q-chip
            v-for="id in selected" :key="id" removable dense color="blue-1" text-color="primary"
            :disable="!editable" @remove="toggle(id)"
          >
            {{ labelOf(id) }}
          </q-chip>
        </template>
        <span v-else class="text-grey-6 text-caption">No marketing methods selected yet.</span>
      </div>

      <app-text-field v-model="search" label="" placeholder="Search marketing methods…" clearable class="q-mb-sm">
        <template #prepend><q-icon name="o_search" /></template>
      </app-text-field>

      <!-- Grouped, searchable options (Global / Geography / Service-Education / Event). -->
      <div v-for="group in filteredGroups" :key="group.key" class="rems-group">
        <div class="rems-group__title">{{ group.label }}</div>
        <div class="row q-gutter-xs">
          <q-chip
            v-for="opt in group.items" :key="opt.value"
            clickable :disable="!editable"
            :color="isSelected(opt.value) ? 'primary' : 'grey-3'"
            :text-color="isSelected(opt.value) ? 'white' : 'grey-9'"
            :icon="isSelected(opt.value) ? 'o_check' : undefined"
            @click="toggle(opt.value)"
          >
            {{ opt.label }}
          </q-chip>
        </div>
      </div>
      <div v-if="!filteredGroups.length" class="text-grey-6 q-pa-sm">No matching marketing methods.</div>

      <!-- Into the card's title row, alongside every other tab's primary save. `defer` because the
           target is rendered by the same tree (see EngagementSetupForm). -->
      <teleport v-if="isVisibleTab && editable" defer to="#engagement-header-actions">
        <q-btn
          unelevated no-caps color="primary" icon-right="o_arrow_forward" label="Save & Next"
          :loading="saving" :disable="selected.length === 0" @click="save"
        >
          <q-tooltip v-if="selected.length === 0">Select at least one marketing method</q-tooltip>
        </q-btn>
      </teleport>
    </template>
  </div>
</template>

<script setup>
// The engagement marketing tags (AC-REMS-017): a searchable, grouped multi-select shown as removable chips.
// Values are OptionSetItem ids. At least one is required to save; a successful save is what makes the
// Approval step reachable (the parent watches the saved marketing count).
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useVisibleTab } from "composables/useVisibleTab";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  // [{ key, label, items:[{ value, label }] }] — value is the OptionSetItem id.
  marketingGroups: { type: Array, default: () => [] },
  marketingUnavailable: { type: Boolean, default: false },
  editable: { type: Boolean, default: true }
});
const emit = defineEmits(["saved", "advance"]);

const notify = useNotify();
const { isVisibleTab } = useVisibleTab();
const selected = ref([...(props.engagement.marketingMethodIds || [])]);
watch(() => props.engagement, (e) => { selected.value = [...(e.marketingMethodIds || [])]; });

const search = ref("");

// Flat id → label lookup for the selected-chip labels.
const labelById = computed(() => {
  const map = {};
  props.marketingGroups.forEach((g) => g.items.forEach((i) => { map[i.value] = i.label; }));
  return map;
});
const labelOf = (id) => labelById.value[id] || id;

const isSelected = (id) => selected.value.includes(id);
const toggle = (id) => {
  if (!props.editable) return;
  selected.value = isSelected(id) ? selected.value.filter((x) => x !== id) : [...selected.value, id];
};

const filteredGroups = computed(() => {
  const needle = (search.value || "").trim().toLowerCase();
  return props.marketingGroups
    .map((g) => ({ ...g, items: needle ? g.items.filter((i) => i.label.toLowerCase().includes(needle)) : g.items }))
    .filter((g) => g.items.length);
});

const saving = ref(false);
const save = async () => {
  if (selected.value.length === 0) return;
  saving.value = true;
  try {
    const view = await remsApi.updateMarketing(props.engagement.id, selected.value);
    emit("saved", view);
    notify.success("Marketing methods saved.");
    emit("advance");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>

<style scoped>
.rems-selected {
  min-height: 34px;
  padding: 6px;
  border: 1px dashed #d4dbe6;
  border-radius: 8px;
}
.rems-group { margin-bottom: 14px; }
.rems-group__title {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 6px;
}
</style>
