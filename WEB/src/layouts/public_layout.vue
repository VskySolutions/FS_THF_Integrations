<template>
  <!-- Minimal PUBLIC layout for the anonymous REMS client EMS form (WO-116). Deliberately bare — a brand
       bar plus a centered page — with NO app menu, navigation drawer or tenant switcher, since it is
       rendered for logged-out clients following an emailed invite link. -->
  <q-layout view="hHh lpR fFf">
    <q-header class="public-header">
      <q-toolbar class="public-toolbar">
        <img src="~assets/logo.png" alt="EMS Portal" class="public-header__logo">
        <q-toolbar-title class="text-weight-bold">EMS Portal</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="public-page">
        <div class="public-page__inner">
          <router-view />
        </div>
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup>
// No shell chrome, no stores — the public form is fully self-contained and unauthenticated.
</script>

<style scoped>
.public-header {
  background: var(--q-primary);
}
.public-toolbar {
  max-width: 960px;
  width: 100%;
  margin: 0 auto;
}
.public-header__logo {
  width: 32px;
  height: 32px;
  margin-right: 12px;
}
.public-page {
  background: #f4f6fb;
  padding: 24px 16px 48px;
}
.public-page__inner {
  width: 100%;
  max-width: 960px;
  margin: 0 auto;
}
</style>
