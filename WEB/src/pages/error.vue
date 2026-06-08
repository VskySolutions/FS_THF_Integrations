<template>
  <div class="fullscreen flex flex-center bg-grey-2 q-pa-md">
    <q-card flat bordered class="text-center q-pa-xl" style="max-width: 460px; border-radius: 16px;">
      <q-icon name="o_travel_explore" size="80px" color="primary" />
      <div class="text-h2 text-weight-bold text-primary q-mt-md">404</div>
      <div class="text-h6 text-grey-9 q-mt-sm">Page not found</div>
      <div class="text-body2 text-grey-7 q-mt-sm">
        The page you’re looking for doesn’t exist or has been moved.
      </div>
      <q-btn
        unelevated
        color="primary"
        icon="o_home"
        label="Go Home"
        no-caps
        class="q-mt-lg"
        @click="$router.push('/')"
      />
    </q-card>
  </div>
</template>

<script setup>
</script>
