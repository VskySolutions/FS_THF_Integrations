<template>
  <q-page padding>
    <app-breadcrumbs :items="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Profile' }]" />
    <div class="q-mx-auto" style="max-width: 760px;">
      <div class="text-h5 text-weight-bold q-mb-md">My Profile</div>

      <!-- Basic info -->
      <q-card flat bordered class="account-card q-mb-md">
        <q-card-section class="row items-center q-gutter-sm">
          <q-icon name="o_badge" color="primary" size="sm" />
          <div class="text-subtitle1 text-weight-medium">Basic information</div>
        </q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <q-input
            v-model="displayName" outlined stack-label hide-bottom-space label="Display Name *" class="col-12 col-sm-6"
            :rules="[(v) => !!v || 'Display name is required']"
          />
          <q-input :model-value="user?.email" outlined stack-label hide-bottom-space readonly label="Email" class="col-12 col-sm-6" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn unelevated no-caps color="primary" label="Save" :loading="savingName" :disable="displayName === user?.displayName" @click="saveName" />
        </q-card-actions>
      </q-card>

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
  </q-page>
</template>

<script setup>
import { ref, reactive, computed } from "vue";
import { useRouter } from "vue-router";
import { authApi, getApiErrorMessage } from "services/api";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";

const router = useRouter();
const authStore = useAuthStore();
const notify = useNotify();

const user = computed(() => authStore.user);
const assignments = computed(() => authStore.user?.tenants || []);
const displayName = ref(authStore.user?.displayName || "");

const savingName = ref(false);
const saveName = async () => {
  savingName.value = true;
  try {
    await authApi.updateMe(displayName.value);
    authStore.setUserInfo({ displayName: displayName.value });
    notify.success("Profile updated.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingName.value = false;
  }
};

// Password change
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
</script>

<style scoped>
.account-card {
  border-radius: 16px;
}
</style>
