<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'Home', icon: 'o_home', to: '/' },
        { label: 'My Account', to: { name: 'account' } },
        { label: 'My Profile' }
      ]"
      :back-to="{ name: 'account' }"
    >
      <template #actions>
        <q-chip v-if="profile" dense color="blue-1" text-color="primary" class="text-weight-medium">
          {{ profile.profileCompletionPercentage }}% complete
        </q-chip>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <template v-else>
      <!-- Profile image -->
      <q-card flat bordered class="profile-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Profile picture</q-card-section>
        <q-separator />
        <q-card-section>
          <app-image-upload
            ref="imageUpload"
            v-model="previewUrl"
            :loading="uploading"
            file-name="profile.png"
            @crop="onCropUpload"
            @remove="onImageRemove"
          />
        </q-card-section>
      </q-card>

      <!-- Personal details -->
      <q-card flat bordered class="profile-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Personal details</q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <div class="col-12 section-subhead">Name</div>
          <app-text-field v-model="form.firstName" label="First Name" class="col-12 col-sm-6" />
          <app-text-field v-model="form.middleName" label="Middle Name" class="col-12 col-sm-6" />
          <app-text-field v-model="form.lastName" label="Last Name" class="col-12 col-sm-6" />
          <app-text-field v-model="form.preferredName" label="Preferred Name" class="col-12 col-sm-6" />
          <app-text-field v-model="form.displayName" label="Display Name" class="col-12 col-sm-6" />

          <div class="col-12 section-subhead">Demographics</div>
          <app-select v-model="form.gender" :options="genderOptions" label="Gender" class="col-12 col-sm-6" />
          <app-date-field v-model="form.dateOfBirth" label="Date of Birth" class="col-12 col-sm-6" />
          <app-select v-model="form.maritalStatus" :options="maritalOptions" label="Marital Status" class="col-12 col-sm-6" />
          <app-select
            v-model="form.nationality" :options="countryNameOptions" label="Nationality"
            use-input class="col-12 col-sm-6" @filter="filterCountryNames"
          />
        </q-card-section>
      </q-card>

      <!-- Contact details -->
      <q-card flat bordered class="profile-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Contact details</q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <div class="col-12 section-subhead">Email</div>
          <app-text-field v-model="form.primaryEmail" type="email" label="Personal Email" class="col-12 col-sm-6" />
          <app-text-field v-model="form.secondaryEmail" type="email" label="Alternate Email" class="col-12 col-sm-6" />

          <div class="col-12 section-subhead">Phone</div>
          <app-phone-input
            v-model="form.mobileNumber" v-model:country="form.phoneCountryCode"
            label="Mobile Number" country-label="Phone Country" :dense="true" class="col-12"
          />
          <app-text-field v-model="form.alternateMobileNumber" label="Alternate Mobile" class="col-12 col-sm-6" />
        </q-card-section>
      </q-card>

      <!-- Emergency contact -->
      <q-card flat bordered class="profile-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Emergency contact</q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <app-text-field v-model="form.emergencyContactName" label="Contact Name" class="col-12 col-sm-4" />
          <app-text-field v-model="form.emergencyContactRelationship" label="Relationship" class="col-12 col-sm-4" />
          <app-text-field v-model="form.emergencyContactNumber" label="Contact Number" class="col-12 col-sm-4" />
        </q-card-section>
      </q-card>

      <!-- Address -->
      <q-card flat bordered class="profile-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Address</q-card-section>
        <q-separator />
        <q-card-section>
          <app-address-fields ref="addressRef" v-model="address" />
        </q-card-section>
      </q-card>

      <div class="row justify-end q-mb-lg">
        <q-btn unelevated no-caps color="primary" label="Save profile" :loading="saving" @click="save" />
      </div>
    </template>

    <!-- Tenant assignments -->
    <q-card flat bordered class="profile-card q-mb-md">
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
    <q-card flat bordered class="profile-card">
      <q-card-section class="text-subtitle1 text-weight-medium">Change password</q-card-section>
      <q-separator />
      <q-form ref="pwForm" greedy @submit.prevent.stop="changePassword">
        <q-card-section class="row q-col-gutter-md">
          <app-text-field
            v-model="pw.current" label="Current Password *" type="password" class="col-12"
            :rules="[(v) => !!v || 'Current password is required']"
          />
          <app-text-field v-model="pw.next" label="New Password *" type="password" class="col-12" :rules="passwordRules" />
          <app-text-field
            v-model="pw.confirm" label="Confirm Password *" type="password" class="col-12"
            :rules="[(v) => !!v || 'Please confirm', (v) => v === pw.next || 'Passwords do not match']"
          />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn unelevated no-caps color="primary" label="Update password" type="submit" :loading="savingPw" />
        </q-card-actions>
      </q-form>
    </q-card>

  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { orderedCountries, countryNameOption } from "composables/useCountries";
import { authApi, profileApi, mediaApi, getApiErrorMessage } from "services/api";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppPhoneInput from "components/common/AppPhoneInput.vue";
import AppAddressFields from "components/common/AppAddressFields.vue";
import AppImageUpload from "components/common/AppImageUpload.vue";

const router = useRouter();
const authStore = useAuthStore();
const notify = useNotify();

const assignments = computed(() => authStore.user?.tenants || []);

const genderOptions = ["Male", "Female", "Other", "Prefer not to say"].map((g) => ({ label: g, value: g }));
const maritalOptions = ["Single", "Married", "Divorced", "Widowed", "Separated"].map((m) => ({ label: m, value: m }));

// ---- Country options for the Nationality field (address country/state/city now live in
// AppAddressFields, which owns its own cascade). ----
const countryNameOptions = ref(orderedCountries.map(countryNameOption));
const filterCountryNames = (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => { countryNameOptions.value = orderedCountries.map(countryNameOption).filter((o) => o.label.toLowerCase().includes(needle)); });
};

const addressRef = ref(null);

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
      // AppAddressFields reloads its own state/city option lists from the country/state codes.
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

// ---- Profile image ----
// AppImageUpload handles picking + cropping; we upload the cropped file and reflect the new URL.
const imageUpload = ref(null);
const previewUrl = ref(null);
const uploading = ref(false);

const onCropUpload = async (file) => {
  uploading.value = true;
  try {
    const media = await mediaApi.upload(file, "Profile");
    form.profileMediaId = media.id;
    previewUrl.value = mediaApi.absoluteUrl(media.publicUrl);
    imageUpload.value?.closeCrop();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    uploading.value = false;
  }
};

const onImageRemove = () => {
  // v-model already cleared previewUrl; also drop the saved media id.
  form.profileMediaId = null;
};

// ---- Save ----
const save = async () => {
  if (!addressRef.value?.validate()) {
    notify.error("Please fix the highlighted fields.");
    return;
  }

  // AppPhoneInput already normalises the mobile to E.164 and tracks its dial code.
  const mobile = form.mobileNumber;
  const dialCode = form.phoneCountryCode || null;

  // AppAddressFields keeps countryName/stateName in sync with the selected ISO codes.
  const countryName = address.countryName;
  const stateName = address.stateName;

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
.profile-card {
  border-radius: 12px;
}
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-top: 4px;
}
</style>
