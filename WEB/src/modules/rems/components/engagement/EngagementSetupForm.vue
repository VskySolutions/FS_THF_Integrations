<template>
  <div>
    <!-- Copy From (AC-REMS-015): only when another entity exists to copy from. -->
    <q-card v-if="editable && otherEntities.length" flat bordered class="rems-inner q-mb-md">
      <q-card-section class="row items-center q-col-gutter-sm q-py-sm">
        <div class="col-auto text-caption text-grey-7">
          <q-icon name="o_content_copy" size="18px" class="q-mr-xs" />Copy setup from another entity
        </div>
        <app-select
          v-model="copySource" :options="otherEntities" label="" class="col-12 col-sm"
          option-value="value" option-label="label"
        />
        <div class="col-auto">
          <q-btn
            outline no-caps color="primary" icon="o_content_copy" label="Copy"
            :disable="!copySource" :loading="copying" @click="copyFrom"
          />
        </div>
      </q-card-section>
    </q-card>

    <!-- Core engagement placement + team + fee/realization (AC-REMS-014.5-10). -->
    <q-form ref="formRef" greedy>
      <div class="row q-col-gutter-md">
        <app-select
          v-model="core.department" :options="deptOptions" label="Department" class="col-12 col-md-6"
          :readonly="!editable" :clearable="false"
        />
        <!-- Government is a Service Line, not a Department (AC-REMS-014.5/6). -->
        <app-select
          v-model="core.serviceLine" :options="serviceLineOptions" label="Service Line" class="col-12 col-md-6"
          :readonly="!editable" :clearable="false"
        />

        <!-- Read-only mapped Department Director, prefilled server-side from the department mapping
             (AC-REMS-014.7). -->
        <div class="app-field col-12 col-md-6">
          <app-field-label label="Department Director" />
          <div class="rems-readonly">{{ directorName || "Not assigned" }}</div>
          <div class="text-caption text-grey-6 q-mt-xs">
            Auto-assigned from the department mapping<span v-if="departmentChangedUnsaved"> — updates when you save</span>.
          </div>
        </div>

        <app-select
          v-model="core.engagementExecutiveId" :options="staff" label="Engagement Executive"
          class="col-12 col-md-6" :readonly="!editable"
        />
        <app-select
          v-model="core.billingManagerId" :options="staff" label="Billing Manager"
          class="col-12 col-md-6" :readonly="!editable"
        />

        <app-text-field
          v-model="core.firstYearFeeEstimate" label="First-Year Fee Estimate" type="number"
          class="col-12 col-md-6" :readonly="!editable" :rules="feeRules"
        >
          <template #prepend><span class="text-grey-7">$</span></template>
        </app-text-field>
        <app-text-field
          v-model="core.realizationPercentage" label="% Realization" type="number"
          class="col-12 col-md-6" :readonly="!editable" :rules="realizationRules"
        >
          <template #append><span class="text-grey-7">%</span></template>
        </app-text-field>
      </div>

      <div v-if="editable" class="row justify-end q-mt-sm">
        <q-btn
          unelevated no-caps color="primary" icon="o_save" label="Save engagement"
          :loading="savingCore" @click="saveCore"
        />
      </div>
    </q-form>

    <!-- Conditional: Audit → required signed CAF PDF upload (AC-REMS-014.11/12). -->
    <q-card v-if="showAudit" flat bordered class="rems-inner q-mt-md">
      <q-card-section class="q-py-sm text-subtitle2 text-primary">
        <q-icon name="o_fact_check" size="18px" class="q-mr-xs" />Audit — Client Acceptance Form
      </q-card-section>
      <q-separator />
      <q-card-section>
        <q-banner v-if="hasCaf" dense class="bg-green-1 text-green-9 rounded-borders q-mb-sm">
          <template #avatar><q-icon name="o_verified" color="green-9" /></template>
          A signed client-acceptance form is on file.
        </q-banner>
        <q-banner v-else dense class="bg-orange-1 text-orange-9 rounded-borders q-mb-sm">
          <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
          A signed client-acceptance form (PDF) is required before this audit engagement can be sent for approval.
        </q-banner>
        <app-single-file-upload
          v-if="editable"
          v-model="cafFile" accept=".pdf" :max-size-mb="25" :loading="cafUploading"
          :label="hasCaf ? 'Replace signed CAF (PDF)' : 'Upload signed CAF (PDF)'"
          hint="PDF up to 25 MB"
        />
      </q-card-section>
    </q-card>

    <!-- Conditional: Government Audit (Department=audit + ServiceLine=government) → contract number +
         Florida 1% state-fee flag (AC-REMS-014.13). -->
    <q-card v-if="showGovernment" flat bordered class="rems-inner q-mt-md">
      <q-card-section class="q-py-sm text-subtitle2 text-primary">
        <q-icon name="o_gavel" size="18px" class="q-mr-xs" />Government Audit — Contract
      </q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-md items-center">
          <app-text-field
            v-model="gov.contractNumber" label="Contract Number" required class="col-12 col-md-6"
            :readonly="!editable"
          />
          <div class="col-12 col-md-6">
            <q-checkbox
              v-model="gov.floridaOnePercentStateFeeApplies" :disable="!editable"
              label="Florida 1% state fee applies"
            />
          </div>
        </div>

        <!-- Contract / PO dates copied from the submission — shown read-only for context. -->
        <div v-if="hasContractDates" class="rems-copied q-mt-sm">
          <div class="rems-copied__item"><span>Contract Start</span>{{ dateOnly(gov.contractStartDate) }}</div>
          <div class="rems-copied__item"><span>Contract End</span>{{ dateOnly(gov.contractEndDate) }}</div>
          <div class="rems-copied__item"><span>PO Start</span>{{ dateOnly(gov.purchaseOrderStartDate) }}</div>
          <div class="rems-copied__item"><span>PO End</span>{{ dateOnly(gov.purchaseOrderEndDate) }}</div>
        </div>

        <div v-if="editable" class="row justify-end q-mt-sm">
          <q-btn
            unelevated no-caps color="primary" icon="o_save" label="Save contract"
            :loading="savingGov" @click="saveGovernment"
          />
        </div>
      </q-card-section>
    </q-card>

    <!-- Conditional: Tax → fiscal year end + calculated due dates + tax-form checklist (AC-REMS-014.14). -->
    <q-card v-if="showTax" flat bordered class="rems-inner q-mt-md">
      <q-card-section class="q-py-sm text-subtitle2 text-primary">
        <q-icon name="o_receipt_long" size="18px" class="q-mr-xs" />Tax — Fiscal Year &amp; Forms
      </q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-md">
          <app-date-field
            v-model="tax.fiscalYearEnd" label="Fiscal Year End" class="col-12 col-md-6" :readonly="!editable"
          />
          <div v-if="dueDates" class="col-12 col-md-6">
            <app-field-label label="Calculated Due Dates" />
            <div class="rems-readonly">
              Original: {{ dateOnly(dueDates.originalDueDate) }} · Extended: {{ dateOnly(dueDates.extendedDueDate) }}
            </div>
          </div>
        </div>

        <div class="section-subhead">Tax Forms</div>
        <div v-if="taxFormUnavailable" class="text-caption text-grey-6">
          The tax-form list could not be loaded for your account.
        </div>
        <div v-else class="row q-col-gutter-x-md">
          <q-checkbox
            v-for="opt in taxFormOptions" :key="opt.value" v-model="tax.taxFormIds" :val="opt.value"
            :label="opt.label" :disable="!editable" class="col-12 col-sm-6"
          />
        </div>

        <div v-if="editable" class="row justify-end q-mt-sm">
          <q-btn
            unelevated no-caps color="primary" icon="o_save" label="Save tax details"
            :loading="savingTax" @click="saveTax"
          />
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
// One entity's engagement setup (AC-REMS-014/015): department + service line placement, the mapped
// department director (read-only), the engagement team, fee/realization, the conditional Audit / Government
// / Tax detail forms, and the one-time Copy-From control. Every save returns the refreshed engagement view,
// which the parent panel adopts as the new source of truth.
import { ref, computed, watch } from "vue";
import { remsApi, mediaApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { isAuditDepartment, isTaxDepartment, isGovernmentAudit } from "modules/rems/useRemsMeta";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppSingleFileUpload from "components/common/AppSingleFileUpload.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  staff: { type: Array, default: () => [] },
  deptOptions: { type: Array, default: () => [] },
  serviceLineOptions: { type: Array, default: () => [] },
  taxFormOptions: { type: Array, default: () => [] },
  taxFormUnavailable: { type: Boolean, default: false },
  // Sibling engagements available as a copy source: [{ label: entityName, value: engagementId }].
  otherEntities: { type: Array, default: () => [] },
  editable: { type: Boolean, default: true }
});
const emit = defineEmits(["saved", "workspace-refresh"]);

