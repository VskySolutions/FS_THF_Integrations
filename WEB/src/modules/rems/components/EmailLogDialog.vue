<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 560px; max-width: 92vw;">
      <q-card-section class="row items-center no-wrap">
        <div>
          <div class="text-h6">Email Log</div>
          <div v-if="subtitle" class="text-caption text-grey-7">{{ subtitle }}</div>
        </div>
        <q-space />
        <q-btn flat round dense icon="o_refresh" :loading="loading" @click="load">
          <q-tooltip>Refresh</q-tooltip>
        </q-btn>
        <q-btn flat round dense icon="o_close" @click="open = false" />
      </q-card-section>
      <q-separator />

      <q-card-section style="max-height: 62vh; overflow: auto;">
        <div v-if="loading" class="row flex-center q-pa-lg"><q-spinner color="primary" size="32px" /></div>

        <!-- What actually happened to each email: what we sent (Sent / Reminder), and what the provider
             reported back about it (Delivered / Opened / Failed), each with its own timestamp
             (AC-REMS-008.6). Delivery and open are NEVER synthesised — an unsent or newly-sent form
             simply shows fewer (or no) events. -->
        <q-list v-else-if="events.length" separator>
          <q-item v-for="ev in events" :key="ev.id">
            <q-item-section avatar>
              <q-icon :name="emailEventIcon(ev.eventType)" :color="emailEventColor(ev.eventType)" size="24px" />
            </q-item-section>
            <q-item-section>
              <q-item-label>
                <q-badge :color="emailEventColor(ev.eventType)">{{ emailEventLabel(ev.eventType) }}</q-badge>
                <span class="q-ml-sm text-grey-8">{{ ev.recipientEmail || "—" }}</span>
              </q-item-label>
              <q-item-label caption>
                {{ fmt.formatDateTime(ev.occurredOnUtc) }}
                <!-- Who chased the client, on the rows where somebody did. The provider's own callbacks
                     have no actor, so they say nothing rather than crediting the last human. -->
                <template v-if="ev.sentBy"> · by {{ ev.sentBy }}</template>
              </q-item-label>
              <q-item-label v-if="ev.providerMessageId" caption class="event-id">
                {{ ev.providerMessageId }}
              </q-item-label>
              <!-- Why a Failed event failed. Only set for failures the portal recorded itself; provider
                   webhook payloads are never echoed here. -->
              <q-item-label v-if="ev.detail" caption class="text-negative event-detail">
                {{ ev.detail }}
              </q-item-label>
            </q-item-section>
          </q-item>
        </q-list>

        <div v-else class="column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_mark_email_unread" size="36px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No email activity yet</div>
          <div class="text-center">
            Every send, reminder and delivery update for this client's intake form appears here.
          </div>
        </div>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right">
        <!-- Why the client cannot be chased from here, when that is worth saying: they have already
             answered, the form was never sent, the request is with somebody else. A caller who simply
             may not send gets neither the button nor an explanation of a permission they do not hold. -->
        <div v-if="!canRemind && remindBlockedReason" class="col text-caption text-grey-7 remind-note">
          {{ remindBlockedReason }}
        </div>
        <q-btn
          v-if="canRemind" unelevated no-caps color="amber-8" icon="o_notifications_active"
          label="Send reminder" @click="reminderOpen = true"
        />
        <q-btn flat no-caps color="grey-8" label="Close" @click="open = false" />
      </q-card-actions>
    </q-card>
  </q-dialog>

  <!-- The same compose-and-send dialog the request page uses, so a reminder reads and behaves identically
       wherever it is sent from. A sibling of the log rather than a child of it — each QDialog portals
       itself — so it simply stacks on top, and sending drops back to a log that has the new Reminder row
       on it. -->
  <send-ems-dialog v-model="reminderOpen" mode="reminder" :rems-id="remsId" :subtitle="subtitle" @sent="onSent" />
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";

import SendEmsDialog from "modules/rems/components/SendEmsDialog.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  remsId: { type: String, default: null },
  subtitle: { type: String, default: "" }
});
// `sent` fires after a reminder goes out, so a list that shows the last email event can refresh itself.
const emit = defineEmits(["update:modelValue", "sent"]);

const notify = useNotify();
const fmt = useDateFormat();
const { emailEventLabel, emailEventColor, emailEventIcon } = useRemsMeta();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const events = ref([]);
const loading = ref(false);
const reminderOpen = ref(false);
// Whether THIS caller can chase the client, decided by the server rather than re-derived from a row: it
// is the reminder endpoint's own answer — permission, whose request it is, and the state window — so the
// button is offered exactly when pressing it would work.
const canRemind = ref(false);
const remindBlockedReason = ref("");

const load = async () => {
  if (!props.remsId) return;
  loading.value = true;
  try {
    const log = await remsApi.emailLog(props.remsId);
    events.value = log?.events || [];
    canRemind.value = !!log?.canRemind;
    remindBlockedReason.value = log?.remindBlockedReason || "";
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    events.value = [];
    canRemind.value = false;
    remindBlockedReason.value = "";
  } finally {
    loading.value = false;
  }
};

const onSent = () => {
  load();
  emit("sent");
};

watch(() => props.modelValue, (isOpen) => { if (isOpen) load(); });
</script>

<style scoped>
.event-id {
  font-family: monospace;
  font-size: 11px;
  word-break: break-all;
}
.event-detail {
  white-space: normal;
  word-break: break-word;
}
/* Sits to the left of the buttons and wraps rather than pushing them off the card. */
.remind-note {
  white-space: normal;
  text-align: left;
  padding-right: 8px;
}
</style>
