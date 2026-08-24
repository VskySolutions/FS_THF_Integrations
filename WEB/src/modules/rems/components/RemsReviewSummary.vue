<template>
  <!-- Read-only, grouped presentation of the in-progress payload for the public form's Review step
       (AC-REMS-024.7): Contact · Addresses · Contract Details (Government only) · Additional Contacts ·
       Other Entities · Billing. That is the order the form asks the questions in, card for card, so
       checking an answer here means looking where it was typed. Rendered as plain text — no inputs.
       Mirrors the admin SubmittedFormDialog grouping so the client sees exactly what the admin will. -->
  <div>
    <div v-for="g in groups" :key="g.title" class="review-group">
      <div class="review-group__title">
        <q-icon :name="g.icon" size="18px" class="q-mr-xs" />{{ g.title }}
      </div>

      <div v-if="g.kind === 'fields'" class="review-group__body">
        <div v-for="r in g.rows" :key="r.label" class="field-row">
          <div class="rems-label">{{ r.label }}</div>
          <div class="rems-value">
            {{ r.value }}
            <!-- The option item's own description, where the value came from a list that has one. -->
            <template v-if="r.hint">
              <q-icon name="o_info" size="14px" class="rems-value__info" />
              <q-tooltip anchor="top middle" self="bottom middle" max-width="320px" :delay="300">
                {{ r.hint }}
              </q-tooltip>
            </template>
          </div>
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
            <div class="rems-value text-weight-medium">{{ e.name || "Unnamed entity" }}</div>
            <div v-for="r in e.rows" :key="r.label" class="field-row field-row--dense">
              <div class="rems-label">{{ r.label }}</div>
              <div class="rems-value">{{ r.value }}</div>
            </div>
          </q-card>
        </div>
        <div v-else class="text-grey-6">No other entities provided.</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from "vue";
import { addressText } from "modules/rems/remsAddress";
import { isBusinessIndustryGroup } from "modules/rems/useRemsMeta";
import {
  answeredRoleKeys, groupKey, normalizeRoles, roleDefsFor, roleDisplayName, roleHasAny
} from "modules/rems/remsContactRoles";

const props = defineProps({
  payload: { type: Object, required: true },
  industryGroup: { type: String, default: "" },
  // The locked request email, shown on the Contact row (the payload email is a courtesy echo only).
  lockedEmail: { type: String, default: "" },
  // The referral-source list, passed down rather than resolved here. This component renders on the
  // PUBLIC form, which is anonymous — reaching for useRemsMeta would fire authenticated option-set
  // requests from a page with no session and hand the 401 to the auth interceptor.
  referralSources: { type: Array, default: () => [] }
});

const isIndividual = computed(() => props.industryGroup === "individual");
const isBusiness = computed(() => isBusinessIndustryGroup(props.industryGroup));
const isGovernment = computed(() => props.industryGroup === "government");

const referralOption = (v) => props.referralSources.find((o) => o.value === v);
// Falls back to the raw value: drafts saved before this was a picker hold free text the client typed.
const referralSourceLabel = (v) => (v ? (referralOption(v)?.label || v) : "—");
const referralSourceHint = (v) => (v ? (referralOption(v)?.description || "") : "");

const val = (v) => (v == null || String(v).trim() === "" ? "—" : v);

// Calendar-date fields (DateOnly "YYYY-MM-DD") are shown as-is (MM-DD-YYYY), never timezone-shifted.
const dateOnly = (v) => {
  if (!v) return "—";
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(v));
  return m ? `${m[2]}-${m[3]}-${m[1]}` : String(v);
};

// The role labels, order and required set come from modules/rems/remsContactRoles — the same definition
// the form above is rendered from, so review shows the questions that were actually asked.

const groups = computed(() => {
  const p = props.payload || {};
  const result = [];

  // Contact. An individual's name is reviewed as the two parts they typed, so an error in either one is
  // visible where it was made; every other entity type has the one name it gave.
  const contact = isIndividual.value
    ? [
      { label: "First Name", value: val(p.clientFirstName) },
      { label: "Last Name", value: val(p.clientLastName) }
    ]
    : [{ label: "Client/Entity Name", value: val(p.clientName) }];
  contact.push(
    { label: "Email (locked)", value: val(props.lockedEmail || p.email) },
    { label: "Phone Number", value: val(p.mobileNumber) },
    { label: "Referral Source", value: referralSourceLabel(p.referralSource), hint: referralSourceHint(p.referralSource) },
    { label: "Referral Details", value: val(p.referralSourceDetail) }
  );
  if (isIndividual.value) {
    // The spouse is asked for once, in the Contacts card, and is reviewed there under "Spouse". These
    // three are retired, and appear only when a draft started before the change still carries one —
    // this step reviews what will actually be submitted, and is silent about what will not.
    if (p.spouseName) contact.push({ label: "Spouse Name", value: p.spouseName });
    if (p.spouseEmail) contact.push({ label: "Spouse Email Address", value: p.spouseEmail });
    if (p.spousePhone) contact.push({ label: "Spouse Phone", value: p.spousePhone });
  }
  if (isBusiness.value) contact.push({ label: "EIN", value: val(p.ein) });
  result.push({ title: "Contact", icon: "o_person", kind: "fields", rows: contact });

  // Addresses — all three are stored in their own right, so each is simply shown.
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

  // Additional Contacts (role contacts, in group order). Any role the client answered that this group is
  // no longer asked — a Banker on a form started before it was retired — is shown after the rest: the
  // review step reports what will be submitted, and that contact will be.
  const roles = normalizeRoles(p.roles);
  const key = groupKey(props.industryGroup, isBusinessIndustryGroup(props.industryGroup));
  const contactRows = roleDefsFor(key, answeredRoleKeys(roles))
    .filter((def) => roleHasAny(roles[def.key]))
    .map((def) => ({
      role: def.label,
      isRequired: def.required,
      ...roles[def.key],
      name: roleDisplayName(roles[def.key])
    }));
  result.push({ title: "Additional Contacts", icon: "o_groups", kind: "contacts", rows: contactRows });

  // Other Entities — a contact each, not a second set of business details. Each becomes its own EMS.
  const entities = (p.relatedEntities || [])
    .filter((e) => [e.fullName, e.emailAddress, e.phoneNumber].some((x) => x && String(x).trim()))
    .map((e, i) => {
      const rows = [];
      if (e.emailAddress) rows.push({ label: "Email Address", value: e.emailAddress });
      if (e.phoneNumber) rows.push({ label: "Phone Number", value: e.phoneNumber });
      return { key: e.sourceKey || `entity-${i}`, name: e.fullName, rows };
    });
  result.push({ title: "Other Entities", icon: "o_apartment", kind: "entities", rows: entities });

  // Billing — the person to bill, and nothing else. The billing ADDRESS lives with the other two in
  // Addresses, which is where the form asks for it; repeating it here showed one answer in two places.
  //
  // Asked of individuals only: every other entity type names a Billing Contact among its contacts above,
  // so this block would review the same person twice. Still shown on a non-individual form that carries
  // an answer from before the change — this step reports what will be submitted.
  if (isIndividual.value || p.billingContactName || p.billingEmail) {
    result.push({
      title: "Billing",
      icon: "o_receipt_long",
      kind: "fields",
      rows: [
        { label: "Billing Contact", value: val(p.billingContactName) },
        { label: "Billing Email", value: val(p.billingEmail) }
      ]
    });
  }

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
