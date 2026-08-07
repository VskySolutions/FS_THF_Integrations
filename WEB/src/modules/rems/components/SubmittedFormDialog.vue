<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 640px; max-width: 92vw;">
      <q-card-section class="row items-center no-wrap">
        <div>
          <div class="text-h6">Submitted EMS Form</div>
          <div class="text-caption text-grey-7">Read-only snapshot of exactly what the client submitted.</div>
        </div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="open = false" />
      </q-card-section>
      <q-separator />

      <q-card-section style="max-height: 74vh; overflow: auto;">
        <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="32px" /></div>

        <q-banner v-else-if="errorMsg" dense class="bg-red-1 text-red-9 rounded-borders">
          <template #avatar><q-icon name="o_error" color="red-9" /></template>
          {{ errorMsg }}
        </q-banner>

        <template v-else-if="view">
          <!-- Summary -->
          <div class="row items-center q-col-gutter-sm q-mb-md">
            <div class="col-auto">
              <q-badge :color="statusColor('customer_submitted')">{{ industryGroupLabel(view.industryGroup) }}</q-badge>
            </div>
            <div class="col text-grey-8">
              <span class="text-weight-medium">{{ payload.clientName || view.remsNumber }}</span>
              <span class="text-grey-6"> · {{ view.remsNumber }}</span>
            </div>
            <div class="col-auto text-caption text-grey-7">Submitted {{ fmt.formatDateTime(view.submittedOnUtc) }}</div>
          </div>

          <!-- Grouped exactly per AC-REMS-012.1 / 024.7: Contact · Contract Details · Other Entities ·
               Address · Additional Contacts · Billing. Rendered as plain read-only text (AC-REMS-013.2). -->
          <div v-for="g in groups" :key="g.title" class="submitted-group">
            <div class="submitted-group__title">
              <q-icon :name="g.icon" size="18px" class="q-mr-xs" />{{ g.title }}
            </div>

            <div v-if="g.kind === 'fields'" class="submitted-group__body">
              <div v-for="r in g.rows" :key="r.label" class="field-row">
                <div class="rems-label">{{ r.label }}</div>
                <div class="rems-value">{{ r.value }}</div>
              </div>
            </div>

            <div v-else-if="g.kind === 'contacts'">
              <div v-if="g.rows.length" class="column q-gutter-sm">
                <div v-for="r in g.rows" :key="r.role" class="role-row">
                  <div class="rems-label">{{ r.role }}</div>
                  <div class="rems-value">{{ r.name || "—" }}</div>
                  <div class="text-caption text-grey-7">{{ r.email || "no email" }} · {{ r.phone || "no phone" }}</div>
                </div>
              </div>
              <div v-else class="text-grey-6">No additional contacts provided.</div>
            </div>

            <div v-else-if="g.kind === 'entities'">
              <div v-if="g.rows.length" class="column q-gutter-sm">
                <q-card v-for="(e, i) in g.rows" :key="e.key || i" flat bordered class="q-pa-sm entity-card">
                  <div class="rems-value text-weight-medium">{{ e.businessName || "Unnamed entity" }}</div>
                  <div v-for="r in e.rows" :key="r.label" class="field-row field-row--dense">
                    <div class="rems-label">{{ r.label }}</div>
                    <div class="rems-value">{{ r.value }}</div>
                  </div>
                </q-card>
              </div>
              <div v-else class="text-grey-6">No related entities provided.</div>
            </div>
          </div>
        </template>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right">
        <q-btn unelevated no-caps color="primary" label="Close" @click="open = false" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";
import { hasAddress, addressText } from "modules/rems/remsAddress";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  remsId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue"]);

const fmt = useDateFormat();
const { industryGroupLabel, statusColor } = useRemsMeta();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const view = ref(null);
const loading = ref(false);
const errorMsg = ref("");

const payload = computed(() => view.value?.payload || {});
const isIndividual = computed(() => view.value?.industryGroup === "individual");
const isBusiness = computed(() => view.value?.industryGroup === "business");
const isGovernment = computed(() => view.value?.industryGroup === "government");

const val = (v) => (v == null || v === "" ? "—" : v);

// Calendar-date fields (DateOnly "YYYY-MM-DD") are shown as-is (MM-DD-YYYY), never timezone-shifted.
const dateOnly = (v) => {
  if (!v) return "—";
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(v));
  return m ? `${m[2]}-${m[3]}-${m[1]}` : String(v);
};

const ROLE_LABELS = {
  self: "Self",
  spouse: "Spouse",
  ceo: "CEO",
  cfo: "CFO",
  accountsPayable: "Accounts Payable",
  banker: "Banker",
  lawyer: "Lawyer",
  financeDirector: "Finance Director"
};
const ROLE_ORDER = ["self", "spouse", "ceo", "cfo", "accountsPayable", "banker", "lawyer", "financeDirector"];
const roleHasAny = (r) => !!r && [r?.name, r?.email, r?.phone].some((x) => x && String(x).trim());

