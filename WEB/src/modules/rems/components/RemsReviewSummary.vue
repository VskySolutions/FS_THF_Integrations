<template>
  <!-- Read-only, grouped presentation of the in-progress payload for the public form's Review step
       (AC-REMS-024.7): Contact · Addresses & Billing · Contract Details (Government only) · Additional
       Contacts · Other Entities. That is the order the form asks the questions in, card for card, so
       checking an answer here means looking where it was typed — which is also why the billing contact
       is inside Addresses & Billing rather than in a block of its own at the end. Rendered as plain
       text — no inputs. Mirrors the admin's submitted-form panel so the client sees what the admin will. -->
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

      <!-- People belonging to a FIELDS group — the billing contacts, under the addresses they are the
           other half of. Each gets a card of its own so a reader can tell one person from the next at a
           glance, which nine consecutive rows in the grid above could not do.

           Outside the kind chain above rather than inside it: a v-else-if has to follow its v-if with
           nothing in between, and this is an ADDITION to the fields group, not a fourth kind of one. -->
      <div v-if="g.people && g.people.length" class="review-people">
        <div class="review-people__title">{{ g.peopleTitle }}</div>
        <div class="review-people__grid">
          <div v-for="(person, i) in g.people" :key="i" class="review-person">
            <div class="rems-label">{{ person.role }}</div>
            <div class="rems-value">{{ person.name || "—" }}</div>
            <div class="text-caption text-grey-7">
              {{ person.email || "no email" }}<template v-if="person.phone !== null"> · {{ person.phone || "no phone" }}</template>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from "vue";
import { addressText } from "modules/rems/remsAddress";
import { formatDateOnly } from "composables/useDateFormat";
import { isBusinessIndustryGroup } from "modules/rems/useRemsMeta";
import {
  answeredRoleKeys, BILLING_ROLE_KEY, groupKey, normalizeRoles, roleAddressedName, roleDefsFor, roleHasAny
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

// Calendar dates read MM/DD/YYYY and are never timezone-shifted — see formatDateOnly.
const dateOnly = formatDateOnly;

// The role labels, order and required set come from modules/rems/remsContactRoles — the same definition
// the form above is rendered from, so review shows the questions that were actually asked.

const groups = computed(() => {
  const p = props.payload || {};
  const result = [];

  // Contact. An individual's name is reviewed as the two parts they typed, so an error in either one is
  // visible where it was made; every other entity type has the one name it gave.
  const contact = isIndividual.value
    ? [
      { label: "Suffix", value: val(p.clientSuffix) },
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
    // The courtesy title the name box used to ask for, before it asked for a generational suffix. Shown
    // only when a draft started under the old box still carries one, for the same reason the three
    // spouse rows below are.
    if (p.clientPrefix) contact.push({ label: "Prefix", value: p.clientPrefix });
    // The spouse is asked for once, in the Contacts card, and is reviewed there under "Spouse". These
    // three are retired, and appear only when a draft started before the change still carries one —
    // this step reviews what will actually be submitted, and is silent about what will not.
    if (p.spouseName) contact.push({ label: "Spouse Name", value: p.spouseName });
    if (p.spouseEmail) contact.push({ label: "Spouse Email Address", value: p.spouseEmail });
    if (p.spousePhone) contact.push({ label: "Spouse Phone", value: p.spousePhone });
  }
  if (isBusiness.value) contact.push({ label: "EIN", value: val(p.ein) });
  result.push({ title: "Contact", icon: "o_person", kind: "fields", rows: contact });

  // Addresses — all three are stored in their own right, so each is simply shown. The billing CONTACT
  // rides with them, because that is where the form asks it: the billing address and the person it is
  // addressed to are two halves of one answer, and reviewing them cards apart is how a form gets sent
  // with one and not the other.
  const roles = normalizeRoles(p.roles);
  const billingRole = roles[BILLING_ROLE_KEY];
  const addressRows = [
    { label: "Physical Address", value: addressText(p.physicalAddress) },
    { label: "Mailing Address", value: addressText(p.mailingAddress) },
    { label: "Billing Address", value: addressText(p.billingAddress) }
  ];
  // The people to invoice, as PEOPLE — one block each, the name with the email and phone beneath it —
  // rather than three label/value rows apiece. Flattened into the address grid, a second contact became
  // fields called "Billing Contact 2 Email" and "Billing Contact 2 Phone" sitting beside "Mailing
  // Address" as though they were the same kind of answer, and nothing said where one person ended and
  // the next began.
  //
  // Numbered only where there is more than one — a "1" over a lone contact answers a question nobody
  // asked. The extras follow it in the order they were typed.
  const extraBilling = (p.additionalBillingContacts || []).filter(roleHasAny);
  const billingLabel = (i) => (extraBilling.length ? `Billing Contact ${i}` : "Billing Contact");
  // Read from the contact block every entity type is asked, falling back to the retired two-box answer on
  // a payload that predates it. A contact left blank is still shown, because blank IS the answer being
  // reviewed; `phone: null` rather than "" records that the question was never PUT on that older form,
  // which is what stops the line reporting "no phone" about a box nobody saw.
  const billingPeople = !roleHasAny(billingRole)
    ? [{ role: billingLabel(1), name: p.billingContactName, email: p.billingEmail, phone: null }]
    : [{
      role: billingLabel(1),
      name: roleAddressedName(billingRole),
      email: billingRole.email,
      phone: billingRole.phone
    }];
  extraBilling.forEach((role, i) => billingPeople.push({
    role: `Billing Contact ${i + 2}`,
    name: roleAddressedName(role),
    email: role.email,
    phone: role.phone
  }));
  result.push({
    title: "Addresses & Billing",
    icon: "o_place",
    kind: "fields",
    rows: addressRows,
    people: billingPeople,
    peopleTitle: billingPeople.length > 1 ? "Billing Contacts" : "Billing Contact"
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
  // review step reports what will be submitted, and that contact will be. The billing contact is not
  // among them: it is reviewed above, with the address it is billed to.
  const key = groupKey(props.industryGroup, isBusinessIndustryGroup(props.industryGroup));
  const contactRows = roleDefsFor(key, answeredRoleKeys(roles))
    .filter((def) => def.key !== BILLING_ROLE_KEY && roleHasAny(roles[def.key]))
    .map((def) => ({
      role: def.label,
      isRequired: def.required,
      ...roles[def.key],
      name: roleAddressedName(roles[def.key])
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

  // Billing is reviewed up in Addresses & Billing, where the form asks it. The one exception: a payload
  // that carries BOTH the contact block and the retired two-box answer. That is not a second copy of the
  // contact above — it is a different answer the client gave on an earlier form — and this step reports
  // what is in hand. Where the block is empty the two boxes ARE the contact above, so there is nothing
  // here to add.
  if ((p.billingContactName || p.billingEmail) && roleHasAny(billingRole)) {
    result.push({
      title: "Billing (as previously given)",
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

/* The billing contacts sit under the addresses they belong to, but outside their grid: a person is not a
   field, and putting three of them in it was what made the section unreadable. */
.review-people { margin-top: 14px; }
.review-people__title {
  font-size: 12px;
  font-weight: 600;
  color: var(--ink-500, #5a6675);
  margin-bottom: 8px;
}
.review-people__grid {
  display: grid;
  /* The same auto-fit rule as the field grid, at a wider minimum: a card carries a whole email address on
     one line, and below the minimum it drops to a single column rather than wrapping every one of them. */
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 10px;
}
.review-person {
  border: 1px solid #e0e6ed;
  border-radius: 8px;
  padding: 8px 10px;
  background: #fbfcfd;
}
</style>
