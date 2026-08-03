<template>
  <div>
    <!-- Copy From (AC-REMS-015): only when another entity exists to copy from. -->
    <q-card v-if="editable && otherEntities.length" flat bordered class="rems-inner q-mb-md">
      <!-- Built like every other field row: the same q-col-gutter-md, a labelled field, and items-end so
           the button's baseline meets the input's rather than floating against the label. -->
      <q-card-section class="row items-end q-col-gutter-md q-py-sm">
        <app-select
          v-model="copySource" :options="otherEntities" label="Copy setup from another entity"
          class="col-12 col-sm"
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
          v-model="core.department" :options="deptOptions" label="Department" required class="col-12 col-md-6"
          :readonly="!editable" :clearable="false" :rules="[requiredRule('a Department')]"
        />
        <!-- Government is a Service Line, not a Department (AC-REMS-014.5/6). -->
        <app-select
          v-model="core.serviceLine" :options="serviceLineOptions" label="Service Line" required
          class="col-12 col-md-6" :readonly="!editable" :clearable="false"
          :rules="[requiredRule('a Service Line')]"
        />

        <!-- Read-only Department Director: the selected department's head, resolved as soon as the
             department is picked and written server-side on save (AC-REMS-014.7). -->
        <app-readonly-field
          :model-value="directorName" label="Department Director" placeholder="Not assigned"
          :hint="directorHint" :hint-alert="directorHintAlert" class="col-12 col-md-6"
        />

        <!-- Scoped to the "Engagement Executive" / "Billing Manager" user groups. When a group has no
             members the picker is empty on purpose and the hint names the group to populate. -->
        <app-select
          v-model="core.engagementExecutiveId" :options="executiveOptions" label="Engagement Executive"
          required class="col-12 col-md-6" :readonly="!editable"
          :rules="[requiredRule('an Engagement Executive')]" :hint="executiveHint"
        />
        <app-select
          v-model="core.billingManagerId" :options="billingManagerOptions" label="Billing Manager"
          required class="col-12 col-md-6" :readonly="!editable"
          :rules="[requiredRule('a Billing Manager')]" :hint="billingManagerHint"
        />

        <app-text-field
          v-model="core.firstYearFeeEstimate" label="First-Year Fee Estimate" type="number"
          class="col-12 col-md-6" :readonly="!editable" :rules="feeRules"
        >
          <template #prepend><span class="text-grey-7">$</span></template>
        </app-text-field>
        <app-text-field
          v-model="core.realizationPercentage" label="% Realization" type="number" required
          class="col-12 col-md-6" :readonly="!editable" :rules="realizationRules"
        >
          <template #append><span class="text-grey-7">%</span></template>
        </app-text-field>
      </div>

      <!-- Rendered into the card's title row (see EngagementWorkspace), and only while Setup is the tab
           on screen — the panels are keep-alive, so an inactive tab would otherwise leave its button up
           there. The sub-card saves below stay put: they write different records.
           `defer` is required: the target is rendered by the same tree, so without it the selector runs
           before that DOM exists and the button silently never appears. -->
      <teleport v-if="isVisibleTab && editable" defer to="#engagement-header-actions">
        <q-btn
          unelevated no-caps color="primary" icon-right="o_arrow_forward" label="Save & Next"
          :loading="savingCore" @click="saveCore"
        />
      </teleport>
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
        <!-- Same `row q-col-gutter-md` as every other field row; the checkbox centres itself inside its
             own cell so it lines up with the input beside it rather than with that input's label. -->
        <div class="row q-col-gutter-md">
          <app-text-field
            v-model="gov.contractNumber" label="Contract Number" required class="col-12 col-md-6"
            :readonly="!editable"
          />
          <div class="col-12 col-md-6 column justify-center">
            <q-checkbox
              v-model="gov.floridaOnePercentStateFeeApplies" :disable="!editable"
              label="Florida 1% state fee applies"
            />
          </div>
        </div>

        <!-- Contract / PO dates copied from the submission — shown read-only for context. -->
        <div v-if="hasContractDates" class="rems-copied q-mt-md">
          <div class="rems-copied__item"><span>Contract Start</span>{{ dateOnly(gov.contractStartDate) }}</div>
          <div class="rems-copied__item"><span>Contract End</span>{{ dateOnly(gov.contractEndDate) }}</div>
          <div class="rems-copied__item"><span>PO Start</span>{{ dateOnly(gov.purchaseOrderStartDate) }}</div>
          <div class="rems-copied__item"><span>PO End</span>{{ dateOnly(gov.purchaseOrderEndDate) }}</div>
        </div>

        <div v-if="editable" class="row justify-end q-mt-md">
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
          <app-readonly-field v-if="dueDates" label="Calculated Due Dates" class="col-12 col-md-6">
            Original: {{ dateOnly(dueDates.originalDueDate) }} · Extended: {{ dateOnly(dueDates.extendedDueDate) }}
          </app-readonly-field>
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

        <div v-if="editable" class="row justify-end q-mt-md">
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
import { useVisibleTab } from "composables/useVisibleTab";
import { isAuditDepartment, isTaxDepartment, isGovernmentAudit } from "modules/rems/useRemsMeta";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppReadonlyField from "components/common/AppReadonlyField.vue";
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
  // Tenant department → director map: [{ department, director: { userId, name } }]. A department's
  // director is its department head, set on the user's detail page.
  departmentDirectors: { type: Array, default: () => [] },
  // Members of the "Engagement Executive" / "Billing Manager" user groups — these two pickers are scoped
  // to their group rather than to every admin.
  executiveOptions: { type: Array, default: () => [] },
  billingManagerOptions: { type: Array, default: () => [] },
  editable: { type: Boolean, default: true }
});
// `advance` drives the wizard's Save & Next. Only the CORE save emits it — the conditional contract/tax
// sub-forms below write different records and are not steps of their own.
const emit = defineEmits(["saved", "advance", "workspace-refresh"]);

