<template>
  <div>
    <!-- Who they are and how to reach them: it is a single question, and the email is the address the
         intake form is sent to rather than an afterthought below the name.
         Four across on a desktop, two across on a tablet (the phone field splits into its own two), and
         stacked on a phone — the sm step matters because without it a 700px window drops straight from
         four columns to four full-width rows.
         Once the client has answered, the page shares its width with their submitted form, and four
         boxes across what is left of it are four cramped boxes. So in that state the name and its
         suffix keep the first line and the two ways of reaching the client take the second — see
         `compact` below. -->
    <div class="row q-col-gutter-md">
      <!-- The generational particle on the name — Jr., Sr., II, III, IV — in a box of its own, and in
           front of the search box rather than after it. Two reasons it is separate: the search matches
           THF's client records, and "John Smith Jr." finds nothing where "John Smith" finds the man; and
           a Person is filed under a given name and a family name, neither of which "Jr." is. It is
           appended to the name wherever the client is shown.
           Free text with the five as suggestions: the list is what most clients need, not all any client
           may have, and a suffix nobody thought to seed is not a reason to file somebody under the wrong
           name. Locked with the rest of the client's identity once the intake form has gone out. -->
      <app-text-field
        v-model="model.clientNameSuffix" label="Suffix" :class="suffixCols"
        placeholder="Jr." :readonly="readonly || clientLocked"
        :error="suffixTooLong" error-message="A suffix is at most 16 characters."
      >
        <template #append>
          <q-icon v-if="clientLocked" name="o_lock" size="18px" color="grey-6" />
          <q-btn
            v-else-if="!readonly" flat dense round size="sm" icon="o_arrow_drop_down" color="grey-7"
            aria-label="Suffix suggestions"
          >
            <q-menu anchor="bottom end" self="top end" auto-close>
              <q-list dense style="min-width: 150px;">
                <q-item
                  v-for="opt in SUFFIX_OPTIONS" :key="opt.value"
                  clickable :active="model.clientNameSuffix === opt.value"
                  active-class="bg-grey-2 text-primary"
                  @click="model.clientNameSuffix = opt.value"
                >
                  <q-item-section>
                    <q-item-label>{{ opt.label }}</q-item-label>
                    <q-item-label caption>{{ opt.caption }}</q-item-label>
                  </q-item-section>
                </q-item>
                <q-separator />
                <q-item clickable :disable="!model.clientNameSuffix" @click="model.clientNameSuffix = ''">
                  <q-item-section class="text-grey-7">No suffix</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </template>
      </app-text-field>

      <!-- The col cell and the field are separate elements on purpose: the results menu is `fit`ted to
           its parent, and a parent carrying the grid gutter's padding would hang the menu 16px wide of
           the box it belongs to. -->
      <div :class="nameCols">
        <!-- The search box IS the client-name field. Picking a result links this request to that THF
             record; typing a name nobody matched files it as a brand-new client under exactly what was
             typed. -->
        <div class="app-field">
          <app-field-label label="Client" required />
          <q-input
            ref="clientFieldRef"
            v-model="clientQuery"
            outlined dense hide-bottom-space
            :readonly="readonly || clientLocked"
            placeholder="Search name, email or phone…"
            autocomplete="off"
            aria-label="Client"
            :error="attempted && !model.clientName"
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
              <q-icon :name="linkedClient ? 'o_verified' : 'o_search'" :color="linkedClient ? 'positive' : 'grey-6'" />
            </template>
            <template #append>
              <q-spinner v-if="clientLoading" size="18px" color="primary" />
              <!-- The padlock takes the clear button's corner once the invite has gone: it answers the
                   question a missing ✕ would otherwise raise, the same way the email field's does. -->
              <q-icon v-else-if="clientLocked" name="o_lock" size="18px" color="grey-6" />
              <q-icon
                v-else-if="clientQuery && !readonly" name="o_close" color="grey-6" class="cursor-pointer"
                aria-label="Clear client" @click="clearClient"
              />
              <!-- Whether this name resolved to a THF record or will file a new one. In the corner of the
                   box that decides it, rather than on a divider underneath: it is a note about this
                   field, and as a full-width rule it read like a section heading between the client and
                   the way to reach them. -->
              <q-icon name="o_info" size="18px" :color="linkedClient ? 'positive' : 'grey-6'" class="rf-note">
                <q-tooltip anchor="top right" self="bottom right" max-width="300px" :delay="200">
                  {{ clientLinkNote }}
                </q-tooltip>
              </q-icon>
            </template>
          </q-input>

          <q-menu
            v-model="clientMenu" fit no-focus no-refocus no-parent-event
            anchor="bottom start" self="top start" :offset="[0, 6]"
          >
            <q-list separator>
              <!-- mousedown.prevent keeps focus in the search box, so choosing a result never fires the
                   blur that closes this menu out from under the click. -->
              <q-item
                v-for="(client, i) in clientOptions" :key="client.id"
                clickable :active="i === activeIndex" active-class="bg-grey-2 text-primary"
                @mousedown.prevent @click="pickClient(client)"
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
      </div>

      <!-- Required, not "one of email or mobile": the intake form is emailed, so a request without an
           address has nowhere to send the thing the whole request exists to collect. -->
      <app-text-field
        v-model="model.customerEmail" label="Client Email Address" type="email" required
        placeholder="jane@company.com" :class="contactCols"
        :readonly="readonly || clientLocked"
        :error="attempted && !hasEmail"
        error-message="The intake form is emailed to the client — an address is required."
      >
        <!-- The padlock IS the hint now: why the field will not take a keystroke, on the thing that is
             refusing them, instead of a caption line under a row of four fields. -->
        <template v-if="clientLocked" #append>
          <q-icon name="o_lock" size="18px" color="grey-6" class="rf-note">
            <q-tooltip anchor="top right" self="bottom right" max-width="300px" :delay="200">
              Locked — the intake form was sent to this address.
            </q-tooltip>
          </q-icon>
        </template>
      </app-text-field>

      <!-- Country + number: one component, one cell of the row it is given. -->
      <app-phone-input
        v-model="model.customerMobileNumber" v-model:country="mobileCountry"
        label="Client Phone Number" :class="contactCols" :readonly="readonly || clientLocked"
      />
    </div>

    <div id="rf-type-question" class="rf-question">How does this referral relate to THF's records?</div>

    <div class="rf-chips" role="radiogroup" aria-labelledby="rf-type-question">
      <button
        v-for="opt in typeOptions" :key="opt.value"
        type="button" role="radio" :aria-checked="model.type === opt.value" :disabled="readonly"
        class="rf-chip" :class="{ 'rf-chip--on': model.type === opt.value }"
        @click="chooseType(opt.value)"
      >
        {{ opt.label }}
        <template v-if="typeHint(opt.value)">
          <q-icon name="o_info" size="15px" class="rf-chip__info" />
          <q-tooltip anchor="top middle" self="bottom middle" max-width="320px" :delay="300">
            {{ typeHint(opt.value) }}
          </q-tooltip>
        </template>
      </button>
    </div>
    <div v-if="attempted && !model.type" class="rf-hint rf-hint--error">
      Choose how this referral relates to THF's records.
    </div>

    <!-- What kind of entity the client is, the trade they are in, and who at THF owns the relationship.
         None of the three is a contact detail, but all three are answers about THIS CLIENT, and two of
         them are what the client's own intake form is minted from — the entity type decides what they are
         asked, and the CSE is filed with it on the same record and by the same write. Asking them over on
         the setup tab described one client in two places, and made an initiator open a second tab before
         the form could be sent at all.
         They obey the SETUP's edit right rather than this tab's: in the two rework states the setup is
         back with the initiator while the client's details above are not theirs to change. -->
    <div class="row q-col-gutter-md q-mt-md">
      <app-select
        :model-value="industryGroup" :options="industryGroupOptions" label="Entity Type" required
        class="col-12 col-sm-6 col-md-4" :readonly="setupReadonly || industryLocked" :clearable="false"
        :hint="industryLocked ? 'Locked — the intake form has been sent.' : ''"
        info="What kind of entity the client is. Decides which questions the client's intake form asks and which trades the Industry list beside it offers, so it is fixed once the form goes out — and an Audit for a Government entity is a Government Audit, which asks for a contract number."
        @update:model-value="onEntityTypeChosen"
      />
      <!-- Optional, and narrowed by the entity type beside it — a Government entity is offered the three
           kinds of government and nothing else. The two do NOT partition cleanly, which is why the map
           behind this (REMS_INDUSTRY_BY_ENTITY_TYPE) is overlapping sets rather than a tree: Health Care
           and Educational Institutions each appear under two entity types. Still clearable — a client
           whose trade is not on the list is better left blank than filed under a wrong one. -->
      <app-select
        :model-value="subIndustry" :options="industryOptions" label="Industry"
        class="col-12 col-sm-6 col-md-4" :readonly="setupReadonly"
        :hint="industryHint" :info="industryInfo"
        @update:model-value="onIndustryPicked"
      />
      <!-- The Client Service Executive. Scoped to the "CSE" role, so an empty picker means nobody holds
           it and the hint names what to assign. It sits beside the entity type because the two are
           written together — the endpoint behind the client's intake form requires the pair. -->
      <app-select
        :model-value="cseUserId" :options="cseOptions" label="CSE" required
        class="col-12 col-sm-6 col-md-4" :readonly="setupReadonly" :clearable="false" :hint="cseHint"
        info="Users holding the &quot;CSE&quot; role, assigned on a user's page in Administration → Users. The CSE owns the client relationship and becomes an approver on this request's engagement."
        @update:model-value="$emit('update:cseUserId', $event)"
      />
    </div>

    <!-- Business documents, not "any file at all": this box took an executable as happily as a letter,
         because it named no accepted types at all. It named no per-file size either, so a 40 MB scan was
         accepted here and refused by the server after it had been uploaded. -->
    <app-multi-file-upload
      v-if="!readonly"
      v-model="attachments" :label="files.length ? 'Add attachments' : 'Attachments'" class="q-mt-md"
      :max-files="MAX_ATTACHMENT_FILES" :accept="ATTACHMENT_ACCEPT" :max-size-mb="MAX_UPLOAD_MB"
      :hint="attachmentHint"
      :error-message="attachmentError"
    />

    <!-- Already-attached files, so a request being edited says what it is already carrying rather than
         showing an empty picker over documents nobody can see from here. Same row the picker above puts
         under a freshly chosen file — the icon for its type, its size, and a click that opens it — so a
         document looks the same before and after it is saved. The ✕ is offered only while the form is
         editable: the wrong document attached to a request is one every approver then reads.

         BELOW the picker, not above it, and that is the whole point: a file waiting to be saved is
         previewed by the picker, which renders its rows underneath the dropzone. With this list on top,
         the same document sat under the dropzone while it was staged and jumped over it the instant the
         auto-save landed — one file appearing to move on its own, for no reason the reader can see. Every
         upload on the platform puts what it is holding underneath itself, staged or saved. -->
    <div v-if="files.length" class="rf-files q-mt-md">
      <app-field-label label="Attached" />
      <div class="column q-gutter-xs">
        <app-stored-file-item
          v-for="file in files" :key="file.id"
          :file="file" :removable="!readonly" :disable="removingId === file.id"
          @remove="removeFile(file)"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
