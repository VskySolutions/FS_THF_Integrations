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
      <q-card flat bordered class="customer-card">
        <q-tabs v-model="activeTab" align="left" no-caps active-color="primary" indicator-color="primary" class="text-grey-7">
          <q-tab name="basic" icon="o_assignment_ind" label="Basic Information" />
          <q-tab v-if="showReviewTab" name="review" icon="o_fact_check" label="Review" />
          <q-tab name="history" icon="o_history" label="Status &amp; Change History" />
        </q-tabs>
        <q-separator />

        <q-tab-panels v-model="activeTab" animated>
          <!-- Tab 1: Basic Information -->
          <q-tab-panel name="basic">
            <div class="row items-center q-mb-md">
              <div class="text-subtitle1 text-weight-medium">Basic Information</div>
              <q-space />
              <q-btn
                v-if="detail.actions.canEdit"
                unelevated no-caps color="primary" label="Save" icon="o_save"
                :loading="savingStep1" @click="saveStep1"
              />
            </div>
            <div class="row q-col-gutter-md">
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
            </div>
          </q-tab-panel>

          <!-- Tab 2: Review (reviewer / approver) — Business Information, Maconomy Fields, Documents -->
          <q-tab-panel v-if="showReviewTab" name="review">
            <!-- Business Information -->
            <div class="row items-center q-mb-sm">
              <div class="text-subtitle1 text-weight-medium">Business Information</div>
              <q-space />
              <q-btn
                v-if="detail.actions.canEnrich"
                unelevated no-caps color="primary" label="Save" icon="o_save"
                :loading="savingEnrich" @click="saveEnrichment"
              />
            </div>
            <div class="row q-col-gutter-md q-mb-lg">
              <app-text-field v-model="enrich.internalCustomerCategory" label="Internal Customer Category" placeholder="e.g. Key Account" hint="Internal classification used for reporting." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.territory" label="Territory" placeholder="e.g. EMEA" hint="Sales territory or region this customer belongs to." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.practiceArea" label="Practice Area" placeholder="e.g. Consulting" hint="Practice or service area owning the account." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.salesRepresentative" label="Sales Representative" placeholder="e.g. Jane Doe" hint="Name of the owning sales representative." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.enrichmentPaymentTerms" label="Payment Terms" placeholder="e.g. Net 30" hint="Agreed payment terms (internal reference)." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.creditTerms" label="Credit Terms" placeholder="e.g. 30 days" hint="Credit period granted to the customer." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.customerType" label="Customer Type" placeholder="e.g. Direct" hint="Direct, Partner, Reseller, etc." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.businessSegment" label="Business Segment" placeholder="e.g. Enterprise" hint="Market segment (SMB, Mid-market, Enterprise…)." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
              <app-text-field v-model="enrich.riskCategory" label="Risk Category" placeholder="e.g. Low" hint="Risk rating: Low, Medium or High." class="col-12 col-sm-6" :readonly="!detail.actions.canEnrich" />
            </div>

            <q-separator class="q-mb-md" />

            <!-- Maconomy Fields -->
            <div class="row items-center q-mb-sm">
              <div class="text-subtitle1 text-weight-medium">Maconomy Fields</div>
              <q-space />
              <q-btn
                v-if="detail.actions.canEditStep2"
                unelevated no-caps color="primary" label="Save" icon="o_save"
                :loading="savingStep2" @click="saveStep2"
              />
            </div>
            <div class="row q-col-gutter-md q-mb-lg">
              <app-text-field v-model="step2.taxNumber" label="Tax Number *" placeholder="e.g. GB123456789" hint="Mandatory. Tax / VAT registration number." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.registrationNumber" label="Registration Number *" placeholder="e.g. 12345678" hint="Mandatory. Company registration number." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.businessUnit" label="Business Unit *" placeholder="e.g. UK-Operations" hint="Mandatory. Maconomy business unit to post against." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.currency" label="Currency *" placeholder="e.g. USD" hint="Mandatory. 3-letter ISO currency code." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.customerGroup" label="Customer Group" placeholder="e.g. Standard" hint="Maconomy customer group for grouping/reporting." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.paymentTerms" label="Payment Terms *" placeholder="e.g. Net 30" hint="Mandatory. Maconomy payment terms code." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.creditLimit" type="number" label="Credit Limit" placeholder="e.g. 50000" hint="Approved credit limit (numeric, in the chosen currency)." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.industry" label="Industry" placeholder="e.g. Manufacturing" hint="Customer's primary industry." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.invoiceLanguage" label="Invoice Language" placeholder="e.g. English" hint="Language Maconomy should use for invoices." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
              <app-text-field v-model="step2.billingEmail" label="Billing Email" placeholder="e.g. billing@acme.com" hint="Email address invoices and billing notices are sent to." class="col-12 col-sm-6" :readonly="!detail.actions.canEditStep2" />
            </div>

            <q-separator class="q-mb-md" />

            <!-- Documents -->
            <div class="text-subtitle1 text-weight-medium q-mb-sm">Documents</div>
            <q-list bordered separator class="rounded-borders">
              <q-item v-for="doc in documents" :key="doc.id">
                <q-item-section avatar><q-icon name="o_description" color="grey-7" /></q-item-section>
                <q-item-section>
                  <q-item-label>{{ doc.fileName }}</q-item-label>
                  <q-item-label caption>{{ formatSize(doc.fileSizeBytes) }} · {{ fmt.formatDateTime(doc.uploadedOnUtc) }}</q-item-label>
                </q-item-section>
                <q-item-section side class="row no-wrap">
                  <q-btn flat round dense color="primary" icon="o_download" @click="downloadDoc(doc)"><q-tooltip>Download</q-tooltip></q-btn>
                  <q-btn flat round dense color="negative" icon="o_delete" @click="removeDoc(doc)"><q-tooltip>Remove</q-tooltip></q-btn>
                </q-item-section>
              </q-item>
              <q-item v-if="!documents.length"><q-item-section class="text-grey-6">No documents uploaded.</q-item-section></q-item>
            </q-list>
            <div class="row items-end q-col-gutter-md q-mt-sm">
              <q-file
                v-model="docFile" outlined dense stack-label class="col" label="Upload a document"
                :accept="acceptExtensions" hint="Allowed: pdf, doc, docx, xls, xlsx, csv, txt, png, jpg, jpeg"
              >
                <template #prepend><q-icon name="o_attach_file" /></template>
              </q-file>
              <q-btn unelevated no-caps color="primary" label="Upload" icon="o_upload" :loading="uploading" :disable="!docFile" @click="uploadDoc" />
            </div>
          </q-tab-panel>

          <!-- Tab 3: Status & Change History -->
          <q-tab-panel name="history">
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

            <q-separator class="q-my-md" />

            <div class="text-subtitle1 text-weight-medium q-mb-sm">Change history</div>
            <q-list bordered separator class="rounded-borders">
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
          </q-tab-panel>
        </q-tab-panels>
      </q-card>

      <!-- Separate bottom action bar: the workflow actions for the caller's role/state. -->
      <q-card v-if="hasActions" flat bordered class="customer-action-bar q-mt-md">
        <q-card-section class="row justify-end items-center q-gutter-sm">
          <q-btn v-if="detail.actions.canSubmit" unelevated no-caps color="primary" icon="o_send" label="Submit for Approval" :loading="submitting" @click="submitForReview" />
          <q-btn v-if="detail.actions.canReopen" outline no-caps color="primary" icon="o_lock_open" label="Reopen" :loading="busy" @click="reopen" />
          <q-btn v-if="detail.actions.canRetrySync" outline no-caps color="primary" icon="o_sync" label="Retry Sync" :loading="busy" @click="retrySync" />
          <q-btn v-if="detail.actions.canSendForApproval" unelevated no-caps color="primary" icon="o_send" label="Send for Approval" :loading="sending" @click="sendForApproval" />
          <q-btn v-if="detail.actions.canReturn" outline no-caps color="warning" icon="o_assignment_return" label="Return to Data Entry" @click="returnOpen = true" />
          <q-btn v-if="detail.actions.canRevertToReviewer" outline no-caps color="warning" icon="o_undo" label="Revert to Reviewer" @click="revertOpen = true" />
          <q-btn v-if="detail.actions.canApprove" unelevated no-caps color="positive" icon="o_check_circle" label="Approve" @click="openApprove" />
        </q-card-section>
      </q-card>
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

    <!-- Revert to reviewer dialog -->
    <q-dialog v-model="revertOpen" persistent>
      <q-card style="min-width: 380px; max-width: 90vw;">
        <q-card-section class="text-h6">Revert to reviewer</q-card-section>
        <q-separator />
        <q-card-section>
          <q-input v-model="revertNotes" outlined dense type="textarea" autogrow label="Notes (optional)" hint="Tell the reviewer what to revisit." />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="revertOpen = false" />
          <q-btn unelevated no-caps color="warning" label="Revert to Reviewer" :loading="busy" @click="submitRevert" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Return for corrections dialog -->
    <q-dialog v-model="returnOpen" persistent>
      <q-card style="min-width: 420px; max-width: 90vw;">
        <q-card-section class="text-h6">Return to data entry</q-card-section>
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

