<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'EMS Inbox', to: { name: 'rems_ems_inbox' } },
        { label: screen?.remsNumber || 'Request' },
        { label: 'Build EMS' }
      ]"
      :back-to="{ name: 'rems_ems_inbox' }"
    >
      <template #actions>
        <q-badge v-if="screen" :color="statusColor(screen.requestStatus)" class="q-mr-sm">
          {{ statusLabel(screen.requestStatus) }}
        </q-badge>
        <q-badge v-if="form" :color="emsStateColor(form.status)" class="q-mr-sm">
          {{ emsStateLabel(form.status) }}
        </q-badge>
        <q-btn
          v-if="canViewEmailLog && form" flat round dense color="primary" icon="o_history"
          @click="emailLogOpen = true"
        >
          <q-tooltip>Email Log</q-tooltip>
        </q-btn>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <template v-else-if="screen">
      <div class="row q-col-gutter-md">
        <!-- Original request info (editable via the existing request edit — AC-REMS-007.1) -->
        <div class="col-12 col-md-6">
          <q-card flat bordered class="rems-card q-mb-md">
            <q-card-section class="row items-center no-wrap">
              <div class="text-subtitle1 text-weight-medium text-primary col">{{ screen.requestTitle }}</div>
              <q-btn flat round dense color="primary" icon="o_edit" @click="editOpen = true">
                <q-tooltip>Edit request</q-tooltip>
              </q-btn>
            </q-card-section>
            <q-separator />
            <q-card-section>
              <div class="row q-col-gutter-md">
                <div v-for="item in infoRows" :key="item.label" class="col-12 col-sm-6">
                  <div class="rems-label">{{ item.label }}</div>
                  <div class="rems-value">{{ item.value }}</div>
                </div>
              </div>
              <q-banner v-if="!clientEmail" dense class="bg-orange-1 text-orange-9 q-mt-md rounded-borders">
                <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
                This client has no email address. Add one (Edit request) before the form can be sent.
              </q-banner>
            </q-card-section>
          </q-card>
        </div>

        <!-- Build EMS form -->
        <div class="col-12 col-md-6">
          <q-card flat bordered class="rems-card">
            <q-card-section class="row items-center no-wrap">
              <div class="text-subtitle1 text-weight-medium col">EMS Form</div>
              <q-chip v-if="isLocked" dense square color="blue-grey-1" text-color="blue-grey-8" icon="o_lock">
                Locked — sent {{ fmt.formatDateTime(form.sentOnUtc) }}
              </q-chip>
            </q-card-section>
            <q-separator />
            <q-card-section>
              <app-select
                v-model="cseId" :options="cseOptions" label="CSE" required
                :loading="csesLoading" :clearable="false" class="q-mb-md"
                info="Lists users holding the Admin or Super Admin role in this tenant. The CSE becomes an approver on every engagement for this request."
              />
              <app-select
                v-model="industryGroup" :options="industryGroupOptions" label="Industry Group" required
                :clearable="false" :readonly="isLocked" class="q-mb-sm"
                info="From the REMS Industry Group option list (Administration → Option Sets). It decides which questions the client's EMS form asks."
              />
              <div v-if="isLocked" class="text-caption text-grey-7 q-mb-md">
                <q-icon name="o_lock" size="14px" /> The industry group and link are locked once the form has been sent.
              </div>

              <!-- Generated form link (read-only). Minted server-side on save; regenerated if the industry
                   group changes before send (AC-REMS-007.4/5). -->
              <template v-if="form?.formLink">
                <div class="rems-label q-mt-sm">Client form link</div>
                <div class="row items-center no-wrap q-gutter-xs">
                  <div class="rems-value form-link col">{{ clientFormLink }}</div>
                  <q-btn flat round dense icon="o_content_copy" color="primary" @click="copyLink">
                    <q-tooltip>Copy link</q-tooltip>
                  </q-btn>
                </div>
                <div v-if="dirty" class="text-caption text-orange-9 q-mt-xs">
                  Saving will regenerate this link with the current industry group.
                </div>
              </template>
              <div v-else class="text-caption text-grey-7 q-mt-sm">
                Select a CSE and an industry group, then Save to generate the client form link.
              </div>
            </q-card-section>

            <q-separator />
            <q-card-actions align="right">
              <q-btn
                unelevated no-caps color="primary" label="Save" icon="o_save"
                :loading="saving" :disable="!canSave" @click="save"
              >
                <q-tooltip v-if="!canSave">CSE and Industry Group are required to save.</q-tooltip>
              </q-btn>

              <template v-if="alreadySent">
                <q-btn
                  v-if="canViewEmailLog" outline no-caps color="primary" icon="o_history" label="Email Log"
                  @click="emailLogOpen = true"
                />
              </template>
              <q-btn
                v-else
                unelevated no-caps color="teal-7" label="Preview & Send" icon="o_send"
                :disable="!canSend" @click="sendOpen = true"
              >
                <q-tooltip v-if="!canSend">{{ sendHint }}</q-tooltip>
              </q-btn>
            </q-card-actions>
          </q-card>
        </div>
      </div>
    </template>

    <!-- Edit the original Partner request (reuses the create/edit drawer). -->
    <new-request-dialog v-model="editOpen" :request-id="requestId" @saved="onEdited" />
    <!-- Preview → confirm → send; offers the Email Log afterwards. -->
    <send-ems-dialog
      v-model="sendOpen" :rems-id="requestId" :subtitle="sendSubtitle"
      :can-view-email-log="canViewEmailLog" @sent="onSent" @view-log="emailLogOpen = true"
    />
    <email-log-dialog v-model="emailLogOpen" :rems-id="requestId" :subtitle="sendSubtitle" />
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { copyToClipboard } from "quasar";
import { remsApi, getApiErrorMessage, webUrl } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta, useRemsIndustryGroups } from "modules/rems/useRemsMeta";

