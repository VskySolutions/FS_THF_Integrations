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
      <!-- The entity type is NOT shown. It is THF's classification of the client, not something the
           client told us or is being asked to confirm — it decides which questions appear below, and
           that is the whole of its job here. A chip stating it invited the client to query a label they
           were never asked about and cannot change. -->
      <div class="pef-head">
        <div class="row items-center q-gutter-sm">
          <div class="text-h5 text-weight-bold col">EMS Onboarding Form</div>
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
              <!-- An individual is a person, so the name is asked as two boxes and stays two: the record
                   we file you under has a given name and a family name, and one box left us guessing
                   where to cut it. A business or a government body has ONE name — its legal name — which
                   does not divide, so it keeps the single box. -->
              <template v-if="isIndividual">
                <app-text-field
                  v-model="payload.clientFirstName" label="First Name" required class="col-12 col-sm-3"
                  :error="!!errors.clientFirstName" :error-message="errors.clientFirstName"
                />
                <app-text-field
                  v-model="payload.clientLastName" label="Last Name" required class="col-12 col-sm-3"
                  :error="!!errors.clientLastName" :error-message="errors.clientLastName"
                />
              </template>
              <app-text-field
                v-else
                v-model="payload.clientName" label="Client/Entity Name" required class="col-12 col-sm-6"
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
            <!-- Each heading carries what that address IS. Three addresses under three similar names is
                 the one place on this form where a client can reasonably give the right answer to the
                 wrong question — a mailing address typed into the physical box sends nothing anywhere
                 wrong, but a physical address typed into the billing box sends the invoice to a building
                 nobody opens post in. The note is on the heading, a hover away, rather than as three
                 caption lines the client reads once and never again. -->
            <div class="pef-addr-head">
              <div class="pef-subhead">
                Physical Address
                <q-icon name="o_info" size="15px" color="grey-6" class="pef-subhead__info">
                  <q-tooltip anchor="top middle" self="bottom middle" max-width="300px" :delay="200">
                    {{ ADDRESS_HINTS.physical }}
                  </q-tooltip>
                </q-icon>
              </div>
            </div>
            <app-address-fields
              v-model="payload.physicalAddress" required :errors="addressErrors(errors, 'physicalAddress')"
            />

            <div class="pef-addr-head q-mt-lg">
              <div class="pef-subhead">
                Mailing Address
                <q-icon name="o_info" size="15px" color="grey-6" class="pef-subhead__info">
                  <q-tooltip anchor="top middle" self="bottom middle" max-width="300px" :delay="200">
                    {{ ADDRESS_HINTS.mailing }}
                  </q-tooltip>
                </q-icon>
              </div>
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
              <div class="pef-subhead">
                Billing Address
                <q-icon name="o_info" size="15px" color="grey-6" class="pef-subhead__info">
                  <q-tooltip anchor="top middle" self="bottom middle" max-width="300px" :delay="200">
                    {{ ADDRESS_HINTS.billing }}
                  </q-tooltip>
                </q-icon>
              </div>
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

        <!-- Billing: who to send the invoice to, directly under the address it goes to. It used to sit at
             the very bottom, three cards below the billing address, which put one answer's two halves at
             opposite ends of the form.
             INDIVIDUAL ONLY. Every other entity type names a Billing Contact in the Contacts card below,
             with a first name, a last name, an email and a phone — asking the same person again here, in
             two weaker boxes, invited two different answers for one billing contact. -->
        <q-card v-if="isIndividual" flat bordered class="pef-card q-mb-md">
          <q-card-section class="pef-card__head">
            Billing
            <div class="text-caption text-grey-7 text-weight-regular">
              Who should receive our invoices, if it is not you.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div class="row q-col-gutter-sm">
              <app-text-field v-model="payload.billingContactName" label="Billing Contact" class="col-12 col-sm-6" />
              <app-text-field
                v-model="payload.billingEmail" label="Billing Email" type="email" class="col-12 col-sm-6"
                :error="!!errors.billingEmail" :error-message="errors.billingEmail"
              />
            </div>
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
              Required contacts need a first name, a last name and an email. Phone is optional.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section class="column q-gutter-md">
            <role-contact-fields
              v-for="def in roleDefs" :key="def.key"
              v-model="payload.roles[def.key]"
              :label="def.label" :hint="def.hint" :required="def.required"
              :prefix="`roles.${def.key}`" :errors="errors"
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
                    <!-- The same dial-code + number control the client's own phone above uses, rather than
                         the plain box this was. These numbers are dialled by staff chasing an entity that
                         has not answered, and a bare string gave no country to read them against — the
                         component stores E.164, which carries it. Wrapped in the grid cell rather than
                         given the column classes itself, matching the phone field above. -->
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

        <!-- The Billing card stood here, last on the form. It has moved up to sit under the billing
             ADDRESS it belongs with — and it is asked of individuals only now: every other entity type
             names a Billing Contact in the Contacts card above. -->

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
import {
  ALL_ROLE_KEYS, GROUP_ROLES, groupKey, normalizeRoles, roleDefsFor
} from "modules/rems/remsContactRoles";

