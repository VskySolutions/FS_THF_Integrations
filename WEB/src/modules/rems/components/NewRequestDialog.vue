<template>
  <app-form-drawer
    v-model="open"
    :title="requestId ? 'Edit REMS Request' : 'New REMS Request'"
    :save-label="primaryLabel"
    :saving="saving"
    @submit="onPrimary"
    @cancel="resetForm"
  >
    <q-form ref="formRef" greedy>
      <app-text-field
        v-model="form.title" label="Title" required class="q-mb-md"
        :rules="[(v) => !!(v && v.trim()) || 'Title is required']"
      />

      <app-text-field
        v-model="form.description" label="Description" type="textarea" autogrow class="q-mb-md"
      />

      <div class="row q-col-gutter-md q-mb-md">
        <app-select
          v-model="form.type" :options="typeOptions" label="Type" required class="col-12 col-sm-6"
          :clearable="false"
        />
        <app-select
          v-model="form.priority" :options="priorityOptions" label="Priority" required class="col-12 col-sm-6"
          :clearable="false"
        />
      </div>

      <!-- Client: search an existing client (2+ chars) or toggle to enter a brand-new client name. -->
      <div class="q-mb-md">
        <div v-if="!isNewClient" class="app-field">
          <app-field-label label="Client" required />
          <q-select
            v-model="selectedClient"
            :options="clientOptions"
            outlined dense hide-bottom-space
            use-input input-debounce="300"
            :loading="clientLoading"
            clearable
            option-label="name"
            option-value="id"
            map-options
            hide-dropdown-icon
            placeholder="Search existing client by name, email or phone"
            @filter="onClientFilter"
            @update:model-value="onClientSelected"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section>
                  <q-item-label>{{ scope.opt.name }}</q-item-label>
                  <q-item-label caption>
                    {{ scope.opt.email || "no email" }} · {{ scope.opt.phone || "no phone" }}
                    <template v-if="scope.opt.parentCompany"> · {{ scope.opt.parentCompany }}</template>
                    <template v-if="scope.opt.pastWork"> · {{ scope.opt.pastWork }}</template>
                  </q-item-label>
                </q-item-section>
              </q-item>
            </template>
            <template #no-option>
              <q-item>
                <q-item-section class="text-grey-6">
                  {{ clientSearch.length < 2
                    ? "Type at least two characters to search"
                    : "No matching client — toggle “new client” to enter a new name" }}
                </q-item-section>
              </q-item>
            </template>
          </q-select>
        </div>
        <app-text-field
          v-else v-model="form.clientName" label="Client" required placeholder="New client name"
          :rules="[(v) => !!(v && v.trim()) || 'Client name is required']"
        />
        <q-toggle
          v-model="isNewClient" dense size="sm" color="primary"
          label="This is a new client (not in the list)" class="q-mt-xs"
          @update:model-value="onClientMode"
        />
      </div>

      <!-- At least one of email / mobile is required (AC-REMS-004.7). -->
      <div class="text-caption text-grey-7 q-mb-xs">Provide a customer email and/or mobile number (at least one).</div>
      <app-text-field
        v-model="form.customerEmail" label="Customer Email" type="email" class="q-mb-md"
        :error="!!contactError" :error-message="contactError"
      />
      <app-phone-input
        v-model="form.customerMobileNumber" v-model:country="mobileCountry" label="Customer Mobile" class="q-mb-md"
      />

      <app-single-file-upload
        v-if="!requestId"
        v-model="attachment" label="Attachment" hint="A single supporting file (optional)."
        class="q-mb-md"
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
        outline no-caps color="primary" label="Submit to Admin Pool"
        :disable="saving || !canSave" @click="doSave(true)"
      />
    </template>
  </app-form-drawer>
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { remsApi, mediaApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useRemsOptionSets, REMS_EXISTING_CLIENT_TYPES } from "modules/rems/useRemsMeta";

import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppSingleFileUpload from "components/common/AppSingleFileUpload.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // When set the drawer edits the existing request; otherwise it creates a new one.
  requestId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const { typeOptions, priorityOptions, load: loadOptionSets } = useRemsOptionSets();

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
  existingClientReferenceId: null
});

