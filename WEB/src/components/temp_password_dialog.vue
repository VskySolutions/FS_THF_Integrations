<template>
  <q-dialog v-model="open" persistent>
    <q-card style="min-width: 380px;">
      <q-card-section class="row items-center q-gutter-sm">
        <q-icon name="o_key" color="primary" size="sm" />
        <div class="text-h6">Temporary password</div>
      </q-card-section>
      <q-card-section>
        <div class="text-body2 text-grey-7 q-mb-sm">
          This password will not be shown again. Share it with the user securely.
        </div>
        <q-input :model-value="password" readonly outlined dense>
          <template #append>
            <q-btn flat round dense icon="o_content_copy" @click="copy">
              <q-tooltip>Copy</q-tooltip>
            </q-btn>
          </template>
        </q-input>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn v-close-popup flat no-caps color="primary" label="Done" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { computed } from "vue";
import { useNotify } from "composables/useNotify";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  password: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue"]);
const notify = useNotify();

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

const copy = async () => {
  try {
    await navigator.clipboard.writeText(props.password);
    notify.success("Copied to clipboard.");
  } catch {
    notify.warning("Copy failed — please select and copy manually.");
  }
};
</script>
