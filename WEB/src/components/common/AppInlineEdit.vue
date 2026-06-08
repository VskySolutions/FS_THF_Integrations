<template>
  <div class="row items-center no-wrap">
    <template v-if="!editing">
      <span>{{ displayValue }}</span>
      <q-btn flat round dense size="sm" icon="o_edit" class="q-ml-xs" @click="enterEdit">
        <q-tooltip>Edit</q-tooltip>
      </q-btn>
    </template>

    <template v-else>
      <q-input
        v-model="draft"
        dense
        outlined
        autofocus
        hide-bottom-space
        :type="type"
        @keyup.enter="onSave"
        @keyup.esc="onCancel"
      />
      <q-btn flat round dense size="sm" color="positive" icon="o_check" :loading="loading" class="q-ml-xs" @click="onSave" />
      <q-btn flat round dense size="sm" color="negative" icon="o_close" :disable="loading" @click="onCancel" />
    </template>
  </div>
</template>

<script setup>
import { ref, computed } from "vue";

const props = defineProps({
  modelValue: { type: [String, Number], default: "" },
  type: { type: String, default: "text" },
  loading: { type: Boolean, default: false }
});

const emit = defineEmits(["save", "cancel"]);

const editing = ref(false);
const draft = ref(props.modelValue);

const displayValue = computed(() => (props.modelValue === "" || props.modelValue == null ? "—" : props.modelValue));

const enterEdit = () => {
  draft.value = props.modelValue;
  editing.value = true;
};

const onSave = () => {
  editing.value = false;
  emit("save", draft.value);
};

const onCancel = () => {
  editing.value = false;
  emit("cancel");
};
</script>