// Section 1 of the REMS form: who the engagement is for, how to reach them, what kind of entity they are,
// what trade they are in, and which CSE owns them.
//
// Client identification is unchanged from the old intake drawer — search and pick, or type a name nobody
// matched and file them as new. No field is treated as a unique key: the same client comes back for a
// second engagement, a third, an audit next year, so nothing here constrains a client to one request.
import { ref, reactive, computed, watch, nextTick, onBeforeUnmount } from "vue";
import { remsApi, mediaApi } from "services/api";
import { useNotify } from "composables/useNotify";
import {
  useRemsMeta, remsIndustryOptions, remsIndustryFitsEntityType,
  REMS_EXISTING_CLIENT_TYPES, REMS_TYPE_BRAND_NEW_CLIENT, REMS_TYPE_EXISTING_CLIENT
} from "modules/rems/useRemsMeta";
import { CLIENT_NAME_SUFFIXES } from "modules/rems/remsContactRoles";
import { dialFromIso, DEFAULT_COUNTRY_ISO } from "composables/useCountries";
import {
  ATTACHMENT_ACCEPT, ATTACHMENT_HINT, MAX_ATTACHMENT_FILES, MAX_ATTACHMENT_TOTAL_MB, MAX_UPLOAD_MB
} from "composables/useFileDrop";

