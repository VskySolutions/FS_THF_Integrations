<template>
  <div class="row items-center q-gutter-xs no-wrap">
    <!-- Pin -->
    <q-btn
      flat round dense
      :icon="pinned ? 'push_pin' : 'o_push_pin'"
      :color="pinned ? 'primary' : 'grey-7'"
      :loading="busy"
      @click="toggle"
    >
      <q-tooltip>{{ pinned ? "Unpin" : "Pin" }}</q-tooltip>
    </q-btn>

    <!-- Colour -->
    <q-btn flat round dense icon="o_palette" :style="{ color: currentColour || '#757575' }">
      <q-tooltip>Colour</q-tooltip>
      <q-menu>
        <div class="q-pa-sm row q-gutter-xs" style="max-width: 168px;">
          <div
            v-for="c in palette"
            :key="c"
            class="uf-swatch cursor-pointer"
            :style="{ backgroundColor: c, outline: c === currentColour ? '2px solid #1976d2' : 'none' }"
            @click="setColour(c)"
          />
          <q-btn v-close-popup flat dense no-caps size="sm" label="None" class="full-width q-mt-xs" @click="setColour(null)" />
        </div>
      </q-menu>
    </q-btn>

    <!-- Reminder -->
    <q-btn flat round dense icon="o_alarm" :color="reminder ? 'orange-8' : 'grey-7'" @click="openReminder">
      <q-tooltip>{{ reminder ? "Reminder set" : "Set reminder" }}</q-tooltip>
    </q-btn>

    <!-- Copy link -->
    <q-btn flat round dense icon="o_link" color="grey-7" @click="copyLink">
      <q-tooltip>Copy link</q-tooltip>
    </q-btn>

    <!-- PDF -->
    <q-btn flat round dense icon="o_print" color="grey-7" @click="pdfOpen = true">
      <q-tooltip>Export PDF</q-tooltip>
    </q-btn>

    <q-badge v-if="reminderOverdue" color="negative" class="q-ml-xs" label="Reminder overdue" />

    <!-- Reminder drawer -->
    <app-form-drawer v-model="reminderOpen" title="Set reminder" :saving="savingReminder" @submit="saveReminder" @cancel="reminderOpen = false">
      <q-form>
        <q-input v-model="reminderForm.dueAt" type="datetime-local" outlined dense label="Remind me at *" />
        <app-text-field v-model="reminderForm.note" label="Note" type="textarea" autogrow class="q-mt-sm" />
      </q-form>
      <template #footer-actions>
        <q-btn v-if="reminder" flat no-caps color="negative" label="Cancel reminder" @click="cancelReminder" />
      </template>
    </app-form-drawer>

    <!-- PDF dialog -->
    <q-dialog v-model="pdfOpen">
      <q-card style="min-width: 320px;">
        <q-card-section class="text-h6">Export to PDF</q-card-section>
        <q-card-section class="q-pt-none">
          <q-toggle v-model="includeNotes" label="Include notes" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Cancel" />
          <q-btn unelevated no-caps color="primary" label="Download" :loading="exporting" @click="exportPdf" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { copyToClipboard } from "quasar";
import { ufReminderApi, ufColourApi, ufPdfApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { usePins } from "composables/uf/usePins";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true },
  label: { type: String, default: "record" }
});

const notify = useNotify();
const { confirm } = useConfirm();
const { pinned, busy, refresh: refreshPin, toggle } = usePins(props.entityType, props.entityId);

const palette = ["#ef5350", "#ec407a", "#ab47bc", "#5c6bc0", "#42a5f5", "#26a69a", "#9ccc65", "#ffa726"];
const currentColour = ref(null);

const reminder = ref(null);
const reminderOpen = ref(false);
const savingReminder = ref(false);
const reminderForm = reactive({ dueAt: "", note: "" });
const reminderOverdue = computed(() => !!reminder.value?.isOverdue);

const pdfOpen = ref(false);
const includeNotes = ref(true);
const exporting = ref(false);

const loadColour = async () => {
  try {
    const map = await ufColourApi.batch(props.entityType, [props.entityId]);
    currentColour.value = map?.[props.entityId] || null;
  } catch { /* ignore */ }
};

const setColour = async (colour) => {
  try {
    await ufColourApi.upsert({ entityType: props.entityType, entityId: props.entityId, colour });
    currentColour.value = colour;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const loadReminder = async () => {
  try {
    const res = await ufReminderApi.list({ page: 1, limit: 100 });
    reminder.value = (res?.data || []).find(
      (r) => Number(r.entityType) === Number(props.entityType) && r.entityId === props.entityId && !r.isDispatched
    ) || null;
  } catch { /* ignore */ }
};

const openReminder = () => {
  reminderForm.dueAt = reminder.value?.dueAtUtc ? toLocalInput(reminder.value.dueAtUtc) : "";
  reminderForm.note = reminder.value?.note || "";
  reminderOpen.value = true;
};

const saveReminder = async () => {
  if (!reminderForm.dueAt) {
    notify.warning("Please choose a date and time.");
    return;
  }
  savingReminder.value = true;
  try {
    const payload = { dueAtUtc: new Date(reminderForm.dueAt).toISOString(), note: reminderForm.note || null };
    if (reminder.value) {
      await ufReminderApi.update(reminder.value.id, payload);
    } else {
      await ufReminderApi.create({ entityType: props.entityType, entityId: props.entityId, ...payload });
    }
    reminderOpen.value = false;
    notify.success("Reminder saved.");
    await loadReminder();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingReminder.value = false;
  }
};

const cancelReminder = async () => {
  if (!reminder.value) return;
  const ok = await confirm({ title: "Cancel reminder", message: "Remove this reminder?", confirmLabel: "Remove", type: "danger" });
  if (!ok) return;
  try {
    await ufReminderApi.remove(reminder.value.id);
    reminder.value = null;
    reminderOpen.value = false;
    notify.success("Reminder cancelled.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const copyLink = async () => {
  try {
    await copyToClipboard(window.location.href);
    notify.success("Link copied.");
  } catch {
    notify.error("Could not copy link.");
  }
};

const exportPdf = async () => {
  exporting.value = true;
  try {
    const blob = await ufPdfApi.export({ entityType: props.entityType, entityId: props.entityId, includeNotes: includeNotes.value });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${props.label}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
    pdfOpen.value = false;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    exporting.value = false;
  }
};

// Convert a UTC ISO string to a value the datetime-local input accepts (local wall-clock).
const toLocalInput = (utc) => {
  const d = new Date(utc);
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

onMounted(async () => {
  await Promise.all([refreshPin(), loadColour(), loadReminder()]);
});
</script>

<style scoped>
.uf-swatch { width: 28px; height: 28px; border-radius: 6px; }
</style>
