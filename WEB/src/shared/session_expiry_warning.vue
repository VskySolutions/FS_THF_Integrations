<template>
  <span class="hidden" />
</template>

<script setup>
// Headless component: mounts the session-expiry timer globally (App.vue).
import { useSessionExpiry } from "composables/useSessionExpiry";

useSessionExpiry();
</script>