import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppMultiFileUpload from "components/common/AppMultiFileUpload.vue";
import AppStoredFileItem from "components/common/AppStoredFileItem.vue";

const props = defineProps({
  modelValue: { type: Object, required: true },
  readonly: { type: Boolean, default: false },
  // The client's identity is fixed once the intake form has gone out — who they are, the address it was
  // emailed to, and the number on file for them. One flag for the three because they lock together: the
  // invite was issued naming this client and sent to these details.
  clientLocked: { type: Boolean, default: false },
  typeOptions: { type: Array, default: () => [] },
  // What the request already carries — [{ id, fileName, url }] off the request detail.
  files: { type: Array, default: () => [] },
  attempted: { type: Boolean, default: false },
  // Set while the client's submitted form is open beside this one, which leaves the tab a fraction of
  // the page rather than the whole of it. It is a width the layout cannot read for itself: the columns
  // below are chosen off the VIEWPORT, and the viewport has not changed — only the share of it this
  // form was given.
  compact: { type: Boolean, default: false },

  // ---- The two classifications, which are NOT part of `model` ----
  // Entity Type belongs to the request's EMS form record and Industry to its engagement, so both are
  // written by endpoints of their own; the page owns the values and v-models them through here. Rendered
  // on this tab because they describe the client (see the template), not because the client record holds
  // them.
  //
  // Their edit right is the SETUP's, not this tab's — hence a second readonly flag rather than reusing
  // `readonly` above.
  setupReadonly: { type: Boolean, default: false },
  industryGroup: { type: String, default: null },
  industryGroupOptions: { type: Array, default: () => [] },
  // The invite has gone out under this entity type; changing it would ask the client different questions
  // from the ones they were sent.
  industryLocked: { type: Boolean, default: false },
  // Labelled "Industry"; still named for the data behind it. See the note at the top of useRemsMeta.
  subIndustry: { type: String, default: null },
  subIndustryOptions: { type: Array, default: () => [] },

  // ---- The CSE, which is not part of `model` either ----
  // It belongs to the request's EMS form record — what the client's invite is minted from — and is saved
  // by the same endpoint as the entity type above, which requires the pair. Asked here rather than on the
  // setup tab because it is the answer to "whose client is this?", and because the intake form cannot be
  // sent without it: an initiator should not have to open a second tab to send the first one's form.
  cseUserId: { type: String, default: null },
  cseOptions: { type: Array, default: () => [] },
  cseHint: { type: String, default: "" }
});
// `change` covers the ATTACHMENT picker only. Every other field on this section writes straight through
// to the object the page owns (see `model` below), so the page watches that and sees those itself; files
// wait here until save time and are invisible to it until then. The two classification updates are
// ordinary v-models: the page owns those values and writes them with endpoints of their own.
// `remove-file` is handed up rather than called here: detaching writes to the server against the request
// id, and it is the page that knows the request exists and owns the `files` list this reads.
const emit = defineEmits([
  "update:modelValue", "change", "update:industryGroup", "update:subIndustry", "update:cseUserId",
  "remove-file"
]);

