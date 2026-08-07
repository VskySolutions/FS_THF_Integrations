<template>
  <app-form-drawer
    v-model="open"
    :title="requestId ? 'Edit REMS Request' : 'New REMS Request'"
    :save-label="primaryLabel"
    :saving="saving"
    @submit="onPrimary"
    @cancel="resetForm"
  >
    <q-form ref="formRef" greedy class="rems-intake">
      <div class="rems-intake__intro">
        Save it as a draft, or send it to the admin pool whenever it's ready.
      </div>

      <!-- ─── 1. Identify the client ──────────────────────────────────────────────────────────────── -->
      <div class="rems-step"><span class="rems-step__num">1</span>Identify the client</div>

      <!-- The search box IS the client-name field. Picking a result links the request to that THF record;
           typing a name nobody matched files it as a brand-new client under exactly what was typed. The
           q-menu hangs off this wrapper so it lines up under the input and inherits its width. -->
      <div class="app-field">
        <app-field-label label="Client" required />
        <q-input
          ref="clientFieldRef"
          v-model="clientQuery"
          outlined dense hide-bottom-space
          placeholder="Search by client name, email, or phone..."
          autocomplete="off"
          aria-label="Client"
          :error="attempted && !form.clientName"
          error-message="Search for the client, or type the new client's name."
          @update:model-value="onClientTyped"
          @focus="onClientFocus"
          @blur="onClientBlur"
          @keydown.down.prevent="moveActive(1)"
          @keydown.up.prevent="moveActive(-1)"
          @keydown.enter.prevent="onClientEnter"
          @keydown.esc="clientMenu = false"
        >
          <template #prepend>
            <q-icon
              :name="linkedClient ? 'o_verified' : 'o_search'"
              :color="linkedClient ? 'positive' : 'grey-6'"
            />
          </template>
          <template #append>
            <q-spinner v-if="clientLoading" size="18px" color="primary" />
            <q-icon
              v-else-if="clientQuery" name="o_close" color="grey-6" class="cursor-pointer"
              aria-label="Clear client" @click="clearClient"
            />
          </template>
        </q-input>

        <q-menu
          v-model="clientMenu"
          fit no-focus no-refocus no-parent-event
          anchor="bottom start" self="top start" :offset="[0, 6]"
        >
          <q-list separator>
            <!-- mousedown.prevent keeps focus in the search box, so choosing a result never fires the
                 blur that closes this menu out from under the click. -->
            <q-item
              v-for="(client, i) in clientOptions" :key="client.id"
              clickable :active="i === activeIndex" active-class="bg-grey-2 text-primary"
              @mousedown.prevent
              @click="pickClient(client)"
            >
              <q-item-section>
                <q-item-label>{{ client.name }}</q-item-label>
                <q-item-label caption>
                  {{ client.email || "no email" }} · {{ client.phone || "no phone" }}
                </q-item-label>
              </q-item-section>
            </q-item>
            <q-item v-if="!clientOptions.length">
              <q-item-section class="text-grey-7">
                No match — “{{ clientQuery.trim() }}” will be filed as a brand-new client.
              </q-item-section>
            </q-item>
          </q-list>
        </q-menu>
      </div>

      <!-- Below the divider: who to contact. Which client it is decides how the divider and the two
           fields read, but they stay put either way — a record's phone number can be out of date, and
           the partner is the one who just spoke to them. -->
      <div class="rems-divider">{{ linkedClient ? "Linked to a THF client record" : "No match / New to THF" }}</div>

      <div v-if="linkedClient" class="rems-linked">
        <q-icon name="o_business_center" size="18px" />
        <span class="rems-linked__name">{{ linkedClient.name }}</span>
        <q-space />
        <q-btn flat dense no-caps size="sm" color="primary" label="Unlink" @click="detachClient" />
      </div>

      <div class="row q-col-gutter-md">
        <!-- The email is outlined red on a failed save, but the wording lives on the shared hint below:
             the rule spans both fields, so saying it under either one alone would be half true. -->
        <app-text-field
          v-model="form.customerEmail" :label="contactLabels.email" type="email"
          placeholder="jane@company.com" class="col-12 col-md-5" :error="showContactError"
        />
        <app-phone-input
          v-model="form.customerMobileNumber" v-model:country="mobileCountry"
          :label="contactLabels.phone" class="col-12 col-md-7"
        />
      </div>
      <div class="rems-hint" :class="{ 'rems-hint--error': showContactError }">
        An admin needs a way to reach them — provide at least one of the two.
      </div>

      <div id="rems-type-question" class="rems-question">How does this referral relate to THF's records?</div>
      <div class="rems-chips" role="radiogroup" aria-labelledby="rems-type-question">
        <button
          v-for="opt in typeOptions" :key="opt.value"
          type="button" role="radio" :aria-checked="form.type === opt.value"
          class="rems-chip" :class="{ 'rems-chip--on': form.type === opt.value }"
          @click="chooseType(opt.value)"
        >
          {{ opt.label }}
        </button>
      </div>
      <div v-if="attempted && !form.type" class="rems-hint rems-hint--error">
        Choose how this referral relates to THF's records.
      </div>
      <div v-if="duplicateClientWarning" class="rems-hint rems-hint--error">
        {{ duplicateClientWarning }}
      </div>

      <!-- ─── 2. Describe the referral ────────────────────────────────────────────────────────────── -->
      <div class="rems-step"><span class="rems-step__num">2</span>Describe the referral</div>

      <div class="row q-col-gutter-md">
        <app-text-field
          v-model="form.title" label="Title" required class="col-12 col-sm-8"
          placeholder="e.g. Meridian Retail Group — new store buildout audit"
          :rules="[(v) => !!(v && v.trim()) || 'Title is required']"
        />
        <app-select
          v-model="form.priority" :options="priorityOptions" label="Priority" required
          class="col-12 col-sm-4" :clearable="false"
        />
      </div>

      <app-rich-text-field
        v-model="form.description" label="Message to Admin" class="q-mt-md"
        placeholder="Paste the client's email, summarize the ask, or add context an admin will need to open the engagement..."
      />
      <div v-if="descriptionNotice" class="rems-hint" :class="{ 'rems-hint--error': descriptionTooLong }">
        {{ descriptionNotice }}
      </div>

      <app-single-file-upload
        v-if="!requestId"
        v-model="attachment" label="Attachments" class="q-mt-md"
        hint="One supporting file (optional)."
      />

      <!-- Routing, and optional: naming an admin sends the request straight to them instead of into the
           pool. Hidden without rems.requests.assign — the server refuses the change either way. -->
      <app-select
        v-if="canAssign"
        v-model="form.assignAdminUserId" :options="assignOptions" label="Assign to Admin"
        class="q-mt-md" :loading="adminsLoading" clearable
        info="Users holding the Admin or Super Admin role in this tenant."
        hint="Leave empty to send it to the Admin Pool for any admin to pick up."
      />
    </q-form>

    <template #footer-actions>
      <!-- Create: Save Draft beside the primary Submit. Edit of a still-draft request: Submit beside Save. -->
      <q-btn
        v-if="!requestId"
        outline no-caps color="primary" label="Save Draft"
        :disable="saving || !canSave" @click="doSave(false)"
      />
      <q-btn
        v-else-if="isDraft"
        outline no-caps color="primary" :label="submitLabel"
        :disable="saving || !canSave" @click="doSave(true)"
      />
    </template>
  </app-form-drawer>
