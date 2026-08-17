<template>
  <div>
    <div class="row items-center q-mb-md">
      <div class="text-body2 text-grey-8 col">
        Who this engagement routes to (AC-REMS-018): the managing shareholder, the Department Director and
        CSE from the setup, and every commission recipient — all four automatically — plus any approvers
        you add below. Sending for approval locks the list.
      </div>
      <q-badge :color="statusMeta.color" class="q-pa-sm text-body2">{{ statusMeta.label }}</q-badge>
    </div>

    <!-- Post-send / decided states. -->
    <q-banner v-if="status === 'PendingApproval'" dense class="bg-orange-1 text-orange-9 rounded-borders q-mb-md">
      <template #avatar><q-icon name="o_hourglass_top" color="orange-9" /></template>
      Sent for approval — awaiting decisions. The approver list is locked.
    </q-banner>
    <q-banner v-else-if="status === 'Approved'" dense class="bg-green-1 text-green-9 rounded-borders q-mb-md">
      <template #avatar><q-icon name="o_verified" color="green-9" /></template>
      This engagement is fully approved.
    </q-banner>
    <q-banner v-else-if="status === 'Rejected'" class="bg-red-1 text-red-9 rounded-borders q-mb-md">
      <template #avatar><q-icon name="o_cancel" color="red-9" /></template>
      <div class="text-weight-medium">This engagement was rejected and returned for rework.</div>
      <div v-if="rejectionReason" class="q-mt-xs" style="white-space: pre-wrap;">Reason: {{ rejectionReason }}</div>
      <div class="q-mt-xs">Update the setup as needed, then resubmit for a fresh approval round.</div>
    </q-banner>

    <div v-if="loading" class="row flex-center q-pa-lg"><q-spinner color="primary" size="28px" /></div>

    <q-banner v-else-if="errorMsg" dense class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else>
      <!-- Extra approvers only: the four automatic ones already route and are shown in the list below.
           Selecting saves immediately and the person appears in the list — then Send. -->
      <div v-if="canPick" class="q-mb-md">
        <app-select
          v-model="picked" :options="approverOptions" label="Add approvers" multiple use-input
          :loading="loadingOptions || savingPicks"
          info="Lists users holding the Approver role in this tenant, shown as Full Name — Job Title. They are ADDED to the four below — the managing shareholder, Department Director, CSE and commission recipients — who approve regardless."
          @update:model-value="savePicks"
        />
      </div>

      <q-list v-if="approvers.length" bordered separator class="rounded-borders">
        <q-item v-for="(a, i) in approvers" :key="i">
          <q-item-section avatar>
            <q-icon :name="roleIcon(a.role)" color="primary" />
          </q-item-section>
          <q-item-section>
            <q-item-label class="text-weight-medium">{{ a.user.name || "Unassigned" }}</q-item-label>
            <q-item-label caption>{{ roleLabel(a.role) }}</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
      <div v-else class="text-grey-6 q-pa-sm">
        No approvers yet. The four automatic ones come from the engagement itself — name a CSE, pick a
        department with a director, add commission recipients, or put somebody in the "Managing Shareholder"
        user group. You can also add approvers above.
      </div>

      <div v-if="canShowSend" class="row justify-end q-mt-md">
        <q-btn
          unelevated no-caps color="primary" icon="o_send" label="Send for Approval"
          :loading="sending" :disable="approvers.length === 0" @click="send"
        />
      </div>

      <!-- Staff resubmission (AC-REMS-020.3): after a rejection, re-route the (regenerated) approver list. -->
      <div v-if="canShowResubmit" class="row justify-end q-mt-md">
        <q-btn
          unelevated no-caps color="primary" icon="o_restart_alt" label="Resubmit for Approval"
          :loading="resubmitting" :disable="approvers.length === 0" @click="resubmit"
        />
      </div>
    </template>
  </div>
</template>

<script setup>
// The Approval tab (AC-REMS-018): shows the live suggested approver list from the engagement, and — for a
// Draft engagement whose holder may send — the Send-for-Approval action. The approver decision/checklist UI
// is a SEPARATE surface (the Approvals inbox) and is intentionally not built here. Once sent, the state is
// read-only and the list is locked.
import { ref, computed, watch, onMounted } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useRemsMeta } from "modules/rems/useRemsMeta";
import AppSelect from "components/common/AppSelect.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  // Whether the caller holds rems.approvals.send.
  canSend: { type: Boolean, default: false },
  // Whether marketing has been saved (the tab is only reachable then; belt-and-braces here too).
  marketingSaved: { type: Boolean, default: false }
});
const emit = defineEmits(["status-changed"]);

