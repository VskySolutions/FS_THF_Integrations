<template>
  <q-card flat bordered>
    <!-- Inline tags chips -->
    <div class="q-px-md q-pt-sm">
      <entity-tags-panel :entity-type="entityType" :entity-id="entityId" />
    </div>
    <q-separator class="q-mt-sm" />

    <q-tabs
      v-model="tab"
      dense
      align="left"
      active-color="primary"
      indicator-color="primary"
      class="text-grey-7"
    >
      <q-tab name="notes" icon="o_chat" label="Notes" no-caps />
      <q-tab name="activity" icon="o_history" label="Activity" no-caps />
      <q-tab name="checklists" icon="o_checklist" label="Checklists" no-caps />
      <q-tab name="attachments" icon="o_attach_file" label="Attachments" no-caps />
    </q-tabs>
    <q-separator />

    <q-tab-panels v-model="tab" keep-alive animated>
      <q-tab-panel name="notes">
        <entity-notes-panel v-if="opened.notes" :entity-type="entityType" :entity-id="entityId" />
      </q-tab-panel>
      <q-tab-panel name="activity">
        <entity-activity-timeline v-if="opened.activity" :entity-type="entityType" :entity-id="entityId" />
      </q-tab-panel>
      <q-tab-panel name="checklists">
        <entity-checklists-panel v-if="opened.checklists" :entity-type="entityType" :entity-id="entityId" />
      </q-tab-panel>
      <q-tab-panel name="attachments">
        <entity-attachments-panel v-if="opened.attachments" :entity-type="entityType" :entity-id="entityId" />
      </q-tab-panel>
    </q-tab-panels>
  </q-card>
</template>

<script setup>
import { ref, reactive, watch } from "vue";
import EntityTagsPanel from "./EntityTagsPanel.vue";
import EntityNotesPanel from "./EntityNotesPanel.vue";
import EntityActivityTimeline from "./EntityActivityTimeline.vue";
import EntityChecklistsPanel from "./EntityChecklistsPanel.vue";
import EntityAttachmentsPanel from "./EntityAttachmentsPanel.vue";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true },
  initialTab: { type: String, default: "notes" }
});

const tab = ref(props.initialTab);
// Lazy-load each tab's content only after it is first opened.
const opened = reactive({ notes: false, activity: false, checklists: false, attachments: false });

const markOpened = (name) => { opened[name] = true; };
markOpened(tab.value);
watch(tab, (value) => markOpened(value));
</script>