import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppAddressFields from "components/common/AppAddressFields.vue";
import RoleContactFields from "modules/rems/components/RoleContactFields.vue";
import RemsReviewSummary from "modules/rems/components/RemsReviewSummary.vue";

// The role sets, their labels and their hints come from modules/rems/remsContactRoles — one definition,
// shared with the review step and with the panel staff read the submission in.

// What each of the three addresses is, said on the heading it belongs to rather than in a caption line.
const ADDRESS_HINTS = {
  physical: "Where the business actually operates, or where you live — the address we would visit. Not a PO box.",
  mailing: "Where post should reach you. Use this for a PO box, or if your post goes somewhere other than the physical address.",
  billing: "Where invoices should be sent, if that is not your mailing address. Leave it blank and we will bill the mailing address."
};

// INDUSTRY_LABELS stood here — the display names behind a chip that named the client's entity type above
// the form. The chip is gone (see the template) and with it the only thing that ever read the map. The
// entity type itself is still very much in use below; it just does its work silently now, deciding which
// questions the client is asked.

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
const blankRole = () => ({ firstName: "", lastName: "", email: "", phone: "" });
const blankRoles = () => Object.fromEntries(ALL_ROLE_KEYS.map((k) => [k, blankRole()]));

const payload = reactive({
  version: 1,
  // The client's name, in one field and in two. An individual fills the two and `clientName` is built
  // from them on the way out; a business or government body fills `clientName` and leaves the two blank.
  clientName: "",
  clientFirstName: "",
  clientLastName: "",
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
  roles: blankRoles(),
  relatedEntities: []   // [{ sourceKey, fullName, emailAddress, phoneNumber }]
});

// ---- Derived ----
const isIndividual = computed(() => industryGroup.value === "individual");
const isBusiness = computed(() => isBusinessIndustryGroup(industryGroup.value));
const isGovernment = computed(() => industryGroup.value === "government");
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

// Which role set this client is asked, from the shared definition. The retired Banker and Lawyer are not
// among them: a client's banker and lawyer are their advisers rather than the firm's contacts on the
// engagement, and both boxes came back blank on almost every form. Anything a returning client already
// answered under a retired role travels with the payload and shows on review — it is simply not asked
// for again.
const roleSetKey = computed(() => groupKey(industryGroup.value, isBusiness.value));
const roleDefs = computed(() => (roleSetKey.value ? roleDefsFor(roleSetKey.value) : []));

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
const roleAny = (r) =>
  filled(r.firstName) || filled(r.lastName) || filled(r.name) || filled(r.email) || filled(r.phone);
// Phone is captured when known but never required — a contact is a name and a valid email.
// Mirrors RemsFormPayloadValidator.ValidateRoleFields, including its allowance for a payload written
// before the name was two boxes: that one carries `name` alone and is accepted as it stands rather than
// asking a client to retype a name they already gave.
const rolePreSplit = (r) => !filled(r.firstName) && !filled(r.lastName) && filled(r.name);
const roleComplete = (r) =>
  (rolePreSplit(r) || (filled(r.firstName) && filled(r.lastName))) && emailOk(r.email);

