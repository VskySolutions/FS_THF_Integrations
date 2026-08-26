<template>
  <div>
    <!-- Core engagement placement + team + fee/realization (AC-REMS-014.5-10).
         The column widths ARE the grouping: 4+4+4 wraps to a trio per line and 6+6 to a pair, so the
         lines below read as what they are — what the firm does and where the work sits, who runs it, what
         it is worth, and how it is billed.
         What the CLIENT is — Entity Type and Industry — is not here: both describe the client rather than
         the engagement, so they are asked on the Client Information tab with the rest of what is known
         about them. This form still reads the entity type, because the Government Audit rule keys off it
         together with the department below, but as a prop it does not own. -->
    <q-form ref="formRef" greedy>
      <div class="row q-col-gutter-md">
        <!-- ── What the firm does, where the work sits, and who heads that ──────────────────────── -->
        <app-select
          v-model="core.subServiceLine" :options="subServiceLineOptions" label="Service Line" required
          class="col-12 col-sm-4" :readonly="!editable" :clearable="false"
          :rules="[requiredRule('a Service Line')]"
          info="From the REMS Service Line option list (Administration → Option Sets). What the firm is actually engaged to do."
        />
        <app-select
          v-model="core.department" :options="deptOptions" label="Department" required class="col-12 col-sm-4"
          :readonly="!editable" :clearable="false" :rules="[requiredRule('a Department')]"
          info="From the REMS Department option list (Administration → Option Sets). The choice drives the conditional detail below: Audit needs a signed CAF, Tax a fiscal year end."
        />
        <!-- Read-only Department Director: the selected department's head, resolved as soon as the
             department is picked and written server-side on save (AC-REMS-014.7). Beside the department
             it is derived from, so the pair reads as one answer and its second half explains itself. -->
        <app-readonly-field
          :model-value="directorName" label="Department Director" placeholder="Not assigned"
          :hint="directorHint" :hint-alert="directorHintAlert" class="col-12 col-sm-4"
        />

        <!-- ── The three people who run it ──────────────────────────────────────────────────────── -->
        <!-- Each is scoped to the ROLE of its own name. When nobody holds the role the picker is empty on
             purpose and the hint names the role to assign. -->
        <app-select
          :model-value="cseUserId" :options="cseOptions" label="CSE" required
          class="col-12 col-sm-4" :readonly="!editable" :clearable="false" :hint="cseHint"
          info="Users holding the &quot;CSE&quot; role. The CSE becomes an approver on this engagement."
          @update:model-value="$emit('update:cseUserId', $event)"
        />
        <app-select
          v-model="core.engagementExecutiveId" :options="executiveOptions" label="Engagement Executive"
          required class="col-12 col-sm-4" :readonly="!editable"
          :rules="[requiredRule('an Engagement Executive')]" :hint="executiveHint"
          info="Lists users holding the &quot;Engagement Executive&quot; role, assigned on a user's page in Administration → Users."
        />
        <app-select
          v-model="core.billingManagerId" :options="billingManagerOptions" label="Billing Manager"
          required class="col-12 col-sm-4" :readonly="!editable"
          :rules="[requiredRule('a Billing Manager')]" :hint="billingManagerHint"
          info="Lists users holding the &quot;Billing Manager&quot; role, assigned on a user's page in Administration → Users."
        />

        <!-- ── What it is worth ─────────────────────────────────────────────────────────────────── -->
        <app-text-field
          v-model="core.firstYearFeeEstimate" label="First-Year Fee Estimate" type="number"
          class="col-12 col-sm-6" :readonly="!editable" :rules="feeRules"
        >
          <template #prepend><span class="text-grey-7">$</span></template>
        </app-text-field>
        <app-text-field
          v-model="core.realizationPercentage" label="% Realization" type="number" required
          class="col-12 col-sm-6" :readonly="!editable" :rules="realizationRules"
        >
          <template #append><span class="text-grey-7">%</span></template>
        </app-text-field>

        <!-- ── How it is billed ─────────────────────────────────────────────────────────────────── -->
        <!-- How often, and how it actually works. The second was a COUNT — "No. of Bills" — which said
             how many invoices without saying what triggered one, and could not record a schedule that
             does not reduce to a number at all ("three progress bills, the balance on delivery"). It is
             the sentence now, and the frequency beside it offers Milestone for the schedules that are
             not a calendar cycle. -->
        <app-select
          v-model="core.billingPeriod" :options="billingPeriodOptions" label="Billing Frequency"
          class="col-12 col-sm-4" :readonly="!editable"
          info="From the REMS Billing Frequency option list (Administration → Option Sets)."
        />
        <app-text-field
          v-model="core.billingProcessDescription" label="Description of Billing Process"
          class="col-12 col-sm-8" :readonly="!editable" autogrow
          placeholder="e.g. three progress bills against the fixed fee, the balance on delivery"
          :rules="billingDescriptionRules"
        />
      </div>

      <!-- No save button of its own. A page that asks to be saved in pieces — client details here, setup
           there — is the one thing this form is meant not to do: it has ONE Save, and that writes this
           section and the conditional cards below along with everything else. -->

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
          <q-banner v-else-if="cafFile" dense class="bg-teal-1 text-teal-9 rounded-borders q-mb-sm">
            <template #avatar><q-icon name="o_upload_file" color="teal-9" /></template>
            The signed client-acceptance form is attached when you save this request.
          </q-banner>
          <q-banner v-else dense class="bg-orange-1 text-orange-9 rounded-borders q-mb-sm">
            <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
            A signed client-acceptance form (PDF) is required before this audit engagement can be sent for approval.
          </q-banner>
          <app-single-file-upload
            v-if="editable"
            v-model="cafFile" accept=".pdf" :max-size-mb="25"
            :label="hasCaf ? 'Replace signed CAF (PDF)' : 'Upload signed CAF (PDF)'"
            hint="PDF up to 25 MB"
          />
        </q-card-section>
      </q-card>

      <!-- Conditional: Government Audit (Department=audit + Entity Type=government) → contract number +
           Florida 1% state-fee flag (AC-REMS-014.13). Saved by Save & Next along with everything else. -->
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
              v-model="gov.contractNumber" label="Contract Number" required class="col-12 col-sm-6"
              :readonly="!editable"
            />
            <div class="col-12 col-sm-6 column justify-center">
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
        </q-card-section>
      </q-card>

      <!-- Conditional: Tax → fiscal year end + calculated due dates + tax-form checklist (AC-REMS-014.14).
           Saved by Save & Next along with everything else. -->
      <q-card v-if="showTax" flat bordered class="rems-inner q-mt-md">
        <q-card-section class="q-py-sm text-subtitle2 text-primary">
          <q-icon name="o_receipt_long" size="18px" class="q-mr-xs" />Tax — Fiscal Year &amp; Forms
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div class="row q-col-gutter-md">
            <app-date-field
              v-model="tax.fiscalYearEnd" label="Fiscal Year End" class="col-12 col-sm-6" :readonly="!editable"
            />
            <app-readonly-field v-if="dueDates" label="Calculated Due Dates" class="col-12 col-sm-6">
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
        </q-card-section>
      </q-card>
    </q-form>
  </div>
