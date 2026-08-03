<template>
  <q-dialog v-model="open" persistent>
    <q-card style="min-width: 520px; max-width: 92vw;">
      <q-card-section class="row items-center no-wrap">
        <div>
          <div class="text-h6">{{ phase === 'sent' ? 'Form sent' : 'Send EMS Form' }}</div>
          <div v-if="subtitle" class="text-caption text-grey-7">{{ subtitle }}</div>
        </div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="open = false" />
      </q-card-section>
      <q-separator />

      <!-- Loading the preview -->
      <q-card-section v-if="phase === 'loading'" class="row flex-center q-pa-xl">
        <q-spinner color="primary" size="32px" />
      </q-card-section>

      <!-- Preview: destination email + the exact link that will be emailed (AC-REMS-008.1) -->
      <q-card-section v-else-if="phase === 'preview' || phase === 'sending'">
        <div class="text-body2 text-grey-8 q-mb-md">
          Review the destination and link below, then confirm to email the client their EMS Form link.
        </div>

        <div class="send-label">Destination email</div>
        <div v-if="preview.destinationEmail" class="send-value q-mb-md">{{ preview.destinationEmail }}</div>
        <!-- No client email → sending is blocked (AC-REMS-008.2). -->
        <q-banner v-else dense class="bg-orange-1 text-orange-9 q-mb-md rounded-borders">
          <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
          This client has no email address on file, so the form cannot be sent. Add a customer email on the
          request first.
        </q-banner>

        <div class="send-label">Form link</div>
        <div class="row items-center no-wrap q-gutter-xs">
          <div class="send-value send-link col">{{ formLink }}</div>
          <q-btn flat round dense icon="o_content_copy" color="primary" @click="copyLink">
            <q-tooltip>Copy link</q-tooltip>
          </q-btn>
        </div>
      </q-card-section>

      <!-- Error resolving the preview (e.g. form not built yet) -->
      <q-card-section v-else-if="phase === 'error'">
        <q-banner dense class="bg-red-1 text-red-9 rounded-borders">
          <template #avatar><q-icon name="o_error" color="red-9" /></template>
          {{ errorMsg }}
        </q-banner>
      </q-card-section>

      <!-- Sent success state -->
      <q-card-section v-else-if="phase === 'sent'">
        <div class="column flex-center q-py-md text-center">
          <q-icon name="o_mark_email_read" color="positive" size="48px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">The EMS Form link was emailed to the client.</div>
          <div class="text-grey-7">{{ sentEmail }}</div>
          <div class="text-caption text-grey-6 q-mt-sm">
            The industry group and link are now locked. Delivery and open status will appear in the Email Log
            as the provider reports them.
          </div>
        </div>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right">
        <template v-if="phase === 'sent'">
          <q-btn
            v-if="canViewEmailLog"
            outline no-caps color="primary" icon="o_history" label="View Email Log"
            @click="viewLog"
          />
          <q-btn unelevated no-caps color="primary" label="Done" @click="open = false" />
        </template>
        <template v-else>
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="open = false" />
          <q-btn
            unelevated no-caps color="primary" label="Confirm & Send" icon="o_send"
            :loading="phase === 'sending'"
            :disable="!canConfirm"
            @click="confirmSend"
          />
        </template>
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { copyToClipboard } from "quasar";
import { remsApi, getApiErrorMessage, webUrl } from "services/api";
import { useNotify } from "composables/useNotify";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  remsId: { type: String, default: null },
  subtitle: { type: String, default: "" },
  // Whether to offer the "View Email Log" hand-off after sending (gated on rems.emailLog.read by the parent).
  canViewEmailLog: { type: Boolean, default: false }
});
const emit = defineEmits(["update:modelValue", "sent", "view-log"]);

const notify = useNotify();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

// loading → preview → sending → sent | error
const phase = ref("loading");
const preview = ref({ destinationEmail: null, formLink: "" });
const errorMsg = ref("");
const sentEmail = ref("");

// Confirm is active only once the preview resolved with a real destination email (AC-REMS-008.2).
const canConfirm = computed(() => phase.value === "preview" && !!preview.value.destinationEmail);

// Show the link exactly as it should be opened — absolute, even if the API's App:BaseUrl is unset.
const formLink = computed(() => webUrl(preview.value.formLink));

const loadPreview = async () => {
  phase.value = "loading";
  errorMsg.value = "";
  try {
    const p = await remsApi.previewForm(props.remsId);
    preview.value = { destinationEmail: p?.destinationEmail || null, formLink: p?.formLink || "" };
    phase.value = "preview";
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
    phase.value = "error";
  }
};

const confirmSend = async () => {
  if (!canConfirm.value) return;
  phase.value = "sending";
  try {
    await remsApi.sendForm(props.remsId);
    sentEmail.value = preview.value.destinationEmail;
    phase.value = "sent";
    notify.success("EMS Form link sent to the client.");
    emit("sent");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    phase.value = "preview";
  }
};

const copyLink = () => {
  if (!formLink.value) return;
  copyToClipboard(formLink.value)
    .then(() => notify.success("Link copied."))
    .catch(() => notify.error("Could not copy the link."));
};

const viewLog = () => {
  emit("view-log");
  open.value = false;
};

watch(() => props.modelValue, (isOpen) => {
  if (isOpen && props.remsId) loadPreview();
});
</script>

<style scoped>
.send-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 2px;
}
.send-value {
  font-size: 14px;
  color: #2c3540;
  word-break: break-word;
}
.send-link {
  font-family: monospace;
  background: #f5f7fa;
  border: 1px solid #e0e6ed;
  border-radius: 6px;
  padding: 6px 8px;
}
</style>
