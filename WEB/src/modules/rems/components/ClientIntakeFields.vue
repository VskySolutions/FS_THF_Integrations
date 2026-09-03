<template>
  <div>
    <!-- 1 · Confirm Your Contact Details ------------------------------------------------------------
         "Confirm", not "Enter": most of what is in these boxes is already on file — staff typed the name
         and the email when they raised the request, and the invitation went to that address. Saying so
         changes what the client does with the card: they read it and correct what is wrong, rather than
         wondering why the form already knows their name. -->
    <q-card flat bordered class="cif-card q-mb-sm">
      <q-card-section class="cif-card__head">
        Confirm Your Contact Details
        <div class="text-caption text-grey-7 text-weight-regular">
          Review the information THF has on file. You can correct your name or mobile number below — your
          email can't be changed here.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-sm">
          <!-- An individual is a person, so the name is asked as two boxes and stays two: the record we
               file them under has a given name and a family name, and one box left us guessing where to
               cut it. A business or a government body has ONE name — its legal name — which does not
               divide, so it keeps the single box. -->
          <template v-if="isIndividual">
            <app-text-field
              v-model="payload.clientFirstName" label="First Name" required class="col-12 col-sm-6"
              :rules="nameRules('First Name')"
              :error="!!errors.clientFirstName" :error-message="errors.clientFirstName"
            />
            <app-text-field
              v-model="payload.clientLastName" label="Last Name" required class="col-8 col-sm-4"
              :rules="nameRules('Last Name')"
              :error="!!errors.clientLastName" :error-message="errors.clientLastName"
            />
            <!-- The generational particle on their name — Jr., Sr., III. Optional, and its own box rather
                 than something typed into the last name: the name is what we file them under, and "Smith
                 Jr." in that box is a client nobody finds by searching for their name. It sits after the
                 surname, which is where it is read. -->
            <app-name-suffix-field v-model="payload.clientSuffix" class="col-4 col-sm-2" />
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

          <!-- No spouse fields here. Whoever else is on this client's return — a spouse, a child, anybody
               the firm is preparing for — is asked for once, in the Spouse & More Individuals card below,
               where the form also asks the two things that actually matter about them: how their return
               is filed, and who pays for it. -->

          <!-- Business (and Trust and Estate, which is asked the same things): EIN -->
          <app-text-field
            v-if="isBusiness" v-model="payload.ein" label="EIN" required class="col-12 col-sm-6"
            :error="!!errors.ein" :error-message="errors.ein"
          />
        </div>
      </q-card-section>
    </q-card>

    <!-- 2 · Address ---------------------------------------------------------------------------------
         ONE card and, for almost every client, one address. The form used to ask for a physical address
         and a mailing address as two required blocks with a "Copy from physical" button between them,
         which made the commonest answer on the whole form — "they are the same" — the one that took the
         most typing. It is a ticked box now, and the second block appears only for the clients whose
         post really does go somewhere else. -->
    <q-card flat bordered class="cif-card q-mb-sm">
      <q-card-section class="cif-card__head">
        Physical &amp; Mailing Addresses
        <div class="text-caption text-grey-7 text-weight-regular">
          Where you live or operate. Tell us below if your post goes somewhere else.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section>
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
          v-model="payload.physicalAddress" required gutter="sm" :cols="ADDRESS_COLS"
          :errors="addressErrors(errors, 'physicalAddress')"
        />

        <!-- The box that decides whether there is a second address, at the END of the first one: it is a
             question about the address just typed, and above it there was nothing yet to answer it about.
             Ticked to start with, because for almost every client it is true. -->
        <q-checkbox
          v-model="payload.mailingSameAsPhysical" dense color="primary" class="cif-same-as q-mt-sm"
          label="Mailing address is the same as physical address"
        />

        <template v-if="!payload.mailingSameAsPhysical">
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
            <!-- No "Copy from physical" here. The tickbox above IS that answer, and it is the better one:
                 a copy gives two addresses that agree today and drift the moment one is corrected, while
                 the tick says they are the same and keeps saying it. These boxes are only ever open
                 because the client said their post goes somewhere else, so a button offering to fill them
                 with the address they have just said it is NOT would undo the answer above it. -->
          </div>
          <app-address-fields
            v-model="payload.mailingAddress" required gutter="sm" :cols="ADDRESS_COLS"
            :errors="addressErrors(errors, 'mailingAddress')"
          />
        </template>
      </q-card-section>
    </q-card>

    <!-- 3 · Billing Information ---------------------------------------------------------------------
         Who the invoice is for and where it goes, in one block and in that order: those are two halves of
         one answer, and asked in two sections a client invoiced at two offices came back with two
         addresses, two names and nothing saying which went with which.
         REQUIRED now, and no longer inferred. "We will bill the mailing address, addressed to you" was
         the form guessing on the client's behalf, and it guessed wrong for every client whose invoices
         go to a bookkeeper. -->
    <q-card flat bordered class="cif-card q-mb-sm">
      <q-card-section class="cif-card__head">
        Billing Information
        <div class="text-caption text-grey-7 text-weight-regular">
          {{ ADDRESS_HINTS.billing }}
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <div class="column q-gutter-sm">
          <!-- The BOX is only drawn where there is more than one: it exists to show a reader where one
               block ends and the next begins, and around a lone block it is a bordered box inside a
               bordered card, paying for a border and two lots of padding to separate one thing from
               nothing. -->
          <div v-for="(row, i) in billingAddresses" :key="row.key" :class="{ 'cif-billing': severalBilling }">
            <div class="cif-addr-head cif-billing__head">
              <!-- Numbered, and shown only once there is more than one. A lone block's heading said
                   "Billing Information" directly under a card head saying "Billing Information" — the
                   same words twice, and a whole line of the form to say them. -->
              <div v-if="severalBilling" class="cif-subhead">Billing Information {{ i + 1 }}</div>
              <!-- BOTH sources, because either can be the right one: a client whose post goes to a PO box
                   is often invoiced at the office they actually work from, and offering only the mailing
                   address made them retype the physical one they had already given us. The copy moves the
                   PLACE only — whoever it is addressed to stays as typed. The mailing button is absent
                   while the two addresses are the same, since it would copy the physical one twice. -->
              <div class="cif-addr-copy">
                <q-btn
                  flat dense no-caps size="sm" color="primary" icon="o_content_copy"
                  label="Copy from physical" :disable="!addressHasAny(payload.physicalAddress)"
                  @click="copyIntakeAddress(payload, 'physicalAddress', row)"
                />
                <q-btn
                  v-if="!payload.mailingSameAsPhysical"
                  flat dense no-caps size="sm" color="primary" icon="o_content_copy"
                  label="Copy from mailing" :disable="!addressHasAny(payload.mailingAddress)"
                  @click="copyIntakeAddress(payload, 'mailingAddress', row)"
                />
                <!-- Only from the second block onwards: billing is required, so the last one is not
                     somebody's to remove. -->
                <q-btn
                  v-if="severalBilling"
                  flat round dense color="negative" icon="o_delete" size="sm"
                  :aria-label="`Remove billing information ${i + 1}`" @click="removeBillingAddress(i)"
                >
                  <q-tooltip>Remove this billing block</q-tooltip>
                </q-btn>
              </div>
            </div>
            <!-- Bound through the payload rather than through the `billingAddresses` computed above: the
                 computed is for reading, and a v-model writing back into one is a warning waiting to
                 happen the first time this field-set replaces the object instead of mutating it. -->
            <app-address-fields
              v-model="payload.billingAddresses[i]" required
              contact contact-first contact-required contact-label="" gutter="sm" :cols="BILLING_COLS"
              :errors="addressErrors(errors, `billingAddresses[${i}]`)"
            />
          </div>
        </div>

        <div class="q-mt-sm">
          <q-btn
            outline no-caps color="primary" icon="o_add" label="Add another billing block"
            :disable="!canAddBillingAddress" @click="addBillingAddress"
          >
            <q-tooltip v-if="!canAddBillingAddress">
              You can give up to {{ MAX_BILLING_ADDRESSES }} billing blocks.
            </q-tooltip>
          </q-btn>
        </div>
      </q-card-section>
    </q-card>

    <!-- 4 · Spouse & More Individuals (individual only) ---------------------------------------------
         Everyone else on this client's return. Asked of an individual and of nobody else: a business's
         people are its contacts, and they are asked for in the Contacts card below. -->
    <q-card v-if="isIndividual" flat bordered class="cif-card q-mb-sm">
      <q-card-section class="cif-card__head">
        Spouse &amp; More Individuals
        <div class="text-caption text-grey-7 text-weight-regular">
          Add a spouse or child if we'll be preparing their return too.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <!-- The client's own surname prefills each person added below: a spouse and children nearly
             always share it, and it is theirs to type over where they do not. -->
        <additional-individuals-fields
          v-model="payload.additionalIndividuals" :errors="errors"
          :default-last-name="payload.clientLastName"
          @confirm-clear="(done) => emit('confirm-clear-individuals', done)"
        />
      </q-card-section>
    </q-card>

    <!-- Contract Details (Government) -->
    <q-card v-if="isGovernment" flat bordered class="cif-card q-mb-sm">
      <q-card-section class="cif-card__head">Contract Details</q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-sm">
          <app-date-field v-model="payload.contractStartDate" label="Contract Start Date" class="col-12 col-sm-6" />
          <app-date-field v-model="payload.contractEndDate" label="Contract End Date" class="col-12 col-sm-6" />
          <app-text-field v-model="payload.originalTerm" label="Original Term" class="col-12 col-sm-6" />
          <app-text-field v-model="payload.renewalTerms" label="Renewal Terms" class="col-12 col-sm-6" />
          <app-date-field v-model="payload.poStartDate" label="Purchase Order Start Date" class="col-12 col-sm-6" />
          <app-date-field v-model="payload.poEndDate" label="Purchase Order End Date" class="col-12 col-sm-6" />
        </div>
      </q-card-section>
    </q-card>

    <!-- Contacts (roles). Empty for an INDIVIDUAL, whose own details are the first card and whose family
         is the fourth — the Self and Spouse roles asked both of those a second time. No Billing Contact
         among them either: whoever an invoice is addressed to is asked for on the billing block itself,
         above.
         Only the roles the group is asked TODAY, so this card is the current form and nothing else. A
         submission that answered a role since retired keeps that answer — it is echoed back untouched on
         every save — and it is shown on the surfaces that report what was sent: the client's review step
         and the staff panel, both of which put retired roles back through roleDefsFor's extraKeys. -->
    <q-card v-if="contactRoleDefs.length" flat bordered class="cif-card q-mb-sm">
      <q-card-section class="cif-card__head">
        Contacts
        <div class="text-caption text-grey-7 text-weight-regular">
          Required contacts need a first name, a last name and an email. Phone is optional.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section class="column q-gutter-sm">
        <role-contact-fields
          v-for="def in contactRoleDefs" :key="def.key"
          v-model="payload.roles[def.key]"
          :label="def.label" :hint="def.hint" :required="def.required"
          :prefix="`roles.${def.key}`" :errors="errors"
        />
      </q-card-section>
    </q-card>

    <!-- Other entities: who to speak to, not a second set of business details. Each one becomes its own
         EMS request, raised by the partner afterwards, which is where its details get asked for.
         Asked of everyone EXCEPT an individual. A person is not a holding structure — the question is
         about the client's other businesses, and for an individual the answer to "who else are we setting
         up?" is the Spouse & More Individuals card above, which asks it in the terms that actually apply
         to people. A submission that answered it before this keeps the answer: it is echoed back on every
         save and still materialises, exactly as the retired contact roles do. -->
    <q-card v-if="!isIndividual" flat bordered class="cif-card q-mb-sm">
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

        <div v-if="hasRelatedEntities" class="column q-gutter-sm q-mt-sm">
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
              <div class="row q-col-gutter-sm">
                <!-- Two across on a tablet and three only from md: an email address in a third of a
                     600px card is an email address nobody can read back to check it. -->
                <app-text-field
                  v-model="entity.fullName" label="Client/Entity Name" required
                  class="col-12 col-sm-6 col-md-4"
                  :error="!!entityErr(i, 'fullName')" :error-message="entityErr(i, 'fullName')"
                />
                <!-- Required, not "email or phone": each of these becomes its own EMS request, and that
                     request is opened by emailing an intake form to this address. A row we cannot write
                     to is a row that never becomes anything. -->
                <app-text-field
                  v-model="entity.emailAddress" label="Email Address" type="email" required
                  class="col-12 col-sm-6 col-md-4"
                  :error="!!entityErr(i, 'emailAddress')" :error-message="entityErr(i, 'emailAddress')"
                />
                <!-- The same dial-code + number control the client's own phone above uses. These numbers
                     are dialled by staff chasing an entity that has not answered, and a bare string gave
                     no country to read them against — the component stores E.164, which carries it. -->
                <div class="col-12 col-sm-6 col-md-4">
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
// The cards, in order: Confirm Your Contact Details · Address · Billing Information · Spouse & More
// Individuals (an individual only) · Contract Details (a government body only) · Contacts (everyone
// else) · Other Entities.
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
import AdditionalIndividualsFields from "modules/rems/components/AdditionalIndividualsFields.vue";

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