</template>

<script setup>
// The request's engagement setup (AC-REMS-014/015): what the firm does, where the work sits and the mapped
// department director (read-only), the engagement team, fee/realization, and the conditional Audit /
// Government / Tax forms.
//
// The two classifications that describe THE CLIENT — Entity Type and Industry — are asked on the Client
// Information tab instead. The entity type still arrives here as a prop, because the Government Audit card
// keys off it together with the department chosen below.
//
// Service Line is labelled here differently from the data it holds — it is `subServiceLine`. The note at
// the top of useRemsMeta says why the data kept its name.
//
// Controlled by the page rather than saving itself. It holds the fields, announces every edit (`change`)
// and exposes saveSetup(engagementId, remsId) for the page's auto-save to call — which is also why the
// engagement id is an argument rather than read off the prop: a request created moments ago has one only
// once the page has filed it. `remsId` rides along because the signed CAF is filed under the request on
// the server, not under the engagement: one request has one engagement, so one folder holds both.
import { ref, computed, watch, nextTick } from "vue";
import { remsApi, mediaApi } from "services/api";
import { isAuditDepartment, isTaxDepartment, isGovernmentAudit } from "modules/rems/useRemsMeta";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppReadonlyField from "components/common/AppReadonlyField.vue";
import AppSingleFileUpload from "components/common/AppSingleFileUpload.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  deptOptions: { type: Array, default: () => [] },
  // Rendered as "Service Line"; still named for the data behind it. See the note at the top of useRemsMeta.
  subServiceLineOptions: { type: Array, default: () => [] },
  taxFormOptions: { type: Array, default: () => [] },
  taxFormUnavailable: { type: Boolean, default: false },
  billingPeriodOptions: { type: Array, default: () => [] },
  // Tenant department → director map: [{ department, director: { userId, name } }]. A department's
  // director is its department head, set on the user's detail page.
  departmentDirectors: { type: Array, default: () => [] },
  // Holders of the "Engagement Executive" / "Billing Manager" roles — these two pickers are scoped to the
  // seat they fill rather than to every admin.
  executiveOptions: { type: Array, default: () => [] },
  billingManagerOptions: { type: Array, default: () => [] },
  editable: { type: Boolean, default: true },

  // The CSE is NOT the engagement's. It lives on the request's EMS form record — what the client's invite
  // is minted from — and is written by a different endpoint, so it is v-modelled through to the page
  // rather than held in `core` below. It is rendered here because it belongs with the other two people
  // who run the engagement: reading who owns this work should not mean reading it in two places.
  cseUserId: { type: String, default: null },
  cseOptions: { type: Array, default: () => [] },
  cseHint: { type: String, default: "" },
  // Read-only here, and owned by the Client Information tab. Present because the Government Audit card
  // below appears only for an Audit department on a Government entity.
  industryGroup: { type: String, default: null }
});
// `change` says the engagement half has something to save — the page cannot see the local copies below.
// The CSE is an ordinary v-model update: the page owns that value and writes it itself.
const emit = defineEmits(["change", "update:cseUserId"]);
const formRef = ref(null);