const notify = useNotify();
const { confirm } = useConfirm();
const { engagementStatusMeta } = useRemsMeta();

const ROLE_LABELS = {
  CSE: "CSE",
  DepartmentDirector: "Department Director",
  ManagingShareholder: "Managing Shareholder",
  CommissionRecipient: "Commission Recipient",
  Approver: "Approver"
};
const ROLE_ICONS = {
  CSE: "o_support_agent",
  DepartmentDirector: "o_account_tree",
  ManagingShareholder: "o_workspace_premium",
  CommissionRecipient: "o_payments",
  Approver: "o_how_to_reg"
};
const roleLabel = (r) => ROLE_LABELS[r] || r;
const roleIcon = (r) => ROLE_ICONS[r] || "o_person";

const status = computed(() => props.engagement.status);
const statusMeta = computed(() => engagementStatusMeta(status.value));
// The rejection reason is shown to staff/CSE when the engagement carries it (AC-REMS-020.2).
const rejectionReason = computed(() => props.engagement.rejectionReason);

// The Send action only appears for a Draft engagement, when the caller may send and marketing is saved.
const canShowSend = computed(() => status.value === "Draft" && props.canSend && props.marketingSaved);
// Resubmission is offered on a Rejected engagement to a caller holding rems.approvals.send (AC-REMS-020.3).
const canShowResubmit = computed(() => status.value === "Rejected" && props.canSend);

const approvers = ref([]);
const loading = ref(false);
const errorMsg = ref("");

// ---- Add approvers ----
// Editable while the engagement is unsent; once routed, the API locks the list too.
const canPick = computed(() => ["Draft", "Rejected"].includes(status.value));
const approverOptions = ref([]);
const loadingOptions = ref(false);
// ONLY the added approvers. The automatic ones (CSE, commission recipients) are never in here — they are
// on the list below regardless, and putting them in the picker would imply they could be removed.
const picked = ref([]);

// Adopt a returned approver list as both the display list and the picker's current state.
const adopt = (list) => {
  approvers.value = list?.approvers || [];
  picked.value = [...(list?.selectedApproverIds || [])];
};

const load = async () => {
  loading.value = true;
  errorMsg.value = "";
  try {
    adopt(await remsApi.approvers(props.engagement.id));
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
  } finally {
    loading.value = false;
  }
};

const loadOptions = async () => {
  if (!canPick.value) return;
  loadingOptions.value = true;
  try {
    const rows = await remsApi.approverOptions(props.engagement.id);
    // "Full Name — Job Title", falling back to the name alone when the person has no title on file.
    approverOptions.value = (rows || []).map((r) => ({
      label: r.jobTitle ? `${r.name} — ${r.jobTitle}` : r.name,
      value: r.userId
    }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingOptions.value = false;
  }
};

// Saved on selection rather than behind a Save button, so the picked person drops straight into the list
// below and the only action left is Send for Approval.
const savingPicks = ref(false);
const savePicks = async () => {
  savingPicks.value = true;
  try {
    adopt(await remsApi.setApprovers(props.engagement.id, picked.value));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    await load(); // put the picker back to what actually persisted
  } finally {
    savingPicks.value = false;
  }
};

const reload = async () => { await Promise.all([load(), loadOptions()]); };

onMounted(reload);
watch(() => props.engagement.id, reload);
// Commission recipients are automatic approvers, so editing them changes the list — re-read it. Picks are
// saved on selection, so there is never unsaved state to clobber here.
watch(() => props.engagement.commissionSplits, reload, { deep: true });

const sending = ref(false);
const send = async () => {
  const ok = await confirm({
    title: "Send for approval",
    message: "This routes the engagement to every listed approver and locks the approver list. Continue?",
    confirmLabel: "Send"
  });
  if (!ok) return;
  sending.value = true;
  try {
    const list = await remsApi.sendApproval(props.engagement.id);
    adopt(list);
    emit("status-changed", list?.engagementStatus || "PendingApproval");
    notify.success("Engagement sent for approval.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    sending.value = false;
  }
};

const resubmitting = ref(false);
const resubmit = async () => {
  const ok = await confirm({
    title: "Resubmit for approval",
    message: "This creates a fresh approval round from the regenerated approver list and re-notifies every approver. Continue?",
    confirmLabel: "Resubmit"
  });
  if (!ok) return;
  resubmitting.value = true;
  try {
    const list = await remsApi.resubmitApproval(props.engagement.id);
    adopt(list);
    emit("status-changed", list?.engagementStatus || "PendingApproval");
    notify.success("Engagement resubmitted for approval.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    resubmitting.value = false;
  }
};
</script>