const notify = useNotify();
const { confirm } = useConfirm();
const formRef = ref(null);

// Calendar-date fields (DateOnly "YYYY-MM-DD") are shown as-is (MM-DD-YYYY), never timezone-shifted — the
// tenant-tz formatter would corrupt a date-only value (mirrors SubmittedFormDialog's handling).
const dateOnly = (v) => {
  if (!v) return "—";
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(v));
  return m ? `${m[2]}-${m[3]}-${m[1]}` : String(v);
};

// ---- Core engagement fields (local editable copy, re-synced from the source view) ----
const buildCore = (e) => ({
  department: e.department || null,
  serviceLine: e.serviceLine || null,
  engagementExecutiveId: e.engagementExecutive?.id || null,
  billingManagerId: e.billingManager?.id || null,
  firstYearFeeEstimate: e.firstYearFeeEstimate ?? "",
  realizationPercentage: e.realizationPercentage ?? ""
});
const core = ref(buildCore(props.engagement));

const buildGov = (g) => ({
  contractNumber: g?.contractNumber || "",
  floridaOnePercentStateFeeApplies: g?.floridaOnePercentStateFeeApplies ?? false,
  contractStartDate: g?.contractStartDate || null,
  contractEndDate: g?.contractEndDate || null,
  originalTerm: g?.originalTerm || null,
  renewalTerms: g?.renewalTerms || null,
  purchaseOrderStartDate: g?.purchaseOrderStartDate || null,
  purchaseOrderEndDate: g?.purchaseOrderEndDate || null
});
const gov = ref(buildGov(props.engagement.government));