const notify = useNotify();
const { typeHint, industryGroupLabel, subIndustryLabel } = useRemsMeta();

// The parent owns the object; this component writes through it. Simpler than a full v-model round-trip
// for a form this size, and it keeps the parent's save path reading one object.
const model = reactive(props.modelValue);

// The top row's columns. Four across the full page; the suffix and the name, then the email and the
// phone, once the page is shared with the client's answers. The suffix leads and is narrow — it is a
// particle in front of a name, so it gets a particle's width and never a half-row of its own — and the
// two share a line even on a phone, which is how the pair reads as one answer.
const nameCols = computed(() =>
  props.compact ? "col-8 col-sm-9" : "col-8 col-sm-10 col-md-4");
const suffixCols = computed(() =>
  props.compact ? "col-4 col-sm-3" : "col-4 col-sm-2 col-md-2");
const contactCols = computed(() =>
  props.compact ? "col-12 col-sm-6" : "col-12 col-sm-6 col-md-3");

const DEFAULT_DIAL_CODE = dialFromIso(DEFAULT_COUNTRY_ISO);
const mobileCountry = ref(DEFAULT_DIAL_CODE);
const attachments = ref([]);

// The cap on the SET, not on each file — the picker enforces MAX_UPLOAD_MB per file. Checked here because
// AppMultiFileUpload only knows about one file at a time. The numbers are the shared ones so that this
// field and the Universal Features panel cannot promise different things (see useFileDrop).
const MAX_TOTAL_BYTES = MAX_ATTACHMENT_TOTAL_MB * 1024 * 1024;

