<template>
  <!-- A single contact role bound to a RemsRolePayload node. A required role needs a first name, a last
       name and a valid email before review/submit — the phone is always optional. An optional role is
       only validated once the client starts filling it in. -->
  <div class="role-block" :class="{ 'role-block--required': required }">
    <div class="role-block__head">
      <div class="role-block__title">
        {{ label }}
        <!-- What this contact is FOR, where the label alone leaves a real question. On the heading it
             belongs to rather than as a caption line, so the block keeps its height. -->
        <q-icon v-if="hint" name="o_info" size="15px" color="grey-6" class="role-block__info">
          <q-tooltip anchor="top middle" self="bottom middle" max-width="280px" :delay="200">
            {{ hint }}
          </q-tooltip>
        </q-icon>
      </div>
      <q-badge
        :color="required ? 'red-1' : 'blue-grey-1'"
        :text-color="required ? 'red-8' : 'blue-grey-8'"
        :label="required ? 'Required' : 'Optional'"
      />
    </div>
    <div class="row q-col-gutter-md">
      <!-- The generational particle on their name — Jr., Sr., III. Never required: most people have none,
           and one guessed on their behalf is worse than none. In a box of its own because a Person is
           filed under a given name and a family name, and "Jr." is neither — typed into the surname it
           makes a contact nobody finds by searching for their name. -->
      <app-name-suffix-field v-model="role.suffix" class="col-4 col-sm-2" />
      <!-- Two boxes, because a contact becomes a Person and a Person is filed under a given name and a
           family name. One box asked the client to write a name and left the application guessing where
           to cut it — which put "Van Der Berg" in a first-name column often enough to matter. -->
      <!-- A contact becomes a Person record, so the two name boxes are held to what a name actually is:
           letters, and the hyphen / apostrophe / period that appear inside real ones. See utils/personName. -->
      <app-text-field
        v-model="role.firstName" label="First Name" :required="required" class="col-8 col-sm-4"
        :rules="nameRules('First Name')"
        :error="!!err('firstName')" :error-message="err('firstName')"
      />
      <app-text-field
        v-model="role.lastName" label="Last Name" :required="required" class="col-12 col-sm-6"
        :rules="nameRules('Last Name')"
        :error="!!err('lastName')" :error-message="err('lastName')"
      />
      <app-text-field
        v-model="role.email" label="Email" type="email" :required="required" class="col-12 col-sm-6"
        :error="!!err('email')" :error-message="err('email')"
      />
      <div class="col-12 col-sm-6">
        <!-- Optional even on a required contact: roleComplete() asks only for a name and a valid email. -->
        <app-phone-input v-model="role.phone" label="Phone Number" />
        <div v-if="err('phone')" class="text-negative text-caption q-mt-xs">{{ err('phone') }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { nameRules } from "utils/personName";
import AppTextField from "components/common/AppTextField.vue";
import AppNameSuffixField from "components/common/AppNameSuffixField.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";

const role = defineModel({ type: Object, required: true });

const props = defineProps({
  label: { type: String, required: true },
  // What this contact is for. Empty on the roles that explain themselves (Self, Spouse).
  hint: { type: String, default: "" },
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
.role-block__info {
  margin-left: 4px;
  cursor: help;
  vertical-align: text-bottom;
}
</style>
