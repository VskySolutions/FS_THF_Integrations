<template>
  <q-page padding>
    <div class="q-mx-auto" style="max-width: 1100px;">
      <!-- Hero banner -->
      <q-card flat class="hero-banner text-white q-pa-lg q-mb-lg">
        <div class="row items-center no-wrap justify-between">
          <div class="row items-center no-wrap">
            <q-icon name="o_sync_alt" size="56px" class="q-mr-md gt-xs" />
            <div>
              <div class="text-h4 text-weight-bold">
                Welcome to THF Integration<span v-if="displayName">, {{ displayName }}</span>
              </div>
              <div class="text-subtitle1 q-mt-xs" style="opacity: 0.9;">
                Concur &rarr; Maconomy integration platform
              </div>
            </div>
          </div>
          <q-btn
            v-if="!isLoggedIn"
            unelevated
            color="white"
            text-color="primary"
            no-caps
            icon="o_login"
            label="Login"
            :to="{ name: 'login' }"
          />
        </div>
      </q-card>

      <!-- Quick links (signed-in users) -->
      <template v-if="isLoggedIn">
        <div class="text-subtitle1 text-weight-medium text-grey-8 q-mb-sm">Quick links</div>
        <div class="row q-col-gutter-md">
          <div
            v-for="card in cards"
            :key="card.title"
            class="col-12 col-sm-6 col-md-4"
          >
            <q-card
              v-ripple
              flat
              bordered
              class="quick-card cursor-pointer full-height"
              @click="$router.push({ name: card.routeName })"
            >
              <q-card-section>
                <div class="row items-center justify-between no-wrap">
                  <q-avatar rounded size="48px" color="primary" text-color="white">
                    <q-icon :name="card.icon" size="26px" />
                  </q-avatar>
                  <q-icon name="o_arrow_forward" color="grey-5" />
                </div>
                <div class="text-h6 q-mt-md">{{ card.title }}</div>
                <div class="text-body2 text-grey-7">{{ card.description }}</div>
              </q-card-section>
            </q-card>
          </div>
        </div>
      </template>
    </div>
  </q-page>
</template>

<script setup>
import { LocalStorage } from "quasar";
import { useAuthStore } from "stores/auth";

const authStore = useAuthStore();
const user = authStore.user;
const displayName = user?.firstName || user?.username || "";
const isLoggedIn = !!LocalStorage.getItem("token");

const cards = [
  {
    title: "My Profile",
    description: "View and update your profile details.",
    icon: "o_account_circle",
    routeName: "profile"
  },
  {
    title: "Change Password",
    description: "Update the password for your account.",
    icon: "o_lock",
    routeName: "change_password"
  },
  {
    title: "Account",
    description: "Manage your account settings.",
    icon: "o_settings",
    routeName: "account"
  }
];
</script>

<style scoped>
.hero-banner {
  border-radius: 16px;
  background: linear-gradient(120deg, var(--q-primary), #5b8def);
}
.quick-card {
  border-radius: 14px;
  transition: box-shadow 0.2s ease, transform 0.2s ease;
}
.quick-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.1);
}
</style>
