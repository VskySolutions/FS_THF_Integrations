<template>
  <q-dialog v-model="open" persistent>
    <q-card style="min-width: 460px; max-width: 92vw;">
      <!-- Header -->
      <q-card-section class="row items-center bg-primary text-white">
        <q-icon name="o_send" size="22px" />
        <div class="text-h6 q-ml-sm">Send Test Email</div>
        <q-space />
        <q-btn flat round dense color="white" icon="o_close" @click="close" />
      </q-card-section>
      <q-separator />

      <q-card-section>
        <!-- Account context -->
        <div class="text-body2 text-grey-8 q-mb-md">
          Sending from <span class="text-weight-medium">{{ account?.accountName }}</span>
          <span v-if="account"> ({{ account.host }}:{{ account.port }})</span>.
        </div>

        <q-form ref="formRef" greedy>
          <app-text-field
            v-model="recipient" label="Recipient Email *" type="email"
            :rules="[
              (v) => !!v || 'Recipient is required',
              (v) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v) || 'Enter a valid email address'
            ]"
          />
        </q-form>

        <!-- Result: stays visible until the user dismisses the dialog (AC-SMTP-006). -->
        <q-banner v-if="result && result.success" dense class="bg-positive text-white q-mt-md rounded-borders">
          <template #avatar><q-icon name="o_check_circle" /></template>
          Test email sent successfully{{ result.sentAtUtc ? ` at ${fmt.formatDateTime(result.sentAtUtc)}` : "" }}.
          <div v-if="result.serverResponse" class="text-caption q-mt-xs">Server: {{ result.serverResponse }}</div>
        </q-banner>

        <q-banner v-else-if="result" dense class="bg-negative text-white q-mt-md rounded-borders">
          <template #avatar><q-icon name="o_error" /></template>
          {{ errorCategoryLabel(result.errorCategory) }}
          <q-expansion-item
            v-if="result.errorDetail"
            dense dense-toggle
            label="Show details"
            class="q-mt-xs text-white"
          >
            <div class="text-caption q-pa-sm" style="white-space: pre-wrap; word-break: break-word;">
              {{ result.errorDetail }}
            </div>
          </q-expansion-item>
        </q-banner>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right" class="q-gutter-sm bg-grey-1">
        <q-btn flat no-caps color="grey-8" label="Close" @click="close" />
        <q-btn
          unelevated no-caps color="primary" label="Send Test"
          :loading="sending" :disable="sending"
          @click="send"
        >
          <template #loading><q-spinner size="20px" color="white" /></template>
        </q-btn>
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { smtpAccountApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useSmtpOptions } from "composables/useSmtpOptions";

import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // The account to test (carries accountName, host, port).
  account: { type: Object, default: null },
  // Super admin's chosen list scope.
  tenantId: { type: String, default: null }
});
const emit = defineEmits(["update:modelValue"]);

const notify = useNotify();
const fmt = useDateFormat();
const { errorCategoryLabel } = useSmtpOptions();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const recipient = ref("");
const sending = ref(false);
const result = ref(null);
const formRef = ref(null);

// Reset the input and previous result each time the dialog opens.
watch(() => props.modelValue, (isOpen) => {
  if (isOpen) {
    recipient.value = "";
    result.value = null;
    sending.value = false;
  }
});

const close = () => { open.value = false; };

const send = async () => {
  if (!(await formRef.value?.validate())) return;
  sending.value = true;
  result.value = null;
  try {
    result.value = await smtpAccountApi.test(props.account.id, recipient.value, props.tenantId || undefined);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    sending.value = false;
  }
};
</script>
