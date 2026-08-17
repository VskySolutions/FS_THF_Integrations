<template>
  <div class="pef">
    <!-- Initial load ---------------------------------------------------------------------------------- -->
    <div v-if="loading" class="row flex-center q-pa-xl">
      <q-spinner color="primary" size="42px" />
    </div>

    <!-- Neutral "not available" — Invalid / Unavailable / a failed load. Discloses NOTHING about the app. -->
    <q-card v-else-if="showUnavailable" flat bordered class="pef-card pef-note">
      <q-card-section class="column flex-center text-center q-py-xl">
        <q-icon name="o_link_off" size="52px" color="grey-6" />
        <div class="text-h6 q-mt-md">This form isn't available</div>
        <div class="text-body2 text-grey-7 q-mt-sm" style="max-width: 460px;">
          The link may be invalid, no longer active, or already completed. If you believe this is a mistake,
          please contact the person who sent you this form.
        </div>
      </q-card-section>
    </q-card>

    <!-- Client cancelled — neutral, non-destructive end state (they can return via the emailed link). -->
    <q-card v-else-if="cancelled" flat bordered class="pef-card pef-note">
      <q-card-section class="column flex-center text-center q-py-xl">
        <q-icon name="o_bookmark_border" size="52px" color="primary" />
        <div class="text-h6 q-mt-md">Your progress is saved</div>
        <div class="text-body2 text-grey-7 q-mt-sm" style="max-width: 460px;">
          You can return to this form at any time using the link in your email and pick up right where you
          left off.
        </div>
      </q-card-section>
    </q-card>

    <!-- Submitted — personalized thank-you. No editable fields, no reset / Start-Over (AC-REMS-012.6/7). -->
    <q-card v-else-if="state === 'Submitted'" flat bordered class="pef-card pef-note">
      <q-card-section class="column flex-center text-center q-py-xl">
        <q-icon name="o_check_circle" size="56px" color="positive" />
        <div class="text-h5 q-mt-md">Thank you{{ thankYouName ? `, ${thankYouName}` : "" }}!</div>
        <div class="text-body1 text-grey-8 q-mt-sm" style="max-width: 480px;">
          Your EMS onboarding form has been submitted successfully. Our team has been notified and will be in
          touch with you shortly.
        </div>
        <div class="text-caption text-grey-6 q-mt-md">You can safely close this window.</div>
      </q-card-section>
    </q-card>

    <!-- Editable ------------------------------------------------------------------------------------- -->
    <template v-else-if="state === 'Editable'">
      <!-- Intro -->
      <div class="pef-head">
        <div class="row items-center q-gutter-sm">
          <div class="text-h5 text-weight-bold col">EMS Onboarding Form</div>
          <q-chip square dense color="teal-1" text-color="primary" class="q-ma-none">
            {{ industryLabel }}
          </q-chip>
        </div>
        <div class="text-body2 text-grey-7 q-mt-xs">
          Please review and complete the details below. Your progress saves automatically as you go.
        </div>
      </div>

      <!-- ============================ FORM STEP ============================ -->
      <template v-if="step === 'form'">
        <!-- Server-side validation summary (from review/submit) — surfaced field-by-field below too. -->
        <q-banner v-if="serverSummary.length" dense class="pef-card bg-red-1 text-red-9 q-mb-md rounded-borders">
          <template #avatar><q-icon name="o_error" color="red-9" /></template>
          <div class="text-weight-medium">Please fix the following:</div>
          <ul class="q-my-xs q-pl-md">
            <li v-for="(m, i) in serverSummary" :key="i">{{ m }}</li>
          </ul>
        </q-banner>

        <!-- Contact -->
        <q-card flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">Contact</q-card-section>
          <q-separator />
          <q-card-section>
            <div class="row q-col-gutter-sm">
              <app-text-field
                v-model="payload.clientName" label="Client Name" required class="col-12 col-sm-6"
                :error="!!errors.clientName" :error-message="errors.clientName"
              />
              <app-text-field
                v-model="payload.email" label="Email" readonly class="col-12 col-sm-6"
                hint="Locked to your invitation"
              >
                <template #append><q-icon name="o_lock" size="18px" color="grey-6" /></template>
              </app-text-field>
              <div class="col-12 col-sm-6">
                <app-phone-input v-model="payload.mobileNumber" label="Phone Number" />
              </div>
              <!-- Each option's description is its own tooltip, maintained by staff in Administration →
                   Option Sets and delivered with the form (this page is anonymous and cannot resolve an
                   option set itself). Choosing one opens a follow-up box for the specifics. -->
              <app-select
                v-model="payload.referralSource" :options="referralOptions" label="Referral Source"
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

              <!-- No spouse fields here. An individual's spouse is asked for once, in the Spouse block of
                   the Contacts card below — name, email and phone together, as one contact. This card used
                   to ask the same three a second time into their own payload fields, which invited two
                   different answers for one spouse and materialised into nothing: only the Contacts answer
                   becomes a person record on submit. -->

              <!-- Business: EIN -->
              <app-text-field
                v-if="isBusiness" v-model="payload.ein" label="EIN" required class="col-12 col-sm-6"
                :error="!!errors.ein" :error-message="errors.ein"
              />
            </div>
          </q-card-section>
        </q-card>

        <!-- Addresses: three of them, each stored in its own right. "Copy from" fills the fields once and
             leaves them editable — it is not a live mirror, so correcting the physical address later does
             not silently move the other two with it. -->
        <q-card flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">
            Addresses
            <div class="text-caption text-grey-7 text-weight-regular">
              If your mailing or billing address is the same, copy it across and edit anything that differs.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div class="pef-subhead">Physical Address</div>
            <app-address-fields
              v-model="payload.physicalAddress" required :errors="addressErrors(errors, 'physicalAddress')"
            />

            <div class="pef-addr-head q-mt-lg">
              <div class="pef-subhead">Mailing Address</div>
              <q-btn
                flat dense no-caps size="sm" color="primary" icon="o_content_copy"
                label="Copy from physical" :disable="!hasAny(payload.physicalAddress)"
                @click="copyAddress('physicalAddress', 'mailingAddress')"
              />
            </div>
            <app-address-fields
              v-model="payload.mailingAddress" required :errors="addressErrors(errors, 'mailingAddress')"
            />

            <div class="pef-addr-head q-mt-lg">
              <div class="pef-subhead">Billing Address</div>
              <q-btn
                flat dense no-caps size="sm" color="primary" icon="o_content_copy"
                label="Copy from mailing" :disable="!hasAny(payload.mailingAddress)"
                @click="copyAddress('mailingAddress', 'billingAddress')"
              />
            </div>
            <app-address-fields
              v-model="payload.billingAddress" :errors="addressErrors(errors, 'billingAddress')"
            />
          </q-card-section>
        </q-card>

        <!-- Contract Details (Government) -->
        <q-card v-if="isGovernment" flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">Contract Details</q-card-section>
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

        <!-- Contacts (roles) -->
        <q-card flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">
            Contacts
            <div class="text-caption text-grey-7 text-weight-regular">
              Required contacts must include a name and an email. Phone is optional.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section class="column q-gutter-md">
            <role-contact-fields
              v-for="def in roleDefs" :key="def.key"
              v-model="payload.roles[def.key]"
              :label="def.label" :required="def.required" :prefix="`roles.${def.key}`" :errors="errors"
            />
          </q-card-section>
        </q-card>

        <!-- Other entities: who to speak to, not a second set of business details. Each one becomes its
             own EMS request, raised by the partner afterwards, which is where its details get asked for. -->
        <q-card flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">
            Other Entities
            <div class="text-caption text-grey-7 text-weight-regular">
              We will set each one up separately and get in touch about it.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <q-toggle
              :model-value="hasRelatedEntities"
              label="Do you have more entities?"
              color="primary"
              @update:model-value="onToggleRelated"
            />

            <div v-if="hasRelatedEntities" class="column q-gutter-md q-mt-sm">
              <q-card
                v-for="(entity, i) in payload.relatedEntities" :key="entity.sourceKey"
                flat bordered class="pef-entity"
              >
                <q-card-section class="row items-center no-wrap q-pb-none">
                  <div class="text-subtitle2 text-weight-medium col">Entity #{{ i + 1 }}</div>
                  <q-btn flat round dense color="negative" icon="o_delete" @click="removeEntity(i)">
                    <q-tooltip>Remove</q-tooltip>
                  </q-btn>
                </q-card-section>
                <q-card-section>
                  <div class="row q-col-gutter-sm">
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
                    <app-text-field v-model="entity.phoneNumber" label="Phone Number" class="col-12 col-sm-4" />
                  </div>
                </q-card-section>
              </q-card>

              <div>
                <q-btn outline no-caps color="primary" icon="o_add" label="Add another entity" @click="addEntity" />
              </div>
            </div>
          </q-card-section>
        </q-card>

        <!-- Billing -->
        <q-card flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">Billing</q-card-section>
          <q-separator />
          <q-card-section>
            <div class="row q-col-gutter-sm">
              <app-text-field v-model="payload.billingContactName" label="Billing Contact" class="col-12 col-sm-6" />
              <app-text-field
                v-model="payload.billingEmail" label="Billing Email" type="email" class="col-12 col-sm-6"
                :error="!!errors.billingEmail" :error-message="errors.billingEmail"
              />
            </div>
            <!-- The billing ADDRESS moved up to the Addresses card with the other two, so this block is
                 only the person to bill. -->
          </q-card-section>
        </q-card>

        <!-- Remaining-required checklist (client-side gate for Review). -->
        <q-card v-if="clientIssues.length" flat bordered class="pef-card pef-todo q-mb-md">
          <q-card-section>
            <div class="row items-center q-gutter-xs text-grey-8">
              <q-icon name="o_checklist" color="primary" />
              <span class="text-weight-medium">Complete these to continue to review:</span>
            </div>
            <ul class="q-my-xs q-pl-lg text-grey-8">
              <li v-for="(m, i) in clientIssues" :key="i">{{ m }}</li>
            </ul>
          </q-card-section>
        </q-card>

        <!-- Action bar -->
        <div class="pef-actionbar">
          <q-btn flat no-caps color="grey-8" icon="o_close" label="Cancel" :disable="busy" @click="onCancel" />
          <div class="pef-save" :class="`pef-save--${saveState}`">
            <q-spinner v-if="saveState === 'saving'" size="16px" color="primary" />
            <q-icon v-else :name="saveIcon" size="18px" />
            <span>{{ saveText }}</span>
          </div>
          <q-btn
            unelevated no-caps color="primary" icon-right="o_arrow_forward" label="Review"
            :disable="!canReview || busy" :loading="reviewing" @click="onReview"
          >
            <q-tooltip v-if="!canReview">Complete the required fields to continue.</q-tooltip>
          </q-btn>
        </div>
      </template>

      <!-- ============================ REVIEW STEP ============================ -->
      <template v-else>
        <q-card flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">
            Review your details
            <div class="text-caption text-grey-7 text-weight-regular">
              Please confirm everything is correct before submitting. Nothing is submitted until you confirm.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <rems-review-summary
              :payload="reviewPayload" :industry-group="industryGroup" :locked-email="payload.email"
              :referral-sources="referralSources"
            />
          </q-card-section>
        </q-card>

        <div class="pef-actionbar">
          <q-btn
            flat no-caps color="primary" icon="o_arrow_back" label="Go Back and Edit"
            :disable="busy" @click="goBackToForm"
          />
          <q-space />
          <q-btn
            unelevated no-caps color="positive" icon="o_check" label="Confirm & Submit"
            :loading="submitting" :disable="busy" @click="onSubmit"
          />
        </div>
      </template>
    </template>
  </div>
