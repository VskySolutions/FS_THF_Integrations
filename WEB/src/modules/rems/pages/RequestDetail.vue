<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'REMS', to: { name: 'rems_partner' } },
        { label: detail?.remsNumber || 'Request' }
      ]"
      :back-to="{ name: 'rems_partner' }"
    >
      <template #actions>
        <q-badge v-if="detail" :color="statusColor(detail.status)" class="q-mr-sm">{{ statusLabel(detail.status) }}</q-badge>
        <q-btn v-if="detail?.actions?.canEdit && detail?.status === 'draft'" flat round dense color="primary" icon="o_edit" @click="openEdit">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn
          v-if="canBuildEms" flat round dense color="primary" icon="o_dynamic_form"
          :to="{ name: 'rems_build_ems', params: { id: requestId } }"
        >
          <q-tooltip>Build EMS Form</q-tooltip>
        </q-btn>
        <q-btn
          v-if="detail?.actions?.canAssign && !customerSubmitted && has(Permissions.RemsPoolRead)"
          flat round dense color="primary" icon="o_person_add" @click="openAssign"
        >
          <q-tooltip>{{ detail.assignedAdmin ? "Reassign Admin" : "Assign / Pick Up" }}</q-tooltip>
        </q-btn>
        <q-btn v-if="detail?.actions?.canDuplicate" flat round dense color="primary" icon="o_content_copy" @click="duplicate">
          <q-tooltip>Duplicate</q-tooltip>
        </q-btn>
        <q-btn v-if="detail?.actions?.canDelete" flat round dense color="negative" icon="o_delete" @click="removeRequest">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <template v-else-if="detail">
      <div class="row q-col-gutter-md">
        <!-- Request details -->
        <div class="col-12 col-md-7">
          <q-card flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium text-primary">{{ detail.title }}</q-card-section>
            <q-separator />
            <q-card-section>
              <div class="row q-col-gutter-md">
                <div v-for="item in infoRows" :key="item.label" class="col-12 col-sm-6">
                  <div class="rems-label">{{ item.label }}</div>
                  <div v-if="item.type === 'priority'"><q-badge :color="priorityColor(detail.priority)">{{ priorityLabel(detail.priority) }}</q-badge></div>
                  <div v-else class="rems-value">{{ item.value }}</div>
                </div>
              </div>
              <template v-if="detail.description">
                <q-separator class="q-my-md" />
                <div class="rems-label">Description</div>
                <!-- Rich text from the request editor. renderRichText sanitizes it and still handles
                     descriptions written before the field became rich (plain text keeps its breaks). -->
                <!-- eslint-disable-next-line vue/no-v-html -->
                <div class="rems-value rems-value--rich" v-html="renderRichText(detail.description)" />
              </template>
            </q-card-section>
          </q-card>

          <!-- Attachments -->
          <q-card flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium">
              Attachments
              <q-badge color="primary" class="q-ml-sm">{{ (detail.files || []).length }}</q-badge>
            </q-card-section>
            <q-separator />
            <q-list separator>
              <q-item v-for="file in detail.files" :key="file.id" clickable tag="a" :href="fileUrl(file)" target="_blank">
                <q-item-section avatar><q-icon name="o_description" color="grey-7" /></q-item-section>
                <q-item-section>
                  <q-item-label>{{ file.fileName || "Attachment" }}</q-item-label>
                  <q-item-label caption>{{ formatSize(file.fileSize) }}</q-item-label>
                </q-item-section>
                <q-item-section side><q-icon name="o_open_in_new" color="grey-6" /></q-item-section>
              </q-item>
              <q-item v-if="!detail.files || !detail.files.length">
                <q-item-section class="text-grey-6">No attachments.</q-item-section>
              </q-item>
            </q-list>
          </q-card>
        </div>

        <!-- Conversation -->
        <div class="col-12 col-md-5">
          <q-card flat bordered class="rems-card">
            <q-card-section class="text-subtitle1 text-weight-medium">Conversation</q-card-section>
            <q-separator />
            <q-card-section>
              <!-- Reuses the Universal Features Notes thread keyed on the REMS request (author + timestamp). -->
              <entity-notes-panel :entity-type="EntityType.Rems" :entity-id="requestId" />
            </q-card-section>
          </q-card>
        </div>
      </div>
    </template>

    <new-request-dialog v-model="formOpen" :request-id="requestId" @saved="onSaved" />
    <assign-admin-dialog
      v-model="assignOpen" :request-id="requestId" :current-admin-id="detail?.assignedAdmin?.id"
      :mode="detail?.assignedAdmin ? 'assign' : 'pickup'" @assigned="onAssigned"
    />
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { remsApi, mediaApi, EntityType, getApiErrorMessage } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";
import { renderRichText } from "utils/richText";

