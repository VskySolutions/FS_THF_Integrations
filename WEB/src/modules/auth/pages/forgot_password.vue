<template>
  <q-card flat bordered class="auth-card q-pa-lg">
    <div class="q-mb-lg">
      <div class="text-h5 text-weight-bold">Forgot password?</div>
      <div class="text-body2 text-grey-7 q-mt-xs">
        Enter your registered email and we’ll send you instructions to reset your password.
      </div>
    </div>

    <q-form greedy @submit.prevent.stop="submit">
      <q-input
        v-model="model.email"
        outlined
        label="Email address"
        stack-label
        hide-bottom-space
        autofocus
        class="q-mb-lg"
        :error="v$.email.$error"
        :error-message="v$.email.$errors[0]?.$message"
        @blur="v$.email.$touch()"
      >
        <template #prepend>
          <q-icon name="o_mail" />
        </template>
      </q-input>

      <q-btn label="Send reset link" type="submit" color="primary" unelevated no-caps size="md" class="full-width" :loading="loading" />

      <div class="text-center q-mt-md">
        <q-btn flat no-caps color="primary" icon="o_arrow_back" label="Back to login" @click="$router.push('/auth/login')" />
      </div>
    </q-form>
  </q-card>
</template>

<script setup>
import { ref } from "vue";
import useVuelidate from "@vuelidate/core";
import { helpers, email } from "@vuelidate/validators";
import authService from "modules/auth/auth.service";
import { notifyError, notifySuccess, notifyWarning } from "assets/utils";

const loading = ref(false);
const model = ref({
  email: ""
});

const rules = {
  email: {
    // required: helpers.withMessage("Email is required", required),
    email: helpers.withMessage("Invalid email address", email)
  }
};

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });

const submit = async () => {
  if (await v$.value.$validate()) {
    if (model.value.email === "") {
      notifyError({ message: "Email address is required" });
      return false;
    }
    loading.value = true;
    authService.forgotPassword(model.value).then((resp) => {
      const { success, message } = resp;
      if (success) {
        notifySuccess({ message });
        model.value.email = "";
      } else {
        notifyWarning({ message });
      }
    }).finally(() => {
      loading.value = false;
    });
  }
};
</script>

<style scoped>
.auth-card {
  width: 100%;
  border-radius: 16px;
}
</style>
