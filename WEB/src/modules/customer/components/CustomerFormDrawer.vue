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
    <q-form ref="formRef" greedy>
      <app-select
        v-if="canChooseTenant" v-model="form.tenantId" :options="tenantOptions" label="Tenant *"
        :loading="loadingTenants" :clearable="false" class="q-mb-md"
        :rules="[(v) => !!v || 'Tenant is required']"
      />

      <div class="section-subhead">Basic Information</div>
      <div class="row q-col-gutter-md q-mb-sm">
        <app-text-field v-model="form.legalName" label="Legal Name *" class="col-12 col-sm-6" :rules="[(v) => !!v || 'Legal name is required']" />
        <app-text-field v-model="form.companyName" label="Company Name *" class="col-12 col-sm-6" :rules="[(v) => !!v || 'Company name is required']" />
        <app-text-field v-model="form.contactPerson" label="Contact Person" class="col-12 col-sm-6" />
        <app-text-field
          v-model="form.emailAddress" type="email" label="Email Address *" class="col-12 col-sm-6"
          :rules="[(v) => !!v || 'Email is required', (v) => /.+@.+\..+/.test(v) || 'Enter a valid email']"
        />
        <app-text-field v-model="form.phoneNumber" label="Phone Number" class="col-12 col-sm-6" />
        <app-text-field v-model="form.website" label="Website" class="col-12 col-sm-6" />
      </div>

      <div class="section-subhead">Address</div>
      <div class="row q-col-gutter-md">
        <app-select
          v-model="form.country" :options="countryOptions" label="Country *" use-input
          class="col-12 col-sm-6" :rules="[(v) => !!v || 'Country is required']"
          @filter="filterCountries"
        />
        <app-text-field v-model="form.stateProvince" label="State / Province" class="col-12 col-sm-6" />
        <app-text-field v-model="form.city" label="City" class="col-12 col-sm-6" />
        <app-text-field v-model="form.postalCode" label="Postal Code" class="col-12 col-sm-6" />
        <app-text-field v-model="form.addressLine1" label="Address Line 1 *" class="col-12" :rules="[(v) => !!v || 'Address Line 1 is required']" />
        <app-text-field v-model="form.addressLine2" label="Address Line 2" class="col-12" />
      </div>

      <q-separator class="q-my-md" />
      <div class="row justify-end q-gutter-sm">
        <q-btn outline no-caps color="primary" label="Save as Draft" :loading="savingDraft" :disable="saving" @click="onSaveDraft" />
      </div>
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
import { useCountries, orderedCountries, countryNameOption } from "composables/useCountries";

import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // Optional pre-selected tenant (super admin's chosen list scope).
  tenantId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const { canChooseTenant, activeTenantId, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();
const { DEFAULT_COUNTRY_ISO } = useCountries();

// Country dropdown: US default + India pinned (orderedCountries), value is the display name.
const countryDefault = orderedCountries.find((c) => c.isoCode === DEFAULT_COUNTRY_ISO)?.name || "United States";
const countryOptions = ref(orderedCountries.map(countryNameOption));
const filterCountries = (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => { countryOptions.value = orderedCountries.map(countryNameOption).filter((o) => o.label.toLowerCase().includes(needle)); });
};

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const blankForm = () => ({
  tenantId: null,
  legalName: "",
  companyName: "",
  contactPerson: "",
  emailAddress: "",
  phoneNumber: "",
  website: "",
  country: countryDefault,
  stateProvince: "",
  city: "",
  addressLine1: "",
  addressLine2: "",
  postalCode: ""
});

const formRef = ref(null);
const drawerRef = ref(null);
const form = reactive(blankForm());
const saving = ref(false);
const savingDraft = ref(false);

const resetForm = () => Object.assign(form, blankForm());
const restoreDraft = (saved) => Object.assign(form, blankForm(), saved);

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
  const payload = {
    legalName: form.legalName,
    companyName: form.companyName,
    contactPerson: form.contactPerson || null,
    emailAddress: form.emailAddress,
    phoneNumber: form.phoneNumber || null,
    website: form.website || null,
    country: form.country,
    stateProvince: form.stateProvince || null,
    city: form.city || null,
    addressLine1: form.addressLine1,
    addressLine2: form.addressLine2 || null,
    postalCode: form.postalCode || null
  };
  // Only super admins send a tenantId; others are auto-scoped server-side.
  if (canChooseTenant.value && form.tenantId) payload.tenantId = form.tenantId;
  return payload;
};

// ---- Save as Draft: create only ----
const onSaveDraft = async () => {
  if (!(await formRef.value?.validate())) return;
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
  if (!(await formRef.value?.validate())) return;
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

<style scoped>
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin: 4px 0 8px;
}
</style>
