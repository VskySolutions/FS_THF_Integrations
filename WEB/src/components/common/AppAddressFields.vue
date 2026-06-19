<template>
  <div class="row q-col-gutter-md">
    <!-- Location -->
    <div class="col-12 section-subhead">Location</div>
    <app-select
      v-model="address.countryCode" :options="countryOptions" :label="countryLabel" :use-input="true"
      class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly"
      :rules="countryRules"
      @filter="filterCountries" @update:model-value="onCountryChange"
    />
    <app-select
      v-model="address.stateCode" :options="stateOptions" :label="stateLabel" :use-input="true"
      class="col-12 col-sm-4" :dense="dense" :readonly="readonly" :disable="disable || !address.countryCode"
      :rules="stateRules"
      @filter="filterStates" @update:model-value="onStateChange"
    />
    <app-select
      v-model="address.cityName" :options="cityOptions" :label="cityLabel" :use-input="true"
      class="col-12 col-sm-4" :dense="dense" :readonly="readonly" :disable="disable || !address.stateCode"
      :rules="cityRules"
      @filter="filterCities"
    />
    <app-text-field
      v-model="address.postalCode" :label="postalLabel" class="col-12 col-sm-4"
      :dense="dense" :disable="disable" :readonly="readonly" :rules="postalRules"
      :error="!!postalError" :error-message="postalError" @blur="validatePostal"
    />

    <!-- Street address -->
    <div class="col-12 section-subhead">Street address</div>
    <app-text-field
      v-model="address.addressLine1" :label="line1Label" class="col-12 col-sm-8"
      :dense="dense" :disable="disable" :readonly="readonly"
      :rules="line1Rules"
    />
    <app-text-field v-model="address.addressLine2" label="Address Line 2" class="col-12 col-sm-6" :dense="dense" :disable="disable" :readonly="readonly" />
    <app-text-field v-model="address.landmark" label="Landmark" class="col-12 col-sm-6" :dense="dense" :disable="disable" :readonly="readonly" />
    <app-text-field v-model="address.buildingName" label="Building / Complex" class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly" />
    <app-text-field v-model="address.floorNumber" label="Floor" class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly" />
    <app-text-field v-model="address.unitNumber" label="Unit / Suite" class="col-12 col-sm-4" :dense="dense" :disable="disable" :readonly="readonly" />
  </div>
</template>

<script setup>
// Single shared address field-set used by every module that captures an address (profile, customer,
// …). Two sub-sections — Location (country → state → city → postal) and Street address — with the
// dependent country/state/city cascade and locale-aware postal validation defined once here.
//
// Binds to a canonical address object via v-model:
//   { countryCode, countryName, stateCode, stateName, cityName, postalCode,
//     addressLine1, addressLine2, landmark, buildingName, floorNumber, unitNumber }
// countryName/stateName are kept in sync from the selected ISO codes so callers can persist names.
import { ref, computed, watch } from "vue";
import { State, City } from "country-state-city";
import validator from "validator";
import { useCountries, orderedCountries, countryOption } from "composables/useCountries";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const address = defineModel({ type: Object, required: true });

const props = defineProps({
  disable: { type: Boolean, default: false },
  readonly: { type: Boolean, default: false },
  dense: { type: Boolean, default: true },
  // When true, Country, State, City, Postal Code and Address Line 1 are mandatory (validated by the
  // surrounding q-form). State/City only become mandatory when the chosen country/state actually has
  // options, so addresses in regions without state/city data can still be submitted.
  required: { type: Boolean, default: false }
});

const { allCountries } = useCountries();

const countryOptions = ref(orderedCountries.map(countryOption));
const stateOptions = ref([]);
const cityOptions = ref([]);
let allStates = [];
let allCities = [];

// ---- Mandatory-field rules + labels (active only when `required`). ----
const requiredRule = (msg) => (v) => (!!v && String(v).trim().length > 0) || msg;
const star = (text, on) => (on ? `${text} *` : text);

const countryRules = computed(() => (props.required ? [requiredRule("Country is required")] : []));
// State/City are required only when the selection actually offers options to pick from.
const stateRules = computed(() => (props.required && stateOptions.value.length ? [requiredRule("State / Province is required")] : []));
const cityRules = computed(() => (props.required && cityOptions.value.length ? [requiredRule("City is required")] : []));
const postalRules = computed(() => (props.required ? [requiredRule("Postal Code is required")] : []));
const line1Rules = computed(() => (props.required ? [requiredRule("Address Line 1 is required")] : []));

const countryLabel = computed(() => star("Country", props.required));
const stateLabel = computed(() => star("State / Province", props.required && stateOptions.value.length > 0));
const cityLabel = computed(() => star("City", props.required && cityOptions.value.length > 0));
const postalLabel = computed(() => star("Postal Code", props.required));
const line1Label = computed(() => star("Address Line 1", props.required));

const filterCountries = (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => { countryOptions.value = orderedCountries.map(countryOption).filter((o) => o.label.toLowerCase().includes(needle)); });
};
const filterStates = (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => { stateOptions.value = allStates.filter((o) => o.label.toLowerCase().includes(needle)); });
};
const filterCities = (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => { cityOptions.value = allCities.filter((o) => o.label.toLowerCase().includes(needle)); });
};

const loadStates = (countryCode) => {
  allStates = State.getStatesOfCountry(countryCode).map((s) => ({ label: s.name, value: s.isoCode }));
  stateOptions.value = allStates;
};
const loadCities = (countryCode, stateCode) => {
  allCities = City.getCitiesOfState(countryCode, stateCode).map((c) => ({ label: c.name, value: c.name }));
  cityOptions.value = allCities;
};

// User changed the country/state — clear the now-stale downstream selections.
const onCountryChange = () => { address.value.stateCode = null; address.value.cityName = null; };
const onStateChange = () => { address.value.cityName = null; };

// Resolve the country/state display names from the selected ISO codes and (re)load the dependent
// option lists. countryName/stateName must always track the codes so callers can persist names.
const syncCountry = () => {
  const a = address.value;
  a.countryName = allCountries.find((c) => c.isoCode === a.countryCode)?.name || null;
  if (a.countryCode) loadStates(a.countryCode); else { allStates = []; stateOptions.value = []; }
};
const syncState = () => {
  const a = address.value;
  a.stateName = a.stateCode ? (State.getStateByCodeAndCountry(a.stateCode, a.countryCode)?.name || null) : null;
  if (a.countryCode && a.stateCode) loadCities(a.countryCode, a.stateCode); else { allCities = []; cityOptions.value = []; }
};

watch(() => address.value.countryCode, syncCountry);
watch(() => [address.value.countryCode, address.value.stateCode], syncState);
// Also resync when the bound object itself is swapped (e.g. a parent form reset / draft restore):
// the codes may carry the same values on the fresh object, so the value-watchers above wouldn't fire
// and countryName/stateName would be left null. Runs immediately on mount too.
watch(() => address.value, () => { syncCountry(); syncState(); }, { immediate: true });

// ---- Locale-aware postal validation ----
const postalError = ref("");
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

// Parents call this at submit time (works whether or not they wrap the fields in a q-form).
defineExpose({ validate: validatePostal });
</script>

<style scoped>
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin: 4px 0 8px;
}
</style>
