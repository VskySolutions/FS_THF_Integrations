<template>
  <app-text-field
    v-model="model"
    :label="label"
    :required="required"
    :type="reveal ? 'text' : 'password'"
    :rules="rules"
    :error="error"
    :error-message="errorMessage"
    :hint="hint"
    :placeholder="placeholder"
    :disable="disable"
    :readonly="readonly"
    :autocomplete="autocomplete"
    :maxlength="maxlength"
    :autofocus="autofocus"
    @blur="emit('blur', $event)"
  >
    <template v-if="$slots.prepend" #prepend>
      <slot name="prepend" />
    </template>
    <template #append>
      <!-- Toggle masking. Reused everywhere a password is entered (UI consistency / DRY). -->
      <q-icon
        :name="reveal ? 'o_visibility_off' : 'o_visibility'"
        class="cursor-pointer"
        @click="reveal = !reveal"
      >
        <q-tooltip>{{ reveal ? "Hide password" : "Show password" }}</q-tooltip>
      </q-icon>
    </template>
  </app-text-field>
</template>

<script setup>
// Masked password input with a show/hide eye toggle. Wraps AppTextField so it inherits the standard
// outlined / dense / stack-label styling and validation surface.
import { ref, computed } from "vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  modelValue: { type: String, default: "" },
  label: { type: String, default: "" },
  required: { type: Boolean, default: false },
  rules: { type: Array, default: () => [] },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  hint: { type: String, default: "" },
  placeholder: { type: String, default: undefined },
  disable: { type: Boolean, default: false },
  readonly: { type: Boolean, default: false },
  // Discourage browser autofill on credential forms by default.
  autocomplete: { type: String, default: "new-password" },
  maxlength: { type: [String, Number], default: undefined },
  autofocus: { type: Boolean, default: false }
});

const emit = defineEmits(["update:modelValue", "blur"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const reveal = ref(false);
</script>
