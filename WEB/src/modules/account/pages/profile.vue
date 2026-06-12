<template>
  <q-page padding>
    <app-breadcrumbs :items="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Profile' }]" />
    <div class="q-mx-auto" style="max-width: 860px;">
      <div class="row items-center q-mb-md">
        <div class="text-h5 text-weight-bold">My Profile</div>
        <q-space />
        <q-chip v-if="profile" dense color="blue-grey-1" text-color="blue-grey-8" :label="`${profile.profileCompletionPercentage}% complete`" />
      </div>

      <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

      <template v-else>
        <!-- Profile image -->
        <q-card flat bordered class="account-card q-mb-md">
          <q-card-section class="row items-center q-gutter-md">
            <q-avatar size="96px" color="grey-3" text-color="grey-8">
              <img v-if="previewUrl" :src="previewUrl" alt="Profile">
              <q-icon v-else name="o_person" size="48px" />
            </q-avatar>
            <div class="column q-gutter-sm">
              <div class="text-subtitle1 text-weight-medium">Profile picture</div>
              <div class="row q-gutter-sm">
                <q-btn outline no-caps color="primary" icon="o_upload" label="Upload" @click="pickImage" />
                <q-btn v-if="previewUrl" flat no-caps color="negative" icon="o_delete" label="Remove" @click="removeImage" />
              </div>
            </div>
          </q-card-section>
          <input ref="fileInput" type="file" accept="image/*" class="hidden" @change="onFileSelected">
        </q-card>

        <!-- Personal details -->
        <q-card flat bordered class="account-card q-mb-md">
          <q-card-section class="row items-center q-gutter-sm">
            <q-icon name="o_badge" color="primary" size="sm" />
            <div class="text-subtitle1 text-weight-medium">Personal details</div>
          </q-card-section>
          <q-separator />
          <q-card-section class="row q-col-gutter-md">
            <div class="col-12 section-subhead">Name</div>
            <q-input v-model="form.firstName" outlined dense stack-label hide-bottom-space label="First Name" class="col-12 col-sm-6" />
            <q-input v-model="form.middleName" outlined dense stack-label hide-bottom-space label="Middle Name" class="col-12 col-sm-6" />
            <q-input v-model="form.lastName" outlined dense stack-label hide-bottom-space label="Last Name" class="col-12 col-sm-6" />
            <q-input v-model="form.preferredName" outlined dense stack-label hide-bottom-space label="Preferred Name" class="col-12 col-sm-6" />
            <q-input v-model="form.displayName" outlined dense stack-label hide-bottom-space label="Display Name" class="col-12 col-sm-6" />

            <div class="col-12 section-subhead">Demographics</div>
            <app-select v-model="form.gender" :options="genderOptions" label="Gender" class="col-12 col-sm-6" />
            <q-input v-model="form.dateOfBirth" outlined dense stack-label hide-bottom-space type="date" label="Date of Birth" class="col-12 col-sm-6" />
            <app-select v-model="form.maritalStatus" :options="maritalOptions" label="Marital Status" class="col-12 col-sm-6" />
            <app-select
              v-model="form.nationality" :options="countryNameOptions" label="Nationality"
              use-input class="col-12 col-sm-6" @filter="filterCountryNames"
            />
          </q-card-section>
        </q-card>

        <!-- Contact details -->
        <q-card flat bordered class="account-card q-mb-md">
          <q-card-section class="row items-center q-gutter-sm">
            <q-icon name="o_contact_mail" color="primary" size="sm" />
            <div class="text-subtitle1 text-weight-medium">Contact details</div>
          </q-card-section>
          <q-separator />
          <q-card-section class="row q-col-gutter-md">
            <div class="col-12 section-subhead">Email</div>
            <q-input v-model="form.primaryEmail" outlined dense stack-label hide-bottom-space type="email" label="Personal Email" class="col-12 col-sm-6" />
            <q-input v-model="form.secondaryEmail" outlined dense stack-label hide-bottom-space type="email" label="Alternate Email" class="col-12 col-sm-6" />

            <div class="col-12 section-subhead">Phone</div>
            <app-phone-input
              v-model="form.mobileNumber" v-model:country="form.phoneCountryCode"
              label="Mobile Number" country-label="Phone Country" :dense="true" class="col-12"
            />
            <q-input v-model="form.alternateMobileNumber" outlined dense stack-label hide-bottom-space label="Alternate Mobile" class="col-12 col-sm-6" />
          </q-card-section>
        </q-card>

        <!-- Emergency contact -->
        <q-card flat bordered class="account-card q-mb-md">
          <q-card-section class="row items-center q-gutter-sm">
            <q-icon name="o_emergency" color="primary" size="sm" />
            <div class="text-subtitle1 text-weight-medium">Emergency contact</div>
          </q-card-section>
          <q-separator />
          <q-card-section class="row q-col-gutter-md">
            <q-input v-model="form.emergencyContactName" outlined dense stack-label hide-bottom-space label="Contact Name" class="col-12 col-sm-4" />
            <q-input v-model="form.emergencyContactRelationship" outlined dense stack-label hide-bottom-space label="Relationship" class="col-12 col-sm-4" />
            <q-input v-model="form.emergencyContactNumber" outlined dense stack-label hide-bottom-space label="Contact Number" class="col-12 col-sm-4" />
          </q-card-section>
        </q-card>

        <!-- Address -->
        <q-card flat bordered class="account-card q-mb-md">
          <q-card-section class="row items-center q-gutter-sm">
            <q-icon name="o_location_on" color="primary" size="sm" />
            <div class="text-subtitle1 text-weight-medium">Address</div>
          </q-card-section>
          <q-separator />
          <q-card-section class="row q-col-gutter-md">
            <div class="col-12 section-subhead">Location</div>
            <app-select
              v-model="address.countryCode" :options="countryOptions" label="Country" use-input
              class="col-12 col-sm-4" @filter="filterCountries" @update:model-value="onCountryChange"
            />
            <app-select
              v-model="address.stateCode" :options="stateOptions" label="State / Province" use-input
              class="col-12 col-sm-4" :disable="!address.countryCode" @filter="filterStates" @update:model-value="onStateChange"
            />
            <app-select
              v-model="address.cityName" :options="cityOptions" label="City" use-input
              class="col-12 col-sm-4" :disable="!address.stateCode" @filter="filterCities"
            />
            <q-input
              v-model="address.postalCode" outlined dense stack-label hide-bottom-space label="Postal Code" class="col-12 col-sm-4"
              :error="!!postalError" :error-message="postalError" @blur="validatePostal"
            />

            <div class="col-12 section-subhead">Street address</div>
            <q-input v-model="address.addressLine1" outlined dense stack-label hide-bottom-space label="Address Line 1" class="col-12 col-sm-8" />
            <q-input v-model="address.addressLine2" outlined dense stack-label hide-bottom-space label="Address Line 2" class="col-12 col-sm-6" />
            <q-input v-model="address.landmark" outlined dense stack-label hide-bottom-space label="Landmark" class="col-12 col-sm-6" />
            <q-input v-model="address.buildingName" outlined dense stack-label hide-bottom-space label="Building / Complex" class="col-12 col-sm-4" />
            <q-input v-model="address.floorNumber" outlined dense stack-label hide-bottom-space label="Floor" class="col-12 col-sm-4" />
            <q-input v-model="address.unitNumber" outlined dense stack-label hide-bottom-space label="Unit / Suite" class="col-12 col-sm-4" />
          </q-card-section>
        </q-card>

        <div class="row justify-end q-mb-lg">
          <q-btn unelevated no-caps color="primary" label="Save profile" :loading="saving" @click="save" />
        </div>
      </template>

      <!-- Tenant assignments -->
      <q-card flat bordered class="account-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Tenant assignments</q-card-section>
        <q-separator />
        <q-list separator>
          <q-item v-for="t in assignments" :key="t.tenantId">
            <q-item-section>
              <q-item-label>{{ t.name || t.identifier }}</q-item-label>
              <q-item-label caption>{{ t.identifier }}</q-item-label>
            </q-item-section>
            <q-item-section side><q-badge color="primary" class="text-capitalize">{{ t.role }}</q-badge></q-item-section>
          </q-item>
          <q-item v-if="!assignments.length"><q-item-section class="text-grey-6">No assignments.</q-item-section></q-item>
        </q-list>
      </q-card>

      <!-- Password change -->
      <q-card flat bordered class="account-card">
        <q-card-section class="row items-center q-gutter-sm">
          <q-icon name="o_lock" color="primary" size="sm" />
          <div class="text-subtitle1 text-weight-medium">Change password</div>
        </q-card-section>
        <q-separator />
        <q-form ref="pwForm" greedy @submit.prevent.stop="changePassword">
          <q-card-section>
            <q-input
              v-model="pw.current" outlined stack-label hide-bottom-space label="Current Password *" type="password" class="q-mb-md"
              :rules="[(v) => !!v || 'Current password is required']"
            />
            <q-input
              v-model="pw.next" outlined stack-label hide-bottom-space label="New Password *" type="password" class="q-mb-md"
              :rules="passwordRules"
            />
            <q-input
              v-model="pw.confirm" outlined stack-label hide-bottom-space label="Confirm Password *" type="password"
              :rules="[(v) => !!v || 'Please confirm', (v) => v === pw.next || 'Passwords do not match']"
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn unelevated no-caps color="primary" label="Update password" type="submit" :loading="savingPw" />
          </q-card-actions>
        </q-form>
      </q-card>
    </div>

    <!-- Image crop dialog -->
    <q-dialog v-model="cropOpen">
      <q-card style="min-width: 360px; max-width: 90vw;">
        <q-card-section class="text-subtitle1 text-weight-medium">Crop image</q-card-section>
        <q-separator />
        <q-card-section>
          <Cropper ref="cropper" :src="cropSrc" :stencil-props="{ aspectRatio: 1 }" class="profile-cropper" />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" @click="cropOpen = false" />
          <q-btn unelevated no-caps color="primary" label="Upload" :loading="uploading" @click="confirmCrop" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { State, City } from "country-state-city";
