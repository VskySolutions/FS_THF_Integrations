<template>
  <div v-if="text" class="app-field-label">
    {{ text }}<span v-if="isRequired" class="app-field-label__star" aria-hidden="true">*</span>
  </div>
</template>

<script setup>
// Standard external field label: sits at the top-left above the input (not inside it). A mandatory
// field is marked with a larger red asterisk. Pair it with any App* field control; the controls do
// this automatically so existing "Label *" strings render correctly with no call-site changes.
import { toRef } from "vue";
import { useFieldLabel } from "composables/useFieldLabel";

const props = defineProps({
  label: { type: String, default: "" },
  required: { type: Boolean, default: false }
});

const { text, isRequired } = useFieldLabel(toRef(props, "label"), toRef(props, "required"));
</script>

<style scoped>
.app-field-label {
  font-size: 12px;
  font-weight: 500;
  color: #423939;
  line-height: 1.2;
  margin-bottom: 4px;
}
/* Mandatory marker: bigger than the label text and red, so required fields stand out. */
.app-field-label__star {
  margin-left: 3px;
  color: #e53935;
  font-size: 18px;
  font-weight: 700;
  line-height: 1;
  vertical-align: text-bottom;
}
</style>