const buildTax = (t) => ({
  fiscalYearEnd: t?.fiscalYearEnd || "",
  taxFormIds: [...(t?.taxFormIds || [])]
});
const tax = ref(buildTax(props.engagement.tax));

// Re-sync every local form when the parent adopts a fresh engagement view.
watch(() => props.engagement, (e) => {
  core.value = buildCore(e);
  gov.value = buildGov(e.government);
  tax.value = buildTax(e.tax);
});

// ---- Conditional visibility keys off the LOCALLY selected department/service line (immediate) ----
const showAudit = computed(() => isAuditDepartment(core.value.department));
const showTax = computed(() => isTaxDepartment(core.value.department));
const showGovernment = computed(() => isGovernmentAudit(core.value.department, core.value.serviceLine));

const directorName = computed(() => props.engagement.departmentDirector?.name || null);
const departmentChangedUnsaved = computed(() => core.value.department !== props.engagement.department);

const hasCaf = computed(() => !!props.engagement.audit?.clientAcceptanceFormMediaId);
const hasContractDates = computed(() =>
  [gov.value.contractStartDate, gov.value.contractEndDate, gov.value.purchaseOrderStartDate, gov.value.purchaseOrderEndDate]
    .some((d) => !!d));

// The stored tax due-date schedule (JSON on the engagement) → { fiscalYearEnd, originalDueDate, extendedDueDate }.
const dueDates = computed(() => {
  const raw = props.engagement.tax?.calculatedDueDates;
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
});