// Set while this component is writing to its own state rather than the user: re-seeding from a fresh
// engagement view, or clearing the CAF picker once its file is uploaded. Neither is an edit, and
// announcing them would queue a save of what was just saved.
let syncing = false;

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
  // No `subIndustry`: the industry is asked on the Client Information tab and written by the page.
  // Leaving a field out of the payload is what preserves it — the endpoint reads an omitted field as
  // "leave this alone" — so this form saving cannot undo it.
  subServiceLine: e.subServiceLine || null,
  engagementExecutiveId: e.engagementExecutive?.id || null,
  billingManagerId: e.billingManager?.id || null,
  firstYearFeeEstimate: e.firstYearFeeEstimate ?? "",
  realizationPercentage: e.realizationPercentage ?? "",
  billingPeriod: e.billingPeriod || null,
  billingProcessDescription: e.billingProcessDescription ?? ""
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
  syncing = true;
  core.value = buildCore(e);
  gov.value = buildGov(e.government);
  tax.value = buildTax(e.tax);
  nextTick(() => { syncing = false; });
});

// ---- Conditional visibility keys off the LOCALLY selected department (immediate) ----
const showAudit = computed(() => isAuditDepartment(core.value.department));
const showTax = computed(() => isTaxDepartment(core.value.department));
// The entity type is the page's, not this form's local copy — it is saved by a different endpoint — so
// this reads the prop. Same rule the API applies when the round is routed.
const showGovernment = computed(() => isGovernmentAudit(core.value.department, props.industryGroup));

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

// The map travels with the workspace, which a request being created does not have yet. Saying a department
// has no head would be a claim this page cannot make from an empty map — it only knows it cannot answer.
const directorsKnown = computed(() => props.departmentDirectors.length > 0);

