<template>
  <app-form-drawer
    ref="drawerRef"
    v-model="open"
    title="Add Customer"
    save-label="Submit for Approval"
    :saving="saving"
    draft-key="customer-create"
    :draft="form"
    @submit="onSubmitForApproval"
    @cancel="resetForm"
    @restore-draft="restoreDraft"
  >
    <!-- All actions on a single footer row: Save as Draft beside Submit for Approval. -->
    <template #footer-actions>
      <q-btn outline no-caps color="primary" label="Save as Draft" :loading="savingDraft" :disable="saving" @click="onSaveDraft" />
    </template>

    <q-form ref="formRef" greedy>
      <!-- Basic Information -->
      <q-card flat bordered class="q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Basic Information</q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <app-select
            v-if="canChooseTenant" v-model="form.tenantId" :options="tenantOptions" label="Tenant *"
            :loading="loadingTenants" :clearable="false" class="col-12"
            :rules="[(v) => !!v || 'Tenant is required']"
          />
          <app-text-field v-model="form.legalName" label="Legal Name *" class="col-12 col-sm-6" :rules="[(v) => !!v || 'Legal name is required']" />
          <app-text-field v-model="form.companyName" label="Company Name *" class="col-12 col-sm-6" :rules="[(v) => !!v || 'Company name is required']" />
          <app-text-field v-model="form.contactPerson" label="Contact Person" class="col-12 col-sm-6" />
          <app-text-field
            v-model="form.emailAddress" type="email" label="Email Address *" class="col-12 col-sm-6"
            :rules="[(v) => !!v || 'Email is required', (v) => /.+@.+\..+/.test(v) || 'Enter a valid email']"
          />
          <app-phone-input
            v-model="form.phoneNumber" v-model:country="form.phoneCountryCode"
            label="Phone Number" class="col-12 col-sm-6"
          />
          <app-text-field v-model="form.website" label="Website" class="col-12 col-sm-6" />
        </q-card-section>
      </q-card>

      <!-- Address (shared field-set: Location + Street address, with the country/state/city cascade) -->
      <q-card flat bordered class="q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Address</q-card-section>
        <q-separator />
        <q-card-section>
          <app-address-fields ref="addressRef" v-model="form.address" required />
        </q-card-section>
      </q-card>
    </q-form>

    <!-- Duplicate-warning dialog: shown when the API flags possible duplicates on submit. -->
    <q-dialog v-model="dupOpen" persistent>
      <q-card style="min-width: 380px; max-width: 90vw;">
        <q-card-section class="row items-center">
          <q-icon name="o_warning" color="warning" size="28px" class="q-mr-sm" />
          <div class="text-h6">Possible duplicate{{ duplicates.length === 1 ? "" : "s" }}</div>
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div class="q-mb-sm text-grey-8">
            We found existing customer(s) that look similar. Submit anyway, or cancel to review.
          </div>
          <q-list bordered separator>
            <q-item v-for="d in duplicates" :key="d.id">
              <q-item-section>
                <q-item-label>{{ d.companyName }}</q-item-label>
                <q-item-label caption>
                  {{ d.customerRequestNumber }} · matched on {{ (d.matchedFields || []).join(", ") }}
                </q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="dupOpen = false" />
          <q-btn unelevated no-caps color="primary" label="Proceed" :loading="saving" @click="proceedSubmit" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </app-form-drawer>
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { customerApi, getApiErrorMessage } from "services/api";
import { useTenantOptions } from "composables/useTenantOptions";
import { useNotify } from "composables/useNotify";
import { useCountries } from "composables/useCountries";