// The shared sentence plus the one thing that is true here and nowhere else.
const attachmentHint = `${ATTACHMENT_HINT} These stay internal to the request — the client never sees them.`;
const attachmentError = computed(() => {
  const total = attachments.value.reduce((sum, f) => sum + (f?.size || 0), 0);
  if (total <= MAX_TOTAL_BYTES) return "";
  return `These files total ${(total / 1024 / 1024).toFixed(1)} MB — the limit is ${MAX_ATTACHMENT_TOTAL_MB} MB across all of them.`;
});

// The file currently being detached, so its row greys out rather than the whole list doing so.
const removingId = ref(null);
const removeFile = (file) => {
  removingId.value = file.id;
  // The page answers by re-seeding `files`; either way the row stops being busy once the list changes.
  emit("remove-file", file, () => { removingId.value = null; });
};

// Uploading happens at the page's save, not as a side effect of picking a file: on a brand-new request
// there is nothing to attach media TO until the request has been created. So the files wait here and the
// parent asks for their media ids when it writes.
// Set while the picker is being emptied by a completed upload — the one change to it that is not the
// user choosing a file, and announcing it would ask the page to save the files it has just saved.
let clearingAttachments = false;
watch(attachments, () => { if (!clearingAttachments) emit("change"); }, { deep: true });

// `remsId` is the request the files are filed under on the server — the page passes it because it is
// the page, not this component, that knows whether the request has been created yet.
const uploadAttachments = async (remsId = null) => {
  if (attachmentError.value) throw new Error(attachmentError.value);
  const pending = [...attachments.value];
  if (!pending.length) return [];
  const entity = remsId ? { type: "Rems", id: remsId } : null;
  const media = await Promise.all(pending.map((file) => mediaApi.upload(file, "Attachment", entity)));
  // Cleared only once every upload has landed — a failure leaves the picker as the user left it, so a
  // retried save re-sends the same files rather than silently dropping them.
  clearingAttachments = true;
  attachments.value = [];
  await nextTick();
  clearingAttachments = false;
  return media.map((m) => m?.id).filter(Boolean);
};

defineExpose({ uploadAttachments });

const hasEmail = computed(() => !!model.customerEmail?.trim());

// ---- Industry, narrowed by the entity type ----
// The trades this kind of entity is in, out of the tenant's own list. Whatever is already stored stays
// offered whatever the map says, so opening an older engagement never drops the industry recorded on it.
const industryOptions = computed(() =>
  remsIndustryOptions(props.subIndustryOptions, props.industryGroup, props.subIndustry));