</template>

<script setup>
// Public, anonymous REMS client EMS form (WO-116, Part B). Loads its state by invite code via the
// unauthenticated remsPublicApi, renders one of Invalid/Unavailable/Submitted/Editable, auto-saves the
// draft as a durable RemsFormPayloadV1, and drives the Review → Submit → thank-you flow. No auth/tenant
// stores are touched — everything is authorised by the invite code alone.
import { ref, reactive, computed, watch, onMounted, nextTick } from "vue";
import { useRoute } from "vue-router";
import { debounce } from "quasar";
import { remsPublicApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import {
  blankAddress, toAddress, fromAddress, addressErrors, addressComplete
} from "modules/rems/remsAddress";
import { REMS_OPTION_SEED } from "modules/rems/useRemsOptionCatalog";
import { isBusinessIndustryGroup } from "modules/rems/useRemsMeta";

import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppAddressFields from "components/common/AppAddressFields.vue";
import RoleContactFields from "modules/rems/components/RoleContactFields.vue";
import RemsReviewSummary from "modules/rems/components/RemsReviewSummary.vue";

// The roles relevant to each industry group, in display order (mirrors RemsFormPayloadValidator).
const GROUP_ROLES = {
  individual: ["self", "spouse"],
  business: ["ceo", "cfo", "accountsPayable", "banker", "lawyer"],
  government: ["financeDirector", "accountsPayable"]
};
const ROLE_KEYS = ["self", "spouse", "ceo", "cfo", "accountsPayable", "banker", "lawyer", "financeDirector"];
// The chip above the form. `business` is kept for forms sent before it was split into the three below.
const INDUSTRY_LABELS = {
  individual: "Individual",
  not_for_profit: "Not-for-Profit",
  insurance: "Insurance",
  commercial: "Commercial",
  business: "Business",
  government: "Government"
};

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const inviteCode = route.params.inviteCode;

// The referral-source list. Seeded from the shared catalogue so the picker is never empty on first
// paint, then replaced by whatever the server sends with the form — the tenant's own wording and
// descriptions. Each option carries `description`, which is the tooltip / caption for that value.
const referralSources = ref([...REMS_OPTION_SEED.referralSource]);
const referralOptions = computed(() => referralSources.value);
const referralDetailPlaceholder = computed(() => {
  const chosen = referralSources.value.find((o) => o.value === payload.referralSource);
  // The option's own description is the best prompt for the follow-up — "Friend, Family, or Colleague"
  // says what to type far better than a generic "Please provide details".
  return chosen?.description || "Please provide details";
});

// ---- Screen state ----
const loading = ref(true);
const loadFailed = ref(false);
const state = ref("");          // "Invalid" | "Unavailable" | "Submitted" | "Editable"
const cancelled = ref(false);
const thankYouName = ref("");
const industryGroup = ref("");  // lowercase code: individual | business | government
const step = ref("form");       // "form" | "review"

// ---- Save / validation state ----
const ready = ref(false);       // becomes true once the initial seed completes (gates autosave)
const reviewing = ref(false);
const submitting = ref(false);
const errors = ref({});         // per-field server messages, keyed by payload path (e.g. "roles.self.email")
const serverSummary = ref([]);  // flat list of server messages for the top banner
const saveState = ref("idle");  // "idle" | "saving" | "saved" | "error"

const hasRelatedEntities = ref(false);

// ---- The RemsFormPayloadV1 (camelCase wire shape — field names match RemsPublicFormModels.cs exactly) ----
// EXCEPT the addresses: those are held in the canonical AppAddressFields shape and converted to/from the
// frozen wire names by modules/rems/remsAddress (toAddress on seed, fromAddress on build).
const blankRole = () => ({ name: "", email: "", phone: "" });

const payload = reactive({
  version: 1,
  clientName: "",
  email: "",            // LOCKED (from prefill; ignored on submit)
  mobileNumber: "",
  referralSource: "",
  referralSourceDetail: "",
  physicalAddress: blankAddress(),
  mailingAddress: blankAddress(),
  billingContactName: "",
  billingEmail: "",
  billingAddress: blankAddress(),
  spouseName: "",
  spousePhone: "",
  spouseEmail: "",
  ein: "",
  contractStartDate: "",
  contractEndDate: "",
  originalTerm: "",
  renewalTerms: "",
  poStartDate: "",
  poEndDate: "",
  roles: {
    self: blankRole(),
    spouse: blankRole(),
    ceo: blankRole(),
    cfo: blankRole(),
    accountsPayable: blankRole(),
    banker: blankRole(),
    lawyer: blankRole(),
    financeDirector: blankRole()
  },
  relatedEntities: []   // [{ sourceKey, fullName, emailAddress, phoneNumber }]
});

// ---- Derived ----
const isIndividual = computed(() => industryGroup.value === "individual");
const isBusiness = computed(() => isBusinessIndustryGroup(industryGroup.value));
const isGovernment = computed(() => industryGroup.value === "government");
const industryLabel = computed(() => INDUSTRY_LABELS[industryGroup.value] || "Onboarding");
const showUnavailable = computed(() => loadFailed.value || state.value === "Invalid" || state.value === "Unavailable");
const busy = computed(() => reviewing.value || submitting.value);

// Whether an address has anything in it at all — what decides if there is something worth copying.
const hasAny = (address) =>
  !!address && Object.values(address).some((v) => typeof v === "string" && v.trim() !== "");

// Copy one address into another, ONCE. Deliberately not a live mirror: the client can correct the copy
// afterwards, and a later edit to the source must not silently drag the copy along with it — which is
// exactly what the old "same as physical" radio did, and why an amended physical address used to move
// the billing address without anyone asking.
//
// Assigned field by field into the EXISTING object rather than replacing it, so the bound model instance
// survives (AppAddressFields resolves its country → state → city cascade from that instance).
const copyAddress = (fromKey, toKey) => {
  Object.assign(payload[toKey], { ...payload[fromKey] });
};

const roleDefs = computed(() => {
  if (isIndividual.value) {
    return [{ key: "self", label: "Self", required: true }, { key: "spouse", label: "Spouse", required: false }];
  }
  if (isBusiness.value) {
    return [
      { key: "ceo", label: "CEO", required: true },
      { key: "cfo", label: "CFO", required: true },
      { key: "accountsPayable", label: "Accounts Payable", required: true },
      { key: "banker", label: "Banker", required: false },
      { key: "lawyer", label: "Lawyer", required: false }
    ];
  }
  if (isGovernment.value) {
    return [
      { key: "financeDirector", label: "Finance Director", required: true },
      { key: "accountsPayable", label: "Accounts Payable", required: false }
    ];
  }
  return [];
});

const entityErr = (i, field) => errors.value[`relatedEntities[${i}].${field}`] || "";

// ---- Save indicator ----
const saveIcon = computed(() => ({
  saved: "o_cloud_done", error: "o_cloud_off", idle: "o_cloud_queue"
}[saveState.value] || "o_cloud_queue"));
const saveText = computed(() => ({
  saving: "Saving…",
  saved: "Saved just now",
  error: "Couldn't save — we'll retry as you edit"
}[saveState.value] || "Your progress saves automatically"));

// ---- Client-side validation (mirrors RemsFormPayloadValidator; gates the Review button) ----
const filled = (v) => !!String(v ?? "").trim();
const emailOk = (v) => /^\S+@\S+\.\S+$/.test(String(v ?? "").trim());
const roleAny = (r) => filled(r.name) || filled(r.email) || filled(r.phone);
// Phone is captured when known but never required — a contact is a name and a valid email.
// Mirrors RemsFormPayloadValidator.ValidateRoleFields.
const roleComplete = (r) => filled(r.name) && emailOk(r.email);

const clientIssues = computed(() => {
  const out = [];
  if (!filled(payload.clientName)) out.push("Client name is required.");
  const addressIssue = "needs country, state, city, address line 1 and zip code.";
  if (!addressComplete(payload.physicalAddress)) out.push(`Physical address ${addressIssue}`);
  // Both are required now: there is no "same as" flag deciding whether a mailing address exists, only a
  // copy button that fills it in for you.
  if (!addressComplete(payload.mailingAddress)) out.push(`Mailing address ${addressIssue}`);
  if (filled(payload.billingEmail) && !emailOk(payload.billingEmail)) out.push("Billing email is not a valid email address.");

  const roles = payload.roles;
  const req = (key, label) => { if (!roleComplete(roles[key])) out.push(`${label} contact needs a name and a valid email.`); };
  const opt = (key, label) => {
    if (roleAny(roles[key]) && !roleComplete(roles[key])) {
      out.push(`${label} contact is partly filled — add a name and a valid email, or clear it.`);
    }
  };

  if (isIndividual.value) {
    req("self", "Self");
    opt("spouse", "Spouse");
  } else if (isBusiness.value) {
    if (!filled(payload.ein)) out.push("EIN is required for a business.");
    req("ceo", "CEO"); req("cfo", "CFO"); req("accountsPayable", "Accounts Payable");
    opt("banker", "Banker"); opt("lawyer", "Lawyer");
  } else if (isGovernment.value) {
    req("financeDirector", "Finance Director");
    opt("accountsPayable", "Accounts Payable");
  }

  // Name and email both required — the phone stays optional, as it is on every contact on this form.
  payload.relatedEntities.forEach((e, i) => {
    if (!filled(e.fullName)) out.push(`Entity #${i + 1} needs a client / entity name.`);
    if (!filled(e.emailAddress)) {
      out.push(`Entity #${i + 1} needs an email address.`);
    } else if (!emailOk(e.emailAddress)) {
      out.push(`Entity #${i + 1} has an invalid email address.`);
    }
  });

  return out;
});
const canReview = computed(() => clientIssues.value.length === 0);

// The review step shows the payload exactly as it will be submitted (wire shape, addresses converted).
const reviewPayload = computed(() => buildPayload());

// ---- Build the outgoing payload (dates: "" → null so DateOnly binds; mailing dropped when same) ----
const s = (v) => (v == null ? "" : String(v));
const dateOrNull = (v) => (filled(v) ? v : null);
const outRole = (r) => ({ name: s(r.name), email: s(r.email), phone: s(r.phone) });

function buildRoles () {
  // The three business groups share one role set, so they all look up under "business".
  const keys = GROUP_ROLES[isBusinessIndustryGroup(industryGroup.value) ? "business" : industryGroup.value] || ROLE_KEYS;
  const out = {};
  keys.forEach((k) => { out[k] = outRole(payload.roles[k]); });
  return out;
}

function buildPayload () {
  return {
    version: 1,
    clientName: s(payload.clientName),
    email: s(payload.email),
    mobileNumber: s(payload.mobileNumber),
    referralSource: s(payload.referralSource),
    referralSourceDetail: s(payload.referralSourceDetail),
    physicalAddress: fromAddress(payload.physicalAddress),
    mailingAddress: fromAddress(payload.mailingAddress),
    billingContactName: s(payload.billingContactName),
    billingEmail: s(payload.billingEmail),
    billingAddress: fromAddress(payload.billingAddress),
    spouseName: s(payload.spouseName),
    spousePhone: s(payload.spousePhone),
    spouseEmail: s(payload.spouseEmail),
    ein: s(payload.ein),
    contractStartDate: dateOrNull(payload.contractStartDate),
    contractEndDate: dateOrNull(payload.contractEndDate),
    originalTerm: s(payload.originalTerm),
    renewalTerms: s(payload.renewalTerms),
    poStartDate: dateOrNull(payload.poStartDate),
    poEndDate: dateOrNull(payload.poEndDate),
    roles: buildRoles(),
    relatedEntities: payload.relatedEntities.map((e, i) => ({
      sourceKey: e.sourceKey || `related-${i + 1}`,
      fullName: s(e.fullName),
      emailAddress: s(e.emailAddress),
      phoneNumber: s(e.phoneNumber)
    }))
  };
}

// ---- Seeding (prefill + draft) ----
function fillRole (target, src) {
  target.name = src?.name ?? "";
  target.email = src?.email ?? "";
  target.phone = src?.phone ?? "";
}
function makeEntity (e, i) {
  return {
    sourceKey: e?.sourceKey || `related-${Date.now()}-${i}`,
    fullName: e?.fullName ?? "",
    emailAddress: e?.emailAddress ?? "",
    phoneNumber: e?.phoneNumber ?? ""
  };
}

function seed (prefill, draft) {
  ready.value = false;
  const d = draft || {};

  payload.clientName = d.clientName ?? prefill?.clientName ?? "";
  payload.email = prefill?.email ?? d.email ?? "";   // LOCKED to the request's customer email.
  payload.mobileNumber = d.mobileNumber ?? prefill?.mobileNumber ?? "";
  payload.referralSource = d.referralSource ?? "";
  payload.referralSourceDetail = d.referralSourceDetail ?? "";
  payload.billingContactName = d.billingContactName ?? "";
  payload.billingEmail = d.billingEmail ?? "";
  payload.spouseName = d.spouseName ?? "";
  payload.spousePhone = d.spousePhone ?? "";
  payload.ein = d.ein ?? "";
  payload.originalTerm = d.originalTerm ?? "";
  payload.renewalTerms = d.renewalTerms ?? "";
  payload.contractStartDate = d.contractStartDate ?? "";
  payload.contractEndDate = d.contractEndDate ?? "";
  payload.poStartDate = d.poStartDate ?? "";
  payload.poEndDate = d.poEndDate ?? "";

  payload.physicalAddress = toAddress(d.physicalAddress);
  payload.mailingAddress = toAddress(d.mailingAddress);
  payload.billingAddress = toAddress(d.billingAddress);

  ROLE_KEYS.forEach((k) => fillRole(payload.roles[k], d.roles?.[k]));

  payload.relatedEntities = (d.relatedEntities || []).map(makeEntity);
  hasRelatedEntities.value = payload.relatedEntities.length > 0;

  // Let the reactive writes flush before re-arming autosave, so seeding never triggers a save.
  nextTick(() => { ready.value = true; });
}

// ---- Load ----
function applySubmitted (res) {
  state.value = "Submitted";
  thankYouName.value = res?.clientName || payload.clientName || "";
  step.value = "form";
}

async function load () {
  loading.value = true;
  loadFailed.value = false;
  try {
    const res = await remsPublicApi.load(inviteCode);
    state.value = res?.state || "Invalid";
    if (res?.state === "Submitted") {
      thankYouName.value = res.clientName || "";
    } else if (res?.state === "Editable") {
      industryGroup.value = String(res.industryGroup || "").toLowerCase();
      // The tenant's own REMS.ReferralSource list, resolved server-side because this page has no
      // session to resolve it with. Absent (an emptied or missing list) leaves the built-in copy.
      if (res.referralSources?.length) referralSources.value = res.referralSources;
      seed(res.prefill, res.draftPayload);
    }
  } catch {
    loadFailed.value = true;
  } finally {
    loading.value = false;
  }
}

// ---- Autosave (debounced) ----
async function doSave () {
  if (!ready.value || state.value !== "Editable") return;
  saveState.value = "saving";
  try {
    const res = await remsPublicApi.saveDraft(inviteCode, buildPayload());
    saveState.value = "saved";
    return res;
  } catch {
    saveState.value = "error";
  }
}
const scheduleSave = debounce(doSave, 1000);

watch(payload, () => {
  // Any edit clears stale server highlights…
  if (Object.keys(errors.value).length || serverSummary.value.length) {
    errors.value = {};
    serverSummary.value = [];
  }
  // …and (once seeded, while editable) schedules a durable autosave.
  if (ready.value && state.value === "Editable") {
    saveState.value = "saving";
    scheduleSave();
  }
}, { deep: true });

// ---- Server validation → per-field + summary ----
function applyServerValidation (err) {
  const details = err?.response?.data?.error?.details || "";
  const map = {};
  const list = [];
  details.split(";").forEach((chunk) => {
    const piece = chunk.trim();
    if (!piece) return;
    const idx = piece.indexOf(":");
    if (idx === -1) { list.push(piece); return; }
    const field = piece.slice(0, idx).trim();
    const message = piece.slice(idx + 1).trim();
    map[field] = message;
    list.push(message);
  });
  errors.value = map;
  serverSummary.value = list.length ? list : ["One or more fields need your attention."];
}

// Non-validation failures (409 not-editable / 404 / network) → resync the load state so the client sees
// the real terminal state (e.g. the admin cancelled, or it was already submitted in another tab).
async function handleActionError (err, fallback) {
  if (getApiErrorCode(err) === ApiErrorCodes.ValidationFailed) {
    applyServerValidation(err);
    step.value = "form";
    notify.error("Please fix the highlighted fields.");
    scrollTop();
    return;
  }
  notify.error(getApiErrorMessage(err, fallback));
  await load();
}

const scrollTop = () => { try { window.scrollTo({ top: 0, behavior: "smooth" }); } catch { /* noop */ } };

// ---- Review / Submit / Cancel ----
async function onReview () {
  if (!canReview.value) return;
  reviewing.value = true;
  errors.value = {};
  serverSummary.value = [];
  try {
    // Persist the latest before validating server-side (belt-and-suspenders with autosave).
    await doSave();
    await remsPublicApi.review(inviteCode, buildPayload());
    step.value = "review";
    scrollTop();
  } catch (err) {
    await handleActionError(err, "We couldn't validate your form. Please try again.");
  } finally {
    reviewing.value = false;
  }
}

function goBackToForm () {
  step.value = "form";
  scrollTop();
}

async function onSubmit () {
  submitting.value = true;
  errors.value = {};
  serverSummary.value = [];
  try {
    const res = await remsPublicApi.submit(inviteCode, buildPayload());
    applySubmitted(res);
    scrollTop();
  } catch (err) {
    await handleActionError(err, "We couldn't submit your form. Please try again.");
  } finally {
    submitting.value = false;
  }
}

async function onCancel () {
  const ok = await confirm({
    title: "Leave this form?",
    message: "Your progress is saved automatically. You can return anytime using the link in your email.",
    confirmLabel: "Leave form",
    cancelLabel: "Keep editing"
  });
  if (!ok) return;
  try { await remsPublicApi.cancel(inviteCode); } catch { /* non-destructive — ignore */ }
  cancelled.value = true;
  scrollTop();
}

// ---- Related businesses ----
const entityHasData = (e) => filled(e.fullName) || filled(e.emailAddress) || filled(e.phoneNumber);

function addEntity () {
  payload.relatedEntities.push({
    sourceKey: `related-${Date.now()}-${payload.relatedEntities.length}`,
    fullName: "",
    emailAddress: "",
    phoneNumber: ""
  });
}

function removeEntity (i) {
  payload.relatedEntities.splice(i, 1);
  if (!payload.relatedEntities.length) hasRelatedEntities.value = false;
}

async function onToggleRelated (val) {
  if (val) {
    hasRelatedEntities.value = true;
    if (!payload.relatedEntities.length) addEntity();
    return;
  }
  if (payload.relatedEntities.some(entityHasData)) {
    const ok = await confirm({
      title: "Remove other entities?",
      message: "This will remove every other entity you've added to this form.",
      confirmLabel: "Remove",
      type: "danger"
    });
    if (!ok) { hasRelatedEntities.value = true; return; }
  }
  payload.relatedEntities = [];
  hasRelatedEntities.value = false;
}

onMounted(load);
</script>

<style scoped>
.pef {
  padding-bottom: 12px;
}
.pef-head {
  margin-bottom: 16px;
}
.pef-card {
  border-radius: 12px;
}
.pef-card__head {
  font-size: 15px;
  font-weight: 600;
  color: var(--q-primary);
}
.pef-note {
  margin-top: 32px;
}
.pef-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 8px;
}
/* Heading and its copy button on one baseline. The button sits with the label it fills in, so it reads as
   "this address, copied from that one" rather than as a stray action above the fields. */
.pef-addr-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.pef-addr-head .pef-subhead {
  margin-bottom: 0;
}
.pef-entity {
  border-radius: 10px;
  background: #fbfcfe;
}
.pef-todo {
  background: #f7f9fc;
}
.pef-actionbar {
  position: sticky;
  bottom: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 4px;
  margin-top: 8px;
  background: #f4f6fb;
  border-top: 1px solid #e0e6ed;
}
.pef-save {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12.5px;
  color: #7a8699;
  margin-left: auto;
  margin-right: auto;
}
.pef-save--saved {
  color: var(--q-positive);
}
.pef-save--error {
  color: var(--q-negative);
}
</style>
