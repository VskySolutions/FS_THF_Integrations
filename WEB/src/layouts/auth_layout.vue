<template>
  <q-layout view="hHh lpR fFf">
    <q-page-container>
      <q-page class="auth-wrap">
        <!-- Brand panel (hidden on small screens) -->
        <div class="auth-brand gt-sm column justify-between text-white">
          <div class="row items-center">
            <img src="~assets/logo.png" alt="logo" class="auth-brand__logo">
            <span class="text-h5 text-weight-bold q-ml-sm">THF Integration</span>
          </div>

          <div>
            <div class="text-h4 text-weight-bold">Automated financial integration</div>
            <div class="text-subtitle1 q-mt-sm" style="opacity: 0.9; max-width: 420px;">
              Seamlessly import expenses, invoices and payments from Concur into Maconomy.
            </div>
            <q-list class="q-mt-lg auth-brand__features">
              <q-item v-for="feature in features" :key="feature" class="q-px-none">
                <q-item-section avatar style="min-width: 36px;">
                  <q-icon name="o_check_circle" color="white" size="22px" />
                </q-item-section>
                <q-item-section class="text-body1">{{ feature }}</q-item-section>
              </q-item>
            </q-list>
          </div>

          <div class="text-caption" style="opacity: 0.8;">
            &copy; 2025 VSky Solutions. All rights reserved.
          </div>
        </div>

        <!-- Form panel -->
        <div class="auth-form column flex-center">
          <div class="auth-form__inner">
            <router-view />
          </div>
        </div>
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup>
const features = [
  "Secure, multi-tenant access",
  "Automated, scheduled imports",
  "Full job & audit visibility"
];
</script>

<style scoped>
.auth-wrap {
  display: flex;
  min-height: 100vh;
}

.auth-brand {
  flex: 1 1 45%;
  padding: 56px 48px;
  background: linear-gradient(150deg, var(--q-primary) 0%, #4f7df0 60%, #5b8def 100%);
}

.auth-brand__logo {
  width: 44px;
  height: 44px;
}

.auth-brand__features :deep(.q-item) {
  min-height: 40px;
}

.auth-form {
  flex: 1 1 55%;
  padding: 24px;
  background: #f4f6fb;
}

.auth-form__inner {
  width: 100%;
  max-width: 420px;
}

@media (max-width: 1023px) {
  .auth-form {
    flex: 1 1 100%;
  }
}
</style>