const notify = useNotify();
const { isVisibleTab } = useVisibleTab();
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

const departmentChangedUnsaved = computed(() => core.value.department !== props.engagement.department);

// The director the CURRENTLY selected department maps to — its department head. Resolved locally so the
// field answers "who will own this?" the moment a department is picked, not only after the save returns.
const mappedDirectorName = computed(() => {
  const dept = core.value.department;
  if (!dept) return null;
  const row = props.departmentDirectors.find((d) => d.department === dept);
  return row?.director?.name || null;
});

// While the selection is unsaved, show where it is heading; otherwise show what is actually stored.
const directorName = computed(() => (departmentChangedUnsaved.value
  ? mappedDirectorName.value
  : props.engagement.departmentDirector?.name || null));

const directorHint = computed(() => {
  if (departmentChangedUnsaved.value) {
    return mappedDirectorName.value
      ? "From the selected department's head — assigned when you save."
      : "The selected department has no head yet — set one on that user's detail page.";
  }
  if (!directorName.value && core.value.department) {
    return "This department has no head yet — set one on that user's detail page, then save again.";
  }
  return "Auto-assigned from the department's head.";
});

// Draw attention while the choice is unsaved, and whenever a department has nobody to direct it.
const directorHintAlert = computed(() =>
  departmentChangedUnsaved.value || (!!core.value.department && !directorName.value));

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

// Department, Service Line, the engagement team and % Realization are mandatory (they are also the
// backend's send-for-approval prerequisites), so Save & Next cannot pass with any of them blank.
const requiredRule = (what) => (v) => (v !== null && v !== undefined && v !== "") || `Select ${what}`;

const feeRules = [(v) => v === "" || v === null || Number(v) >= 0 || "Enter a valid amount"];
const realizationRules = [
  (v) => (v !== "" && v !== null && v !== undefined) || "Enter a % Realization",
  (v) => (Number(v) >= 0 && Number(v) <= 100) || "Enter 0–100"
];

// An empty picker means the group has no members; say which group so the fix is obvious.
const groupHint = (options, group) => (options.length
  ? ""
  : `No members in the "${group}" group — add them in Administration → User Groups.`);
const executiveHint = computed(() => groupHint(props.executiveOptions, "Engagement Executive"));
const billingManagerHint = computed(() => groupHint(props.billingManagerOptions, "Billing Manager"));

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
    // After `saved` so the panel re-evaluates the step rules against the refreshed engagement; it stays
    // put when this save did not actually complete the step (an audit engagement still missing its CAF).
    emit("advance");
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
