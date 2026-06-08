<template>
  <router-view />
</template>

<script setup>
import { watch } from "vue";
import { useIdle } from "@vueuse/core";
// import { zwAlert } from "assets/utils";

const { idle } = useIdle(0.5 * 60 * 1000);

watch(idle, (idleValue) => {
  if (idleValue) {
    // zwAlert({ message: "Application is idle more than 2 mins." });
  }
});

</script>