// Said on the field, because a list that has just gone from twenty-nine values to three looks broken
// unless something says why.
const industryInfo = computed(() => (props.industryGroup
  ? "From the REMS Industry option list (Administration → Option Sets), narrowed to the trades a " +
    `${industryGroupLabel(props.industryGroup)} entity is in. Some trades belong to more than one ` +
    "entity type and appear under each."
  : "From the REMS Industry option list (Administration → Option Sets). Which trades are offered depends " +
    "on the Entity Type, so the list is empty until that is chosen."));

// What just happened to a stored industry the new entity type does not offer. A field that empties itself
// with no explanation reads as data lost rather than as an answer that stopped applying.
const industryCleared = ref("");
// The cleared-industry note when there is one, otherwise the reason an empty picker is empty — a dropdown
// that opens on nothing reads as broken unless something says why.
const industryHint = computed(() =>
  industryCleared.value ||
  (props.industryGroup ? "" : "Choose an Entity Type first — it decides which trades are offered."));

// Changing the entity type can strand the industry: "Retail" is not a trade a Government entity is in,
// and leaving it would store a pair the picker cannot even show. So it is cleared — and said out loud.
//
// Done in the PICKER'S handler rather than in a watcher on the prop, deliberately. A watcher cannot tell
// the page seeding this form from the server apart from somebody choosing a different entity type, so on
// any engagement whose stored industry predates this pairing it would clear a saved answer on load — and
// the auto-save would then make that permanent. This only ever runs when a human opens the dropdown.
const onEntityTypeChosen = (value) => {
  emit("update:industryGroup", value);
  industryCleared.value = "";
  if (remsIndustryFitsEntityType(value, props.subIndustry)) return;
  const stranded = subIndustryLabel(props.subIndustry);
  emit("update:subIndustry", null);
  industryCleared.value =
    `Industry cleared — ${stranded} is not a trade a ${industryGroupLabel(value)} entity is in.`;
};

// The note has done its job the moment a trade is chosen.
const onIndustryPicked = (value) => {
  industryCleared.value = "";
  emit("update:subIndustry", value);
};

// The suffix suggestions, and the one thing that can be wrong with a free-text suffix. Checked here so
// the field says so while it is being typed rather than the save coming back with a 400 on a name
// particle.
const SUFFIX_OPTIONS = CLIENT_NAME_SUFFIXES;
const suffixTooLong = computed(() => (model.clientNameSuffix?.trim().length || 0) > 16);

// ---- Client lookup ----
const clientQuery = ref(model.clientName || "");
const linkedClient = ref(model.existingClientReferenceId
  ? { id: model.existingClientReferenceId, name: model.clientName }
  : null);
const clientOptions = ref([]);
const clientLoading = ref(false);
const clientMenu = ref(false);
const clientSearched = ref(false);
const clientFocused = ref(false);
const activeIndex = ref(-1);
const clientFieldRef = ref(null);
// A saved type is a deliberate answer, whoever gave it — editing the client name must not rewrite it.
const typeChosenByUser = ref(!!model.type);

// Which of the two things typing a name here did — matched a record, or named somebody new. Worth saying
// because the answer decides what gets filed, and nothing else on screen shows it. An empty box has done
// neither yet, so it explains the field instead of reporting on a name nobody has typed.
const clientLinkNote = computed(() => {
  if (props.clientLocked) {
    return "Locked — the intake form has gone out for this client. Changing who they are, or the details " +
      "it was sent to, would leave the request naming somebody nobody wrote to.";
  }
  if (linkedClient.value) {
    return "Linked to a THF client record — this request hangs off the client THF already has on file.";
  }
  if (!model.clientName?.trim()) {
    return "Search by name, email or phone. A name nothing matches is filed as a brand-new client.";
  }
  return "No match / New to THF — this name will be filed as a brand-new client.";
});