// Asked when the client is confirming a change they may not have intended. Two questions, because they
// throw away two different things and the hosts word them differently.
const emit = defineEmits(["confirm-clear-entities", "confirm-clear-individuals"]);

// What each kind of address is, said where it belongs — on the heading for the two that have one, and in
// the card's own subtitle for billing, which is a whole card now.
const ADDRESS_HINTS = {
  physical: "Where the business actually operates, or where the client lives — the address we would visit. Not a PO box.",
  mailing: "Where post should reach them. Use this for a PO box, or if their post goes somewhere other than the physical address.",
  billing: "Who each invoice is for, and where it should be sent. Add another block for every further " +
    "place you are invoiced at."
};

// The grid every address block on this form uses, so a client reads the same shape whether they are
// telling us where they live or where the invoice goes. Three steps, because the form is filled in on a
// phone as often as on a desk:
//   xs   — one box per line. There is no width to share below 600px.
//   sm   — the country/state/city cascade three across, the two street lines side by side.
//   md+  — the zip drops to a quarter, which is what a zip code is: a short box that spent the whole
//          layout pretending to be as wide as a street name.
// Nothing is set at lg or xl: this form is capped at 960px on the public page and 1100px in the admin's
// correction dialog, so past md there is no more width to spend and the boxes would only get emptier.
const ADDRESS_COLS = {
  country: "col-12 col-sm-4",
  state: "col-12 col-sm-4",
  city: "col-12 col-sm-4",
  addressLine1: "col-12 col-sm-6",
  addressLine2: "col-12 col-sm-6",
  postalCode: "col-12 col-sm-4 col-md-3"
};

