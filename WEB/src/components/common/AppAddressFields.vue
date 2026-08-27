<template>
  <div class="row q-col-gutter-md">
    <!-- The standard address block, in the standard order: Country → State → City → Address Line 1 →
         Address Line 2 → Zip. Everything except Address Line 2 is mandatory whenever the surrounding
         form marks the block `required`. -->
    <app-select
      v-model="address.countryCode" :options="countryOptions" :label="label('Country')" :required="required"
      use-input class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly"
      :rules="rulesFor('Country is required')"
      :error="!!errorFor('countryCode')" :error-message="errorFor('countryCode')"
      @update:model-value="onCountryChange"
    />

    <!-- State / City come from the dataset when it has them and degrade to a free-text input when it
         doesn't (a country with no states, a state with no cities), so nobody is blocked by a data gap. -->
    <app-select
      v-if="stateMode !== 'text'" v-model="address.stateCode" :options="stateOptions"
      :label="label('State / Province')" :required="required" use-input class="col-12 col-sm-4"
      :dense="dense" :readonly="readonly" :disable="disable || stateMode === 'pending'"
      :rules="rulesFor('State / Province is required')"
      :error="!!errorFor('stateName')" :error-message="errorFor('stateName')"
      @update:model-value="onStateChange"
    />
    <app-text-field
      v-else v-model="address.stateName" :label="label('State / Province')" :required="required"
      class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly"
      :rules="rulesFor('State / Province is required')"
      :error="!!errorFor('stateName')" :error-message="errorFor('stateName')"
    />

    <app-select
      v-if="cityMode !== 'text'" v-model="address.cityName" :options="cityOptions"
      :label="label('City')" :required="required" use-input class="col-12 col-sm-4"
      :dense="dense" :readonly="readonly" :disable="disable || cityMode === 'pending'"
      :rules="rulesFor('City is required')"
      :error="!!errorFor('cityName')" :error-message="errorFor('cityName')"
    />
    <app-text-field
      v-else v-model="address.cityName" :label="label('City')" :required="required"
      class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly"
      :rules="rulesFor('City is required')"
      :error="!!errorFor('cityName')" :error-message="errorFor('cityName')"
    />

    <app-text-field
      v-model="address.addressLine1" :label="label('Address Line 1')" :required="required"
      class="col-12 col-sm-8" :dense="dense" :disable="disable" :readonly="readonly"
      :rules="rulesFor('Address Line 1 is required')"
      :error="!!errorFor('addressLine1')" :error-message="errorFor('addressLine1')"
    />
    <app-text-field
      v-model="address.addressLine2" label="Address Line 2" class="col-12 col-sm-4"
      :dense="dense" :disable="disable" :readonly="readonly"
      :error="!!errorFor('addressLine2')" :error-message="errorFor('addressLine2')"
    />

    <app-text-field
      v-model="address.postalCode" :label="label('Zip Code')" :required="required" class="col-12 col-sm-4"
      :dense="dense" :disable="disable" :readonly="readonly"
      :rules="rulesFor('Zip Code is required')"
      :error="!!postalMessage" :error-message="postalMessage" @blur="validatePostal"
    />

    <!-- Opt-in extras kept for the records that already capture them (profile / person). Not part of the
         standard block, so a form has to ask for them. -->
    <template v-if="extended">
      <div class="col-12 section-subhead">Additional details</div>
      <app-text-field v-model="address.landmark" label="Landmark" class="col-12 col-sm-6" :dense="dense" :disable="disable" :readonly="readonly" />
      <app-text-field v-model="address.buildingName" label="Building / Complex" class="col-12 col-sm-6" :dense="dense" :disable="disable" :readonly="readonly" />
      <app-text-field v-model="address.floorNumber" label="Floor" class="col-12 col-sm-6" :dense="dense" :disable="disable" :readonly="readonly" />
      <app-text-field v-model="address.unitNumber" label="Unit / Suite" class="col-12 col-sm-6" :dense="dense" :disable="disable" :readonly="readonly" />
    </template>
  </div>
</template>

<script setup>
// THE address field-set. Every form in the app that captures an address renders this component, so the
// fields, their order, the country → state → city dependency and the postal-code check are defined once.
//
// Binds a canonical address object via v-model — the same names the Address record and the profile /
// person APIs use:
//   { countryCode, countryName, stateCode, stateName, cityName, addressLine1, addressLine2, postalCode,
//     landmark, buildingName, floorNumber, unitNumber }   // the last four only with `extended`
// countryName / stateName are kept in sync from the selected ISO codes so callers can persist names.
// Callers on a different wire shape (REMS stores a frozen legacy shape) map at their own boundary.
//
// Validation works two ways, because not every host is a q-form:
//   * inside a q-form — `required` emits real rules, validated with the rest of the form;
//   * outside one — the host calls the exposed validate(), or feeds server messages in via errors/prefix.
import { ref, reactive, computed, watch } from "vue";
import { State, City } from "country-state-city";
import validator from "validator";
import { orderedCountries, countryOption, countryNameFromIso } from "composables/useCountries";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const address = defineModel({ type: Object, required: true });