</template>

<script setup>
import { ref, reactive, computed, watch, onBeforeUnmount } from "vue";
import { remsApi, mediaApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import {
  useRemsOptionSets, useRemsMeta, REMS_EXISTING_CLIENT_TYPES,
  REMS_TYPE_BRAND_NEW_CLIENT, REMS_TYPE_EXISTING_CLIENT
} from "modules/rems/useRemsMeta";
import { usePermissions, Permissions } from "composables/usePermissions";

import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppSingleFileUpload from "components/common/AppSingleFileUpload.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppRichTextField from "components/common/AppRichTextField.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // When set the drawer edits the existing request; otherwise it creates a new one.
  requestId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const { typeOptions, priorityOptions, load: loadOptionSets } = useRemsOptionSets();
const { typeLabel } = useRemsMeta();
// Choosing who owns a request is its own right. Without it the picker is not offered and the request
// reaches the pool the way it always did — the server enforces the same split on the way in.
const { has } = usePermissions();
const canAssign = computed(() => has(Permissions.RemsRequestsAssign));

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const blankForm = () => ({
  title: "",
  description: "",
  type: "",
  priority: "medium",
  clientName: "",
  customerEmail: "",
  customerMobileNumber: "",
  existingClientReferenceId: null,
  assignAdminUserId: null
});

const form = reactive(blankForm());
const mobileCountry = ref(null);
const attachment = ref(null);
const loadedStatus = ref(null);
const formRef = ref(null);
const saving = ref(false);
// Set on the first save attempt. Until then the form stays quiet: a drawer that opens already covered in
// red for fields nobody has reached yet reads as broken rather than as guidance.
const attempted = ref(false);

const isDraft = computed(() => loadedStatus.value === "draft");
// The button says where the request is actually going: naming an admin sends it to them, not to the pool.
const submitLabel = computed(() => (form.assignAdminUserId ? "Submit & Assign" : "Submit to Admin Pool"));
const primaryLabel = computed(() => (props.requestId ? "Save Changes" : submitLabel.value));

// ---- Assign to Admin (optional on both create and edit) ----
const adminOptions = ref([]);
const adminsLoading = ref(false);
// The assignee the request was loaded with. An edit that never touches the picker must say nothing about
// assignment rather than re-asserting it — or, worse, reading as an instruction to clear it.
const loadedAdmin = ref(null);
const loadedAdminId = computed(() => loadedAdmin.value?.id || null);

// The current owner stays selectable even if they have since dropped off the Admin list, so opening the
// form on their request does not quietly offer to unassign them.
const assignOptions = computed(() => {
  const current = loadedAdmin.value;
  if (!current?.id || adminOptions.value.some((o) => o.value === current.id)) return adminOptions.value;
  return [{ label: current.name || "Current assignee", value: current.id }, ...adminOptions.value];
});

const loadAdmins = async () => {
  if (!canAssign.value) return;
  adminsLoading.value = true;
  try {
    const admins = await remsApi.admins();
    adminOptions.value = (admins || []).map((a) => ({ label: a.name, value: a.id }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    adminOptions.value = [];
  } finally {
    adminsLoading.value = false;
  }
};

// Only says something about the assignment when it actually changed.
const assignmentPatch = () => {
  if (!canAssign.value) return {};
  const next = form.assignAdminUserId || null;
  if (next === loadedAdminId.value) return {};
  return next ? { assignAdminUserId: next } : { unassignAdmin: true };
};

// ---- Client lookup: the search box doubles as the new-client name field ----
const clientQuery = ref("");
// The THF record this request is linked to, or null when the client is new to us.
const linkedClient = ref(null);
const clientOptions = ref([]);
const clientLoading = ref(false);
const clientMenu = ref(false);
const clientSearched = ref(false);
const activeIndex = ref(-1);
const clientFieldRef = ref(null);
// A lookup lands ~300ms after the last keystroke, by which time the partner may have moved on to the
// email field. The menu only opens over a box they are still in.
const clientFocused = ref(false);

const MIN_LOOKUP_CHARS = 2;
const LOOKUP_DEBOUNCE_MS = 300;

let lookupTimer = null;
// Bumped on every query so a slow response for an abandoned term cannot land on top of a newer one.
let lookupSeq = 0;

const runLookup = (term) => {
  clearTimeout(lookupTimer);
  lookupSeq += 1;
  const seq = lookupSeq;
  clientSearched.value = false;
  if (term.length < MIN_LOOKUP_CHARS) {
    clientLoading.value = false;
    clientOptions.value = [];
    clientMenu.value = false;
    return;
  }
  clientLoading.value = true;
  lookupTimer = setTimeout(async () => {
    let items = [];
    try {
      items = (await remsApi.clientLookup(term)) || [];
    } catch {
      // A failed lookup reads as "no match": the partner can still file the client as new, which is the
      // only thing they could have done with an empty result anyway.
      items = [];
    }
    if (seq !== lookupSeq) return;
    clientOptions.value = items;
    activeIndex.value = items.length ? 0 : -1;
    clientLoading.value = false;
    clientSearched.value = true;
    // Opened even when empty — "no match" is the answer that sends them down the brand-new branch.
    clientMenu.value = clientFocused.value;
    // Covers typing a full name and tabbing straight on: the results land after the box lost focus.
    linkExactMatchIfSettled();
  }, LOOKUP_DEBOUNCE_MS);
};

// Whether the partner answered the type question themselves (see chooseType). Only consulted where
// their answer could still be right — a linked record overrules it outright.
const typeChosenByUser = ref(false);

// A tenant can retire either code from their list. Auto-selecting one that is no longer offered would
// light no chip and then warn about a value nobody picked, so it degrades to leaving the question open.
const autoType = (code) => (typeOptions.value.some((o) => o.value === code) ? code : "");

// Keeps the type answer honest about the client below it. Called from every path that changes which
// client the request is about — typing, picking, unlinking, clearing.
const syncTypeToClient = () => {
  if (linkedClient.value) {
    // A linked record settles the question: THF already has this client. Both existing-client answers
    // say that, so a partner's "subsidiary" survives; anything else is corrected.
    if (!REMS_EXISTING_CLIENT_TYPES.includes(form.type)) form.type = autoType(REMS_TYPE_EXISTING_CLIENT);
    return;
  }
  // Nothing linked, so brand-new is the default answer — but not the only defensible one. The lookup
  // only covers Person records, so a partner may know THF has this client even when the search cannot
  // find them. Their own answer stands; otherwise the name alone decides.
  if (typeChosenByUser.value) return;
  form.type = form.clientName ? autoType(REMS_TYPE_BRAND_NEW_CLIENT) : "";
};

const onClientTyped = (val) => {
  const term = (val || "").trim();
  // Set before the sync below, which reads the name to tell "brand new" from "nothing entered yet".
  form.clientName = term;
  // Editing the box after picking somebody detaches the link: whatever is being typed now is not them.
  if (linkedClient.value && term !== (linkedClient.value.name || "").trim()) detachClient();
  syncTypeToClient();
  runLookup(term);
};

// What the lookup copied into the contact fields. Tracked so unlinking can take back exactly what it
// put there — and nothing else. Left empty in edit mode: a saved request's contact details were stored
// on the request, not auto-filled this session, so unlinking must not touch them.
const autofilled = reactive({ email: "", phone: "" });

const sameEmail = (a, b) =>
  String(a || "").trim().toLowerCase() === String(b || "").trim().toLowerCase();

// AppPhoneInput normalises whatever it is handed — a record's "(555) 010-2231" comes back as
// "+15550102231" — so recognising its own autofill cannot be a string comparison. Compare the digits
// from the right, past any dial code.
const samePhone = (a, b) => {
  const x = String(a || "").replace(/\D/g, "");
  const y = String(b || "").replace(/\D/g, "");
  return !!x && !!y && (x.endsWith(y) || y.endsWith(x));
};

// Hands back the contact details the lookup wrote, but only where they are still sitting there
// untouched. Anything the partner typed or corrected since is theirs and stays.
const releaseAutofill = () => {
  if (autofilled.email && sameEmail(form.customerEmail, autofilled.email)) form.customerEmail = "";
  if (autofilled.phone && samePhone(form.customerMobileNumber, autofilled.phone)) form.customerMobileNumber = "";
  autofilled.email = "";
  autofilled.phone = "";
};

// Picking a client out of the lookup is THF saying it already has them, so the request becomes a new
// engagement for an existing client — unless the partner already answered the question themselves
// (say, "subsidiary", which is an existing-client answer too and theirs to keep).
const pickClient = (client) => {
  if (!client) return;
  // Give back the previous pick's details first, so switching clients never leaves the last one's
  // email or phone sitting under the new one's name.
  releaseAutofill();
  linkedClient.value = client;
  clientQuery.value = client.name || "";
  form.clientName = clientQuery.value.trim();
  form.existingClientReferenceId = client.id;
  if (client.email && !form.customerEmail?.trim()) {
    form.customerEmail = client.email;
    autofilled.email = client.email;
  }
  if (client.phone && !form.customerMobileNumber?.trim()) {
    form.customerMobileNumber = client.phone;
    autofilled.phone = client.phone;
  }
  syncTypeToClient();
  clientMenu.value = false;
  activeIndex.value = -1;
};

// Drops the link without touching the name — it is now a brand-new client's name — and lets the type
// and the copied-in contact details follow it back, since neither existing-client answer can be true
// with nothing linked.
const detachClient = () => {
  linkedClient.value = null;
  form.existingClientReferenceId = null;
  releaseAutofill();
  syncTypeToClient();
};

const clearClient = () => {
  clientQuery.value = "";
  form.clientName = "";
  // Runs the empty term through the same path, so an in-flight lookup for the term just cleared is
  // cancelled and superseded rather than landing on the now-empty box.
  runLookup("");
  detachClient();
  clientFieldRef.value?.focus();
};

// ---- One name, one client ----
// THF treats a name already on file as the same client, so a typed name that matches one exactly is
// linked to it rather than filed as somebody new. Two records under one name is a real ambiguity (two
// people can share a name), so that resolves to nothing and the partner picks. The server applies the
// same rule on save, over the same search, so neither layer can catch what the other would not.
const soleExactMatch = computed(() => {
  const name = form.clientName.trim().toLowerCase();
  if (!name) return null;
  const matches = clientOptions.value.filter((c) => (c.name || "").trim().toLowerCase() === name);
  return matches.length === 1 ? matches[0] : null;
});

// Held until the partner leaves the box: linking on each keystroke would grab "Acme" while they were
// still typing "Acme Industries", then unlink on the very next letter.
const linkExactMatchIfSettled = () => {
  if (linkedClient.value || clientFocused.value || !clientSearched.value) return;
  const match = soleExactMatch.value;
  if (!match) return;
  pickClient(match);
  notify.info(`“${match.name}” is already a THF client — linked to their record.`);
};

// The one route left to a duplicate: answering "brand-new client" over a name already on file. Saving
// is blocked rather than quietly corrected — the server would link it regardless, and a partner who
// really means a different client of the same name needs to know why it will not go through.
const duplicateClientWarning = computed(() => {
  if (linkedClient.value || clientFocused.value || !soleExactMatch.value) return "";
  return `A client named “${soleExactMatch.value.name}” is already on file. Pick them from the search ` +
    "rather than filing a second record for the same client.";
});

// The partner's own answer wins over the automatic one — but "brand-new client" and a linked THF record
// cannot both be true, so choosing it drops the link.
const chooseType = (value) => {
  typeChosenByUser.value = true;
  form.type = value;
  if (value === REMS_TYPE_BRAND_NEW_CLIENT && linkedClient.value) detachClient();
};

const openMenuIfResults = () => {
  if (clientOptions.value.length || (clientSearched.value && clientQuery.value.trim().length >= MIN_LOOKUP_CHARS)) {
    clientMenu.value = true;
  }
};

const onClientFocus = () => {
  clientFocused.value = true;
  openMenuIfResults();
};

// Safe to close outright: the results suppress the blur (mousedown.prevent), so reaching here means
// focus genuinely left the field.
const onClientBlur = () => {
  clientFocused.value = false;
  clientMenu.value = false;
  linkExactMatchIfSettled();
};

const moveActive = (delta) => {
  if (!clientMenu.value) {
    openMenuIfResults();
    return;
  }
  const count = clientOptions.value.length;
  if (!count) return;
  activeIndex.value = (activeIndex.value + delta + count) % count;
};

const onClientEnter = () => {
  if (clientMenu.value && activeIndex.value >= 0) pickClient(clientOptions.value[activeIndex.value]);
};

onBeforeUnmount(() => clearTimeout(lookupTimer));

// ---- Contact + description rules ----
const contactLabels = computed(() => (linkedClient.value
  ? { email: "Client Email", phone: "Client Phone" }
  : { email: "New Client Email (if known)", phone: "New Client Phone (if known)" }));

// AC-REMS-004.7: one of the two is required. Held back until a save is attempted.
const hasContact = computed(() =>
  !!form.customerEmail?.trim() || !!form.customerMobileNumber?.trim());
const showContactError = computed(() => attempted.value && !hasContact.value);

// REMS.Description is nvarchar(500) and the server measures the markup it is sent, not the words in it.
// Counting the same thing here — and saying which — beats letting a pasted email come back as a 400.
const DESCRIPTION_MAX = 500;
const descriptionLength = computed(() => (form.description || "").length);
const descriptionTooLong = computed(() => descriptionLength.value > DESCRIPTION_MAX);
const descriptionNotice = computed(() => {
  if (descriptionTooLong.value) {
    const over = descriptionLength.value - DESCRIPTION_MAX;
    return `Message is ${over} characters too long — the limit is ${DESCRIPTION_MAX}, formatting included.`;
  }
  if (descriptionLength.value < DESCRIPTION_MAX * 0.7) return "";
  return `${descriptionLength.value} / ${DESCRIPTION_MAX} characters (formatting included)`;
});

// Save is enabled once the required intake fields are present: title, client, type, priority, and at
// least one contact channel (AC-REMS-004.7).
const canSave = computed(() =>
  !!form.title?.trim() &&
  !!form.clientName?.trim() &&
  !!form.type &&
  !!form.priority &&
  hasContact.value &&
  !descriptionTooLong.value &&
  !duplicateClientWarning.value);

// ---- Reset / prefill ----
const resetForm = () => {
  Object.assign(form, blankForm());
  mobileCountry.value = null;
  attachment.value = null;
  loadedStatus.value = null;
  attempted.value = false;
  clientQuery.value = "";
  linkedClient.value = null;
  loadedAdmin.value = null;
  typeChosenByUser.value = false;
  autofilled.email = "";
  autofilled.phone = "";
  clientOptions.value = [];
  clientLoading.value = false;
  clientMenu.value = false;
  clientSearched.value = false;
  clientFocused.value = false;
  activeIndex.value = -1;
  clearTimeout(lookupTimer);
  lookupSeq += 1;
};

const prefillFrom = (detail) => {
  form.title = detail.title || "";
  form.description = detail.description || "";
  form.type = detail.type || "";
  form.priority = detail.priority || "medium";
  form.clientName = detail.clientName || "";
  form.customerEmail = detail.customerEmail || "";
  form.customerMobileNumber = detail.customerMobileNumber || "";
  form.existingClientReferenceId = detail.existingClientReferenceId || null;
  loadedStatus.value = detail.status || null;
  loadedAdmin.value = detail.assignedAdmin || null;
  form.assignAdminUserId = detail.assignedAdmin?.id || null;
  // A saved type is a deliberate answer, whoever gave it — editing the client name must not rewrite it.
  typeChosenByUser.value = !!detail.type;
  clientQuery.value = detail.clientName || "";
  // Show the referenced client without a fresh lookup; the name is the one stored on the request.
  linkedClient.value = detail.existingClientReferenceId
    ? { id: detail.existingClientReferenceId, name: detail.clientName }
    : null;
};

watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen) return;
  resetForm();
  await Promise.all([loadOptionSets(), loadAdmins()]);
  if (props.requestId) {
    try {
      const detail = await remsApi.get(props.requestId);
      prefillFrom(detail);
    } catch (err) {
      notify.error(getApiErrorMessage(err));
    }
  }
});