// The billing block adds the three boxes saying who the invoice is for, and they LEAD it. At md they
// share the first line with each other — 3 + 3 + 6 — and below that the email takes a line of its own
// rather than sitting beside a country picker, which is what a plain half-width would have left it doing.
const BILLING_COLS = {
  ...ADDRESS_COLS,
  firstName: "col-12 col-sm-6 col-md-3",
  lastName: "col-12 col-sm-6 col-md-3",
  email: "col-12 col-md-6"
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

// Every role this entity type is asked. Empty for an individual, which is what hides the whole card.
const contactRoleDefs = computed(() => intakeRoleDefs(props.industryGroup));

// Where the client is invoiced. Read defensively: a payload seeded from a draft saved before this list
// existed simply has none, and a missing key must render an empty section rather than throw on the way in.
const billingAddresses = computed(() => payload.value.billingAddresses || []);

const canAddBillingAddress = computed(() => billingAddresses.value.length < MAX_BILLING_ADDRESSES);

// Whether the client is invoiced in more than one place — which is what decides the block's own chrome.
// One block needs no number, no heading (the card's own says it) and no box around it; several need all
// three, or a reader cannot tell where one ends.
const severalBilling = computed(() => billingAddresses.value.length > 1);

function addBillingAddress () {
  if (!payload.value.billingAddresses) payload.value.billingAddresses = [];
  if (canAddBillingAddress.value) payload.value.billingAddresses.push(newBillingAddress());
}

// No confirmation. Unlike the Other Entities toggle — which throws away every row at once — this takes
// one block off, and the block below it is still on screen to make the mistake obvious. Never offered on
// the last one: billing is required.
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
/* Six cards of questions is a long page, and on a phone the client is scrolling all of it. Every card
   gives back some of Quasar's default 16px section padding: it buys nothing on a form whose job is to be
   got through, and four pixels a side across six cards and three nested blocks is most of a screen. The
   gutters between the boxes come down with it (q-col-gutter-sm), so the density is consistent rather than
   tight in one dimension and loose in the other. */
.cif-card .q-card__section {
  padding: 10px 12px;
}
.cif-card .cif-card__head {
  padding: 9px 12px;
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
  margin-bottom: 6px;
}
/* The rule between two groups of fields inside one card. Addresses stacked one under another are answers
   to different questions, and whitespace alone left them reading as one long block — which is how a
   mailing address ends up typed into the physical one. The line does the separating and carries the
   spacing with it, so the headings below it sit where the old q-mt-lg put them. */
.cif-rule {
  margin: 14px 0 12px;
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
/* The billing block has TWO sources to copy from. They wrap together rather than one of them dropping to
   a line of its own, so the pair still reads as one choice.

   Pushed right by `margin-left: auto` rather than by the row's space-between, because on a lone block
   there is no heading beside them for space-between to push against — one child in a space-between row
   sits at the start. The margin puts them on the right either way, which is where an action on the block
   below belongs: the client reads the fields down the left edge, not the buttons. */
.cif-addr-copy {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 4px 8px;
  margin-left: auto;
}
/* What this address IS, a hover away on the heading it belongs to. Not uppercased with the rest of the
   heading — it is an icon, and the tooltip carries the words. */
.cif-subhead__info {
  margin-left: 5px;
  cursor: help;
  vertical-align: text-bottom;
}
/* The box that decides whether a second address exists. Given the label weight of a field rather than of
   a caption: it is the answer to a question, and at caption weight it read as a note about the block
   above it. */
.cif-same-as :deep(.q-checkbox__label) {
  font-size: 13px;
  color: #423939;
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
  padding: 10px 12px;
  background: #fff;
}
/* The block's own heading sits inside it, so it needs the gap the card-level headings get from .cif-rule.
   Small, because on a lone block this row carries the copy buttons and nothing else — there is no heading
   above the fields for it to hold off. */
.cif-billing__head {
  margin-bottom: 4px;
}
</style>
