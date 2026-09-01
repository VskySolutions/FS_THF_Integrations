<template>
  <div>
    <!-- Contact -->
    <q-card flat bordered class="cif-card q-mb-md">
      <q-card-section class="cif-card__head">Contact</q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-md">
          <!-- An individual is a person, so the name is asked as two boxes and stays two: the record we
               file them under has a given name and a family name, and one box left us guessing where to
               cut it. A business or a government body has ONE name — its legal name — which does not
               divide, so it keeps the single box. -->
          <template v-if="isIndividual">
            <!-- The generational particle on their name — Jr., Sr., III. Optional, and its own box rather
                 than something typed into the last name: the name is what we file them under, and "Smith
                 Jr." in that box is a client nobody finds by searching for their name. -->
            <app-name-suffix-field v-model="payload.clientSuffix" class="col-4 col-sm-2" />
            <app-text-field
              v-model="payload.clientFirstName" label="First Name" required class="col-8 col-sm-4"
              :rules="nameRules('First Name')"
              :error="!!errors.clientFirstName" :error-message="errors.clientFirstName"
            />
            <app-text-field
              v-model="payload.clientLastName" label="Last Name" required class="col-12 col-sm-6"
              :rules="nameRules('Last Name')"
              :error="!!errors.clientLastName" :error-message="errors.clientLastName"
            />
          </template>
          <app-text-field
            v-else
            v-model="payload.clientName" label="Client/Entity Name" required class="col-12 col-sm-6"
            :error="!!errors.clientName" :error-message="errors.clientName"
          />
          <!-- Locked wherever this form is shown: it is the address the invite went to, so a request
               that named somebody else would be a record of a conversation that never happened. -->
          <app-text-field
            v-model="payload.email" label="Email" readonly class="col-12 col-sm-6"
            :hint="emailHint"
          >
            <template #append><q-icon name="o_lock" size="18px" color="grey-6" /></template>
          </app-text-field>
          <div class="col-12 col-sm-6">
            <app-phone-input v-model="payload.mobileNumber" label="Phone Number" />
          </div>
          <!-- Each option's description is its own tooltip, maintained by staff in Administration →
               Option Sets. Choosing one opens a follow-up box for the specifics. -->
          <app-select
            v-model="payload.referralSource" :options="referralSources" label="Referral Source"
            class="col-12 col-sm-6" clearable
            hint="How did you hear about us?"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                  <q-item-label v-if="scope.opt.description" caption>{{ scope.opt.description }}</q-item-label>
                </q-item-section>
              </q-item>
            </template>
          </app-select>
          <app-text-field
            v-if="payload.referralSource"
            v-model="payload.referralSourceDetail" label="Tell us more" class="col-12 col-sm-6"
            :placeholder="referralDetailPlaceholder"
          />

          <!-- No spouse fields here. An individual's spouse is asked for once, in the Spouse block of the
               Contacts card below — name, email and phone together, as one contact. Asking the same three
               a second time here would invite two different answers for one spouse, and only the Contacts
               answer becomes a person record on submit. -->

          <!-- Business (and Trust and Estate, which is asked the same things): EIN -->
          <app-text-field
            v-if="isBusiness" v-model="payload.ein" label="EIN" required class="col-12 col-sm-6"
            :error="!!errors.ein" :error-message="errors.ein"
          />
        </div>
      </q-card-section>
    </q-card>

    <!-- Addresses. One physical, one mailing, and however many places the client is invoiced at — each
         stored in its own right. "Copy from" fills the fields once and leaves them editable: it is not a
         live mirror, so correcting the physical address later does not silently move the others with it.
         Each billing address carries the person the invoice is addressed to, because where it goes and
         who it is addressed to are two halves of one answer — asked in two sections, a form came back
         with three addresses, two names and nothing saying which went with which. -->
    <q-card flat bordered class="cif-card q-mb-md">
      <q-card-section class="cif-card__head">
        Addresses &amp; Billing
        <div class="text-caption text-grey-7 text-weight-regular">
          If the mailing or billing address is the same, copy it across and edit anything that differs.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <!-- Each heading carries what that address IS. Several addresses under similar names are the one
             place on this form where a client can reasonably give the right answer to the wrong question
             — a mailing address typed into the physical box sends nothing anywhere wrong, but a physical
             address typed into the billing box sends the invoice to a building nobody opens post in. The
             note is on the heading, a hover away, rather than as caption lines read once and never
             again. -->
        <div class="cif-addr-head">
          <div class="cif-subhead">
            Physical Address
            <q-icon name="o_info" size="15px" color="grey-6" class="cif-subhead__info">
              <q-tooltip anchor="top middle" self="bottom middle" max-width="300px" :delay="200">
                {{ ADDRESS_HINTS.physical }}
              </q-tooltip>
            </q-icon>
          </div>
        </div>
        <app-address-fields
          v-model="payload.physicalAddress" required :errors="addressErrors(errors, 'physicalAddress')"
        />

        <q-separator class="cif-rule" />

        <div class="cif-addr-head">
          <div class="cif-subhead">
            Mailing Address
            <q-icon name="o_info" size="15px" color="grey-6" class="cif-subhead__info">
              <q-tooltip anchor="top middle" self="bottom middle" max-width="300px" :delay="200">
                {{ ADDRESS_HINTS.mailing }}
              </q-tooltip>
            </q-icon>
          </div>
          <q-btn
            flat dense no-caps size="sm" color="primary" icon="o_content_copy"
            label="Copy from physical" :disable="!addressHasAny(payload.physicalAddress)"
            @click="copyIntakeAddress(payload, 'physicalAddress', 'mailingAddress')"
          />
        </div>
        <app-address-fields
          v-model="payload.mailingAddress" required :errors="addressErrors(errors, 'mailingAddress')"
        />

        <!-- Billing addresses: as many as the client is invoiced at, each carrying the person it is
             addressed to. Where an invoice goes and who it is addressed to are one question, so they are
             one block — the form used to ask them in two sections with nothing saying which name belonged
             to which address, and a client invoiced at two offices could give only one of them.
             None of them is required: say nothing here and we bill the mailing address. -->
        <q-separator class="cif-rule" />

        <div class="cif-addr-head">
          <div class="cif-subhead">
            {{ billingAddresses.length > 1 ? "Billing Addresses" : "Billing Address" }}
            <q-icon name="o_info" size="15px" color="grey-6" class="cif-subhead__info">
              <q-tooltip anchor="top middle" self="bottom middle" max-width="300px" :delay="200">
                {{ ADDRESS_HINTS.billing }}
              </q-tooltip>
            </q-icon>
          </div>
        </div>

        <div v-if="!billingAddresses.length" class="text-caption text-grey-7 q-mb-sm">
          None given — invoices go to the mailing address above.
        </div>

        <div v-else class="column q-gutter-md">
          <div v-for="(row, i) in billingAddresses" :key="row.key" class="cif-billing">
            <div class="cif-addr-head cif-billing__head">
              <!-- Numbered only once there is more than one: a "1" over a lone block answers a question
                   nobody asked. -->
              <div class="cif-subhead">Billing Address<template v-if="billingAddresses.length > 1"> {{ i + 1 }}</template></div>
              <!-- BOTH sources, because either can be the right one: a client whose post goes to a PO box
                   is often invoiced at the office they actually work from, and offering only the mailing
                   address made them retype the physical one they had already given us. The copy moves the
                   PLACE only — whoever it is addressed to stays as typed. -->
              <div class="cif-addr-copy">
                <q-btn
                  flat dense no-caps size="sm" color="primary" icon="o_content_copy"
                  label="Copy from physical" :disable="!addressHasAny(payload.physicalAddress)"
                  @click="copyIntakeAddress(payload, 'physicalAddress', row)"
                />
                <q-btn
                  flat dense no-caps size="sm" color="primary" icon="o_content_copy"
                  label="Copy from mailing" :disable="!addressHasAny(payload.mailingAddress)"
                  @click="copyIntakeAddress(payload, 'mailingAddress', row)"
                />
                <q-btn
                  flat round dense color="negative" icon="o_delete" size="sm"
                  :aria-label="`Remove billing address ${i + 1}`" @click="removeBillingAddress(i)"
                >
                  <q-tooltip>Remove this billing address</q-tooltip>
                </q-btn>
              </div>
            </div>
            <!-- Bound through the payload rather than through the `billingAddresses` computed above: the
                 computed is for reading, and a v-model writing back into one is a warning waiting to
                 happen the first time this field-set replaces the object instead of mutating it. -->
            <app-address-fields
              v-model="payload.billingAddresses[i]" contact contact-label="Invoice addressed to"
              :errors="addressErrors(errors, `billingAddresses[${i}]`)"
            />
          </div>
        </div>

        <div class="q-mt-md">
          <q-btn
            outline no-caps color="primary" icon="o_add"
            :label="billingAddresses.length ? 'Add another billing address' : 'Add a billing address'"
            :disable="!canAddBillingAddress" @click="addBillingAddress"
          >
            <q-tooltip v-if="!canAddBillingAddress">
              You can give up to {{ MAX_BILLING_ADDRESSES }} billing addresses.
            </q-tooltip>
          </q-btn>
        </div>

      </q-card-section>
    </q-card>

    <!-- Contract Details (Government) -->
    <q-card v-if="isGovernment" flat bordered class="cif-card q-mb-md">
      <q-card-section class="cif-card__head">Contract Details</q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-md">
          <app-date-field v-model="payload.contractStartDate" label="Contract Start Date" class="col-12 col-sm-6" />
          <app-date-field v-model="payload.contractEndDate" label="Contract End Date" class="col-12 col-sm-6" />
          <app-text-field v-model="payload.originalTerm" label="Original Term" class="col-12 col-sm-6" />
          <app-text-field v-model="payload.renewalTerms" label="Renewal Terms" class="col-12 col-sm-6" />
          <app-date-field v-model="payload.poStartDate" label="Purchase Order Start Date" class="col-12 col-sm-6" />
          <app-date-field v-model="payload.poEndDate" label="Purchase Order End Date" class="col-12 col-sm-6" />
        </div>
      </q-card-section>
    </q-card>

    <!-- Contacts (roles). No Billing Contact among them: whoever an invoice is addressed to is asked for
         on the billing address itself, above. A submission that answered the retired role still shows it
         here — roleDefsFor puts back any role a payload carries that the group is no longer asked. -->
    <q-card v-if="contactRoleDefs.length" flat bordered class="cif-card q-mb-md">
      <q-card-section class="cif-card__head">
        Contacts
        <div class="text-caption text-grey-7 text-weight-regular">
          Required contacts need a first name, a last name and an email. Phone is optional.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section class="column q-gutter-md">
        <role-contact-fields
          v-for="def in contactRoleDefs" :key="def.key"
          v-model="payload.roles[def.key]"
          :label="def.label" :hint="def.hint" :required="def.required"
          :prefix="`roles.${def.key}`" :errors="errors"
        />
      </q-card-section>
    </q-card>

    <!-- Other entities: who to speak to, not a second set of business details. Each one becomes its own
         EMS request, raised by the partner afterwards, which is where its details get asked for. -->
    <q-card flat bordered class="cif-card q-mb-md">
      <q-card-section class="cif-card__head">
        Other Entities
        <div class="text-caption text-grey-7 text-weight-regular">
          We will set each one up separately and get in touch about it.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <q-toggle
          :model-value="hasRelatedEntities"
          label="Are there more entities?"
          color="primary"
          @update:model-value="onToggleRelated"
        />

        <div v-if="hasRelatedEntities" class="column q-gutter-md q-mt-sm">
          <q-card
            v-for="(entity, i) in payload.relatedEntities" :key="entity.sourceKey"
            flat bordered class="cif-entity"
          >
            <q-card-section class="row items-center no-wrap q-pb-none">
              <div class="text-subtitle2 text-weight-medium col">Entity #{{ i + 1 }}</div>
              <q-btn flat round dense color="negative" icon="o_delete" @click="removeEntity(i)">
                <q-tooltip>Remove</q-tooltip>
              </q-btn>
            </q-card-section>
            <q-card-section>
              <div class="row q-col-gutter-md">
                <app-text-field
                  v-model="entity.fullName" label="Client/Entity Name" required class="col-12 col-sm-4"
                  :error="!!entityErr(i, 'fullName')" :error-message="entityErr(i, 'fullName')"
                />
                <!-- Required, not "email or phone": each of these becomes its own EMS request, and that
                     request is opened by emailing an intake form to this address. A row we cannot write
                     to is a row that never becomes anything. -->
                <app-text-field
                  v-model="entity.emailAddress" label="Email Address" type="email" required
                  class="col-12 col-sm-4"
                  :error="!!entityErr(i, 'emailAddress')" :error-message="entityErr(i, 'emailAddress')"
                />
                <!-- The same dial-code + number control the client's own phone above uses. These numbers
                     are dialled by staff chasing an entity that has not answered, and a bare string gave
                     no country to read them against — the component stores E.164, which carries it. -->
                <div class="col-12 col-sm-4">
                  <app-phone-input v-model="entity.phoneNumber" label="Phone Number" />
                </div>
              </div>
            </q-card-section>
          </q-card>

          <div>
            <q-btn outline no-caps color="primary" icon="o_add" label="Add another entity" @click="addEntity" />
          </div>
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
// THE client intake field set — the cards a client fills in, and the cards an Admin corrects afterwards.
//
// It is a component rather than a stretch of the public page because two screens hold this form now: the
// client's own anonymous page (PublicEmsForm) and the Admin's correction dialog
// (EditSubmittedFormDialog). They differ in everything AROUND the form — one auto-saves against an invite
// code and walks a review step, the other opens over a request and saves once — and in nothing inside it.
//
// It renders fields and nothing else: no loading, no saving, no action bar. The payload belongs to the
// host and is written through directly (a `reactive` object from useRemsIntakeForm, which also knows how
// to seed it, build it and say what is still missing).
import { computed } from "vue";
import { isBusinessIndustryGroup } from "modules/rems/useRemsMeta";
import { addressErrors, addressHasAny } from "modules/rems/remsAddress";
import {
  copyIntakeAddress, intakeRoleDefs, newBillingAddress, newRelatedEntity, relatedEntityHasData,
  MAX_BILLING_ADDRESSES
} from "modules/rems/useRemsIntakeForm";

