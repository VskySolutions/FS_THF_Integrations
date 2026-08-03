<template>
  <q-card flat bordered class="rems-card q-mb-md">
    <q-card-section class="rems-card__title row items-center q-py-sm">
      <div class="text-subtitle1 text-primary">
        <q-icon name="o_badge" size="20px" class="q-mr-xs" />Client Submitted (Editable)
      </div>
      <q-space />
      <q-btn
        unelevated no-caps color="primary" icon="o_save" label="Save client"
        :loading="saving" :disable="!dirty" @click="save"
      />
    </q-card-section>
    <q-separator />

    <q-card-section>
      <q-form ref="formRef" greedy>
        <div class="row q-col-gutter-md">
          <app-text-field
            v-model="form.name" label="Client Name" required class="col-12 col-md-6"
            :rules="[(v) => (!!v && v.trim().length > 0) || 'Client name is required']"
          />
          <!-- The client email is the request's authoritative customer email — locked, never editable
               (AC-REMS-014.2). -->
          <app-text-field
            :model-value="client.email" label="Email (locked)" readonly class="col-12 col-md-6"
          >
            <template #append><q-icon name="o_lock" size="18px" color="grey-6" /></template>
          </app-text-field>
          <app-text-field v-model="form.mobileNumber" label="Mobile Number" class="col-12 col-md-6" />
          <app-text-field
            :model-value="client.referralSource || '—'" label="Referral Source" readonly class="col-12 col-md-6"
          />
        </div>

        <div class="section-subhead">Billing</div>
        <div class="row q-col-gutter-md">
          <app-text-field v-model="form.billingContactName" label="Billing Contact" class="col-12 col-md-6" />
          <app-text-field
            v-model="form.billingEmail" label="Billing Email" type="email" class="col-12 col-md-6"
            :rules="emailRules"
          />
        </div>
        <div class="q-mt-sm">
          <!-- Billing address stays optional here: this edits an existing client, and the address may
               simply not have been collected yet. -->
          <app-address-fields ref="addressRef" v-model="form.billingAddress" />
        </div>
      </q-form>
    </q-card-section>
  </q-card>
</template>

<script setup>
// The editable client record at the top of the engagement workspace (AC-REMS-014.2/3). Name, mobile and
// the billing contact/email/address are editable; the email is the locked authoritative customer email.
import { ref, computed, watch, nextTick } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import AppTextField from "components/common/AppTextField.vue";
import AppAddressFields from "components/common/AppAddressFields.vue";
import { toAddress, fromAddress } from "modules/rems/remsAddress";

const props = defineProps({
  client: { type: Object, required: true },
  remsId: { type: String, required: true }
});
const emit = defineEmits(["updated"]);

const notify = useNotify();
const formRef = ref(null);
// The address block's own checks (postal format) are not q-form rules, so they are run explicitly.
const addressRef = ref(null);
const saving = ref(false);

const buildForm = (c) => ({
  name: c.name || "",
  mobileNumber: c.mobileNumber || "",
  billingContactName: c.billingContactName || "",
  billingEmail: c.billingEmail || "",
  billingAddress: toAddress(c.billingAddress)
});

const form = ref(buildForm(props.client));

// The dirty baseline is snapshotted AFTER the address field-set has settled: on mount it fills in the
// values it derives from the ISO codes (country name, a recovered state code), and comparing against a
// freshly-built form would read those as unsaved edits and light up Save on load.
const baseline = ref("");
watch(
  () => props.client,
  (c) => { form.value = buildForm(c); nextTick(() => { baseline.value = JSON.stringify(form.value); }); },
  { immediate: true }
);

const emailRules = [
  (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v) || "Enter a valid email address"
];

// Enable the Save button only when something actually changed (avoids no-op writes).
const dirty = computed(() => !!baseline.value && JSON.stringify(form.value) !== baseline.value);

const save = async () => {
  const addressOk = addressRef.value?.validate() !== false;
  if (!(await formRef.value?.validate()) || !addressOk) return;
  saving.value = true;
  try {
    const view = await remsApi.updateClient(props.remsId, {
      name: form.value.name,
      mobileNumber: form.value.mobileNumber,
      billingContactName: form.value.billingContactName,
      billingEmail: form.value.billingEmail,
      billingAddress: fromAddress(form.value.billingAddress)
    });
    notify.success("Client details saved.");
    emit("updated", view);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>

<style scoped>
.rems-card { border-radius: 12px; }
</style>