import AppDetailHeader from "components/common/AppDetailHeader.vue";
import NewRequestDialog from "modules/rems/components/NewRequestDialog.vue";
import AssignAdminDialog from "modules/rems/components/AssignAdminDialog.vue";

const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
const fmt = useDateFormat();
const {
  typeLabel, priorityLabel, statusLabel, priorityColor, statusColor, emsStateLabel, submissionStateLabel
} = useRemsMeta();

const requestId = route.params.id;
const loading = ref(true);
const detail = ref(null);

// Once the customer has submitted their EMS form the request is past the build-and-assign stage — the
// form is done and the work moves to the engagement workspace — so both actions are withdrawn.
const customerSubmitted = computed(() => detail.value?.status === "customer_submitted");
// The Build EMS workspace is Admin-only (rems.forms.manage); Partners see the request but not this entry.
const canBuildEms = computed(() => has(Permissions.RemsFormsManage) && !customerSubmitted.value);

const infoRows = computed(() => {
  const d = detail.value;
  if (!d) return [];
  return [
    { label: "Request ID", value: d.remsNumber },
    { label: "Client", value: d.clientName || "—" },
    { label: "Type", value: typeLabel(d.type) },
    { label: "Priority", type: "priority" },
    { label: "Status", value: statusLabel(d.status) },
    { label: "Customer Email", value: d.customerEmail || "—" },
    { label: "Customer Mobile", value: d.customerMobileNumber || "—" },
    { label: "Assigned Admin", value: d.assignedAdmin?.name || "Unassigned" },
    { label: "CSE", value: d.cse?.name || "—" },
    { label: "Industry Group", value: d.industryGroup || "—" },
    { label: "EMS Form State", value: emsStateLabel(d.emsFormState) },
    { label: "Client Submission", value: submissionStateLabel(d.clientSubmissionState) },
    { label: "Created", value: `${d.createdBy || "—"} · ${fmt.formatDateTime(d.createdOnUtc)}` },
    { label: "Updated", value: `${d.updatedBy || "—"} · ${fmt.formatDateTime(d.updatedOnUtc)}` }
  ];
});

const fileUrl = (file) => mediaApi.absoluteUrl(file.url);
const formatSize = (bytes) => {
  if (!bytes && bytes !== 0) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const load = async () => {
  loading.value = true;
  try {
    detail.value = await remsApi.get(requestId);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

// ---- Edit ----
const formOpen = ref(false);
const openEdit = () => { formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

// ---- Assign ----
const assignOpen = ref(false);
const openAssign = () => { assignOpen.value = true; };
const onAssigned = () => { assignOpen.value = false; load(); };

// ---- Duplicate ----
const duplicate = async () => {
  const ok = await confirm({
    title: "Duplicate request",
    message: `Create a new draft copy of ${detail.value.remsNumber}?`,
    confirmLabel: "Duplicate"
  });
  if (!ok) return;
  try {
    const copy = await remsApi.duplicate(requestId);
    notify.success("Request duplicated as a new draft.");
    if (copy?.id) router.push({ name: "rems_request_detail", params: { id: copy.id } });
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Delete ----
const removeRequest = async () => {
  const ok = await confirm({
    title: "Delete REMS request",
    message: `Delete ${detail.value.remsNumber} for "${detail.value.clientName}"? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await remsApi.remove(requestId);
    notify.success("REMS request deleted.");
    router.push({ name: "rems_partner" });
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(load);
</script>

<style scoped>
.rems-card {
  border-radius: 12px;
}
.rems-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 2px;
}
.rems-value {
  font-size: 14px;
  color: #2c3540;
  word-break: break-word;
}
/* Rich-text description: keep the editor's paragraphs and lists readable inside the detail card. */
.rems-value--rich :deep(p) {
  margin: 0 0 0.5em;
}
.rems-value--rich :deep(p:last-child) {
  margin-bottom: 0;
}
.rems-value--rich :deep(ul),
.rems-value--rich :deep(ol) {
  margin: 0 0 0.5em;
  padding-left: 1.25rem;
}
</style>
