<template>
  <app-view-drawer v-model="open" :title="`Change History — ${fieldLabel}`" :width="560">
    <q-input
      v-model="search"
      dense
      outlined
      clearable
      placeholder="Search history"
      debounce="200"
      class="q-mb-sm"
    >
      <template #prepend><q-icon name="o_search" /></template>
    </q-input>

    <q-inner-loading :showing="loading && !entries.length" />
    <div v-if="!loading && !entries.length" class="text-grey-6 q-pa-md text-center">
      No changes recorded yet for this field.
    </div>

    <q-markup-table v-if="filtered.length" flat dense bordered separator="horizontal">
      <thead>
        <tr>
          <th class="text-left">Changed On</th>
          <th class="text-left">Changed By</th>
          <th class="text-left">From</th>
          <th class="text-left">To</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="e in filtered" :key="e.id">
          <td>{{ formatDateTime(e.changedOnUtc) }}</td>
          <td>
            <q-icon v-if="e.changedByName === 'System'" name="o_settings" size="14px" class="q-mr-xs" />
            {{ e.changedByName }}
          </td>
          <td>{{ e.oldValue ?? "—" }}</td>
          <td>{{ e.newValue ?? "—" }}</td>
        </tr>
      </tbody>
    </q-markup-table>

    <div v-if="hasMore" class="row justify-center q-mt-sm">
      <q-btn flat no-caps color="primary" label="Load more" :loading="loading" @click="loadMore" />
    </div>
  </app-view-drawer>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { ufModifiedLogApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import AppViewDrawer from "components/common/AppViewDrawer.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true },
  fieldName: { type: String, required: true },
  fieldLabel: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue"]);

const notify = useNotify();
const { formatDateTime } = useDateFormat();

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

const entries = ref([]);
const loading = ref(false);
const page = ref(1);
const total = ref(0);
const limit = 50;
const hasMore = ref(false);
const search = ref("");

const filtered = computed(() => {
  const q = (search.value || "").toLowerCase().trim();
  if (!q) return entries.value;
  return entries.value.filter((e) =>
    [e.changedByName, e.oldValue, e.newValue, formatDateTime(e.changedOnUtc)]
      .some((v) => String(v || "").toLowerCase().includes(q))
  );
});

const load = async (reset = true) => {
  loading.value = true;
  try {
    const res = await ufModifiedLogApi.history({
      entityType: props.entityType,
      entityId: props.entityId,
      fieldName: props.fieldName,
      page: page.value,
      limit
    });
    const data = res?.data || [];
    entries.value = reset ? data : [...entries.value, ...data];
    total.value = res?.meta?.totalRecords || entries.value.length;
    hasMore.value = entries.value.length < total.value;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const loadMore = () => {
  page.value += 1;
  load(false);
};

// Load when opened.
watch(open, (isOpen) => {
  if (isOpen) {
    page.value = 1;
    search.value = "";
    load();
  }
});
</script>
