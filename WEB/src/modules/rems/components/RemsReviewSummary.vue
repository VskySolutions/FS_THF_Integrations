<template>
  <!-- Read-only, grouped presentation of the in-progress payload for the public form's Review step
       (AC-REMS-024.7): Contact · Contract Details (Government only) · Other Entities · Address ·
       Additional Contacts · Billing. Rendered as plain text — no inputs. Mirrors the admin
       SubmittedFormDialog grouping so the client sees exactly what the admin will. -->
  <div>
    <div v-for="g in groups" :key="g.title" class="review-group">
      <div class="review-group__title">
        <q-icon :name="g.icon" size="18px" class="q-mr-xs" />{{ g.title }}
      </div>

      <div v-if="g.kind === 'fields'" class="review-group__body">
        <div v-for="r in g.rows" :key="r.label" class="field-row">
          <div class="rems-label">{{ r.label }}</div>
          <div class="rems-value">{{ r.value }}</div>
        </div>
      </div>

      <div v-else-if="g.kind === 'contacts'">
        <div v-if="g.rows.length" class="column q-gutter-sm">
          <div v-for="r in g.rows" :key="r.role" class="role-row">
            <div class="rems-label">{{ r.role }}<span v-if="r.isRequired" class="text-red-6"> *</span></div>
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
  </div>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  payload: { type: Object, required: true },
  industryGroup: { type: String, default: "" },
  // The locked request email, shown on the Contact row (the payload email is a courtesy echo only).
  lockedEmail: { type: String, default: "" }
});

const isIndividual = computed(() => props.industryGroup === "individual");
const isBusiness = computed(() => props.industryGroup === "business");
const isGovernment = computed(() => props.industryGroup === "government");

const val = (v) => (v == null || String(v).trim() === "" ? "—" : v);

// Calendar-date fields (DateOnly "YYYY-MM-DD") are shown as-is (MM-DD-YYYY), never timezone-shifted.
const dateOnly = (v) => {
  if (!v) return "—";
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(v));
  return m ? `${m[2]}-${m[3]}-${m[1]}` : String(v);
};

const hasAddress = (a) => !!a && [a?.street, a?.city, a?.state, a?.zip].some((x) => x && String(x).trim());
const addressText = (a) => {
  if (!a) return "—";
  const line2 = [a.city, a.state, a.zip].filter((x) => x && String(x).trim()).join(" ");
  const parts = [a.street, line2].filter((x) => x && String(x).trim());
  return parts.length ? parts.join(", ") : "—";
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
// Required role per industry group — drives the "*" marker on the review contact rows.
const REQUIRED_ROLES = {
  individual: ["self"],
  business: ["ceo", "cfo", "accountsPayable"],
  government: ["financeDirector"]
};
// The roles relevant to each industry group, in display order.
const GROUP_ROLES = {
  individual: ["self", "spouse"],
  business: ["ceo", "cfo", "accountsPayable", "banker", "lawyer"],
  government: ["financeDirector", "accountsPayable"]
};
const roleHasAny = (r) => !!r && [r?.name, r?.email, r?.phone].some((x) => x && String(x).trim());

const groups = computed(() => {
  const p = props.payload || {};
  const result = [];

  // Contact
  const contact = [
    { label: "Client Name", value: val(p.clientName) },
    { label: "Email (locked)", value: val(props.lockedEmail || p.email) },
    { label: "Mobile Number", value: val(p.mobileNumber) },
    { label: "Referral Source", value: val(p.referralSource) }
  ];
  if (isIndividual.value) {
    contact.push({ label: "Spouse Name", value: val(p.spouseName) });
    contact.push({ label: "Spouse Phone", value: val(p.spousePhone) });
  }
  if (isBusiness.value) contact.push({ label: "EIN", value: val(p.ein) });
  result.push({ title: "Contact", icon: "o_person", kind: "fields", rows: contact });

  // Contract Details (Government)
  if (isGovernment.value) {
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

  // Additional Contacts (role contacts, in group order)
  const roles = p.roles || {};
  const order = GROUP_ROLES[props.industryGroup] || [];
  const required = REQUIRED_ROLES[props.industryGroup] || [];
  const contactRows = order
    .filter((k) => roleHasAny(roles[k]))
    .map((k) => ({ role: ROLE_LABELS[k], isRequired: required.includes(k), ...roles[k] }));
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
</script>

<style scoped>
.review-group {
  margin-bottom: 18px;
}
.review-group__title {
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
.review-group__body {
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
