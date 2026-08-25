<template>
  <q-layout view="hHh lpR fFf">
    <q-page-container>
      <q-page class="auth-wrap column flex-center">
        <div class="auth-panel column items-center">
          <div class="row items-center justify-center q-mb-lg">
            <img src="~assets/logo.png" alt="EMS Portal" class="auth-brand__logo">
            <span class="text-h5 text-weight-bold q-ml-sm text-grey-9">EMS Portal</span>
          </div>

          <router-view />

          <!-- Kept from the removed panel: it was the only copyright notice on the sign-in screen. -->
          <div class="text-caption text-grey-6 q-mt-lg text-center">
            &copy; {{ year }} VSky Solutions. All rights reserved.
          </div>
        </div>
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup>
import { computed } from "vue";

// Derived rather than hard-coded — the old panel still read 2025.
const year = computed(() => new Date().getFullYear());
</script>

<style scoped>
.auth-wrap {
  min-height: 100vh;
  padding: 24px;
  background: #f4f6fb;
}

.auth-panel {
  width: 100%;
  max-width: 420px;
}

.auth-brand__logo {
  width: 44px;
  height: 44px;
}

/* The routed page owns its own card; this just makes every one of them fill the centred column. */
.auth-panel :deep(> *) {
  width: 100%;
}
</style>