import { useCountries, orderedCountries, countryOption, countryNameOption } from "composables/useCountries";
import validator from "validator";
import { Cropper } from "vue-advanced-cropper";
import "vue-advanced-cropper/dist/style.css";
import { authApi, profileApi, mediaApi, getApiErrorMessage } from "services/api";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";

const router = useRouter();
const authStore = useAuthStore();
const notify = useNotify();

const assignments = computed(() => authStore.user?.tenants || []);

const genderOptions = ["Male", "Female", "Other", "Prefer not to say"].map((g) => ({ label: g, value: g }));
const maritalOptions = ["Single", "Married", "Divorced", "Widowed", "Separated"].map((m) => ({ label: m, value: m }));

// ---- Geographic data (country-state-city) ----
const { allCountries } = useCountries();
const countryOptions = ref(orderedCountries.map(countryOption));
const countryNameOptions = ref(orderedCountries.map(countryNameOption));
const stateOptions = ref([]);
const cityOptions = ref([]);
let allStates = [];
let allCities = [];

const filterFactory = (source, target) => (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => { target.value = source().filter((o) => o.label.toLowerCase().includes(needle)); });
};
const filterCountries = filterFactory(() => orderedCountries.map(countryOption), countryOptions);
const filterCountryNames = filterFactory(() => orderedCountries.map(countryNameOption), countryNameOptions);
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