const groups = computed(() => {
  const p = payload.value;
  const result = [];

  // Contact
  const contact = [
    { label: "Client Name", value: val(p.clientName) },
    { label: "Email (locked)", value: val(view.value?.lockedEmail || p.email) },
    { label: "Mobile Number", value: val(p.mobileNumber) },
    { label: "Referral Source", value: val(p.referralSource) }
  ];
  if (isIndividual.value) {
    contact.push({ label: "Spouse Name", value: val(p.spouseName) });
    contact.push({ label: "Spouse Phone", value: val(p.spousePhone) });
  }
  if (isBusiness.value) contact.push({ label: "EIN", value: val(p.ein) });
  result.push({ title: "Contact", icon: "o_person", kind: "fields", rows: contact });

  // Contract Details (Government / when any contract field present)
  const anyContract = [p.contractStartDate, p.contractEndDate, p.originalTerm, p.renewalTerms, p.poStartDate, p.poEndDate]
    .some((x) => x != null && x !== "");
  if (isGovernment.value || anyContract) {
    result.push({
      title: "Contract Details",
      icon: "o_gavel",
      kind: "fields",
      rows: [
        { label: "Contract Start Date", value: dateOnly(p.contractStartDate) },
        { label: "Contract End Date", value: dateOnly(p.contractEndDate) },
        { label: "Original Term", value: val(p.originalTerm) },
        { label: "Renewal Terms", value: val(p.renewalTerms) },
        { label: "PO Start Date", value: dateOnly(p.poStartDate) },
        { label: "PO End Date", value: dateOnly(p.poEndDate) }
      ]
    });
  }

  // Other Entities (related businesses)
  const entities = (p.relatedEntities || [])
    .filter((e) => [e.businessName, e.ein, e.contactName].some((x) => x && String(x).trim()) ||
      hasAddress(e.physicalAddress) || hasAddress(e.mailingAddress))
    .map((e, i) => {
      const rows = [];
      if (e.ein) rows.push({ label: "EIN", value: e.ein });
      if (e.contactName) rows.push({ label: "Contact", value: e.contactName });
      if (hasAddress(e.physicalAddress)) rows.push({ label: "Physical Address", value: addressText(e.physicalAddress) });
      if (hasAddress(e.mailingAddress)) rows.push({ label: "Mailing Address", value: addressText(e.mailingAddress) });
      return { key: e.sourceKey || `entity-${i}`, businessName: e.businessName, rows };
    });
  result.push({ title: "Other Entities", icon: "o_apartment", kind: "entities", rows: entities });

  // Address
  const address = [
    { label: "Physical Address", value: addressText(p.physicalAddress) },
    { label: "Mailing address differs?", value: p.mailingDiffers ? "Yes" : "No" }
  ];
  if (p.mailingDiffers) address.push({ label: "Mailing Address", value: addressText(p.mailingAddress) });
  result.push({ title: "Address", icon: "o_place", kind: "fields", rows: address });

  // Additional Contacts (role contacts)
  const roles = p.roles || {};
  const contactRows = ROLE_ORDER.filter((k) => roleHasAny(roles[k])).map((k) => ({ role: ROLE_LABELS[k], ...roles[k] }));
  result.push({ title: "Additional Contacts", icon: "o_groups", kind: "contacts", rows: contactRows });

  // Billing
  result.push({
    title: "Billing",
    icon: "o_receipt_long",
    kind: "fields",
    rows: [
      { label: "Billing Contact", value: val(p.billingContactName) },
      { label: "Billing Email", value: val(p.billingEmail) },
      { label: "Billing Address", value: addressText(p.billingAddress) }
    ]
  });

  return result;
});

const load = async () => {
  if (!props.remsId) return;
  loading.value = true;
  errorMsg.value = "";
  view.value = null;
  try {
    view.value = await remsApi.submission(props.remsId);
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
  } finally {
    loading.value = false;
  }
};

watch(() => props.modelValue, (isOpen) => { if (isOpen) load(); });
</script>

<style scoped>
.submitted-group {
  margin-bottom: 18px;
}
.submitted-group__title {
  display: flex;
  align-items: center;
  font-size: 13px;
  font-weight: 600;
  color: var(--q-primary);
  text-transform: uppercase;
  letter-spacing: 0.03em;
  border-bottom: 1px solid #e0e6ed;
  padding-bottom: 4px;
  margin-bottom: 10px;
}
.submitted-group__body {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 24px;
}
.field-row--dense {
  margin-top: 4px;
}
.rems-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: #7a8699;
  margin-bottom: 2px;
}
.rems-value {
  font-size: 14px;
  color: #2c3540;
  word-break: break-word;
  white-space: pre-wrap;
}
</style>
