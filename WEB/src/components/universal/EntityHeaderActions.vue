<template>
  <div class="row items-center q-gutter-xs no-wrap">
    <!-- Pin -->
    <q-btn
      flat round dense
      icon="o_push_pin"
      :color="pinned ? 'primary' : 'grey-7'"
      :loading="busy"
      @click="togglePin"
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
          <q-toggle v-model="includeConversation" label="Include conversation" />
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
import { onMounted } from "vue";
import { useEntityActions } from "composables/uf/useEntityActions";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppTextField from "components/common/AppTextField.vue";

// Detail-header icon bar for the Universal Features per-record actions. Shares all behaviour with
// the list-row menu (EntityRowActionsMenu) via the useEntityActions composable.
const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true },
  label: { type: String, default: "record" }
});

const {
  palette,
  pinned, busy, togglePin,
  currentColour, setColour,
  reminder, reminderOverdue, reminderOpen, savingReminder, reminderForm, openReminder, saveReminder, cancelReminder,
  pdfOpen, includeConversation, exporting, exportPdf,
  copyLink, loadAll
} = useEntityActions(props.entityType, props.entityId, props.label);

onMounted(loadAll);
</script>

<style scoped>
.uf-swatch { width: 28px; height: 28px; border-radius: 6px; }
</style>