const feeRules = [(v) => v === "" || v === null || Number(v) >= 0 || "Enter a valid amount"];
const realizationRules = [
  (v) => v === "" || v === null || (Number(v) >= 0 && Number(v) <= 100) || "Enter 0–100"
];

const toNum = (v) => (v === "" || v === null || v === undefined ? null : Number(v));

// ---- Save: core engagement ----
const savingCore = ref(false);
const saveCore = async () => {
  if (!(await formRef.value?.validate())) return;
  savingCore.value = true;
  try {
    const result = await remsApi.updateEngagement(props.engagement.id, {
      department: core.value.department,
      serviceLine: core.value.serviceLine,
      engagementExecutiveId: core.value.engagementExecutiveId,
      billingManagerId: core.value.billingManagerId,
      firstYearFeeEstimate: toNum(core.value.firstYearFeeEstimate),
      realizationPercentage: toNum(core.value.realizationPercentage)
    });
    emit("saved", result.engagement);
    notify.success("Engagement saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingCore.value = false;
  }
};

// ---- Save: audit client-acceptance form (upload media → link the media id) ----
const cafFile = ref(null);
const cafUploading = ref(false);
watch(cafFile, async (file) => {
  if (!file) return;
  cafUploading.value = true;
  try {
    const media = await mediaApi.upload(file, "Document");
    const view = await remsApi.uploadCaf(props.engagement.id, media.id);
    emit("saved", view);
    notify.success("Signed client-acceptance form linked.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    cafFile.value = null;
    cafUploading.value = false;
  }
});

// ---- Save: government-audit contract detail (passing through the copied dates to preserve them) ----
const savingGov = ref(false);
const saveGovernment = async () => {
  savingGov.value = true;
  try {
    const view = await remsApi.updateGovernment(props.engagement.id, { ...gov.value });
    emit("saved", view);
    notify.success("Contract details saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingGov.value = false;
  }
};

// ---- Save: tax detail (fiscal year end recomputes the due dates server-side) ----
const savingTax = ref(false);
const saveTax = async () => {
  savingTax.value = true;
  try {
    const view = await remsApi.updateTax(props.engagement.id, {
      fiscalYearEnd: tax.value.fiscalYearEnd || null,
      taxFormIds: tax.value.taxFormIds
    });
    emit("saved", view);
    notify.success("Tax details saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingTax.value = false;
  }
};

// ---- Copy From: one-time overwrite of address / department / service line / executive / billing manager ----
const copySource = ref(null);
const copying = ref(false);
const copyFrom = async () => {
  if (!copySource.value) return;
  const ok = await confirm({
    title: "Copy engagement setup",
    message: "This overwrites this entity's address, Department, Service Line, Engagement Executive and Billing " +
      "Manager with the selected entity's values. Fee, realization, marketing and approval are not copied. Continue?",
    confirmLabel: "Copy"
  });
  if (!ok) return;
  copying.value = true;
  try {
    await remsApi.copyEngagement(props.engagement.id, copySource.value);
    notify.success("Engagement setup copied.");
    copySource.value = null;
    emit("workspace-refresh");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    copying.value = false;
  }
};
</script>

<style scoped>
.rems-inner { border-radius: 10px; }
.rems-readonly {
  min-height: 40px;
  display: flex;
  align-items: center;
  padding: 6px 12px;
  border: 1px solid #e0e6ed;
  border-radius: 8px;
  background: #f7f9fc;
  color: #2c3540;
  font-size: 14px;
}
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin: 16px 0 8px;
}
.rems-copied {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 6px 24px;
}
.rems-copied__item {
  font-size: 13px;
  color: #2c3540;
}
.rems-copied__item span {
  display: block;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: #7a8699;
}
</style>
