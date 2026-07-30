<template>
  <!-- Plain street / city / state / zip inputs that bind EXACTLY to the RemsAddressPayload wire shape
       ({ street, city, state, zip }). The shared AppAddressFields is intentionally NOT used here: it binds
       a richer canonical model (countryCode / stateCode / cityName / postalCode / addressLine1 …) driven by
       the country-state-city cascade, which does not match the public form's simple 4-field payload. -->
  <div class="row q-col-gutter-sm">
    <app-text-field
      v-model="address.street" label="Street Address" :required="required" class="col-12"
      :error="!!err('street')" :error-message="err('street')"
    />
    <app-text-field
      v-model="address.city" label="City" :required="required" class="col-12 col-sm-5"
      :error="!!err('city')" :error-message="err('city')"
    />
    <app-text-field
      v-model="address.state" label="State" :required="required" class="col-12 col-sm-4"
      :error="!!err('state')" :error-message="err('state')"
    />
    <app-text-field
      v-model="address.zip" label="ZIP / Postal Code" :required="required" class="col-12 col-sm-3"
      :error="!!err('zip')" :error-message="err('zip')"
    />
  </div>
</template>

<script setup>
import AppTextField from "components/common/AppTextField.vue";

const address = defineModel({ type: Object, required: true });

const props = defineProps({
  required: { type: Boolean, default: false },
  // Dotted property path this address occupies in the payload (e.g. "physicalAddress"), used to look up
  // per-field server validation messages by "<prefix>.<field>".
  prefix: { type: String, default: "" },
  errors: { type: Object, default: () => ({}) }
});

const err = (field) => (props.prefix ? props.errors[`${props.prefix}.${field}`] : props.errors[field]) || "";
</script>