import { nameRules } from "utils/personName";
import AppTextField from "components/common/AppTextField.vue";
import AppNameSuffixField from "components/common/AppNameSuffixField.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppAddressFields from "components/common/AppAddressFields.vue";
import RoleContactFields from "modules/rems/components/RoleContactFields.vue";

// The payload the host owns; this component writes through it rather than round-tripping a v-model, which
// for a form this size would be a copy of the whole thing on every keystroke.
const payload = defineModel({ type: Object, required: true });

const props = defineProps({
  // The client's entity type (lowercase code). Decides which questions appear — it is THF's own
  // classification, never something the client is asked to confirm, so it is a prop and not a field.
  industryGroup: { type: String, default: "" },
  // Per-field server messages, keyed by payload path (e.g. "roles.self.email").
  errors: { type: Object, default: () => ({}) },
  // The tenant's REMS.ReferralSource list, resolved by whoever is hosting this: the public page is
  // anonymous and is handed the list with the form, the Admin dialog reads the shared catalogue.
  referralSources: { type: Array, default: () => [] },
  // What the locked email field says about itself. The client is told it is locked to their invitation;
  // an admin is told it is the request's.
  emailHint: { type: String, default: "Locked to your invitation" }
});

// Asked when the client is confirming a change they may not have intended.
const emit = defineEmits(["confirm-clear-entities"]);