const directorHint = computed(() => {
  if (!directorsKnown.value) return "Assigned from the selected department's head when you save.";
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
  directorsKnown.value && (departmentChangedUnsaved.value || (!!core.value.department && !directorName.value)));

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

// Service Line, Department, the engagement team and % Realization are mandatory (they are also the
// backend's send-for-approval prerequisites), so Save & Next cannot pass with any of them blank.
//
// Service Line is among them because it is what the firm is actually engaged to DO — an engagement routed
// for approval without one asks the approvers to sign off a piece of work nobody has named.
const requiredRule = (what) => (v) => (v !== null && v !== undefined && v !== "") || `Select ${what}`;

const feeRules = [(v) => v === "" || v === null || Number(v) >= 0 || "Enter a valid amount"];
// A description of how the client is billed, not a treatise. Mirrors the column and the API validator.
const BILLING_DESCRIPTION_MAX = 1000;
const billingDescriptionRules = [
  (v) => (v ?? "").length <= BILLING_DESCRIPTION_MAX ||
    `Keep the billing process to ${BILLING_DESCRIPTION_MAX} characters or fewer.`
];
const realizationRules = [
  (v) => (v !== "" && v !== null && v !== undefined) || "Enter a % Realization",
  (v) => (Number(v) >= 0 && Number(v) <= 100) || "Enter 0–100"
];

// An empty picker means nobody holds the role; say which role so the fix is obvious.
const seatHint = (options, role) => (options.length
  ? ""
  : `Nobody holds the "${role}" role — assign it on a user's page in Administration → Users.`);
const executiveHint = computed(() => seatHint(props.executiveOptions, "Engagement Executive"));
const billingManagerHint = computed(() => seatHint(props.billingManagerOptions, "Billing Manager"));

const toNum = (v) => (v === "" || v === null || v === undefined ? null : Number(v));

// ---- Save, driven by the page ----
// The whole section in one write: the core first (it is what decides whether the conditional cards apply
// at all), then each card that is on screen, then the signed CAF if one is waiting. Called by the page's
// Save with the engagement id, which on a brand-new request only exists once the request has been created
// — the reason this is a method the page calls rather than a button of its own.
//
// Saves what has been filled, not only a complete setup: an initiator entering a referral may not know
// the fee or the billing manager yet, and refusing to store the half they do know would be the two-step
// form the page exists to replace. The fields still carry their `required` markers, because they ARE
// required — of an engagement being sent for approval, which is where the API enforces them.
//
// Ranges are checked all the same. A blank fee means "not known yet"; a fee of -5 or a realization of
// 300% is wrong however early it is typed, and both the API and the DB reject them.
const saveSetup = async (engagementId, remsId = null) => {
  if (!validateFormats()) {
    throw new Error(
      "Check the engagement setup: the fee cannot be negative, realization is 0–100%, and the billing " +
      `process description is at most ${BILLING_DESCRIPTION_MAX} characters.`);
  }

  let view = (await remsApi.updateEngagement(engagementId, {
    department: core.value.department,
    // Empty string rather than null for the clearable one: the endpoint reads null as "leave this field
    // alone" and only an empty value clears it, so sending null would make Clear look like it worked and
    // then bring the old value back on the next read.
    subServiceLine: core.value.subServiceLine ?? "",
    engagementExecutiveId: core.value.engagementExecutiveId,
    billingManagerId: core.value.billingManagerId,
    firstYearFeeEstimate: toNum(core.value.firstYearFeeEstimate),
    realizationPercentage: toNum(core.value.realizationPercentage),
    billingPeriod: core.value.billingPeriod,
    // Empty string rather than null, like the clearable code above it: the endpoint reads null as
    // "leave this field alone", so a description taken back out would otherwise come back on the next read.
    billingProcessDescription: core.value.billingProcessDescription ?? ""
  })).engagement;

  if (showGovernment.value) {
    view = await remsApi.updateGovernment(engagementId, { ...gov.value });
  }
  if (showTax.value) {
    // The fiscal year end recomputes the due-date schedule server-side.
    view = await remsApi.updateTax(engagementId, {
      fiscalYearEnd: tax.value.fiscalYearEnd || null,
      taxFormIds: tax.value.taxFormIds
    });
  }
  if (cafFile.value) {
    const media = await mediaApi.upload(
      cafFile.value, "ClientAcceptance", remsId ? { type: "Rems", id: remsId } : null);
    view = await remsApi.uploadCaf(engagementId, media.id);
    syncing = true;
    cafFile.value = null;
    await nextTick();
    syncing = false;
  }
  return view;
};

// The range rules only. Quasar validates a whole form or nothing, and its rules here also enforce
// "required", so the ranges are re-stated rather than borrowed.
const validateFormats = () => {
  const blank = (v) => v === "" || v === null || v === undefined;
  const inRange = (v, min, max) =>
    blank(v) || (Number.isFinite(Number(v)) && Number(v) >= min && Number(v) <= max);
  return inRange(core.value.firstYearFeeEstimate, 0, Number.MAX_SAFE_INTEGER) &&
    inRange(core.value.realizationPercentage, 0, 100) &&
    (core.value.billingProcessDescription ?? "").length <= BILLING_DESCRIPTION_MAX;
};

// ---- The signed client-acceptance form ----
// Held rather than uploaded on selection: on a brand-new request there is no engagement to link it to yet,
// and on an existing one it belongs with the same Save as everything else the user just typed.
const cafFile = ref(null);

// Declared here rather than beside the re-seed watcher above because the CAF picker is part of what the
// page saves, and it is not in scope until now.
watch([core, gov, tax, cafFile], () => {
  if (!syncing) emit("change");
}, { deep: true });

defineExpose({ saveSetup });
</script>

<style scoped>
.rems-inner { border-radius: 10px; }
.rems-copied {
  display: grid;
  /* auto-fit rather than a fixed pair: four dates in two columns on a phone leaves each of them about
     eighty pixels wide, which is narrower than the date they hold. */
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
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