const props = defineProps({
  disable: { type: Boolean, default: false },
  readonly: { type: Boolean, default: false },
  dense: { type: Boolean, default: true },
  // When true, Country, State, City, Address Line 1 and Zip Code are mandatory (Address Line 2 never is).
  // Whether an address block is required at all is the surrounding form's call.
  required: { type: Boolean, default: false },
  // Also capture Landmark / Building / Floor / Unit.
  extended: { type: Boolean, default: false },
  // Server-side messages for THIS address, keyed by the canonical field names above. A host whose API
  // reports them under other names re-keys first (see modules/rems/remsAddress).
  errors: { type: Object, default: () => ({}) }
});

// The full lists for each level. AppSelect narrows them as the user types, so these stay complete —
// which also means "no options" always genuinely means the dataset has none, never a filtering hiccup.
const countryOptions = orderedCountries.map(countryOption);
const stateOptions = ref([]);
const cityOptions = ref([]);

// How each dependent level renders: "pending" — an empty disabled dropdown until the level above it is
// chosen; "select" — the dataset's list; "text" — a free-text input where the dataset has no list at all.
const stateMode = computed(() => {
  if (!address.value.countryCode) return "pending";
  return stateOptions.value.length ? "select" : "text";
});
const cityMode = computed(() => {
  if (stateMode.value === "pending" || (stateMode.value === "select" && !address.value.stateCode)) return "pending";
  return cityOptions.value.length ? "select" : "text";
});

// ---- Labels, rules and messages ----
const label = (text) => (props.required ? `${text} *` : text);

// Rules are what a q-form validates; the same requirement is enforced for hosts without one by validate().
const requiredRule = (message) => (v) => (!!v && String(v).trim().length > 0) || message;
const rulesFor = (message) => (props.required ? [requiredRule(message)] : []);

// Messages raised by our own validate() for hosts that are not inside a q-form, and the postal-format
// message, which is checked on blur as well.
const localErrors = reactive({});
const postalError = ref("");

const errorFor = (field) => props.errors[field] || localErrors[field] || "";
// The postal field carries the server message, the required message and the format message in one slot.
const postalMessage = computed(() => errorFor("postalCode") || postalError.value);

// ---- The cascade ----
// Each level reloads the level below it from the dataset and re-derives the display names the caller
// persists (countryName / stateName) from the selected ISO codes.
const syncCountry = () => {
  const a = address.value;
  a.countryName = countryNameFromIso(a.countryCode);
  stateOptions.value = a.countryCode
    ? State.getStatesOfCountry(a.countryCode).map((s) => ({ label: s.name, value: s.isoCode }))
    : [];
};

const syncState = () => {
  const a = address.value;
  // A record saved as free text (or from before this cascade existed) carries a state name with no ISO
  // code — recover the code so the dropdown shows what is already stored.
  if (!a.stateCode && a.stateName && stateOptions.value.length) {
    const stored = String(a.stateName).trim().toLowerCase();
    a.stateCode = stateOptions.value.find((o) => o.label.toLowerCase() === stored || o.value.toLowerCase() === stored)?.value || null;
  }
  if (a.stateCode) {
    a.stateName = State.getStateByCodeAndCountry(a.stateCode, a.countryCode)?.name || a.stateName;
  }
  cityOptions.value = a.countryCode && a.stateCode
    ? City.getCitiesOfState(a.countryCode, a.stateCode).map((c) => ({ label: c.name, value: c.name }))
    : [];
};

// Also runs when the bound object itself is swapped (a record loads, a parent resets) and immediately on
// mount: the codes can carry the same values on the fresh object, so value-watchers alone would miss it.
watch(
  () => [address.value, address.value.countryCode, address.value.stateCode],
  () => { syncCountry(); syncState(); },
  { immediate: true }
);

// A user-driven pick invalidates everything below it — clear the now-stale selections (the watcher above
// then reloads the dependent lists).
const onCountryChange = () => {
  const a = address.value;
  a.stateCode = null;
  a.stateName = "";
  a.cityName = "";
};
const onStateChange = () => { address.value.cityName = ""; };

// ---- Locale-aware postal validation ----
const validatePostal = () => {
  postalError.value = "";
  const { postalCode, countryCode } = address.value;
  if (!postalCode || !countryCode) return true;
  const locale = validator.isPostalCodeLocales.includes(countryCode) ? countryCode : "any";
  if (!validator.isPostalCode(postalCode, locale)) {
    postalError.value = "Invalid postal code for the selected country.";
    return false;
  }
  return true;
};

// Clear our own messages as the user fixes things (server messages are the host's to clear).
const clearLocalErrors = () => {
  Object.keys(localErrors).forEach((k) => delete localErrors[k]);
  postalError.value = "";
};
watch(address, clearLocalErrors, { deep: true });

// Hosts that are NOT a q-form call this at submit time; a q-form host gets the same coverage from rules.
const REQUIRED_FIELDS = [
  ["countryCode", "Country is required"],
  ["stateName", "State / Province is required"],
  ["cityName", "City is required"],
  ["addressLine1", "Address Line 1 is required"],
  ["postalCode", "Zip Code is required"]
];

const validate = () => {
  clearLocalErrors();
  if (props.required) {
    REQUIRED_FIELDS.forEach(([field, message]) => {
      if (!String(address.value[field] ?? "").trim()) localErrors[field] = message;
    });
  }
  const postalOk = validatePostal();
  return postalOk && Object.keys(localErrors).length === 0;
};

defineExpose({ validate });
</script>

<style scoped>
</style>
