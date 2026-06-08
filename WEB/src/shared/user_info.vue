<template>
  <q-btn dense flat color="grey-8">
    <div class="flex items-center nav-user-info">
      <q-icon name="o_account_circle" color="orange" class="material-icons-outlined q-mr-sm" size="38px" />
      <div class="line-height-normal q-mr-sm text-left">
        <div class="fs-16 text-capitalize flex justify-between items-center">
          <span>{{ displayName }}</span>
          <q-icon side name="o_keyboard_arrow_down" size="sm" class="q-ml-sm" style="color:#697A8D;" />
        </div>
      </div>
    </div>
    <q-menu>
      <q-list style="min-width: 250px" class="user-card">
        <q-item class="q-py-sm">
          <q-item-section avatar>
            <q-icon name="o_account_circle" color="orange" class="material-icons-outlined q-mr-sm" size="38px" />
          </q-item-section>
          <q-item-section>
            <q-item-label class="text-subtitle1 text-weight-medium text-capitalize">{{ displayName }}</q-item-label>
            <q-item-label caption>{{ email }}</q-item-label>
            <q-item-label v-if="role" caption class="text-capitalize">{{ role }}</q-item-label>
          </q-item-section>
        </q-item>
        <q-separator class="q-mb-sm" />
        <q-item v-ripple :to="{ name: 'profile' }" clickable>
          <q-item-section avatar>
            <q-icon name="person" class="material-icons-outlined" color="orange" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Profile</q-item-label>
          </q-item-section>
        </q-item>
        <q-item v-ripple :to="{ name: 'change_password' }" clickable>
          <q-item-section avatar>
            <q-icon name="lock" color="orange" class="material-icons-outlined" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Change Password</q-item-label>
          </q-item-section>
        </q-item>
        <q-separator class="q-my-sm" />
        <q-item v-ripple clickable @click="onLogout">
          <q-item-section avatar>
            <q-icon name="logout" color="orange" class="material-icons-outlined" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Logout</q-item-label>
          </q-item-section>
        </q-item>
        <q-item v-ripple clickable @click="onLogoutAll">
          <q-item-section avatar>
            <q-icon name="o_devices" color="orange" class="material-icons-outlined" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Logout all devices</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </q-menu>
  </q-btn>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import { useTenantStore } from "stores/tenant";

const router = useRouter();
const authStore = useAuthStore();
const tenantStore = useTenantStore();

const displayName = computed(() => authStore.user?.displayName || authStore.user?.email || "Account");
const email = computed(() => authStore.user?.email || "");
const role = computed(() => tenantStore.activeRole);

const onLogout = async () => {
  await authStore.logout();
  router.replace({ name: "login" });
};

const onLogoutAll = async () => {
  await authStore.logoutAll();
  router.replace({ name: "login" });
};
</script>
