<template>
  <q-card flat bordered class="rems-card q-mb-md">
    <q-card-section class="row items-center q-py-sm">
      <div class="text-subtitle1 text-primary">
        <q-icon name="o_badge" size="20px" class="q-mr-xs" />Client
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
          <public-address-fields v-model="form.billingAddress" />
        </div>
      </q-form>
    </q-card-section>
  </q-card>
</template>

<script setup>
// The editable client record at the top of the engagement workspace (AC-REMS-014.2/3). Name, mobile and
// the billing contact/email/address are editable; the email is the locked authoritative customer email.
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import AppTextField from "components/common/AppTextField.vue";
import PublicAddressFields from "modules/rems/components/PublicAddressFields.vue";

const props = defineProps({
  client: { type: Object, required: true },
  remsId: { type: String, required: true }
});
const emit = defineEmits(["updated"]);

const notify = useNotify();
const formRef = ref(null);
const saving = ref(false);

const blankAddress = (a) => ({ street: a?.street || "", city: a?.city || "", state: a?.state || "", zip: a?.zip || "" });
const buildForm = (c) => ({
  name: c.name || "",
  mobileNumber: c.mobileNumber || "",
  billingContactName: c.billingContactName || "",
  billingEmail: c.billingEmail || "",
  billingAddress: blankAddress(c.billingAddress)
});

const form = ref(buildForm(props.client));
watch(() => props.client, (c) => { form.value = buildForm(c); });

const emailRules = [
  (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v) || "Enter a valid email address"
];

// Enable the Save button only when something actually changed (avoids no-op writes).
const dirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(buildForm(props.client)));

const save = async () => {
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    const view = await remsApi.updateClient(props.remsId, {
      name: form.value.name,
      mobileNumber: form.value.mobileNumber,
      billingContactName: form.value.billingContactName,
      billingEmail: form.value.billingEmail,
      billingAddress: form.value.billingAddress
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
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin: 16px 0 8px;
}
</style>
