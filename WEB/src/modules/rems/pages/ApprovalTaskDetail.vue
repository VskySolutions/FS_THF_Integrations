<template>
  <q-page padding>
    <app-detail-header :items="breadcrumbs" :back-to="{ name: 'rems_approvals' }">
      <template #actions>
        <q-badge v-if="task" :color="approvalStatusColor(task.status)" class="q-pa-sm text-body2">
          {{ approvalStatusLabel(task.status) }}
        </q-badge>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <q-banner v-else-if="errorMsg" class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else-if="task">
      <!-- Decision-state banner. -->
      <q-banner v-if="task.status === 'Approved'" dense class="bg-green-1 text-green-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_verified" color="green-9" /></template>
        You approved this task{{ task.decidedOnUtc ? ` on ${fmt.formatDateTime(task.decidedOnUtc)}` : "" }}.
      </q-banner>
      <q-banner v-else-if="task.status === 'Rejected'" class="bg-red-1 text-red-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_cancel" color="red-9" /></template>
        <div class="text-weight-medium">
          You rejected this task{{ task.decidedOnUtc ? ` on ${fmt.formatDateTime(task.decidedOnUtc)}` : "" }}.
        </div>
        <div v-if="task.rejectionReason" class="q-mt-xs" style="white-space: pre-wrap;">
          Reason: {{ task.rejectionReason }}
        </div>
      </q-banner>
      <q-banner v-else-if="!task.canDecide" dense class="bg-grey-2 text-grey-8 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_lock" color="grey-7" /></template>
        This approval round has closed. No further action is required on your task.
      </q-banner>
      <q-banner v-else dense class="bg-teal-1 text-blue-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_fact_check" color="blue-9" /></template>
        Review the details below, complete every checklist item, then approve — or reject with a reason.
      </q-banner>

      <div class="row q-col-gutter-md">
        <div class="col-12 col-md-7">
          <!-- Engagement summary (always shown). -->
          <q-card flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium text-primary">
              Engagement
              <span class="text-grey-6 text-body2">· acting as {{ approverRoleLabel(task.role) }}</span>
            </q-card-section>
            <q-separator />
            <q-card-section>
              <div class="row q-col-gutter-md">
                <div class="col-12 col-sm-6"><div class="rems-label">REMS #</div><div class="rems-value">{{ engagement.remsNumber }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Entity</div><div class="rems-value">{{ engagement.entityName || "—" }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Department</div><div class="rems-value">{{ deptLabel(engagement.department) }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Service Line</div><div class="rems-value">{{ serviceLineLabel(engagement.serviceLine) }}</div></div>
                <div v-if="engagement.marketingMethodIds" class="col-12 col-sm-6">
                  <div class="rems-label">Marketing Methods</div>
                  <div class="rems-value">{{ engagement.marketingMethodIds.length }} selected</div>
                </div>
                <div class="col-12 col-sm-6"><div class="rems-label">Round</div><div class="rems-value">#{{ task.roundNumber }}</div></div>
              </div>
            </q-card-section>
          </q-card>

          <!-- Client (CSE + Director/MS see the full client; Commission sees the basics). -->
          <q-card v-if="client" flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium">Client</q-card-section>
            <q-separator />
            <q-card-section>
              <div class="row q-col-gutter-md">
                <div class="col-12 col-sm-6"><div class="rems-label">Name</div><div class="rems-value">{{ client.name || "—" }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Email</div><div class="rems-value">{{ client.email || "—" }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Mobile</div><div class="rems-value">{{ client.mobileNumber || "—" }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Referral Source</div><div class="rems-value">{{ client.referralSource || "—" }}</div></div>
              </div>
              <template v-if="client.entities && client.entities.length">
                <q-separator class="q-my-md" />
                <div class="rems-label q-mb-xs">Entities</div>
                <q-list dense>
                  <q-item v-for="e in client.entities" :key="e.id" class="q-px-none">
                    <q-item-section avatar><q-icon :name="e.isMainEntity ? 'o_star' : 'o_apartment'" color="grey-7" /></q-item-section>
                    <q-item-section>
                      <q-item-label class="rems-value">{{ e.name }}</q-item-label>
                      <q-item-label caption>{{ e.ein ? `EIN ${e.ein}` : "No EIN" }} · {{ e.isMainEntity ? "Main entity" : "Related entity" }}</q-item-label>
                    </q-item-section>
                  </q-item>
                </q-list>
              </template>
            </q-card-section>
          </q-card>

          <!-- Review data (fee + realization) — Director / Managing Shareholder only. -->
          <q-card v-if="hasReview" flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium">Review</q-card-section>
            <q-separator />
            <q-card-section>
              <div class="row q-col-gutter-md">
                <div class="col-12 col-sm-6">
                  <div class="rems-label">First-Year Fee Estimate</div>
                  <div class="rems-value">{{ engagement.firstYearFeeEstimate != null ? formatMoney(engagement.firstYearFeeEstimate) : "—" }}</div>
                </div>
                <div class="col-12 col-sm-6">
                  <div class="rems-label">Realization</div>
                  <div class="rems-value">{{ engagement.realizationPercentage != null ? `${engagement.realizationPercentage}%` : "—" }}</div>
                </div>
              </div>
            </q-card-section>
          </q-card>

          <!-- Engagement team — present for CSE / Director / MS. -->
          <q-card v-if="hasTeam" flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium">Engagement Team</q-card-section>
            <q-separator />
            <q-card-section>
              <div class="row q-col-gutter-md">
                <div class="col-12 col-sm-6"><div class="rems-label">Department Director</div><div class="rems-value">{{ engagement.departmentDirector?.name || "—" }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Engagement Executive</div><div class="rems-value">{{ engagement.engagementExecutive?.name || "—" }}</div></div>
                <div class="col-12 col-sm-6"><div class="rems-label">Billing Manager</div><div class="rems-value">{{ engagement.billingManager?.name || "—" }}</div></div>
              </div>
            </q-card-section>
          </q-card>

          <!-- Commission splits — Commission Recipient only. -->
          <q-card v-if="engagement.commissionSplits && engagement.commissionSplits.length" flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium">Commission Splits</q-card-section>
            <q-separator />
            <q-list separator>
              <q-item v-for="s in engagement.commissionSplits" :key="s.id">
                <q-item-section avatar><q-icon name="o_payments" color="primary" /></q-item-section>
                <q-item-section><q-item-label class="rems-value">{{ s.employee?.name || "—" }}</q-item-label></q-item-section>
                <q-item-section side><q-badge color="primary">{{ s.percentage }}%</q-badge></q-item-section>
              </q-item>
            </q-list>
          </q-card>
        </div>

        <!-- Checklist + decision. -->
        <div class="col-12 col-md-5">
          <q-card flat bordered class="rems-card">
            <q-card-section class="text-subtitle1 text-weight-medium">
              Your Checklist
              <q-badge :color="allChecklistComplete ? 'positive' : 'grey-6'" class="q-ml-sm">
                {{ completedCount }}/{{ checklist.length }}
              </q-badge>
            </q-card-section>
            <q-separator />
            <q-list separator>
              <q-item v-for="item in checklist" :key="item.id">
                <q-item-section avatar>
                  <q-checkbox
                    :model-value="item.isCompleted"
                    :disable="!task.canDecide || busy || savingItemId === item.id"
                    color="primary"
                    @update:model-value="(val) => toggleItem(item, val)"
                  />
                </q-item-section>
                <q-item-section>
                  <q-item-label :class="item.isCompleted ? 'text-grey-6' : 'rems-value'">{{ item.label }}</q-item-label>
                </q-item-section>
                <q-item-section v-if="savingItemId === item.id" side><q-spinner color="primary" size="18px" /></q-item-section>
              </q-item>
              <q-item v-if="!checklist.length"><q-item-section class="text-grey-6">No checklist items.</q-item-section></q-item>
            </q-list>

            <template v-if="task.canDecide">
              <q-separator />
              <q-card-section>
                <div v-if="!allChecklistComplete" class="text-caption text-grey-7 q-mb-sm">
                  Complete every checklist item to enable Approve.
                </div>
                <div class="row q-col-gutter-sm">
                  <div class="col">
                    <q-btn
                      outline no-caps color="negative" icon="o_cancel" label="Reject" class="full-width"
                      :disable="busy" @click="rejectOpen = true"
                    />
                  </div>
                  <div class="col">
                    <q-btn
                      unelevated no-caps color="primary" icon="o_check_circle" label="Approve" class="full-width"
                      :loading="approving" :disable="approveDisabled" @click="approve"
                    >
                      <q-tooltip v-if="!allChecklistComplete">All checklist items must be completed first</q-tooltip>
                    </q-btn>
                  </div>
                </div>
              </q-card-section>
            </template>
          </q-card>
        </div>
      </div>
    </template>

    <!-- Reject: a required reason (AC-REMS-020.1). -->
    <q-dialog v-model="rejectOpen" persistent>
      <q-card style="min-width: 380px; max-width: 90vw;">
        <q-card-section class="text-subtitle1 text-weight-medium">Reject this approval task</q-card-section>
        <q-separator />
        <q-card-section>
          <div class="text-body2 text-grey-8 q-mb-sm">
            The engagement is returned for rework. A reason is required and shared with the staff/CSE.
          </div>
          <q-form ref="rejectFormRef" greedy>
            <q-input
              v-model="rejectReason" outlined type="textarea" autogrow label="Reason for rejection *"
              hide-bottom-space :rules="[(v) => (!!v && v.trim().length > 0) || 'A reason is required']"
            />
          </q-form>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" :disable="rejecting" @click="cancelReject" />
          <q-btn unelevated no-caps color="negative" label="Reject" :loading="rejecting" @click="submitReject" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
// The role-scoped REMS approval-task detail (WO-117 Part B, AC-REMS-019/020). Renders ONLY the data the
// server returns for the approver's role (CSE full client + team; Director/MS additionally fee + realization;
// Commission the commission splits) — sections the role is not entitled to arrive null and are simply not
// rendered. The per-role checklist gates Approve (disabled until every item is completed; the server
// re-verifies and a 409 is surfaced), and Reject requires a reason. Once decided, the task is read-only.
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import {
  useRemsMeta, REMS_DEPARTMENT_OPTIONS, REMS_SERVICE_LINE_OPTIONS
} from "modules/rems/useRemsMeta";

import AppDetailHeader from "components/common/AppDetailHeader.vue";

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const { approverRoleLabel, approvalStatusLabel, approvalStatusColor } = useRemsMeta();

const taskId = route.params.taskId;

const task = ref(null);
const loading = ref(true);
const errorMsg = ref("");
const busy = ref(false);
const approving = ref(false);
const savingItemId = ref(null);

const engagement = computed(() => task.value?.engagement || {});
const client = computed(() => engagement.value.client);
const checklist = computed(() => task.value?.checklist || []);
const completedCount = computed(() => checklist.value.filter((i) => i.isCompleted).length);
// Approve is enabled only when every checklist item is completed (AC-REMS-019.7).
const allChecklistComplete = computed(() => checklist.value.length > 0 && checklist.value.every((i) => i.isCompleted));
const approveDisabled = computed(() => !task.value?.canDecide || !allChecklistComplete.value || busy.value || !!savingItemId.value);
const hasReview = computed(() => engagement.value.firstYearFeeEstimate != null || engagement.value.realizationPercentage != null);
const hasTeam = computed(() => !!(engagement.value.departmentDirector || engagement.value.engagementExecutive || engagement.value.billingManager));

const breadcrumbs = computed(() => [
  { label: "Home", icon: "o_home", to: "/" },
  { label: "Approvals", to: { name: "rems_approvals" } },
  { label: task.value?.engagement?.remsNumber || "Approval Task" }
]);

// Department / Service Line are stored as closed codes — the static seed lists label them without needing
// optionSets.read (which the approver roles do not carry).
const deptLabel = (v) => REMS_DEPARTMENT_OPTIONS.find((o) => o.value === v)?.label || v || "—";
const serviceLineLabel = (v) => REMS_SERVICE_LINE_OPTIONS.find((o) => o.value === v)?.label || v || "—";
const formatMoney = (v) => new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(Number(v) || 0);

const load = async () => {
  loading.value = true;
  errorMsg.value = "";
  try {
    task.value = await remsApi.approvalTask(taskId);
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
  } finally {
    loading.value = false;
  }
};

// Check / uncheck a checklist item; the server returns the updated item, which we adopt. On failure we
// re-sync from the server so the UI never diverges from the authoritative state.
const toggleItem = async (item, val) => {
  savingItemId.value = item.id;
  try {
    const updated = await remsApi.setChecklistItem(taskId, item.id, val);
    item.isCompleted = updated?.isCompleted ?? val;
    item.completedOnUtc = updated?.completedOnUtc ?? null;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    await load();
  } finally {
    savingItemId.value = null;
  }
};

const approve = async () => {
  const ok = await confirm({
    title: "Approve task",
    message: "Approve this engagement for your role? This decision is final.",
    confirmLabel: "Approve"
  });
  if (!ok) return;
  busy.value = true;
  approving.value = true;
  try {
    task.value = await remsApi.approveTask(taskId);
    notify.success("Task approved.");
  } catch (err) {
    // 409 = the server re-verified and the task is not approvable (checklist incomplete / already decided /
    // round closed). Surface the message and refresh so the UI reflects the true state (AC-REMS-019.8).
    if (err?.response?.status === 409) {
      notify.warning(getApiErrorMessage(err));
      await load();
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    busy.value = false;
    approving.value = false;
  }
};

const rejectOpen = ref(false);
const rejecting = ref(false);
const rejectReason = ref("");
const rejectFormRef = ref(null);

const cancelReject = () => {
  rejectOpen.value = false;
  rejectReason.value = "";
};

const submitReject = async () => {
  if (!(await rejectFormRef.value?.validate())) return;
  busy.value = true;
  rejecting.value = true;
  try {
    task.value = await remsApi.rejectTask(taskId, { reason: rejectReason.value.trim() });
    rejectOpen.value = false;
    rejectReason.value = "";
    notify.success("Task rejected; the engagement was returned for rework.");
  } catch (err) {
    if (err?.response?.status === 409) {
      notify.warning(getApiErrorMessage(err));
      await load();
      rejectOpen.value = false;
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    busy.value = false;
    rejecting.value = false;
  }
};

onMounted(load);
</script>

<style scoped>
.rems-card { border-radius: 12px; }
.rems-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 2px;
}
.rems-value { font-size: 14px; color: #2c3540; word-break: break-word; }
</style>
