<template>
  <q-dialog v-model="open">
    <q-card style="width: 640px; max-width: 92vw;">
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
          <!-- Summary. No entity-type badge: this dialog is the snapshot of what the CLIENT submitted,
               and the entity type is THF's own classification — never something they were asked. It is
               read on the request's Client Information tab, where it is set. -->
          <div class="row items-center q-col-gutter-sm q-mb-md">
            <div class="col text-grey-8">
              <span class="text-weight-medium">{{ payload.clientName || view.remsNumber }}</span>
              <span class="text-grey-6"> · {{ view.remsNumber }}</span>
            </div>
            <div class="col-auto text-caption text-grey-7">Submitted {{ fmt.formatDateTime(view.submittedOnUtc) }}</div>
          </div>

          <!-- Grouped exactly per AC-REMS-012.1 / 024.7: Contact · Addresses · Contract Details ·
               Additional Contacts · Other Entities · Billing — the order the client was asked, so the
               admin reads the answers as they were given. Plain read-only text (AC-REMS-013.2). -->
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
                  <div class="rems-value text-weight-medium">{{ e.name || "Unnamed entity" }}</div>
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
import { useRemsMeta, isBusinessIndustryGroup } from "modules/rems/useRemsMeta";
import { addressText } from "modules/rems/remsAddress";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  remsId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue"]);

const fmt = useDateFormat();
const { referralSourceLabel, referralSourceHint } = useRemsMeta();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const view = ref(null);
const loading = ref(false);
const errorMsg = ref("");

const payload = computed(() => view.value?.payload || {});
const isIndividual = computed(() => view.value?.industryGroup === "individual");
const isBusiness = computed(() => isBusinessIndustryGroup(view.value?.industryGroup));
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
    { label: "Phone Number", value: val(p.mobileNumber) },
    { label: "Referral Source", value: referralSourceLabel(p.referralSource), hint: referralSourceHint(p.referralSource) },
    { label: "Referral Details", value: val(p.referralSourceDetail) }
  ];
  if (isIndividual.value) {
    // Retired from the form — the spouse is a contact now, and shows under Additional Contacts. Still
    // rendered when a snapshot carries them: this dialog is the record of what that client actually
    // submitted, so it shows what was in the envelope.
    if (p.spouseName) contact.push({ label: "Spouse Name", value: p.spouseName });
    if (p.spouseEmail) contact.push({ label: "Spouse Email Address", value: p.spouseEmail });
    if (p.spousePhone) contact.push({ label: "Spouse Phone", value: p.spousePhone });
  }
  if (isBusiness.value) contact.push({ label: "EIN", value: val(p.ein) });
  result.push({ title: "Contact", icon: "o_person", kind: "fields", rows: contact });

  // Addresses — three of them, each stored in its own right. This used to show one address and a
  // "Mailing address differs?" answer, from a time when the form asked exactly that; it now asks for
  // three and offers a copy button, and `mailingDiffers` is never written. Branching on it hid the
  // mailing address the client actually gave behind a flag that is always false.
  result.push({
    title: "Addresses",
    icon: "o_place",
    kind: "fields",
    rows: [
      { label: "Physical Address", value: addressText(p.physicalAddress) },
      { label: "Mailing Address", value: addressText(p.mailingAddress) },
      { label: "Billing Address", value: addressText(p.billingAddress) }
    ]
  });

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

  // Additional Contacts (role contacts)
  const roles = p.roles || {};
  const contactRows = ROLE_ORDER.filter((k) => roleHasAny(roles[k])).map((k) => ({ role: ROLE_LABELS[k], ...roles[k] }));
  result.push({ title: "Additional Contacts", icon: "o_groups", kind: "contacts", rows: contactRows });

  // Other Entities — a contact each, not a second set of business details. The payload node is
  // { fullName, emailAddress, phoneNumber }: the business name, EIN and addresses went when an additional
  // entity became a CONTACT to raise a separate request from. This read the retired shape, so the filter
  // dropped every row and the dialog reported no entities over the ones the client had named.
  const entities = (p.relatedEntities || [])
    .filter((e) => [e.fullName, e.emailAddress, e.phoneNumber].some((x) => x && String(x).trim()))
    .map((e, i) => {
      const rows = [];
      if (e.emailAddress) rows.push({ label: "Email Address", value: e.emailAddress });
      if (e.phoneNumber) rows.push({ label: "Phone Number", value: e.phoneNumber });
      return { key: e.sourceKey || `entity-${i}`, name: e.fullName, rows };
    });
  result.push({ title: "Other Entities", icon: "o_apartment", kind: "entities", rows: entities });

  // Billing — the person to bill. The billing address is shown once, up in Addresses with the other two.
  result.push({
    title: "Billing",
    icon: "o_receipt_long",
    kind: "fields",
    rows: [
      { label: "Billing Contact", value: val(p.billingContactName) },
      { label: "Billing Email", value: val(p.billingEmail) }
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
  /* auto-fit rather than a hard pair: two columns of a phone-width dialog leave each answer about a
     hundred and thirty pixels wide, which wraps an email address over three lines. Under the minimum it
     drops to one column and every label keeps its value beside it. */
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
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