// Keep the box in step when the parent re-seeds after a save or reload.
watch(() => props.modelValue.clientName, (name) => {
  if ((name || "") !== clientQuery.value) clientQuery.value = name || "";
});

// Every term searches, however short: a minimum length would leave a client actually NAMED in two or
// three characters unfindable by typing their name. The debounce keeps the traffic down instead, and the
// server caps the result set at 20 whatever the term. Only an empty box searches for nothing.
const LOOKUP_DEBOUNCE_MS = 500;
let lookupTimer = null;
// Bumped on every query so a slow response for an abandoned term cannot land on top of a newer one.
let lookupSeq = 0;

const runLookup = (term) => {
  clearTimeout(lookupTimer);
  lookupSeq += 1;
  const seq = lookupSeq;
  clientSearched.value = false;
  if (!term) {
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
      // A failed lookup reads as "no match": filing the client as new is the only thing an empty result
      // would have allowed anyway.
      items = [];
    }
    if (seq !== lookupSeq) return;
    clientOptions.value = items;
    activeIndex.value = items.length ? 0 : -1;
    clientLoading.value = false;
    clientSearched.value = true;
    clientMenu.value = clientFocused.value;
    linkExactMatchIfSettled();
  }, LOOKUP_DEBOUNCE_MS);
};

const autoType = (code) => (props.typeOptions.some((o) => o.value === code) ? code : "");

const syncTypeToClient = () => {
  if (linkedClient.value) {
    if (!REMS_EXISTING_CLIENT_TYPES.includes(model.type)) model.type = autoType(REMS_TYPE_EXISTING_CLIENT);
    return;
  }
  if (typeChosenByUser.value) return;
  model.type = model.clientName ? autoType(REMS_TYPE_BRAND_NEW_CLIENT) : "";
};

const onClientTyped = (val) => {
  const term = (val || "").trim();
  model.clientName = term;
  if (linkedClient.value && term !== (linkedClient.value.name || "").trim()) detachClient();
  syncTypeToClient();
  runLookup(term);
};

const autofilled = reactive({ email: "", phone: "" });

const sameEmail = (a, b) =>
  String(a || "").trim().toLowerCase() === String(b || "").trim().toLowerCase();

// AppPhoneInput normalises whatever it is handed, so recognising its own autofill cannot be a string
// comparison. Compare the digits from the right, past any dial code.
const samePhone = (a, b) => {
  const x = String(a || "").replace(/\D/g, "");
  const y = String(b || "").replace(/\D/g, "");
  return !!x && !!y && (x.endsWith(y) || y.endsWith(x));
};

const releaseAutofill = () => {
  if (autofilled.email && sameEmail(model.customerEmail, autofilled.email)) model.customerEmail = "";
  if (autofilled.phone && samePhone(model.customerMobileNumber, autofilled.phone)) {
    model.customerMobileNumber = "";
    mobileCountry.value = DEFAULT_DIAL_CODE;
  }
  autofilled.email = "";
  autofilled.phone = "";
};

const pickClient = (client) => {
  if (!client || props.readonly) return;
  releaseAutofill();
  linkedClient.value = client;
  clientQuery.value = client.name || "";
  model.clientName = clientQuery.value.trim();
  model.existingClientReferenceId = client.id;
  if (client.email && !model.customerEmail?.trim() && !props.clientLocked) {
    model.customerEmail = client.email;
    autofilled.email = client.email;
  }
  if (client.phone && !model.customerMobileNumber?.trim()) {
    // Blanked first so THIS client's number decides the country, not the last one's.
    mobileCountry.value = null;
    model.customerMobileNumber = client.phone;
    autofilled.phone = client.phone;
  }
  syncTypeToClient();
  clientMenu.value = false;
  activeIndex.value = -1;
};

const detachClient = () => {
  linkedClient.value = null;
  model.existingClientReferenceId = null;
  releaseAutofill();
  syncTypeToClient();
};