const onCountryChange = (countryCode) => {
  address.stateCode = null;
  address.cityName = null;
  stateOptions.value = [];
  cityOptions.value = [];
  if (countryCode) loadStates(countryCode);
};
const onStateChange = (stateCode) => {
  address.cityName = null;
  cityOptions.value = [];
  if (address.countryCode && stateCode) loadCities(address.countryCode, stateCode);
};

// ---- Form state ----
const loading = ref(true);
const saving = ref(false);
const profile = ref(null);
const form = reactive({
  firstName: "",
  middleName: "",
  lastName: "",
  preferredName: "",
  displayName: "",
  gender: null,
  dateOfBirth: "",
  maritalStatus: null,
  nationality: null,
  primaryEmail: "",
  secondaryEmail: "",
  mobileNumber: "",
  phoneCountryCode: null,
  alternateMobileNumber: "",
  emergencyContactName: "",
  emergencyContactRelationship: "",
  emergencyContactNumber: "",
  profileMediaId: null
});
const address = reactive({
  countryCode: null,
  countryName: null,
  stateCode: null,
  stateName: null,
  cityName: null,
  postalCode: "",
  addressLine1: "",
  addressLine2: "",
  landmark: "",
  buildingName: "",
  floorNumber: "",
  unitNumber: ""
});

