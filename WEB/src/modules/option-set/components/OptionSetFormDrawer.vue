<template>
  <app-form-drawer
    v-model="open"
    :title="setId ? 'Edit Option List' : 'New Option List'"
    save-label="Save"
    :saving="saving"
    @submit="onSubmit"
    @cancel="resetForm"
  >
    <q-form ref="formRef" greedy class="column q-gutter-md">
      <!-- Entity + key are immutable after creation (they identify the list). -->
      <app-select
        v-model="form.entityType"
        :options="entityOptions"
        label="Applies to *"
        :readonly="!!setId"
        :clearable="false"
        :rules="[(v) => v != null || 'Select an entity']"
      />

      <app-text-field
        v-if="!setId"
        v-model="form.key"
        label="Key *"
        hint="Stable code, e.g. payment_terms"
        :error="!!keyError"
        :error-message="keyError"
        :rules="[(v) => !!v || 'Key is required']"
        @update:model-value="keyError = ''"
      />

      <app-text-field
        v-model="form.name"
        label="Name *"
        :rules="[(v) => !!v || 'Name is required']"
      />

      <app-select
        v-model="form.itemSortMode"
        :options="sortModeOptions"
        label="Item order"
        :clearable="false"
      />

      <!-- Optional dependency: this list's values are driven by a parent list (cascading). -->
      <app-select
        v-if="!setId"
        v-model="form.parentSetId"
        :options="parentSetOptions"
        label="Depends on (optional)"
        clearable
      />

      <q-toggle v-if="setId" v-model="form.isActive" label="Active" />
    </q-form>
  </app-form-drawer>
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { optionSetApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes, OptionItemSortMode } from "services/api";
import { useNotify } from "composables/useNotify";
import { useEntityTypeOptions } from "composables/useOptionSet";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // Null = create; otherwise the set being edited.
  set: { type: Object, default: null },
  // The already-loaded sets, used to offer parent (dependency) lists for the same entity.
  sets: { type: Array, default: () => [] }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const { options: entityOptions } = useEntityTypeOptions();

const sortModeOptions = [
  { label: "Alphabetical (A → Z)", value: OptionItemSortMode.AlphabeticalAsc },
  { label: "Alphabetical (Z → A)", value: OptionItemSortMode.AlphabeticalDesc },
  { label: "Custom (drag to order)", value: OptionItemSortMode.Custom }
];

const formRef = ref(null);
const saving = ref(false);
const keyError = ref("");

const blankForm = () => ({
  entityType: null,
  key: "",
  name: "",
  itemSortMode: OptionItemSortMode.Custom,
  parentSetId: null,
  isActive: true
});
const form = reactive(blankForm());

const setId = computed(() => props.set?.id || null);

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

// Parent options: other editable lists for the same entity (a list can't depend on itself).
const parentSetOptions = computed(() =>
  props.sets
    .filter((s) => s.entityType === form.entityType && s.id !== setId.value)
    .map((s) => ({ label: s.name, value: s.id })));

const resetForm = () => {
  Object.assign(form, blankForm());
  keyError.value = "";
};

// Hydrate the form whenever the drawer opens for a given set (or for create).
watch(
  () => props.modelValue,
  (isOpen) => {
    if (!isOpen) return;
    if (props.set) {
      Object.assign(form, blankForm(), {
        entityType: props.set.entityType,
        name: props.set.name,
        itemSortMode: props.set.itemSortMode,
        parentSetId: props.set.parentSetId,
        isActive: props.set.isActive
      });
    } else {
      resetForm();
    }
  }
);

const onSubmit = async () => {
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    if (setId.value) {
      await optionSetApi.update(setId.value, {
        name: form.name.trim(),
        itemSortMode: form.itemSortMode,
        isActive: form.isActive
      });
      notify.success("Option list updated.");
    } else {
      await optionSetApi.create({
        entityType: form.entityType,
        key: form.key.trim(),
        name: form.name.trim(),
        parentSetId: form.parentSetId || null,
        itemSortMode: form.itemSortMode
      });
      notify.success("Option list created.");
    }
    resetForm();
    emit("saved");
  } catch (err) {
    if (getApiErrorCode(err) === ApiErrorCodes.DuplicateIdentifier) {
      keyError.value = "A list with this key already exists for this entity.";
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    saving.value = false;
  }
};
</script>
