<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 560px; max-width: 92vw;">
      <q-card-section class="row items-center no-wrap">
        <div>
          <div class="text-h6">Conversation</div>
          <div v-if="subtitle" class="text-caption text-grey-7">{{ subtitle }}</div>
        </div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="open = false" />
      </q-card-section>
      <q-separator />
      <q-card-section style="max-height: 70vh; overflow: auto;">
        <!-- Reuses the Universal Features Notes thread keyed on the REMS request. -->
        <entity-notes-panel v-if="requestId" :entity-type="EntityType.Rems" :entity-id="requestId" />
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { computed } from "vue";
import { EntityType } from "services/api";
// Must be imported explicitly: <script setup> resolves components from this scope, and only ZwDate /
// ZwCurrency / ZwNumeric are registered globally (boot/components.js). Without this the tag silently
// resolves to nothing and the dialog opens with an empty body.
import EntityNotesPanel from "components/universal/EntityNotesPanel.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  requestId: { type: String, default: null },
  subtitle: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue"]);

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});
</script>
