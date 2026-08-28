<template>
  <!-- The client's submitted EMS form, read-only, rendered from the immutable submission. This is a PANEL
       rather than a dialog: it lives in the left pane of the request page, beside the setup it is being
       read against, so checking one answer against one field costs no opening and closing. -->
  <div class="sfp">
    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="32px" /></div>

    <q-banner v-else-if="errorMsg" dense class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else-if="view">
      <!-- Summary. No entity-type badge: this is the snapshot of what the CLIENT submitted, and the
           entity type is THF's own classification — never something they were asked. It is read on the
           request's Client Information tab, where it is set. -->
      <div class="row items-center q-col-gutter-sm q-mb-md">
        <div class="col text-grey-8">
          <span class="text-weight-medium">{{ clientName || view.remsNumber }}</span>
          <span class="text-grey-6"> · {{ view.remsNumber }}</span>
        </div>
        <div class="col-auto text-caption text-grey-7">Submitted {{ fmt.formatDateTime(view.submittedOnUtc) }}</div>
      </div>

      <!-- Said before the answers, not after them: a reader checking an EIN against the setup needs to
           know whether it is the client's own answer or a colleague's correction of it BEFORE they read
           it. Absent entirely while the snapshot is untouched, which is the ordinary case. -->
      <q-banner v-if="view.editedBy" dense class="sfp-edited q-mb-md rounded-borders">
        <template #avatar><q-icon name="o_edit_note" color="amber-9" /></template>
        Corrected by {{ view.editedBy }} on {{ fmt.formatDateTime(view.editedOnUtc) }} — these are no
        longer only the client's own answers.
      </q-banner>

      <!-- Grouped in the order the client was asked, so the admin reads the answers as they were given:
           Contact · Addresses & Billing · Contract Details · Contacts · Other Entities. Plain read-only
           text (AC-REMS-013.2). -->
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
          <div v-else class="text-grey-6">No contacts provided.</div>
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
             other half of. A card each, so one person is told from the next at a glance rather than by
             counting rows in the grid above.

             Outside the kind chain rather than inside it: a v-else-if has to follow its v-if with nothing
             in between, and this is an ADDITION to the fields group, not a fourth kind of one. -->
        <div v-if="g.people && g.people.length" class="submitted-people">
          <div class="submitted-people__title">{{ g.peopleTitle }}</div>
          <div class="submitted-people__grid">
            <div v-for="(person, i) in g.people" :key="i" class="submitted-person">
              <div class="rems-label">{{ person.role }}</div>
              <div class="rems-value">{{ person.name || "—" }}</div>
              <div class="text-caption text-grey-7">
                {{ person.email || "no email" }}<template v-if="person.phone !== null"> · {{ person.phone || "no phone" }}</template>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup>
// Renders one REMS request's submitted client form. Loads it itself from the request id, because the
// only thing every caller has is the request — and reloads when told to, so a page that has just seen a
// submission arrive can ask for it without remounting.
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useDateFormat, formatDateOnly } from "composables/useDateFormat";
import { useRemsMeta, isBusinessIndustryGroup } from "modules/rems/useRemsMeta";
import { addressText } from "modules/rems/remsAddress";
import {
  answeredRoleKeys, BILLING_ROLE_KEY, clientDisplayName, groupKey, normalizeRoles, roleAddressedName,
  roleDefsFor, roleHasAny
} from "modules/rems/remsContactRoles";

const props = defineProps({
  remsId: { type: String, default: null },
  // Held back until the panel is actually on screen — the dialog that preceded this loaded on open, and
  // a pane that is collapsed or on a request with no submission should not fetch either.
  active: { type: Boolean, default: true }
});

const fmt = useDateFormat();
const { referralSourceLabel } = useRemsMeta();

const view = ref(null);
const loading = ref(false);
const errorMsg = ref("");

const payload = computed(() => view.value?.payload || {});
const isIndividual = computed(() => view.value?.industryGroup === "individual");
const isBusiness = computed(() => isBusinessIndustryGroup(view.value?.industryGroup));
const isGovernment = computed(() => view.value?.industryGroup === "government");

const val = (v) => (v == null || String(v).trim() === "" ? "—" : v);

// The client's name as one string, whichever shape the payload is in: the two parts a person gave, or
// the single entity name a company gave. Older payloads carry only `clientName`.
//
// Read WITH the request's generational suffix, which the payload does not carry — the intake form never
// asks for one — so this heading says "John Smith Jr." like every other REMS surface. The First Name and
// Last Name ROWS below stay exactly as the client typed them: those report the answer, this names them.
const clientName = computed(() => {
  const p = payload.value;
  const joined = [p.clientFirstName, p.clientLastName]
    .filter((v) => v != null && String(v).trim() !== "")
    .map((v) => String(v).trim())
    .join(" ");
  return clientDisplayName(joined || String(p.clientName ?? "").trim(), view.value?.clientNameSuffix);
});

// Calendar dates read MM/DD/YYYY and are never timezone-shifted — see formatDateOnly.
const dateOnly = formatDateOnly;