const form = reactive(blankForm());
const mobileCountry = ref(null);
const attachment = ref(null);
const isNewClient = ref(false);
const selectedClient = ref(null);
const loadedStatus = ref(null);
const formRef = ref(null);
const saving = ref(false);

const isDraft = computed(() => loadedStatus.value === "draft");
const primaryLabel = computed(() => {
  if (props.requestId) return "Save Changes";
  return "Submit to Admin Pool";
});

// Save is enabled once the required intake fields are present: title, client, type, priority, and
// at least one contact channel (email or mobile) — AC-REMS-004.7.
const canSave = computed(() =>
  !!form.title?.trim() &&
  !!form.clientName?.trim() &&
  !!form.type &&
  !!form.priority &&
  (!!form.customerEmail?.trim() || !!form.customerMobileNumber?.trim()));

const contactError = computed(() =>
  (!form.customerEmail?.trim() && !form.customerMobileNumber?.trim())
    ? "Enter an email or a mobile number."
    : "");

// ---- Client lookup (existing-client search) ----
const clientOptions = ref([]);
const clientLoading = ref(false);
const clientSearch = ref("");

const onClientFilter = (val, update) => {
  const term = (val || "").trim();
  clientSearch.value = term;
  if (term.length < 2) {
    update(() => { clientOptions.value = []; });
    return;
  }
  clientLoading.value = true;
  remsApi.clientLookup(term)
    .then((items) => update(() => { clientOptions.value = items || []; }))
    .catch(() => update(() => { clientOptions.value = []; }))
    .finally(() => { clientLoading.value = false; });
};

// Selecting a client references it and marks the request as an existing-client type (AC-REMS-004).
const onClientSelected = (client) => {
  if (!client) {
    form.existingClientReferenceId = null;
    form.clientName = "";
    return;
  }
  form.existingClientReferenceId = client.id;
  form.clientName = client.name || "";
  if (client.email && !form.customerEmail) form.customerEmail = client.email;
  if (client.phone && !form.customerMobileNumber) form.customerMobileNumber = client.phone;
  if (!REMS_EXISTING_CLIENT_TYPES.includes(form.type)) form.type = "existing_client";
};

// Toggling "new client" (USER action only — bound via @update:model-value so it never fires during
// programmatic prefill) clears the reference and marks a brand-new type (AC-REMS-004).
const onClientMode = (v) => {
  selectedClient.value = null;
  form.existingClientReferenceId = null;
  form.clientName = "";
  if (v && REMS_EXISTING_CLIENT_TYPES.includes(form.type)) form.type = "brand_new_client";
};

// ---- Reset / prefill ----
const resetForm = () => {
  Object.assign(form, blankForm());
  mobileCountry.value = null;
  attachment.value = null;
  isNewClient.value = false;
  selectedClient.value = null;
  clientOptions.value = [];
  clientSearch.value = "";
  loadedStatus.value = null;
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
  if (detail.existingClientReferenceId) {
    isNewClient.value = false;
    // Show the referenced client's name without a fresh lookup.
    selectedClient.value = { id: detail.existingClientReferenceId, name: detail.clientName };
  } else {
    isNewClient.value = true;
  }
};

watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen) return;
  resetForm();
  await loadOptionSets();
  if (props.requestId) {
    try {
      const detail = await remsApi.get(props.requestId);
      prefillFrom(detail);
    } catch (err) {
      notify.error(getApiErrorMessage(err));
    }
  }
});

// ---- Save ----
const onPrimary = () => {
  // Create → submit to pool; edit → save changes (draft stays a draft unless "Submit" is used).
  doSave(!props.requestId);
};

const doSave = async (submit) => {
  if (!(await formRef.value?.validate())) return;
  if (!canSave.value) {
    notify.warning("Complete the required fields: title, client, type, priority, and an email or mobile number.");
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
        submit
      };
      const detail = await remsApi.update(props.requestId, payload);
      notify.success(submit ? "Request submitted to the Admin Pool." : "Request updated.");
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
        mediaId,
        submit
      };
      const detail = await remsApi.create(payload);
      notify.success(submit ? "Request submitted to the Admin Pool." : "Draft saved.");
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