// A request saved under a type the tenant has since retired would silently lose it the moment the chip
// row rendered — no chip matches, so nothing looks selected and the next save writes whatever is
// clicked. Surfacing it keeps the row honest about what the request actually holds.
const retiredType = computed(() =>
  (form.type && !typeOptions.value.some((o) => o.value === form.type) ? typeLabel(form.type) : ""));

watch(retiredType, (label) => {
  if (label) notify.warning(`This request's type (${label}) is no longer offered — choose one below.`);
});

// ---- Save ----
// An assigned request never touched the pool, so the confirmation must not claim it did.
const submittedMessage = () => {
  const admin = assignOptions.value.find((o) => o.value === form.assignAdminUserId);
  return admin ? `Request submitted and assigned to ${admin.label}.` : "Request submitted to the Admin Pool.";
};

const onPrimary = () => {
  // Create → submit to pool; edit → save changes (draft stays a draft unless "Submit" is used).
  doSave(!props.requestId);
};

const doSave = async (submit) => {
  attempted.value = true;
  if (!(await formRef.value?.validate())) return;
  if (!canSave.value) {
    if (duplicateClientWarning.value) notify.warning(duplicateClientWarning.value);
    else if (descriptionTooLong.value) notify.warning("Shorten the message to admin — it is over the 500-character limit.");
    else notify.warning("Complete the required fields: client, type, title, priority, and an email or phone number.");
    return;
  }
  saving.value = true;
  try {
    if (props.requestId) {
      const payload = {
        title: form.title,
        description: form.description || null,
        type: form.type,
        priority: form.priority,
        clientName: form.clientName,
        customerEmail: form.customerEmail || null,
        customerMobileNumber: form.customerMobileNumber || null,
        existingClientReferenceId: form.existingClientReferenceId || null,
        ...assignmentPatch(),
        submit
      };
      const detail = await remsApi.update(props.requestId, payload);
      notify.success(submit ? submittedMessage() : "Request updated.");
      emit("saved", detail);
    } else {
      let mediaId;
      if (attachment.value) {
        const media = await mediaApi.upload(attachment.value, "Attachment");
        mediaId = media?.id || undefined;
      }
      const payload = {
        existingClientReferenceId: form.existingClientReferenceId || undefined,
        clientName: form.clientName,
        type: form.type,
        priority: form.priority,
        title: form.title,
        description: form.description || undefined,
        customerEmail: form.customerEmail || undefined,
        customerMobileNumber: form.customerMobileNumber || undefined,
        assignAdminUserId: (canAssign.value && form.assignAdminUserId) || undefined,
        mediaId,
        submit
      };
      const detail = await remsApi.create(payload);
      notify.success(submit ? submittedMessage() : "Draft saved.");
      emit("saved", detail);
    }
    open.value = false;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>

<style scoped>
.rems-intake__intro {
  font-size: 13px;
  color: var(--ink-500);
}

/* Numbered step heading — the form reads as two questions, not one wall of fields. */
.rems-step {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 26px 0 16px;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--gold-600);
}
.rems-step__num {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--gold-600);
  color: var(--white);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0;
}

