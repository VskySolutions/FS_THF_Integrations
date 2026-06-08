<template>
  <q-layout view="lHh Lpr lFf">
    <q-header bordered class="header">
      <q-toolbar class="header-top flex items-center justify-between">
        <div class="flex items-center">
          <q-btn v-if="isLoggedIn" flat dense round icon="o_menu" class="text-black" aria-label="Menu" @click="toggleLeftDrawer" />
          <q-btn flat no-caps class="no-padding q-ml-md" @click="$router.push('/')">
            <span class="text-weight-bold fs-18 text-primary">THF Integration</span>
          </q-btn>
        </div>
        <!-- User menu when signed in, otherwise a login action -->
        <div class="row q-gutter-md items-center no-wrap">
          <user-info v-if="isLoggedIn" />
          <q-btn v-else unelevated color="primary" no-caps icon="o_login" label="Login" :to="{ name: 'login' }" />
        </div>
      </q-toolbar>
    </q-header>

    <q-drawer v-if="isLoggedIn" v-model="leftDrawerOpen" bordered :width="292" :breakpoint="1024" class="bg-white">
      <aside-header />
      <q-scroll-area class="fit">
        <AppMenu />
      </q-scroll-area>
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>

    <q-footer bordered class="bg-white">
      <div class="text-center q-py-sm">
        <h6 class="q-my-none text-black" style="font-size: 13px; font-weight: 400;">
          Copyright &copy; 2025 Vsky. Website Designed and Developed by
          <a href="https://www.vskysolutions.com/" target="_blank" style="text-decoration: none; color: #007bff;">
            VSky Solutions.
          </a>
        </h6>
      </div>
    </q-footer>
  </q-layout>
</template>

<script setup>
import { ref } from "vue";
import { LocalStorage } from "quasar";

import UserInfo from "shared/user_info.vue";
import AsideHeader from "shared/aside_header.vue";
import AppMenu from "src/components/app_menu.vue";

const isLoggedIn = !!LocalStorage.getItem("token");

const leftDrawerOpen = ref(false);
const toggleLeftDrawer = () => { leftDrawerOpen.value = !leftDrawerOpen.value; };
</script>

<style scoped>
  .q-item.q-router-link--active, .q-item--active {
    color: #3ba5e5;
    font-weight: 500;
  }
  .no-underline {
    text-decoration: none !important;
  }
</style>