// Active tab; reviewers/approvers default to the Review tab, everyone else to Basic Information.
const activeTab = ref("basic");
let tabInitialised = false;
const showReviewTab = computed(() => !!detail.value?.actions?.canViewStep2);

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

// The bottom action bar shows whenever the caller has any workflow action available.
const hasActions = computed(() => {
  const a = detail.value?.actions || {};
  return a.canSubmit || a.canSendForApproval || a.canReturn || a.canRevertToReviewer || a.canApprove || a.canReopen || a.canRetrySync;
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
    // Pick the default tab once (don't yank the user off their tab on subsequent reloads).
    if (!tabInitialised) {
      activeTab.value = d.actions?.canViewStep2 ? "review" : "basic";
      tabInitialised = true;
    }
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

// ---- Submit for review (data entry) ----
const submitting = ref(false);
const submitForReview = async () => {
  submitting.value = true;
  try {
    let result = await customerApi.submit(customerId, false);
    if (result?.submitted === false) {
      const dupes = result?.duplicates || [];
      const names = dupes.map((d) => d.companyName).filter(Boolean).join(", ");
      const ok = await confirm({
        title: "Possible duplicates",
        message: `We found ${dupes.length} similar customer(s)${names ? `: ${names}` : ""}. Submit anyway?`,
        confirmLabel: "Submit anyway"
      });
      if (!ok) { submitting.value = false; return; }
      result = await customerApi.submit(customerId, true);
    }
    notify.success("Submitted for review.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    submitting.value = false;
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

// Mandatory Maconomy (Step 2) fields that must be present before approval (mirrors the backend).
const MANDATORY_STEP2 = [
  { key: "taxNumber", label: "Tax Number" },
  { key: "registrationNumber", label: "Registration Number" },
  { key: "businessUnit", label: "Business Unit" },
  { key: "currency", label: "Currency" },
  { key: "paymentTerms", label: "Payment Terms" }
];

const sending = ref(false);
const sendForApproval = async () => {
  // 1) Block until the mandatory Maconomy fields are filled (jump to the Review tab to fix them).
  const missing = MANDATORY_STEP2.filter((f) => !String(step2[f.key] ?? "").trim()).map((f) => f.label);
  if (missing.length) {
    notify.error(`Complete the mandatory Maconomy fields first: ${missing.join(", ")}.`);
    activeTab.value = "review";
    return;
  }

  // 2) Confirm before handing the record to the approver.
  const ok = await confirm({
    title: "Send for approval",
    message: "Send this customer to the approver? You won't be able to edit it unless it's sent back to you.",
    confirmLabel: "Send for Approval"
  });
  if (!ok) return;

  sending.value = true;
  try {
    // Persist any unsaved Step 2 edits so the approver sees exactly what was reviewed.
    if (detail.value?.actions?.canEditStep2) {
      await customerApi.saveStep2(customerId, buildStep2(step2));
    }
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

// ---- Action bar ----
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

// Revert to reviewer (approver sends an awaiting-approval request back to the reviewer)
const revertOpen = ref(false);
const revertNotes = ref("");
const submitRevert = async () => {
  busy.value = true;
  try {
    await customerApi.revertToReviewer(customerId, revertNotes.value || null);
    revertOpen.value = false;
    revertNotes.value = "";
    notify.success("Reverted to the reviewer.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    busy.value = false;
  }
};

// Return for corrections (reviewer sends the record back to data entry)
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
    notify.success("Returned to data entry.");
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
.customer-card,
.customer-action-bar {
  border-radius: 12px;
}
.maconomy-chip {
  font-size: 14px;
}
</style>