// What each kind of address is, said on the heading it belongs to rather than in a caption line.
const ADDRESS_HINTS = {
  physical: "Where the business actually operates, or where the client lives — the address we would visit. Not a PO box.",
  mailing: "Where post should reach them. Use this for a PO box, or if their post goes somewhere other than the physical address.",
  billing: "Where invoices should be sent, and who each one should be addressed to. Add another for " +
    "every further place you are invoiced at. Leave it blank and we will bill the mailing address, " +
    "addressed to you."
};

const isIndividual = computed(() => props.industryGroup === "individual");
const isBusiness = computed(() => isBusinessIndustryGroup(props.industryGroup));
const isGovernment = computed(() => props.industryGroup === "government");

const referralDetailPlaceholder = computed(() => {
  const chosen = props.referralSources.find((o) => o.value === payload.value.referralSource);
  // The option's own description is the best prompt for the follow-up — "Friend, Family, or Colleague"
  // says what to type far better than a generic "Please provide details".
  return chosen?.description || "Please provide details";
});

// Every role this entity type is asked. A plain lookup now: the billing contact used to be lifted out of
// this list and rendered with the billing address, and it is not asked at all any more.
const contactRoleDefs = computed(() => intakeRoleDefs(props.industryGroup));

// Where the client is invoiced. Read defensively: a payload seeded from a draft saved before this list
// existed simply has none, and a missing key must render an empty section rather than throw on the way in.
const billingAddresses = computed(() => payload.value.billingAddresses || []);