import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import NewRequestDialog from "modules/rems/components/NewRequestDialog.vue";
import SendEmsDialog from "modules/rems/components/SendEmsDialog.vue";
import EmailLogDialog from "modules/rems/components/EmailLogDialog.vue";

const route = useRoute();
const notify = useNotify();
const fmt = useDateFormat();
const { has } = usePermissions();
const { statusLabel, statusColor, emsStateLabel, emsStateColor } = useRemsMeta();
const { industryGroupOptions, load: loadIndustryGroups } = useRemsIndustryGroups();

const requestId = route.params.id;
const loading = ref(true);
const saving = ref(false);
const screen = ref(null);
const cseId = ref(null);
const industryGroup = ref(null);
const cseOptions = ref([]);
const csesLoading = ref(false);

const editOpen = ref(false);
const sendOpen = ref(false);
const emailLogOpen = ref(false);

const canViewEmailLog = computed(() => has(Permissions.RemsEmailLogRead));

const form = computed(() => screen.value?.form || null);
// The link the client will actually open — always absolute, so copying it out of the browser works
// even when the API's App:BaseUrl is unset (it would otherwise be a bare "/rems/form/{code}").
const clientFormLink = computed(() => webUrl(form.value?.formLink));
const isLocked = computed(() => !!form.value?.isLocked);
const alreadySent = computed(() => !!form.value?.sentOnUtc);
const clientEmail = computed(() => screen.value?.customerEmail || null);

const infoRows = computed(() => {
  const s = screen.value;
  if (!s) return [];
  return [
    { label: "Request ID", value: s.remsNumber },
    { label: "Client", value: s.clientName || "—" },
    { label: "Request Status", value: statusLabel(s.requestStatus) },
    { label: "Customer Email", value: s.customerEmail || "—" },
    { label: "Customer Mobile", value: s.customerMobileNumber || "—" },
    { label: "CSE", value: s.cse?.name || "Not assigned" }
  ];
});

// Unsaved edits: the current CSE / industry-group selection differs from what is persisted.
const dirty = computed(() =>
  cseId.value !== (screen.value?.cse?.id || null) ||
  (industryGroup.value || null) !== (form.value?.industryGroup || null));

// Save requires a CSE and an industry group (AC-REMS-007.7).
const canSave = computed(() => !!cseId.value && !!industryGroup.value && !saving.value);

// "Saved" = a form persisted in Saved/Sent/Submitted with no pending edits.
const isSaved = computed(() =>
  !!form.value && ["Saved", "Sent", "Submitted"].includes(form.value.status) && !dirty.value);

// Send-enabled condition (AC-REMS-007.10 / 008.2): send permission + CSE + Industry Group + saved (no
// pending edits) + a client email on file, and the form has not already been sent.
const canSend = computed(() =>
  has(Permissions.RemsFormsSend) &&
  !!cseId.value &&
  !!industryGroup.value &&
  isSaved.value &&
  !!clientEmail.value &&
  !alreadySent.value);

const sendHint = computed(() => {
  if (!has(Permissions.RemsFormsSend)) return "You do not have permission to send the form.";
  if (!cseId.value || !industryGroup.value) return "Select a CSE and an industry group first.";
  if (!isSaved.value) return "Save the form before sending.";
  if (!clientEmail.value) return "The client has no email address on file.";
  return "";
});

const sendSubtitle = computed(() => (screen.value ? `${screen.value.remsNumber} — ${screen.value.clientName}` : ""));

const loadScreen = async () => {
  loading.value = true;
  try {
    const s = await remsApi.getForm(requestId);
    screen.value = s;
    cseId.value = s?.cse?.id || null;
    industryGroup.value = s?.form?.industryGroup || null;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const loadCses = async () => {
  csesLoading.value = true;
  try {
    const admins = await remsApi.admins();
    cseOptions.value = (admins || []).map((a) => ({ label: a.name, value: a.id }));
  } catch {
    cseOptions.value = [];
  } finally {
    csesLoading.value = false;
  }
};

const save = async () => {
  if (!canSave.value) {
    notify.warning("Select a CSE and an industry group before saving.");
    return;
  }
  saving.value = true;
  try {
    screen.value = await remsApi.saveForm(requestId, { cseUserId: cseId.value, industryGroup: industryGroup.value });
    cseId.value = screen.value?.cse?.id || cseId.value;
    industryGroup.value = screen.value?.form?.industryGroup || industryGroup.value;
    notify.success("EMS form saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

const copyLink = () => {
  if (!clientFormLink.value) return;
  copyToClipboard(clientFormLink.value)
    .then(() => notify.success("Link copied."))
    .catch(() => notify.error("Could not copy the link."));
};

const onEdited = () => { editOpen.value = false; loadScreen(); };
const onSent = () => { loadScreen(); };

onMounted(() => {
  loadScreen();
  loadCses();
  loadIndustryGroups();
});
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
.form-link {
  font-family: monospace;
  background: #f5f7fa;
  border: 1px solid #e0e6ed;
  border-radius: 6px;
  padding: 6px 8px;
}
</style>
