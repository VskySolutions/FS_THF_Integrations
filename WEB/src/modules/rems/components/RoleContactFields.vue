<template>
  <!-- A single contact role (name / email / phone) bound to a RemsRolePayload node. Required roles must be
       complete (all three) before review/submit; optional roles are only validated once started. -->
  <div class="role-block" :class="{ 'role-block--required': required }">
    <div class="role-block__head">
      <div class="role-block__title">{{ label }}</div>
      <q-badge
        :color="required ? 'red-1' : 'blue-grey-1'"
        :text-color="required ? 'red-8' : 'blue-grey-8'"
        :label="required ? 'Required' : 'Optional'"
      />
    </div>
    <div class="row q-col-gutter-sm">
      <app-text-field
        v-model="role.name" label="Name" :required="required" class="col-12 col-sm-6"
        :error="!!err('name')" :error-message="err('name')"
      />
      <app-text-field
        v-model="role.email" label="Email" type="email" :required="required" class="col-12 col-sm-6"
        :error="!!err('email')" :error-message="err('email')"
      />
      <div class="col-12">
        <app-phone-input v-model="role.phone" label="Phone" />
        <div v-if="err('phone')" class="text-negative text-caption q-mt-xs">{{ err('phone') }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import AppTextField from "components/common/AppTextField.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";

const role = defineModel({ type: Object, required: true });

const props = defineProps({
  label: { type: String, required: true },
  required: { type: Boolean, default: false },
  // Dotted payload path (e.g. "roles.self") used to look up per-field server messages.
  prefix: { type: String, default: "" },
  errors: { type: Object, default: () => ({}) }
});

const err = (field) => (props.prefix ? props.errors[`${props.prefix}.${field}`] : "") || "";
</script>

<style scoped>
.role-block {
  border: 1px solid #e0e6ed;
  border-radius: 10px;
  padding: 12px 14px;
  background: #fff;
}
.role-block--required {
  border-color: #d5deea;
}
.role-block__head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.role-block__title {
  font-size: 13px;
  font-weight: 600;
  color: #2c3540;
}
</style>
