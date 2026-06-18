<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'Customers', to: { name: 'customers' } },
        { label: detail?.customerRequestNumber || 'Customer' }
      ]"
      :back-to="{ name: 'customers' }"
    >
      <template #actions>
        <q-badge v-if="detail" :color="statusColor(detail.status)" class="q-mr-sm">{{ statusLabel(detail.status) }}</q-badge>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <template v-else-if="detail">
      <!-- Status + workflow meta -->
      <q-card flat bordered class="customer-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Status</q-card-section>
        <q-separator />
        <q-card-section>
          <div class="row items-center q-gutter-sm q-mb-md">
            <q-badge :color="statusColor(detail.status)" class="text-subtitle2">{{ statusLabel(detail.status) }}</q-badge>
            <q-chip
              v-if="detail.status === 'Synced' && detail.maconomyCustomerNumber"
              square color="positive" text-color="white" icon="o_verified"
              class="text-weight-bold maconomy-chip"
            >
              Maconomy Customer #: {{ detail.maconomyCustomerNumber }}
            </q-chip>
          </div>

          <q-banner v-if="detail.status === 'Failed' && detail.lastSyncError" dense rounded class="bg-red-1 text-red-9 q-mb-sm">
            <template #avatar><q-icon name="o_error" color="negative" /></template>
            Last sync error: {{ detail.lastSyncError }}
          </q-banner>
          <q-banner v-if="detail.status === 'Returned' && detail.returnNotes" dense rounded class="bg-orange-1 text-orange-9 q-mb-sm">
            <template #avatar><q-icon name="o_assignment_return" color="warning" /></template>
            Returned for corrections: {{ detail.returnNotes }}
          </q-banner>
          <q-banner v-if="detail.status === 'Rejected' && detail.rejectionReason" dense rounded class="bg-red-1 text-red-9 q-mb-sm">
            <template #avatar><q-icon name="o_block" color="negative" /></template>
            Rejected: {{ detail.rejectionReason }}
          </q-banner>

          <q-timeline color="primary" layout="comfortable">
            <q-timeline-entry
              v-for="stage in stages"
              :key="stage.key"
              :title="stage.label"
              :icon="stageReached(stage.key) ? 'o_check_circle' : 'o_radio_button_unchecked'"
              :color="stageReached(stage.key) ? 'positive' : 'grey-5'"
            />
          </q-timeline>
        </q-card-section>
      </q-card>

      <!-- Step 1: Basic Information -->
      <q-card flat bordered class="customer-card q-mb-md">
        <q-card-section class="row items-center text-subtitle1 text-weight-medium">
          Basic Information
          <q-space />
          <q-btn
            v-if="detail.actions.canEdit"
            unelevated no-caps color="primary" label="Save" icon="o_save"
            :loading="savingStep1" @click="saveStep1"
          />
        </q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <app-text-field v-model="step1.legalName" label="Legal Name" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.companyName" label="Company Name" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.contactPerson" label="Contact Person" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.emailAddress" label="Email Address" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.phoneNumber" label="Phone Number" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.website" label="Website" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.country" label="Country" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.stateProvince" label="State / Province" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.city" label="City" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.postalCode" label="Postal Code" class="col-12 col-sm-6" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.addressLine1" label="Address Line 1" class="col-12" :readonly="!detail.actions.canEdit" />
          <app-text-field v-model="step1.addressLine2" label="Address Line 2" class="col-12" :readonly="!detail.actions.canEdit" />
        </q-card-section>
      </q-card>

      <!-- Enrichment: Business Information -->
      <q-card v-if="showEnrichment" flat bordered class="customer-card q-mb-md">
        <q-card-section class="row items-center text-subtitle1 text-weight-medium">
          Business Information
          <q-space />
          <q-btn
            v-if="detail.actions.canEnrich"
            unelevated no-caps color="primary" label="Save" icon="o_save"
            :loading="savingEnrich" class="q-mr-sm" @click="saveEnrichment"
          />
          <q-btn
            v-if="detail.actions.canSendForApproval"
            outline no-caps color="primary" label="Send for Approval" icon="o_send"
            :loading="sending" @click="sendForApproval"
          />
        </q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <app-text-field v-model="enrich.internalCustomerCategory" label="Internal Customer Category" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.territory" label="Territory" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.practiceArea" label="Practice Area" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.salesRepresentative" label="Sales Representative" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.enrichmentPaymentTerms" label="Payment Terms" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.creditTerms" label="Credit Terms" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.customerType" label="Customer Type" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.businessSegment" label="Business Segment" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
          <app-text-field v-model="enrich.riskCategory" label="Risk Category" class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
        </q-card-section>
      </q-card>

      <!-- Step 2: Maconomy Fields (only when present) -->
      <q-card v-if="detail.step2 !== null" flat bordered class="customer-card q-mb-md">
        <q-card-section class="row items-center text-subtitle1 text-weight-medium">
          Maconomy Fields
          <q-space />
          <q-btn
            v-if="detail.actions.canEditStep2"
            unelevated no-caps color="primary" label="Save Step 2" icon="o_save"
            :loading="savingStep2" @click="saveStep2"
          />
        </q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <app-text-field v-model="step2.taxNumber" label="Tax Number *" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.registrationNumber" label="Registration Number *" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.businessUnit" label="Business Unit *" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.currency" label="Currency *" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.customerGroup" label="Customer Group" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.paymentTerms" label="Payment Terms *" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.creditLimit" type="number" label="Credit Limit" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.industry" label="Industry" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.invoiceLanguage" label="Invoice Language" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
          <app-text-field v-model="step2.billingEmail" label="Billing Email" class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
        </q-card-section>
      </q-card>

      <!-- Documents -->
      <q-card flat bordered class="customer-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Documents</q-card-section>
        <q-separator />
        <q-list separator>
          <q-item v-for="doc in documents" :key="doc.id">
            <q-item-section avatar><q-icon name="o_description" color="grey-7" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ doc.fileName }}</q-item-label>
              <q-item-label caption>{{ formatSize(doc.fileSizeBytes) }} · {{ fmt.formatDateTime(doc.uploadedOnUtc) }}</q-item-label>
            </q-item-section>
            <q-item-section side class="row no-wrap">
              <q-btn flat round dense color="primary" icon="o_download" @click="downloadDoc(doc)">
                <q-tooltip>Download</q-tooltip>
              </q-btn>
              <q-btn flat round dense color="negative" icon="o_delete" @click="removeDoc(doc)">
                <q-tooltip>Remove</q-tooltip>
              </q-btn>
            </q-item-section>
          </q-item>
          <q-item v-if="!documents.length"><q-item-section class="text-grey-6">No documents uploaded.</q-item-section></q-item>
        </q-list>
        <q-separator />
        <q-card-section class="row items-end q-col-gutter-md">
          <q-file
            v-model="docFile" outlined dense stack-label class="col" label="Upload a document"
            :accept="acceptExtensions" hint="Allowed: pdf, doc, docx, xls, xlsx, csv, txt, png, jpg, jpeg"
          >
            <template #prepend><q-icon name="o_attach_file" /></template>
          </q-file>
          <q-btn unelevated no-caps color="primary" label="Upload" icon="o_upload" :loading="uploading" :disable="!docFile" @click="uploadDoc" />
        </q-card-section>
      </q-card>

      <!-- Audit Trail -->
      <q-card flat bordered class="customer-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Audit Trail</q-card-section>
        <q-separator />
        <q-list separator>
          <q-item v-for="entry in detail.auditTrail" :key="entry.id">
            <q-item-section avatar><q-icon name="o_history" color="grey-7" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ entry.actionType }}</q-item-label>
              <q-item-label caption>
                {{ entry.performedBy }} · {{ fmt.formatDateTime(entry.performedOnUtc) }}
                <template v-if="entry.notes"> — {{ entry.notes }}</template>
              </q-item-label>
            </q-item-section>
          </q-item>
          <q-item v-if="!detail.auditTrail || !detail.auditTrail.length">
            <q-item-section class="text-grey-6">No activity yet.</q-item-section>
          </q-item>
        </q-list>
      </q-card>

      <!-- Action row -->
      <div v-if="hasActions" class="row justify-end q-gutter-sm q-mb-lg">
        <q-btn v-if="detail.actions.canReopen" outline no-caps color="primary" icon="o_lock_open" label="Reopen" :loading="busy" @click="reopen" />
        <q-btn v-if="detail.actions.canRetrySync" outline no-caps color="primary" icon="o_sync" label="Retry Sync" :loading="busy" @click="retrySync" />
        <q-btn v-if="detail.actions.canReturn" outline no-caps color="warning" icon="o_assignment_return" label="Return for Corrections" @click="returnOpen = true" />
        <q-btn v-if="detail.actions.canReject" outline no-caps color="negative" icon="o_block" label="Reject" @click="rejectOpen = true" />
        <q-btn v-if="detail.actions.canApprove" unelevated no-caps color="positive" icon="o_check_circle" label="Approve" @click="openApprove" />
      </div>
    </template>

    <!-- Approve dialog: Step 2 fields (all mandatory) -->
    <q-dialog v-model="approveOpen" persistent>
      <q-card style="min-width: 480px; max-width: 92vw;">
        <q-card-section class="text-h6">Approve customer</q-card-section>
        <q-separator />
        <q-card-section>
          <q-form ref="approveFormRef" greedy class="row q-col-gutter-md">
            <app-text-field v-model="approveStep2.taxNumber" label="Tax Number *" class="col-12 col-sm-6" :rules="req" />
            <app-text-field v-model="approveStep2.registrationNumber" label="Registration Number *" class="col-12 col-sm-6" :rules="req" />
            <app-text-field v-model="approveStep2.businessUnit" label="Business Unit *" class="col-12 col-sm-6" :rules="req" />
            <app-text-field v-model="approveStep2.currency" label="Currency *" class="col-12 col-sm-6" :rules="req" />
            <app-text-field v-model="approveStep2.customerGroup" label="Customer Group" class="col-12 col-sm-6" />
            <app-text-field v-model="approveStep2.paymentTerms" label="Payment Terms *" class="col-12 col-sm-6" :rules="req" />
            <app-text-field v-model="approveStep2.creditLimit" type="number" label="Credit Limit" class="col-12 col-sm-6" />
            <app-text-field v-model="approveStep2.industry" label="Industry" class="col-12 col-sm-6" />
            <app-text-field v-model="approveStep2.invoiceLanguage" label="Invoice Language" class="col-12 col-sm-6" />
            <app-text-field v-model="approveStep2.billingEmail" label="Billing Email" class="col-12 col-sm-6" />
          </q-form>
          <q-banner v-if="approveDuplicates.length" dense rounded class="bg-orange-1 text-orange-9 q-mt-md">
            <template #avatar><q-icon name="o_warning" color="warning" /></template>
            A customer with a matching tax number already exists. Approve anyway to proceed.
            <q-list dense>
              <q-item v-for="d in approveDuplicates" :key="d.id">
                <q-item-section>{{ d.companyName }} ({{ d.customerRequestNumber }})</q-item-section>
              </q-item>
            </q-list>
          </q-banner>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="approveOpen = false" />
          <q-btn
            unelevated no-caps color="positive"
            :label="approveDuplicates.length ? 'Approve anyway' : 'Approve'"
            :loading="busy" @click="submitApprove"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Reject dialog -->
    <q-dialog v-model="rejectOpen" persistent>
      <q-card style="min-width: 380px; max-width: 90vw;">
        <q-card-section class="text-h6">Reject customer</q-card-section>
        <q-separator />
        <q-card-section>
          <q-input v-model="rejectReason" outlined dense type="textarea" autogrow label="Reason *" :rules="req" />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="rejectOpen = false" />
          <q-btn unelevated no-caps color="negative" label="Reject" :loading="busy" :disable="!rejectReason" @click="submitReject" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Return for corrections dialog -->
    <q-dialog v-model="returnOpen" persistent>
      <q-card style="min-width: 420px; max-width: 90vw;">
        <q-card-section class="text-h6">Return for corrections</q-card-section>
        <q-separator />
        <q-card-section>
          <q-input v-model="returnNotes" outlined dense type="textarea" autogrow label="Notes *" :rules="req" class="q-mb-md" />
          <app-select v-model="returnFields" :options="returnFieldOptions" label="Fields to correct" multiple />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="returnOpen = false" />
          <q-btn unelevated no-caps color="warning" label="Return" :loading="busy" :disable="!returnNotes" @click="submitReturn" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { customerApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { useCustomerStatus } from "composables/useCustomerStatus";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const { customerStatusColor: statusColor, customerStatusLabel: statusLabel, CUSTOMER_STAGES: stages } = useCustomerStatus();

const customerId = route.params.id;
const loading = ref(true);
const detail = ref(null);
const documents = ref([]);

const req = [(v) => !!v || "Required"];

// ---- Step 1 (basic information) ----
const step1 = reactive({});
const STEP1_FIELDS = ["legalName", "companyName", "contactPerson", "emailAddress", "phoneNumber", "website", "country", "stateProvince", "city", "addressLine1", "addressLine2", "postalCode"];

// ---- Enrichment (business information) ----
const enrich = reactive({});
const ENRICH_FIELDS = ["internalCustomerCategory", "territory", "practiceArea", "salesRepresentative", "enrichmentPaymentTerms", "creditTerms", "customerType", "businessSegment", "riskCategory"];

// ---- Step 2 (Maconomy fields) ----
const step2 = reactive({});
const STEP2_FIELDS = ["taxNumber", "registrationNumber", "businessUnit", "currency", "customerGroup", "paymentTerms", "creditLimit", "industry", "invoiceLanguage", "billingEmail"];

const showEnrichment = computed(() => {
  const a = detail.value?.actions || {};
  return a.canEnrich || a.canSendForApproval || ENRICH_FIELDS.some((f) => enrich[f]);
});

const STAGE_ORDER = stages.map((s) => s.key);
const stageReached = (stageKey) => {
  const current = detail.value?.status;
  // Terminal/branch states still mark the prior stages as reached.
  const reachedIndex = {
    Draft: 0,
    Submitted: 1,
    UnderReview: 2,
    PendingApproval: 3,
    PartiallyApproved: 3,
    Approved: 4,
    SyncInProgress: 4,
    Synced: 5,
    Rejected: 1,
    Returned: 1,
    Failed: 4
  }[current] ?? 0;
  return STAGE_ORDER.indexOf(stageKey) <= reachedIndex;
};

const hasActions = computed(() => {
  const a = detail.value?.actions || {};
  return a.canApprove || a.canReject || a.canReturn || a.canRetrySync || a.canReopen;
});

const fill = (target, source, fields) => {
  fields.forEach((f) => { target[f] = source?.[f] ?? ""; });
};

const load = async () => {
  loading.value = true;
  try {
    const d = await customerApi.get(customerId);
    detail.value = d;
    fill(step1, d, STEP1_FIELDS);
    fill(enrich, d, ENRICH_FIELDS);
    fill(step2, d.step2 || {}, STEP2_FIELDS);
    documents.value = d.documents || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

// ---- Step 1 save ----
const savingStep1 = ref(false);
const saveStep1 = async () => {
  savingStep1.value = true;
  try {
    const payload = {};
    STEP1_FIELDS.forEach((f) => { payload[f] = step1[f] || null; });
    await customerApi.update(customerId, payload);
    notify.success("Basic information saved.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingStep1.value = false;
  }
};

// ---- Enrichment save / send ----
const savingEnrich = ref(false);
const saveEnrichment = async () => {
  savingEnrich.value = true;
  try {
    const payload = {};
    ENRICH_FIELDS.forEach((f) => { payload[f] = enrich[f] || null; });
    await customerApi.enrich(customerId, payload);
    notify.success("Business information saved.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingEnrich.value = false;
  }
};

const sending = ref(false);
const sendForApproval = async () => {
  sending.value = true;
  try {
    await customerApi.sendForApproval(customerId);
    notify.success("Sent for approval.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    sending.value = false;
  }
};

// ---- Step 2 save ----
const savingStep2 = ref(false);
const saveStep2 = async () => {
  savingStep2.value = true;
  try {
    await customerApi.saveStep2(customerId, buildStep2(step2));
    notify.success("Maconomy fields saved.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingStep2.value = false;
  }
};

const buildStep2 = (src) => {
  const payload = {};
  STEP2_FIELDS.forEach((f) => { payload[f] = src[f] === "" ? null : src[f]; });
  if (payload.creditLimit != null && payload.creditLimit !== "") payload.creditLimit = Number(payload.creditLimit);
  return payload;
};

// ---- Documents ----
const ALLOWED_EXT = ["pdf", "doc", "docx", "xls", "xlsx", "csv", "txt", "png", "jpg", "jpeg"];
const acceptExtensions = ALLOWED_EXT.map((e) => `.${e}`).join(",");
const docFile = ref(null);
const uploading = ref(false);

const formatSize = (bytes) => {
  if (!bytes && bytes !== 0) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const uploadDoc = async () => {
  const file = docFile.value;
  if (!file) return;
  const ext = file.name.split(".").pop()?.toLowerCase();
  if (!ALLOWED_EXT.includes(ext)) {
    notify.error(`Unsupported file type. Allowed: ${ALLOWED_EXT.join(", ")}.`);
    return;
  }
  uploading.value = true;
  try {
    const created = await customerApi.uploadDocument(customerId, file);
    documents.value = [...documents.value, created];
    docFile.value = null;
    notify.success("Document uploaded.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    uploading.value = false;
  }
};

const downloadDoc = async (doc) => {
  try {
    const blob = await customerApi.downloadDocument(customerId, doc.id);
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = doc.fileName;
    a.click();
    URL.revokeObjectURL(url);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const removeDoc = async (doc) => {
  const ok = await confirm({
    title: "Remove document",
    message: `Remove "${doc.fileName}"?`,
    confirmLabel: "Remove",
    type: "danger"
  });
  if (!ok) return;
  try {
    await customerApi.removeDocument(customerId, doc.id);
    documents.value = documents.value.filter((d) => d.id !== doc.id);
    notify.success("Document removed.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Action row ----
const busy = ref(false);

// Approve
const approveOpen = ref(false);
const approveFormRef = ref(null);
const approveStep2 = reactive({});
const approveDuplicates = ref([]);

const openApprove = () => {
  approveDuplicates.value = [];
  // Pre-fill the approval Step 2 form from any saved Step 2 values.
  STEP2_FIELDS.forEach((f) => { approveStep2[f] = step2[f] ?? ""; });
  approveOpen.value = true;
};

const submitApprove = async () => {
  const acknowledged = approveDuplicates.value.length > 0;
  if (!acknowledged && !(await approveFormRef.value?.validate())) return;
  busy.value = true;
  try {
    const result = await customerApi.approve(customerId, buildStep2(approveStep2), acknowledged);
    if (result?.approved === false) {
      approveDuplicates.value = result?.duplicates || [];
      busy.value = false;
      return;
    }
    approveOpen.value = false;
    notify.success("Customer approved.");
    load();
  } catch (err) {
    if (getApiErrorCode(err) === ApiErrorCodes.ValidationFailed) {
      notify.error(getApiErrorMessage(err, "Please complete all mandatory Maconomy fields."));
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    busy.value = false;
  }
};

// Reject
const rejectOpen = ref(false);
const rejectReason = ref("");
const submitReject = async () => {
  if (!rejectReason.value) return;
  busy.value = true;
  try {
    await customerApi.reject(customerId, rejectReason.value);
    rejectOpen.value = false;
    rejectReason.value = "";
    notify.success("Customer rejected.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    busy.value = false;
  }
};

// Return for corrections
const returnOpen = ref(false);
const returnNotes = ref("");
const returnFields = ref([]);
const returnFieldOptions = STEP1_FIELDS.map((f) => ({ label: f, value: f }));
const submitReturn = async () => {
  if (!returnNotes.value) return;
  busy.value = true;
  try {
    await customerApi.returnForCorrections(customerId, returnNotes.value, returnFields.value || []);
    returnOpen.value = false;
    returnNotes.value = "";
    returnFields.value = [];
    notify.success("Returned for corrections.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    busy.value = false;
  }
};

// Retry sync
const retrySync = async () => {
  busy.value = true;
  try {
    await customerApi.retrySync(customerId);
    notify.success("Sync retried.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    busy.value = false;
  }
};

// Reopen
const reopen = async () => {
  const ok = await confirm({ title: "Reopen customer", message: "Reopen this customer for editing?", confirmLabel: "Reopen" });
  if (!ok) return;
  busy.value = true;
  try {
    await customerApi.reopen(customerId);
    notify.success("Customer reopened.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    busy.value = false;
  }
};

onMounted(load);
</script>

<style scoped>
.customer-card {
  border-radius: 12px;
}
.maconomy-chip {
  font-size: 14px;
}
</style>