const canAddBillingAddress = computed(() => billingAddresses.value.length < MAX_BILLING_ADDRESSES);

function addBillingAddress () {
  if (!payload.value.billingAddresses) payload.value.billingAddresses = [];
  if (canAddBillingAddress.value) payload.value.billingAddresses.push(newBillingAddress());
}

// No confirmation. Unlike the Other Entities toggle — which throws away every row at once — this takes
// one block off, and the block below it is still on screen to make the mistake obvious.
function removeBillingAddress (i) {
  payload.value.billingAddresses.splice(i, 1);
}

const entityErr = (i, field) => props.errors[`relatedEntities[${i}].${field}`] || "";

// ---- Other entities ----
// The toggle follows the list rather than holding a state of its own, so a payload that already carries
// entities opens with it on and clearing the last row turns it off.
const hasRelatedEntities = computed(() => payload.value.relatedEntities.length > 0);

function addEntity () {
  payload.value.relatedEntities.push(newRelatedEntity(payload.value.relatedEntities.length));
}

function removeEntity (i) {
  payload.value.relatedEntities.splice(i, 1);
}

// Turning the toggle OFF throws away whatever has been typed, so where there is anything to lose the host
// is asked to confirm it — the host owns the dialog, and the two hosts word it differently.
function onToggleRelated (val) {
  if (val) {
    if (!payload.value.relatedEntities.length) addEntity();
    return;
  }
  if (!payload.value.relatedEntities.some(relatedEntityHasData)) {
    payload.value.relatedEntities = [];
    return;
  }
  emit("confirm-clear-entities", () => { payload.value.relatedEntities = []; });
}
</script>

