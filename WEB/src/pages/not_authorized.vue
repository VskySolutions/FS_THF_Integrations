<template>
  <div class="fullscreen flex flex-center bg-grey-2 q-pa-md">
    <q-card flat bordered class="text-center q-pa-xl" style="max-width: 460px; border-radius: 16px;">
      <q-icon name="o_lock" size="80px" color="negative" />
      <div class="text-h2 text-weight-bold text-negative q-mt-md">403</div>
      <div class="text-h6 text-grey-9 q-mt-sm">Access denied</div>
      <div class="text-body2 text-grey-7 q-mt-sm">
        You don’t have permission to view this page. Contact your administrator if you believe this is a mistake.
      </div>
      <q-btn
        unelevated
        color="primary"
        icon="o_home"
        label="Go Home"
        no-caps
        class="q-mt-lg"
        to="/"
      />
    </q-card>
  </div>
</template>

<script setup>
</script>
