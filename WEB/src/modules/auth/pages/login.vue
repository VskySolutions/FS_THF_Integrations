<template>
  <q-card flat bordered class="auth-card q-pa-lg">
    <div class="q-mb-lg">
      <div class="text-h5 text-weight-bold">Welcome back 👋</div>
      <div class="text-body2 text-grey-7 q-mt-xs">Please sign in to your account to continue.</div>
    </div>

    <q-banner v-if="errorMessage" dense rounded class="bg-red-1 text-negative q-mb-md auth-error">
      <template #avatar>
        <q-icon name="o_error" color="negative" />
      </template>
      {{ errorMessage }}
    </q-banner>

    <q-form greedy @submit.prevent.stop="login">
      <q-input
        v-model="model.email"
        outlined
        type="email"
        label="Email"
        stack-label
        hide-bottom-space
        maxlength="128"
        autofocus
        class="q-mb-md"
        :error="v$.email.$error"
        :error-message="v$.email.$errors[0]?.$message"
        @blur="v$.email.$touch"
      >
        <template #prepend>
          <q-icon name="o_mail" />
        </template>
      </q-input>

      <q-input
        v-model="model.password"
        outlined
        label="Password"
        stack-label
        hide-bottom-space
        autocomplete="off"
        maxlength="28"
        :type="isPassword ? 'password' : 'text'"
        :error="v$.password.$error"
        :error-message="v$.password.$errors[0]?.$message"
        @blur="v$.password.$touch"
      >
        <template #prepend>
          <q-icon name="o_lock" />
        </template>
        <template #append>
          <q-icon :name="isPassword ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isPassword = !isPassword" />
        </template>
      </q-input>

      <div class="row items-center justify-between q-mt-sm q-mb-lg">
        <q-checkbox v-model="model.isRememberMeChecked" dense label="Remember me" color="primary" />
        <router-link :to="{ name: 'forgot_password', params: {} }" class="text-primary text-weight-medium auth-link">
          Forgot password?
        </router-link>
      </div>

      <q-btn label="Login" type="submit" color="primary" unelevated no-caps size="md" class="full-width" :loading="loading" />
    </q-form>
  </q-card>
</template>

<script setup>
import { ref } from "vue";
import useVuelidate from "@vuelidate/core";
import { required, helpers, email } from "@vuelidate/validators";
import { useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import { getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { setLocalStorage, getLocalStorage, clearLocalStorage } from "assets/utils";

const router = useRouter();
const authStore = useAuthStore();

const loading = ref(false);
const isPassword = ref(true);
const errorMessage = ref("");

// Remember-me persistence (email only; never persist the password).
const localStorageKey = "Login";
const filterLocalStorage = getLocalStorage(localStorageKey);

const model = ref({
  email: filterLocalStorage?.email || "",
  password: "",
  isRememberMeChecked: filterLocalStorage?.isRememberMeChecked || false
});

const rules = {
  email: {
    required: helpers.withMessage("Email is required", required),
    email: helpers.withMessage("Enter a valid email address", email)
  },
  password: { required: helpers.withMessage("Password is required", required) }
};

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });

const login = async () => {
  errorMessage.value = "";
  if (!(await v$.value.$validate())) {
    return;
  }

  loading.value = true;
  try {
    await authStore.login({ email: model.value.email, password: model.value.password });

    if (model.value.isRememberMeChecked) {
      setLocalStorage(localStorageKey, { email: model.value.email, isRememberMeChecked: true });
    } else {
      clearLocalStorage(localStorageKey);
    }

    // AC-UI-001.5: first login with a temporary password.
    if (authStore.mustChangePassword) {
      router.push({ name: "change_password" });
      return;
    }
    redirectToLanding();
  } catch (err) {
    // AC-UI-001.3 (no field hint) / AC-UI-001.4 (inactive) / AC-UI-001.6 (server error).
    const code = getApiErrorCode(err);
    if (err?.response?.status === 401) {
      errorMessage.value = getApiErrorMessage(err, "Invalid email or password.");
    } else if (code === ApiErrorCodes.Forbidden || err?.response?.status === 403) {
      errorMessage.value = "Your account is disabled. Please contact your administrator.";
    } else {
      errorMessage.value = getApiErrorMessage(err, "Unable to sign in right now. Please try again.");
    }
  } finally {
    loading.value = false;
  }
};

const redirectToLanding = () => {
  localStorage.setItem("last_route", "/");
  router.push("/");
};
</script>

<style scoped>
.auth-card {
  width: 100%;
  border-radius: 16px;
}
.auth-link {
  text-decoration: none;
}
.auth-link:hover {
  text-decoration: underline;
}
</style>