<style scoped>
.cif-card {
  border-radius: 12px;
}
.cif-card__head {
  font-size: 15px;
  font-weight: 600;
  color: var(--q-primary);
}
.cif-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 8px;
}
/* The rule between two groups of fields inside one card. Addresses stacked one under another are answers
   to different questions, and whitespace alone left them reading as one long block — which is how a
   mailing address ends up typed into the physical one. The line does the separating and carries the
   spacing with it, so the headings below it sit where the old q-mt-lg put them. */
.cif-rule {
  margin: 22px 0 16px;
  background: var(--line, #e0e6ed);
}
/* Heading and its copy button on one baseline. The button sits with the label it fills in, so it reads as
   "this address, copied from that one" rather than as a stray action above the fields. */
.cif-addr-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.cif-addr-head .cif-subhead {
  margin-bottom: 0;
}
/* The billing address has TWO sources to copy from. They wrap together rather than one of them dropping
   to a line of its own, so the pair still reads as one choice. */
.cif-addr-copy {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 8px;
}
/* What this address IS, a hover away on the heading it belongs to. Not uppercased with the rest of the
   heading — it is an icon, and the tooltip carries the words. */
.cif-subhead__info {
  margin-left: 5px;
  cursor: help;
  vertical-align: text-bottom;
}
.cif-entity {
  border-radius: 10px;
  background: #fbfcfe;
}
/* One place the client is invoiced at: a boxed block, so where several run down the card a reader can see
   where one ends and the next begins. Without the border they were address grids of identical shape
   separated by whitespace, and the addressee of the second read as the tail of the first. */
.cif-billing {
  border: 1px solid #e0e6ed;
  border-radius: 10px;
  padding: 12px 14px;
  background: #fff;
}
/* The block's own heading sits inside it, so it needs the gap the card-level headings get from .cif-rule. */
.cif-billing__head {
  margin-bottom: 10px;
}
</style>