const load = async () => {
  loading.value = true;
  try {
    const p = await profileApi.getMine();
    profile.value = p;
    form.firstName = p.firstName || "";
    form.middleName = p.middleName || "";
    form.lastName = p.lastName || "";
    form.preferredName = p.preferredName || "";
    form.displayName = p.displayName || "";
    form.gender = p.gender || null;
    form.dateOfBirth = p.dateOfBirth ? p.dateOfBirth.substring(0, 10) : "";
    form.maritalStatus = p.maritalStatus || null;
    form.nationality = p.nationality || null;
    form.primaryEmail = p.primaryEmail || "";
    form.secondaryEmail = p.secondaryEmail || "";
    form.mobileNumber = p.mobileNumber || "";
    form.phoneCountryCode = p.countryCode || null;
    form.alternateMobileNumber = p.alternateMobileNumber || "";
    form.emergencyContactName = p.emergencyContactName || "";
    form.emergencyContactRelationship = p.emergencyContactRelationship || "";
    form.emergencyContactNumber = p.emergencyContactNumber || "";
    form.profileMediaId = p.profileMediaId || null;
    if (p.profileMediaUrl) previewUrl.value = mediaApi.absoluteUrl(p.profileMediaUrl);
    if (p.address) {
      address.countryCode = p.address.countryCode || null;
      address.countryName = p.address.countryName || null;
      address.stateCode = p.address.stateCode || null;
      address.stateName = p.address.stateName || null;
      address.cityName = p.address.cityName || null;
      address.postalCode = p.address.postalCode || "";
      address.addressLine1 = p.address.addressLine1 || "";
      address.addressLine2 = p.address.addressLine2 || "";
      address.landmark = p.address.landmark || "";
      address.buildingName = p.address.buildingName || "";
      address.floorNumber = p.address.floorNumber || "";
      address.unitNumber = p.address.unitNumber || "";
      if (address.countryCode) loadStates(address.countryCode);
      if (address.countryCode && address.stateCode) loadCities(address.countryCode, address.stateCode);
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

// ---- Postal validation (validator) ----
const postalError = ref("");
const validatePostal = () => {
  postalError.value = "";
  if (!address.postalCode || !address.countryCode) return;
  const locale = validator.isPostalCodeLocales.includes(address.countryCode) ? address.countryCode : "any";
  if (!validator.isPostalCode(address.postalCode, locale)) {
    postalError.value = "Invalid postal code for the selected country.";
  }
};

// ---- Profile image ----
const fileInput = ref(null);
const previewUrl = ref(null);
const cropOpen = ref(false);
const cropSrc = ref(null);
const cropper = ref(null);
const uploading = ref(false);

const pickImage = () => fileInput.value?.click();
const onFileSelected = (e) => {
  const file = e.target.files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => { cropSrc.value = reader.result; cropOpen.value = true; };
  reader.readAsDataURL(file);
  e.target.value = ""; // allow re-selecting the same file
};
const confirmCrop = () => {
  const result = cropper.value?.getResult();
  if (!result?.canvas) { cropOpen.value = false; return; }
  uploading.value = true;
  result.canvas.toBlob(async (blob) => {
    try {
      const file = new File([blob], "profile.png", { type: "image/png" });
      const media = await mediaApi.upload(file, "Profile");
      form.profileMediaId = media.id;
      previewUrl.value = mediaApi.absoluteUrl(media.publicUrl);
      cropOpen.value = false;
    } catch (err) {
      notify.error(getApiErrorMessage(err));
    } finally {
      uploading.value = false;
    }
  }, "image/png");
};
const removeImage = () => {
  form.profileMediaId = null;
  previewUrl.value = null;
};

// ---- Save ----
const save = async () => {
  validatePostal();
  if (postalError.value) {
    notify.error("Please fix the highlighted fields.");
    return;
  }

  // AppPhoneInput already normalises the mobile to E.164 and tracks its dial code.
  const mobile = form.mobileNumber;
  const dialCode = form.phoneCountryCode || null;

  const countryName = allCountries.find((c) => c.isoCode === address.countryCode)?.name || address.countryName;
  const stateName = allStates.find((s) => s.value === address.stateCode)?.label || address.stateName;

  const payload = {
    firstName: form.firstName,
    middleName: form.middleName,
    lastName: form.lastName,
    preferredName: form.preferredName,
    displayName: form.displayName,
    gender: form.gender,
    dateOfBirth: form.dateOfBirth || null,
    maritalStatus: form.maritalStatus,
    nationality: form.nationality,
    primaryEmail: form.primaryEmail,
    secondaryEmail: form.secondaryEmail,
    mobileNumber: mobile,
    countryCode: dialCode,
    alternateMobileNumber: form.alternateMobileNumber,
    emergencyContactName: form.emergencyContactName,
    emergencyContactRelationship: form.emergencyContactRelationship,
    emergencyContactNumber: form.emergencyContactNumber,
    profileMediaId: form.profileMediaId,
    removeProfileMedia: !form.profileMediaId,
    address: {
      addressType: "Home",
      countryCode: address.countryCode,
      countryName,
      stateCode: address.stateCode,
      stateName,
      cityName: address.cityName,
      postalCode: address.postalCode,
      addressLine1: address.addressLine1,
      addressLine2: address.addressLine2,
      landmark: address.landmark,
      buildingName: address.buildingName,
      floorNumber: address.floorNumber,
      unitNumber: address.unitNumber
    }
  };

  saving.value = true;
  try {
    const updated = await profileApi.updateMine(payload);
    profile.value = updated;
    // Keep the auth/header display name in sync with the profile.
    if (form.displayName && form.displayName !== authStore.user?.displayName) {
      try {
        await authApi.updateMe(form.displayName);
        authStore.setUserInfo({ displayName: form.displayName });
      } catch { /* non-fatal */ }
    }
    notify.success("Profile saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

// ---- Password change ----
const pwForm = ref(null);
const savingPw = ref(false);
const pw = reactive({ current: "", next: "", confirm: "" });
const passwordRules = [
  (v) => !!v || "New password is required",
  (v) => (v || "").length >= 8 || "At least 8 characters",
  (v) => /[A-Z]/.test(v) || "Must contain an uppercase letter",
  (v) => /[0-9]/.test(v) || "Must contain a digit"
];

const changePassword = async () => {
  if (!(await pwForm.value?.validate())) return;
  savingPw.value = true;
  try {
    await authApi.changePassword(pw.current, pw.next);
    notify.success("Password updated. Please sign in again.");
    authStore.clearSession();
    router.replace({ name: "login" });
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingPw.value = false;
  }
};

onMounted(load);
</script>

<style scoped>
.account-card {
  border-radius: 16px;
}
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-top: 4px;
}
.profile-cropper {
  max-height: 50vh;
  background: #f5f5f5;
}
.hidden {
  display: none;
}
</style>