import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppAddressFields from "components/common/AppAddressFields.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // Optional pre-selected tenant (super admin's chosen list scope).
  tenantId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const { canChooseTenant, activeTenantId, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();
const { DEFAULT_COUNTRY_ISO } = useCountries();

// Address cascade + postal validation now live in the shared AppAddressFields component.
const addressRef = ref(null);

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

// Canonical address shape shared with AppAddressFields (countryName/stateName are resolved by it).
const blankAddress = () => ({
  countryCode: DEFAULT_COUNTRY_ISO,
  countryName: null,
  stateCode: null,
  stateName: null,
  cityName: null,
  postalCode: "",
  addressLine1: "",
  addressLine2: "",
  landmark: "",
  buildingName: "",
  floorNumber: "",
  unitNumber: ""
});

const blankForm = () => ({
  tenantId: null,
  legalName: "",
  companyName: "",
  contactPerson: "",
  emailAddress: "",
  phoneNumber: "",
  // Dial code driving the phone input's country dropdown / formatting; the stored phoneNumber is
  // normalised to E.164 (which already embeds the country), so this is UI-only and not sent.
  phoneCountryCode: null,
  website: "",
  address: blankAddress()
});

const formRef = ref(null);
const drawerRef = ref(null);
const form = reactive(blankForm());
const saving = ref(false);
const savingDraft = ref(false);

const resetForm = () => Object.assign(form, blankForm());
// Merge a saved draft, keeping a complete address object even if the draft predates a field.
const restoreDraft = (saved) => Object.assign(form, blankForm(), saved, { address: { ...blankAddress(), ...(saved?.address || {}) } });

// Prepare the form (tenant scoping) whenever the drawer opens.
watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen) return;
  resetForm();
  if (canChooseTenant.value) {
    await loadTenants();
    form.tenantId = props.tenantId || activeTenantId.value;
  }
});

const buildPayload = () => {
  // AppAddressFields resolves the selected ISO codes to display names; the backend stores names.
  const a = form.address;
  const payload = {
    legalName: form.legalName,
    companyName: form.companyName,
    contactPerson: form.contactPerson || null,
    emailAddress: form.emailAddress,
    phoneNumber: form.phoneNumber || null,
    website: form.website || null,
    country: a.countryName || null,
    stateProvince: a.stateName || null,
    city: a.cityName || null,
    addressLine1: a.addressLine1,
    addressLine2: a.addressLine2 || null,
    postalCode: a.postalCode || null
  };
  // Only super admins send a tenantId; others are auto-scoped server-side.
  if (canChooseTenant.value && form.tenantId) payload.tenantId = form.tenantId;
  return payload;
};

// Runs the q-form rules plus the address component's locale-aware postal-code check.
const validateForm = async () => {
  const formOk = await formRef.value?.validate();
  const addrOk = addressRef.value?.validate() ?? true;
  if (!formOk || !addrOk) {
    if (!addrOk) notify.error("Please fix the highlighted fields.");
    return false;
  }
  return true;
};

// ---- Save as Draft: create only ----
const onSaveDraft = async () => {
  if (!(await validateForm())) return;
  savingDraft.value = true;
  try {
    await customerApi.create(buildPayload());
    drawerRef.value?.clearDraft?.();
    resetForm();
    notify.success("Draft saved.");
    emit("saved");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingDraft.value = false;
  }
};

// ---- Submit for Approval: create then submit (with duplicate handling) ----
const createdId = ref(null);
const dupOpen = ref(false);
const duplicates = ref([]);
let pendingClearDraft = null;

const onSubmitForApproval = async ({ clearDraft } = {}) => {
  if (!(await validateForm())) return;
  pendingClearDraft = clearDraft;
  saving.value = true;
  try {
    const created = await customerApi.create(buildPayload());
    createdId.value = created?.customerId;
    await doSubmit(false);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    saving.value = false;
  }
};

const doSubmit = async (duplicateAcknowledged) => {
  try {
    const result = await customerApi.submit(createdId.value, duplicateAcknowledged);
    if (result?.submitted === false) {
      // Duplicates detected: surface them and let the user proceed or cancel.
      duplicates.value = result?.duplicates || [];
      dupOpen.value = true;
      saving.value = false;
      return;
    }
    finishSubmit();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    saving.value = false;
  }
};

const proceedSubmit = async () => {
  saving.value = true;
  await doSubmit(true);
};

const finishSubmit = () => {
  dupOpen.value = false;
  pendingClearDraft?.();
  pendingClearDraft = null;
  resetForm();
  saving.value = false;
  notify.success("Customer submitted for approval.");
  emit("saved");
};
</script>
