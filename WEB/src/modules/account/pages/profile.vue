<template>
  <q-page padding>
    <div class="q-mx-auto" style="max-width: 960px;">
      <div class="text-h5 text-weight-bold q-mb-md">Profile Details</div>
      <q-card flat bordered class="account-card">
        <q-card-section class="row items-center q-gutter-sm">
          <q-icon name="o_badge" color="primary" size="sm" />
          <div class="text-subtitle1 text-weight-medium">Personal information</div>
        </q-card-section>
        <q-separator />
        <q-form greedy @submit.prevent.stop="onSubmit">
          <q-card-section>
            <div class="row q-col-gutter-md q-mb-sm">
              <div class="col-12 col-sm-6 col-md-4">
                <q-input
                  v-model="model.firstName" outlined stack-label hide-bottom-space label="First Name *"
                  :error="v$.firstName.$error" :error-message="v$.firstName.$errors[0]?.$message" @blur="v$.firstName.$touch"
                >
                  <template #prepend>
                    <q-icon name="o_person" />
                  </template>
                </q-input>
              </div>
              <div class="col-12 col-sm-6 col-md-4">
                <q-input v-model="model.middleName" outlined stack-label hide-bottom-space label="Middle Name" />
              </div>
              <div class="col-12 col-sm-6 col-md-4">
                <q-input
                  v-model="model.lastName" outlined stack-label hide-bottom-space label="Last Name *"
                  :error="v$.lastName.$error" :error-message="v$.lastName.$errors[0]?.$message" @blur="v$.lastName.$touch"
                />
              </div>
            </div>
            <div class="row q-col-gutter-md q-mb-sm">
              <div class="col-12 col-sm-6 col-md-4">
                <q-input
                  v-model="model.primaryEmailAddress" outlined stack-label hide-bottom-space type="email" label="Email Address *"
                  :error="v$.primaryEmailAddress.$error" :error-message="v$.primaryEmailAddress.$errors[0]?.$message" @blur="v$.primaryEmailAddress.$touch"
                >
                  <template #prepend>
                    <q-icon name="o_mail" />
                  </template>
                </q-input>
              </div>
              <div class="col-12 col-sm-6 col-md-4">
                <q-input v-model="model.bgColor" outlined stack-label hide-bottom-space label="Background Color">
                  <template #append>
                    <q-icon name="o_colorize" class="cursor-pointer">
                      <q-popup-proxy cover transition-show="scale" transition-hide="scale">
                        <q-color v-model="model.bgColor" no-header no-footer default-view="palette" />
                      </q-popup-proxy>
                    </q-icon>
                  </template>
                </q-input>
              </div>
              <div class="col-12 col-sm-6 col-md-4">
                <q-input v-model="model.color" outlined stack-label hide-bottom-space label="Text Color">
                  <template #append>
                    <q-icon name="o_colorize" class="cursor-pointer">
                      <q-popup-proxy cover transition-show="scale" transition-hide="scale">
                        <q-color v-model="model.color" no-header no-footer default-view="palette" />
                      </q-popup-proxy>
                    </q-icon>
                  </template>
                </q-input>
              </div>
            </div>
            <div class="row q-col-gutter-md items-start">
              <div class="col-12 col-sm-6 col-md-4">
                <div class="q-mb-xs text-grey-7">Profile Picture</div>
                <!-- <div v-if="!model.pictureId">
                        <q-uploader
                          ref="documentUploaderRef"
                          color="white"
                          text-color="dark"
                          with-credentials
                          hide-upload-btn
                          field-name="personfile"
                          flat
                          bordered
                          label="Drag file here or (+) to upload. (image)"
                          @uploaded="onUploaded"
                          @added="onFileAdded"
                          style="min-height: 128px; width: 100%"
                        />
                        <div class="text-grey-7 text-caption q-mt-xs">
                          <i>Allowed Files: jpg, png, jpeg</i><br>
                          <i>500 * 500 below 1mb. </i>
                        </div>
                      </div> -->
                <!-- <div v-if="model.pictureId" class="column items-center">
                          <img :src="model.virtualPath" alt="" style="width: 150px;">
                          <q-btn
                            color="negative"
                            label="Remove"
                            outline
                            no-caps
                            class="q-mt-sm"
                            @click="clearImage"
                          />
                        </div> -->
                <singleFileUploader
                  :allowed-types="['image/jpeg','image/png','image/jpg']"
                  :max-size-in-mb="25"
                  :image-size="500"
                  :image-height="500"
                  :is-image="true"
                  label="Upload Profile Image"
                  :initial-url="model.virtualPath"
                  @file-selected="handleFile"
                  @file-valid="isFileValid = $event"
                />
              </div>
              <div class="col-12 col-sm-6 col-md-4">
                <div v-if="shouldShowPreview">
                  <div class="text-subtitle2 text-grey-7 q-mb-sm">Preview</div>
                  <div
                    class="q-pa-sm"
                    :style="{
                      backgroundColor: model.bgColor,
                      color: model.color,
                      borderRadius: '8px',
                      display: 'inline-block',
                      whiteSpace: 'nowrap',
                      maxWidth: '100%'
                    }"
                  >
                    {{ initialsName }}
                  </div>
                </div>
              </div>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right" class="q-pa-md">
            <q-btn flat color="grey-8" label="Close" type="button" no-caps :to="{ name: 'account' }" />
            <q-btn unelevated color="primary" label="Save" type="submit" no-caps :loading="processing" />
          </q-card-actions>
        </q-form>
      </q-card>
    </div>
  </q-page>