const groups = computed(() => {
  const p = payload.value;
  const result = [];

  // Contact — the name as the client was asked for it.
  const contact = isIndividual.value
    ? [
      { label: "Prefix", value: val(p.clientPrefix) },
      { label: "First Name", value: val(p.clientFirstName ?? p.clientName) },
      { label: "Last Name", value: val(p.clientLastName) }
    ]
    : [{ label: "Client/Entity Name", value: val(p.clientName) }];
  contact.push(
    { label: "Email (locked)", value: val(view.value?.lockedEmail || p.email) },
    { label: "Phone Number", value: val(p.mobileNumber) },
    { label: "Referral Source", value: referralSourceLabel(p.referralSource) },
    { label: "Referral Details", value: val(p.referralSourceDetail) }
  );
  if (isIndividual.value) {
    // Retired from the form — the spouse is a contact now, and shows under Contacts. Still rendered when
    // a snapshot carries them: this panel is the record of what that client actually submitted, so it
    // shows what was in the envelope.
    if (p.spouseName) contact.push({ label: "Spouse Name", value: p.spouseName });
    if (p.spouseEmail) contact.push({ label: "Spouse Email Address", value: p.spouseEmail });
    if (p.spousePhone) contact.push({ label: "Spouse Phone", value: p.spousePhone });
  }
  if (isBusiness.value) contact.push({ label: "EIN", value: val(p.ein) });
  result.push({ title: "Contact", icon: "o_person", kind: "fields", rows: contact });

  // Addresses — three of them, each stored in its own right — and the billing CONTACT with them, which
  // is where the form asks it: the address an invoice goes to and the person it is addressed to are two
  // halves of one answer, and reading them sections apart is what made checking either of them awkward.
  const roles = normalizeRoles(p.roles);
  const billingRole = roles[BILLING_ROLE_KEY];
  const addressRows = [
    { label: "Physical Address", value: addressText(p.physicalAddress) },
    { label: "Mailing Address", value: addressText(p.mailingAddress) },
    { label: "Billing Address", value: addressText(p.billingAddress) }
  ];
  // A client may name several people to invoice, and they are rendered as PEOPLE — one block each, the
  // name with the email and phone beneath it — rather than three label/value rows apiece. Flattened into
  // the address grid, a second contact became fields called "Billing Contact 2 Email" and "Billing
  // Contact 2 Phone" sitting beside "Mailing Address", and an admin checking who to invoice had to count
  // rows to work out where one person ended. The client's review step shows the same shape, which is the
  // point of this panel mirroring it.
  //
  // Numbered only where they named more than one — a "1" over a lone contact answers a question nobody
  // asked — and in the order they gave them.
  const extraBilling = (p.additionalBillingContacts || []).filter(roleHasAny);
  const billingLabel = (i) => (extraBilling.length ? `Billing Contact ${i}` : "Billing Contact");
  // `phone: null` on the lighter two-box version records that the question was never PUT, which is what
  // stops the line reporting "no phone" about a box the client was never shown.
  const billingPeople = isIndividual.value || !roleHasAny(billingRole)
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

  // A non-individual submission that ALSO carries the retired two-box billing answer — sent before every
  // entity type named a Billing Contact. Not a duplicate of the contact above: a different answer the
  // client gave, and this panel reports what was in the envelope.
  if (!isIndividual.value && (p.billingContactName || p.billingEmail) && roleHasAny(billingRole)) {
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

  // Contacts. Normalized, so a submission written under the old business role names reads under the names
  // those roles are known by now; the roles this entity type is no longer asked follow the rest, because
  // what the client sent is what this panel is for. The billing contact is not here — it is read above,
  // with the address it is billed to.
  const key = groupKey(view.value?.industryGroup, isBusiness.value);
  const contactRows = roleDefsFor(key, answeredRoleKeys(roles))
    .filter((def) => def.key !== BILLING_ROLE_KEY && roleHasAny(roles[def.key]))
    .map((def) => ({
      role: def.label,
      name: roleAddressedName(roles[def.key]),
      email: roles[def.key]?.email,
      phone: roles[def.key]?.phone
    }));
  result.push({ title: "Contacts", icon: "o_groups", kind: "contacts", rows: contactRows });

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

  return result;
});

const load = async () => {
  if (!props.remsId || !props.active) return;
  loading.value = true;
  errorMsg.value = "";
  try {
    view.value = await remsApi.submission(props.remsId);
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
    view.value = null;
  } finally {
    loading.value = false;
  }
};

watch(() => [props.remsId, props.active], load, { immediate: true });

// So the page can refresh the snapshot when a submission lands while it is open.
defineExpose({ reload: load });
</script>

<style scoped>
/* Amber rather than red: a corrected snapshot is not a problem, it is a fact worth noticing. */
.sfp-edited {
  background: #fff8e1;
  color: #6d4c00;
}
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
  /* auto-fit rather than a hard pair: this panel is 40% of the page and narrower still on a laptop, and
     two columns of that leave an email address wrapping over three lines. Under the minimum it drops to
     one column and every label keeps its value beside it. */
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: 10px 24px;
}
.field-row--dense {
  margin-top: 4px;
}

/* The billing contacts sit under the addresses they belong to, but outside their grid: a person is not a
   field, and putting three of them in it was what made the section unreadable. */
.submitted-people { margin-top: 14px; }
.submitted-people__title {
  font-size: 12px;
  font-weight: 600;
  color: var(--ink-500, #5a6675);
  margin-bottom: 8px;
}
.submitted-people__grid {
  display: grid;
  /* Same auto-fit rule as the field grid, at a wider minimum: a card carries a whole email address on one
     line, and in this pane — 40% of the page — it will usually be the single column that rule falls to. */
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 10px;
}
.submitted-person {
  border: 1px solid #e0e6ed;
  border-radius: 8px;
  padding: 8px 10px;
  background: #fbfcfd;
}
</style>
