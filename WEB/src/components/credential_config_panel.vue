<template>
  <q-card flat bordered class="cred-card">
    <q-card-section class="row items-center q-gutter-sm">
      <q-icon :name="system === 'Concur' ? 'o_receipt_long' : 'o_account_balance'" color="primary" size="sm" />
      <div class="text-subtitle1 text-weight-medium">{{ system }} credentials</div>
      <q-space />
      <q-badge :color="configured ? 'positive' : 'grey'">
        {{ configured ? "Configured" : "Not configured" }}
      </q-badge>
    </q-card-section>

    <q-banner v-if="testResult" dense :class="testResult.connected ? 'bg-green-1 text-positive' : 'bg-red-1 text-negative'" class="q-mx-md q-mb-sm">
      <template #avatar>
        <q-icon :name="testResult.connected ? 'o_check_circle' : 'o_error'" />
      </template>
      {{ testResult.message }}
    </q-banner>

    <q-separator />
    <q-card-actions>
      <q-btn flat no-caps color="primary" :icon="configured ? 'o_edit' : 'o_add'" :label="configured ? 'Update' : 'Configure'" @click="openForm" />
      <q-btn v-if="configured" flat no-caps color="primary" icon="o_wifi_tethering" label="Test" :loading="testing" @click="test" />
      <q-space />
      <q-btn v-if="configured" flat no-caps color="negative" icon="o_delete" label="Clear" @click="clear" />
    </q-card-actions>

    <app-form-drawer
      v-model="formOpen"
      :title="`${configured ? 'Update' : 'Configure'} ${system}`"
      :saving="saving"
      @submit="submit"
      @cancel="formOpen = false"
    >
      <q-form ref="formRef" greedy>
        <template v-if="system === 'Concur'">
          <q-input v-model="form.clientId" outlined stack-label hide-bottom-space label="Client ID *" class="q-mb-md" :rules="[req]" />
          <q-input v-model="form.clientSecret" outlined stack-label hide-bottom-space label="Client Secret *" type="password" class="q-mb-md" :rules="[req]" />
          <q-input v-model="form.baseUrl" outlined stack-label hide-bottom-space label="Base URL *" class="q-mb-md" :rules="[req]" />
          <q-input v-model="form.companyUuid" outlined stack-label hide-bottom-space label="Company UUID *" :rules="[req]" />
        </template>
        <template v-else>
          <q-input v-model="form.baseUrl" outlined stack-label hide-bottom-space label="Base URL *" class="q-mb-md" :rules="[req]" />
          <q-input v-model="form.username" outlined stack-label hide-bottom-space label="Username *" class="q-mb-md" :rules="[req]" />
          <q-input v-model="form.password" outlined stack-label hide-bottom-space label="Password *" type="password" :rules="[req]" />
        </template>
      </q-form>
    </app-form-drawer>
  </q-card>
</template>

<script setup>
import { ref, reactive } from "vue";
import { tenantApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import AppFormDrawer from "components/common/AppFormDrawer.vue";

const props = defineProps({
  tenantId: { type: String, required: true },
  system: { type: String, required: true }, // "Concur" | "Maconomy"
  configured: { type: Boolean, default: false }
});
const emit = defineEmits(["changed"]);

const notify = useNotify();
const { confirm } = useConfirm();

const req = (v) => !!v || "Required";
const formOpen = ref(false);
const saving = ref(false);
const testing = ref(false);
const testResult = ref(null);
const formRef = ref(null);
const form = reactive({ clientId: "", clientSecret: "", baseUrl: "", companyUuid: "", username: "", password: "" });

const isConcur = () => props.system === "Concur";

const openForm = () => {
  Object.assign(form, { clientId: "", clientSecret: "", baseUrl: "", companyUuid: "", username: "", password: "" });
  formOpen.value = true;
};

const submit = async ({ clearDraft } = {}) => {
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    if (isConcur()) {
      await tenantApi.setConcurConfig(props.tenantId, {
        clientId: form.clientId, clientSecret: form.clientSecret, baseUrl: form.baseUrl, companyUuid: form.companyUuid
      });
    } else {
      await tenantApi.setMaconomyConfig(props.tenantId, {
        baseUrl: form.baseUrl, username: form.username, password: form.password
      });
    }
    notify.success(`${props.system} credentials saved.`);
    clearDraft?.();
    formOpen.value = false;
    emit("changed");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

const test = async () => {
  testing.value = true;
  testResult.value = null;
  try {
    const result = isConcur()
      ? await tenantApi.testConcurConfig(props.tenantId)
      : await tenantApi.testMaconomyConfig(props.tenantId);
    testResult.value = result;
  } catch (err) {
    testResult.value = { connected: false, message: getApiErrorMessage(err) };
  } finally {
    testing.value = false;
  }
};

const clear = async () => {
  const ok = await confirm({
    title: `Clear ${props.system} credentials`,
    message: `Clearing will stop ${props.system} imports for this tenant until reconfigured. Continue?`,
    confirmLabel: "Clear",
    type: "danger"
  });
  if (!ok) return;
  try {
    if (isConcur()) {
      await tenantApi.clearConcurConfig(props.tenantId);
    } else {
      await tenantApi.clearMaconomyConfig(props.tenantId);
    }
    notify.success(`${props.system} credentials cleared.`);
    testResult.value = null;
    emit("changed");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
</script>

<style scoped>
.cred-card {
  border-radius: 12px;
}
</style>
