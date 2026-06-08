<template>
  <router-view />
  <session-expiry-warning />
</template>

<script setup>
import { onMounted } from "vue";
import { useAuthStore } from "stores/auth";
import SessionExpiryWarning from "shared/session_expiry_warning.vue";

const authStore = useAuthStore();

onMounted(() => {
  // Restore the session (load profile + tenants) if a token is present.
  authStore.initialize();
});
</script>