</template>

<script setup>
import { ref, onMounted, computed } from "vue";
import useVuelidate from "@vuelidate/core";
import { required, helpers, email } from "@vuelidate/validators";
import accountService from "modules/account/account.service";
import _ from "lodash";
import { notifySuccess, notifyWarning } from "assets/utils";
import { useAuthStore } from "stores/auth";

// Shared Inputs
import singleFileUploader from "src/components/form-inputs/_singleFileUpload.vue";

const authStore = useAuthStore();
const isFileValid = ref(true);
const processing = ref(false);

const model = ref({
  firstName: "",
  lastName: "",
  primaryEmailAddress: "",
  pictureId: null,
  bgColor: "",
  color: ""
});

const rules = {
  firstName: { required: helpers.withMessage("First name is required", required) },
  lastName: { required: helpers.withMessage("Last Name is required", required) },
  primaryEmailAddress: {
    required: helpers.withMessage("Email is required", required),
    email: helpers.withMessage("Invalid email", email)
  }
};

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });

function getProfile () {
  accountService.getProfile().then(resp => {
    model.value = _.cloneDeep(resp);
    model.value.id = resp.id;
    model.value.virtualPath = resp.picture ? resp.picture.virtualPath : "";
  });
}

// Upload Image
// -------------------------------------------------------------------------------------------------------
// const documentUploaderRef = ref(null);

// function onFileAdded (files) {
//   if (files[0]) {
//     model.value.personPic = files[0];
//     model.value.personChangeFlag = "edit";
//   }
// }

// function onFileAdded (files) {
//   const file = files[0];
//   if (!file) return;

//   // Allowed types
//   const allowedTypes = ["image/jpeg", "image/png", "image/jpg"];
//   if (!allowedTypes.includes(file.type)) {
//     notifyWarning({ message: "Only JPG, JPEG, and PNG files are allowed." });
//     documentUploaderRef.value.reset();
//     return;
//   }

//   // File size (1MB = 1048576 bytes)
//   if (file.size > 1048576) {
//     notifyWarning({ message: "File size must be below 1MB." });
//     documentUploaderRef.value.reset();
//     return;
//   }

//   // Image dimension check (500x500)
//   const img = new Image();
//   const objectUrl = URL.createObjectURL(file);

//   img.onload = function () {
//     if (img.width !== 500 || img.height !== 500) {
//       notifyWarning({ message: "Image must be exactly 500 x 500 pixels." });
//       documentUploaderRef.value.reset();
//       return;
//     }

//     // If all validations pass
//     model.value.personPic = file;
//     model.value.personChangeFlag = "edit";
//   };

//   img.onerror = function () {
//     notifyError({ message: "Invalid image file." });
//     documentUploaderRef.value.reset();
//   };

//   img.src = objectUrl;
// }

// function onUploaded (info) {
//   notifySuccess({ message: "File Uploaded successfully." });
//   documentUploaderRef.value.reset();
// }

// function clearImage () {
//   zwConfirm({ message: "Do you want to clear this Picture ?" }, () => {
//     model.value.pictureId = null;
//     model.value.personChangeFlag = "remove";
//   }, () => {
//   });
// }

function handleFile (file) {
  model.value.personPic = file;

  if (file) {
    model.value.personChangeFlag = "edit";
  } else {
    model.value.personPic = null;
    model.value.pictureId = null;
    model.value.personChangeFlag = "remove";
  }
}

const onSubmit = async () => {
  const isValid = await v$.value.$validate();

  if (!isFileValid.value) {
    notifyWarning({ message: "Please upload a valid file" });
    return;
  }

  if (isValid) {
    processing.value = true;
    accountService.saveProfile(model.value).then(resp => {
      const user = {
        firstName: resp.firstName,
        lastName: resp.lastName,
        email: resp.email
      };
      authStore.setUserInfo(user);
      notifySuccess({ message: "Your profile has been successfully updated." });
      getProfile();
      window.location.reload();
    }).finally(() => {
      processing.value = false;
    });
  }
};

// Only show preview if all required fields are filled
const shouldShowPreview = computed(() => {
  return model.value.firstName &&
        model.value.lastName &&
         model.value.bgColor &&
         model.value.color;
});

const initialsName = computed(() => {
  const first = model.value.firstName?.charAt(0) || "";
  const last = model.value.lastName?.charAt(0) || "";
  return (first + last).toUpperCase();
});

onMounted(() => {
  getProfile();
});

</script>

<style scoped>
.account-card {
  border-radius: 16px;
}
</style>
