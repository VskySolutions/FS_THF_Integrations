<template>
  <div>
    <q-inner-loading :showing="loading && !events.length" />
    <div v-if="!loading && !events.length" class="text-grey-6 q-pa-md text-center">No activity yet.</div>

    <q-timeline color="primary">
      <q-timeline-entry
        v-for="e in events"
        :key="e.id"
        :icon="iconFor(e.eventType)"
        :subtitle="formatDateTime(e.occurredOnUtc)"
      >
        <template #title>
          <span class="fs-14">{{ describe(e) }}</span>
        </template>
        <div class="fs-13 text-grey-8">
          <q-icon v-if="!e.actorId" name="o_settings" size="14px" class="q-mr-xs" />
          {{ e.actorName || "System" }}
        </div>
      </q-timeline-entry>
    </q-timeline>

    <div v-if="hasMore" class="row justify-center q-mt-sm">
      <q-btn flat no-caps color="primary" label="Load more" :loading="loading" @click="loadMore" />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { ufActivityApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true }
});

const notify = useNotify();
const { formatDateTime } = useDateFormat();

const events = ref([]);
const loading = ref(false);
const page = ref(1);
const total = ref(0);
const limit = 20;
const hasMore = ref(false);

const ICONS = {
  StatusChanged: "o_swap_horiz",
  FieldEdited: "o_edit",
  NoteAdded: "o_chat",
  TagApplied: "o_label",
  TagRemoved: "o_label_off",
  AttachmentUploaded: "o_attach_file",
  AttachmentDeleted: "o_delete",
  ChecklistItemCompleted: "o_check_circle",
  SyncCompleted: "o_cloud_done",
  SyncFailed: "o_error",
  Restored: "o_restore"
};
const iconFor = (type) => ICONS[type] || "o_circle";

const LABELS = {
  StatusChanged: "Status changed",
  FieldEdited: "Field edited",
  NoteAdded: "Note added",
  TagApplied: "Tag applied",
  TagRemoved: "Tag removed",
  AttachmentUploaded: "Attachment uploaded",
  AttachmentDeleted: "Attachment deleted",
  ChecklistItemCompleted: "Checklist item completed",
  SyncCompleted: "Sync completed",
  SyncFailed: "Sync failed",
  Restored: "Record restored"
};

const describe = (e) => {
  const base = LABELS[e.eventType] || e.eventType;
  if (e.oldValue && e.newValue) return `${base}: ${e.oldValue} → ${e.newValue}`;
  if (e.newValue) return `${base}: ${e.newValue}`;
  return base;
};

const load = async (reset = true) => {
  loading.value = true;
  try {
    const res = await ufActivityApi.list({
      entityType: props.entityType,
      entityId: props.entityId,
      page: page.value,
      limit
    });
    const data = res?.data || [];
    events.value = reset ? data : [...events.value, ...data];
    total.value = res?.meta?.totalRecords || events.value.length;
    hasMore.value = events.value.length < total.value;
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

onMounted(() => load());
defineExpose({ load: () => { page.value = 1; return load(); } });
</script>