/* Centred caption on a rule: the hinge between "search THF" and "describe someone new". */
.rems-divider {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 22px 0 16px;
  font-size: 12px;
  color: var(--ink-500);
}
.rems-divider::before,
.rems-divider::after {
  content: "";
  flex: 1 1 0;
  height: 1px;
  background: var(--line);
}

.rems-linked {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  padding: 6px 8px 6px 12px;
  border: 1px solid var(--teal-300);
  border-radius: 8px;
  background: var(--teal-050);
  font-size: 13px;
  color: var(--teal-900);
}
.rems-linked__name {
  font-weight: 600;
}

.rems-question {
  margin: 22px 0 10px;
  font-size: 13px;
  font-weight: 600;
  color: var(--ink-900);
}

.rems-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}
.rems-chip {
  padding: 9px 16px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--white);
  color: var(--ink-700);
  font: inherit;
  font-size: 13px;
  line-height: 1.2;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s, color 0.15s;
}
.rems-chip:hover {
  border-color: var(--teal-300);
  background: var(--teal-050);
}
.rems-chip:focus-visible {
  outline: 2px solid var(--teal-500);
  outline-offset: 2px;
}
.rems-chip--on,
.rems-chip--on:hover {
  background: var(--teal-900);
  border-color: var(--teal-900);
  color: var(--white);
}

.rems-hint {
  margin-top: 6px;
  font-size: 12px;
  color: var(--ink-500);
}
.rems-hint--error {
  color: #c10015;
}
</style>
