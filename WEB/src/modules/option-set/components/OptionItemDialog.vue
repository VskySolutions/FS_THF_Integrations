<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 360px;">
      <q-card-section class="text-h6">{{ item ? "Edit value" : "New value" }}</q-card-section>
      <q-card-section class="q-pt-none column q-gutter-md">
        <app-text-field
          v-model="form.label"
          label="Label *"
          hint="Shown to users, e.g. NET 30"
        />
        <app-text-field
          v-model="form.value"
          label="Value *"
          hint="Stored code, e.g. net_30"
          :error="!!valueError"
          :error-message="valueError"
          @update:model-value="valueError = ''"
        />

        <!-- Cascading lists: tie this value to a parent-list item. -->
        <app-select
          v-if="parentOptions.length"
          v-model="form.parentItemId"
          :options="parentOptions"
          label="Valid under (parent)"
          clearable
        />

        <q-toggle v-model="form.isDefault" label="Default selection" />
        <q-toggle v-if="item" v-model="form.isActive" label="Active" />
      </q-card-section>
      <q-card-actions align="right">
        <q-btn v-close-popup flat no-caps label="Cancel" />
        <q-btn
          unelevated
          no-caps
          color="primary"
          :label="item ? 'Save' : 'Add'"
          :disable="!form.label.trim() || !form.value.trim()"
          :loading="saving"
          @click="submit"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { optionSetApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useNotify } from "composables/useNotify";
import AppTextField from "components/common/AppTextField.vue";
import AppSelect from "components/common/AppSelect.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  setId: { type: String, required: true },
  // Null = create a new value; otherwise the value being edited.
  item: { type: Object, default: null },
  // For dependency lists: the parent set's items as { label, value } options.
  parentOptions: { type: Array, default: () => [] }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const saving = ref(false);
const valueError = ref("");

const blankForm = () => ({ label: "", value: "", parentItemId: null, isDefault: false, isActive: true });
const form = reactive(blankForm());

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

watch(
  () => props.modelValue,
  (isOpen) => {
    if (!isOpen) return;
    valueError.value = "";
    if (props.item) {
      Object.assign(form, blankForm(), {
        label: props.item.label,
        value: props.item.value,
        parentItemId: props.item.parentItemId,
        isDefault: props.item.isDefault,
        isActive: props.item.isActive
      });
    } else {
      Object.assign(form, blankForm());
    }
  }
);

const submit = async () => {
  saving.value = true;
  try {
    const payload = {
      label: form.label.trim(),
      value: form.value.trim(),
      parentItemId: form.parentItemId || null,
      isDefault: form.isDefault
    };
    if (props.item) {
      await optionSetApi.updateItem(props.setId, props.item.id, { ...payload, isActive: form.isActive });
      notify.success("Value updated.");
    } else {
      await optionSetApi.createItem(props.setId, payload);
      notify.success("Value added.");
    }
    open.value = false;
    emit("saved");
  } catch (err) {
    if (getApiErrorCode(err) === ApiErrorCodes.DuplicateIdentifier) {
      valueError.value = "This value already exists in the list.";
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    saving.value = false;
  }
};
</script>