// Taking the client out takes their contact details with them: the address and number on screen are the
// ones that client is reached on, so leaving them standing would send the next client's intake form to
// the last one's inbox. Unconditional, unlike the autofill release above — details typed by hand or
// loaded with a saved request belong to the client that was just removed just as much as autofilled ones
// do. A locked email is the exception: the form has already gone to that address, so it is not ours to
// clear (the field is read-only for the same reason).
const clearClient = () => {
  clientQuery.value = "";
  model.clientName = "";
  // The suffix belongs to the name it was typed beside, so it goes with it. Left standing, the next
  // client typed into this box would inherit the last one's "Jr.".
  model.clientNameSuffix = "";
  runLookup("");
  detachClient();
  if (!props.clientLocked) model.customerEmail = "";
  model.customerMobileNumber = "";
  mobileCountry.value = DEFAULT_DIAL_CODE;
  clientFieldRef.value?.focus();
};

// THF treats a name already on file as the same client, so a typed name matching one exactly is linked to
// it rather than filed as somebody new. Two records under one name is a real ambiguity, so that resolves
// to nothing and the partner picks. The server applies the same rule on save, over the same search.
const soleExactMatch = computed(() => {
  const name = (model.clientName || "").trim().toLowerCase();
  if (!name) return null;
  const matches = clientOptions.value.filter((c) => (c.name || "").trim().toLowerCase() === name);
  return matches.length === 1 ? matches[0] : null;
});

// Held until they leave the box: linking on each keystroke would grab "Acme" while they were still typing
// "Acme Industries", then unlink on the very next letter.
const linkExactMatchIfSettled = () => {
  if (linkedClient.value || clientFocused.value || !clientSearched.value) return;
  const match = soleExactMatch.value;
  if (!match) return;
  pickClient(match);
  notify.info(`“${match.name}” is already a THF client — linked to their record.`);
};

const chooseType = (value) => {
  if (props.readonly) return;
  typeChosenByUser.value = true;
  model.type = value;
  if (value === REMS_TYPE_BRAND_NEW_CLIENT && linkedClient.value) detachClient();
};

const openMenuIfResults = () => {
  if (clientOptions.value.length || (clientSearched.value && !!clientQuery.value.trim())) {
    clientMenu.value = true;
  }
};

const onClientFocus = () => {
  if (props.readonly) return;
  clientFocused.value = true;
  openMenuIfResults();
};

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

onBeforeUnmount(() => {
  clearTimeout(lookupTimer);
});
</script>

<style scoped>
/* Field hints: on the field they are about, at the end of it, and only reading themselves out when
   someone asks. */
.rf-note { cursor: help; }

.rf-hint {
  margin-top: 6px;
  font-size: 12px;
  color: var(--ink-500);
}
.rf-hint--error { color: #c10015; }

/* The attached files stack as preview rows (AppStoredFileItem) rather than wrapping as a line of links,
   so each one carries its type icon, its size and its own ✕. */
.rf-files { display: block; }

.rf-question {
  margin: 10px 0 10px;
  font-size: 13px;
  font-weight: 600;
  color: var(--ink-900);
}
/* .rf-typerow wrapped these chips alongside the Parent Client box and kept the two top-aligned so the
   chips did not shift as the box appeared. With the box gone the chips are the whole row and lay
   themselves out. */
.rf-chips { display: flex; flex-wrap: wrap; gap: 10px; }
.rf-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
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
.rf-chip:disabled { cursor: default; opacity: 0.7; }
.rf-chip__info { opacity: 0.5; transition: opacity 0.15s; }
.rf-chip:hover .rf-chip__info,
.rf-chip--on .rf-chip__info { opacity: 0.9; }
.rf-chip:not(:disabled):hover { border-color: var(--teal-300); background: var(--teal-050); }
.rf-chip:focus-visible { outline: 2px solid var(--teal-500); outline-offset: 2px; }
.rf-chip--on,
.rf-chip--on:hover {
  background: var(--teal-900);
  border-color: var(--teal-900);
  color: var(--white);
}
</style>