const clientIssues = computed(() => {
  const out = [];
  if (isIndividual.value) {
    if (!filled(payload.clientFirstName)) out.push("First name is required.");
    if (!filled(payload.clientLastName)) out.push("Last name is required.");
  } else if (!filled(payload.clientName)) {
    out.push("Client / entity name is required.");
  }
  const addressIssue = "needs country, state, city, address line 1 and zip code.";
  if (!addressComplete(payload.physicalAddress)) out.push(`Physical address ${addressIssue}`);
  // Both are required now: there is no "same as" flag deciding whether a mailing address exists, only a
  // copy button that fills it in for you.
  if (!addressComplete(payload.mailingAddress)) out.push(`Mailing address ${addressIssue}`);
  if (filled(payload.billingEmail) && !emailOk(payload.billingEmail)) out.push("Billing email is not a valid email address.");

  // Driven off the same role definitions the cards are rendered from, so a role added or retired changes
  // in one place rather than in two that can disagree.
  if (isBusiness.value && !filled(payload.ein)) out.push("EIN is required for a business.");
  roleDefs.value.forEach(({ key, label, required }) => {
    const role = payload.roles[key];
    if (required) {
      if (!roleComplete(role)) out.push(`${label} needs a first name, a last name and a valid email.`);
    } else if (roleAny(role) && !roleComplete(role)) {
      out.push(`${label} is partly filled — complete the name and email, or clear it.`);
    }
  });

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
// `name` is sent alongside the two parts, not instead of them: it is the pair already joined, so every
// reader of "the contact's name" — the review summary, the staff panel, the Person that gets minted —
// has one field to read. A pre-split contact the client has not retouched keeps whatever it arrived with.
const outRole = (r) => {
  const joined = [r.firstName, r.lastName].map((v) => s(v).trim()).filter(Boolean).join(" ");
  return {
    firstName: s(r.firstName),
    lastName: s(r.lastName),
    name: joined || s(r.name),
    email: s(r.email),
    phone: s(r.phone)
  };
};

function buildRoles () {
  // The roles this client is ASKED, plus any they have already answered under a role the form has since
  // retired — dropping those on the next autosave would delete an answer the client gave us.
  const asked = GROUP_ROLES[roleSetKey.value] || ALL_ROLE_KEYS;
  const answeredElsewhere = ALL_ROLE_KEYS.filter((k) => !asked.includes(k) && roleAny(payload.roles[k]));
  const out = {};
  [...asked, ...answeredElsewhere].forEach((k) => { out[k] = outRole(payload.roles[k]); });
  return out;
}

// The client's name as one string: the two boxes joined for an individual, the single box otherwise.
// Mirrors RemsFormPayloadV1.EffectiveClientName, which is what the server files them under.
function buildClientName () {
  const joined = [payload.clientFirstName, payload.clientLastName]
    .map((v) => s(v).trim()).filter(Boolean).join(" ");
  return joined || s(payload.clientName);
}

function buildPayload () {
  return {
    version: 1,
    clientName: buildClientName(),
    clientFirstName: s(payload.clientFirstName),
    clientLastName: s(payload.clientLastName),
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
// A draft saved before the name was split carries only `name`. It is kept AS `name` rather than cut into
// two on the client's behalf: guessing where a name divides is precisely what the two boxes exist to
// stop, so the answer stands until the client edits it — at which point they fill in the two boxes and
// the single string is superseded (see outRole).
function fillRole (target, src) {
  target.firstName = src?.firstName ?? "";
  target.lastName = src?.lastName ?? "";
  target.name = src?.firstName || src?.lastName ? "" : (src?.name ?? "");
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
  // The two parts come from the draft where the client has already given them, and from the prefill's own
  // split of the name staff typed at intake where they have not. `?? ""` rather than a fallback chain into
  // clientName: a business's single name is not a first name, and prefilling one into that box would put
  // "Acme Holdings" where a given name goes the moment somebody switched an entity type.
  payload.clientFirstName = d.clientFirstName ?? prefill?.clientFirstName ?? "";
  payload.clientLastName = d.clientLastName ?? prefill?.clientLastName ?? "";
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

  // Normalized first: a draft filled in under the old business role names still has its three contacts,
  // and they belong in the boxes those roles are called by now.
  const draftRoles = normalizeRoles(d.roles);
  ALL_ROLE_KEYS.forEach((k) => fillRole(payload.roles[k], draftRoles[k]));

  payload.relatedEntities = (d.relatedEntities || []).map(makeEntity);
  hasRelatedEntities.value = payload.relatedEntities.length > 0;

  // Let the reactive writes flush before re-arming autosave, so seeding never triggers a save.
  nextTick(() => { ready.value = true; });
}

// ---- Load ----
function applySubmitted (res) {
  state.value = "Submitted";
  thankYouName.value = res?.clientName || buildClientName() || "";
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
/* What this address IS, a hover away on the heading it belongs to. Not uppercased with the rest of the
   heading — it is an icon, and the tooltip carries the words. */
.pef-subhead__info {
  margin-left: 5px;
  cursor: help;
  vertical-align: text-bottom;
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
  flex-wrap: wrap;
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

/* The client fills this in on a phone as often as not. Below sm the three items across the bar do not
   fit on one line, so the save state takes the line above and the two buttons split the one below,
   each wide enough to be a thumb target rather than a 60px stub. */
@media (max-width: 599px) {
  .pef-actionbar {
    gap: 8px 10px;
    padding: 10px 0;
  }
  .pef-save {
    order: -1;
    width: 100%;
    margin: 0;
    justify-content: center;
  }
  /* Splits the row evenly between them; the spacer that separates the pair on a desktop would
     otherwise claim a third of the line for itself. */
  .pef-actionbar > .q-btn {
    flex: 1 1 0;
  }
  .pef-actionbar > .q-space {
    display: none;
  }
}
</style>
