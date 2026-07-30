<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 520px; max-width: 92vw;">
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

      <q-card-section style="max-height: 70vh; overflow: auto;">
        <div v-if="loading" class="row flex-center q-pa-lg"><q-spinner color="primary" size="32px" /></div>

        <!-- Provider events only: Sent / Delivered / Opened / Failed, each with its own timestamp
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
                <template v-if="ev.providerMessageId"> · {{ ev.providerMessageId }}</template>
              </q-item-label>
            </q-item-section>
          </q-item>
        </q-list>

        <div v-else class="column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_mark_email_unread" size="36px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No email activity yet</div>
          <div>Delivery and open events appear here once the client form link email is sent.</div>
        </div>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  remsId: { type: String, default: null },
  subtitle: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue"]);

const notify = useNotify();
const fmt = useDateFormat();
const { emailEventLabel, emailEventColor, emailEventIcon } = useRemsMeta();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const events = ref([]);
const loading = ref(false);

const load = async () => {
  if (!props.remsId) return;
  loading.value = true;
  try {
    events.value = (await remsApi.emailLog(props.remsId)) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    events.value = [];
  } finally {
    loading.value = false;
  }
};

watch(() => props.modelValue, (isOpen) => { if (isOpen) load(); });
</script>
