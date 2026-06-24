<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 560px; max-width: 92vw;">
      <q-card-section class="row items-center bg-primary text-white">
        <q-icon name="o_visibility" size="22px" />
        <div class="text-h6 q-ml-sm">Preview</div>
        <q-space />
        <q-btn flat round dense color="white" icon="o_close" @click="open = false" />
      </q-card-section>
      <q-separator />

      <q-card-section>
        <div class="text-caption text-grey-7">Subject</div>
        <div class="text-weight-medium q-mb-md">{{ subject }}</div>
        <div class="text-caption text-grey-7 q-mb-xs">Body (rendered with sample data)</div>
        <!-- Rendered template HTML; content is admin-authored and previewed with sample placeholder values. -->
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="email-preview rounded-borders q-pa-md" v-html="body" />
      </q-card-section>

      <q-separator />
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat no-caps color="grey-8" label="Close" @click="open = false" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  subject: { type: String, default: "" },
  body: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue"]);

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});
</script>

<style scoped>
.email-preview {
  border: 1px solid var(--q-primary);
  background: #fff;
  max-height: 50vh;
  overflow: auto;
}
</style>
